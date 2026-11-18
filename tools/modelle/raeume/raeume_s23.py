"""Hinterzimmer und Bibliothek fuer die Wohnhaeuser Mensch der Stufen 2 und 3: die Raummodelle von raum_hinterzimmer.py und
raum_bibliothek.py (gleiche Moebel, gleiche Masse), an ihren Platz im Haus gelegt.

    blender -b --factory-startup --python tools/modelle/raeume/raeume_s23.py -- --haus=wohnhaus_mensch_s2_g1 --raum=hinterzimmer|bibliothek [--ohne-ao] [--bilder=<ordner>]

Schreibt game/assets/modelle/gebaeude/neubau/<haus>_<raum>_innen.glb (im Spiel „InteriorRoot_<Raum>“, Stadtansicht.Wohnraeume.cs).

Was anders ist als im Haus der Stufe 1 (dort liegt der Raum im Grundriss der Wohnstube): Der Raum liegt im Obergeschoss des Hauses
(tools/modelle/neubau/wohnhaus_mensch_s23_masse.py, RAUM_VERSATZ), darum
  * die Fenster der Huelle: eins zur Gasse, eins in der rechten Aussenwand (links liegt der Flur, hinten der Treppengang);
    in der Bibliothek steht dafuer das vordere Regal der rechten Wand an der linken Wand, die Leiter am hinteren Regal,
  * die Tuer zum Flur in der linken Wand bei y 0,85..1,75 (Hinterzimmer wie bisher, Bibliothek von -1,80..-0,90 dorthin verlegt),
  * eine eigene Zelle im AO-Atlas der Raeume (Platz „<raum>_s23“): Das Modul ist in jedem Haus gleich, Verschiebung erst nach dem Backen.
Die Raumskripte selbst bleiben unveraendert; dieses Skript setzt nur die Masse in raeume_masse.py vor dem Bauen um.
"""
import os
import sys

import bpy
from mathutils import Matrix, Vector

HIER = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HIER)
sys.path.insert(0, os.path.join(os.path.dirname(HIER), "neubau"))
import raeume_masse as M                                                  # noqa: E402

# Lage des Raums im Obergeschoss: nur das Fenster zur Gasse, Tuer zum Flur links hinten (vor dem Import der Raumskripte)
import wohnhaus_mensch_s23_masse as HM                                    # noqa: E402

# Fenster wie die Huelle des Hauses (innen = aussen): zur Gasse und in der rechten Aussenwand (wohnhaus_mensch_s23_masse.MODUL_FENSTER*)
M.FENSTER_EG = [("vorn_eg", a, z) for a, z in HM.MODUL_FENSTER_VORN] + [("rechts_eg", HM.MODUL_FENSTER_RECHTS[0], HM.MODUL_FENSTER_RECHTS[1])]
M.RAUM_TUER = {"Hinterzimmer": ("links", (0.85, 0.85 + M.TUER_B)), "Bibliothek": ("links", (0.85, 0.85 + M.TUER_B))}
# Bibliothek: das vordere Regal der rechten Wand (dort ist jetzt das Fenster) an die linke Wand, die Leiter an das hintere Regal rechts
# (Fenster nach der Fachwerkregel des Hauses: zwei zur Gasse, eins rechts vor dem Kamin; Kamin und Regale ruecken dafuer)
M.BI_REGALE = [("links", -2.30, -0.95), ("rechts", 0.95, 2.10), ("vorn", 0.35, 1.95)]
M.BI_KAMIN = ("rechts", -0.45, 0.75)
M.BI_LEITER = (1.80, M.BI_LEITER[1], M.BI_LEITER[2])
M.BI_BEISTELL = (M.BI_BEISTELL[0], 1.20, M.BI_BEISTELL[2])

import raeume_bib as R                                                    # noqa: E402

# Steinhaus (Stufe 3): Bibliothek im Dachgeschoss (Menschen, Zwerge). Die Elben setzen das ab (raeume_elb_s23.py): ihr Steinhaus hat
# keine Gaube, die Bibliothek bleibt rechteckig im Obergeschoss am Platz des Hinterzimmers (Nutzer 17.11.2026).
DACH_BIBLIOTHEK = True


def _fenster_der_wand(wand):
    """Wie raeume_bib.fenster_der_wand, fuer alle vier Waende (die Raeume der Stufe 1 hatten Fenster nur vorn und links)."""
    return [(a0, a1, z0, z1) for w, (a0, a1), (z0, z1) in M.FENSTER_EG if w == wand + "_eg"]


_laibung_alt = R.fensterlaibung


def _fensterlaibung(teil, band, bank_band=None):
    """Wie raeume_bib.fensterlaibung, fuer alle vier Waende."""
    alt = M.FENSTER_EG
    try:
        for wand in ("vorn", "links", "rechts", "hinten"):
            M.FENSTER_EG = [(f"{'links' if wand == 'links' else 'vorn'}_eg", (a0, a1), (z0, z1)) for a0, a1, z0, z1 in _fenster_der_wand(wand)]
            if not M.FENSTER_EG: continue
            if wand in ("vorn", "links"):
                _laibung_alt(teil, band, bank_band)
            else:
                ziel = wand
                for a0, a1, z0, z1 in [(f[1][0], f[1][1], f[2][0], f[2][1]) for f in M.FENSTER_EG]:
                    n = R.wandpunkt(ziel, 0, 0, 1) - R.wandpunkt(ziel, 0, 0, 0)
                    for a, b, breite in ((R.wandpunkt(ziel, a0 - 0.10, z0 - 0.04), R.wandpunkt(ziel, a1 + 0.10, z0 - 0.04), 0.08),
                                         (R.wandpunkt(ziel, a0 - 0.10, z1 + 0.04), R.wandpunkt(ziel, a1 + 0.10, z1 + 0.04), 0.08),
                                         (R.wandpunkt(ziel, a0 - 0.05, z0), R.wandpunkt(ziel, a0 - 0.05, z1), 0.10),
                                         (R.wandpunkt(ziel, a1 + 0.05, z0), R.wandpunkt(ziel, a1 + 0.05, z1), 0.10)):
                        teil.brett(R.S, a, b, breite, n, band, tiefe=0.035)
    finally:
        M.FENSTER_EG = alt


R.fenster_der_wand = _fenster_der_wand
R.fensterlaibung = _fensterlaibung

ZIEL = R.ZIEL_MODELL


def main():
    opt = R.optionen()
    haus, raum = opt["haus"], opt["raum"]
    form = int(haus.rsplit("_g", 1)[1])               # Grundform aus dem Namen des Hauses (der Kern ist je Grundform verschoben)
    stufe = int(haus.rsplit("_s", 1)[1].split("_")[0])
    HM.stufe_setzen(stufe)
    dach = raum == "bibliothek" and stufe == 3 and DACH_BIBLIOTHEK
    if raum == "hinterzimmer":
        import raum_hinterzimmer as Q
        teile = [Q.baue_raum, Q.baue_kartentisch, Q.baue_sitzgruppe, Q.baue_waffenstaender, Q.baue_garderobe, Q.baue_vorrat, Q.baue_truhe, Q.baue_ofen, Q.baue_borde, Q.baue_glas]
        eye, ziel = M.HZ_KAMERA
        sichten = [("raumkamera", eye, ziel), ("tuer", eye, (-1.98, 1.30, 1.0)), ("fenster", (-0.9, 1.2, M.Z_BODEN + 1.6), (1.4, -1.9, 1.5))]
        uebersicht, schnitt = ((4.2, -5.0, 5.4), (0.0, 0.1, 0.5)), ("vorn", "rechts")
        mass = [(-1.55, 1.30, M.Z_BODEN, 1.75, 0.44), (0.95, -1.20, M.Z_BODEN, 1.85, 0.44)]
    elif dach:                                         # Steinhaus: Bibliothek im Dachgeschoss unter den Schraegen
        import raum_bibliothek as Q
        import raum_bibliothek_dach as RD
        R.GRUPPE = "wohnhaus_mensch_dachraeume_innen"
        teile = lambda: RD.teile(Q)
        eye, ziel = (0.0, RD.H - 0.45, M.Z_BODEN + M.AUGE), (0.15, RD.V, M.Z_BODEN + 0.95)
        sichten = [("raumkamera", eye, ziel), ("gaube", eye, (1.7, -0.15, 1.9)), ("schraege", (0.3, -1.6, 1.75), (-2.0, 1.0, 0.6))]
        uebersicht, schnitt = ((-4.4, 5.0, 5.4), (0.2, -0.3, 0.5)), ("hinten",)
        mass = [(-0.95, -0.40, M.Z_BODEN, 1.75, 0.44), (0.80, -1.00, M.Z_BODEN, 1.85, 0.44)]
    elif raum == "bibliothek":
        import raum_bibliothek as Q
        teile = [Q.baue_raum, Q.baue_regale, Q.baue_kamin, Q.baue_sessel, Q.baue_schreibtisch, Q.baue_lesepult, Q.baue_truhe, Q.baue_glas]
        eye, ziel = M.BI_KAMERA
        sichten = [("raumkamera", eye, ziel), ("kamin", eye, (1.7, -0.2, 1.0)), ("fenster", eye, (1.6, -1.9, 1.5))]
        uebersicht, schnitt = ((-4.4, 5.0, 5.4), (0.2, -0.3, 0.5)), ("hinten", "links")
        mass = [(-0.95, 0.50, M.Z_BODEN, 1.75, 0.44), (-1.45, -1.35, M.Z_BODEN, 1.85, 0.44)]
    else:
        raise SystemExit("--raum=hinterzimmer oder --raum=bibliothek")

    bpy.ops.wm.read_factory_settings(use_empty=True)
    if bpy.context.scene.world is None: bpy.context.scene.world = bpy.data.worlds.new("Welt")
    st = R.stoffe()
    teile = teile() if dach else [t() for t in teile]
    rechts = [(a0, a1, z0, z1) for a0, a1, z0, z1 in _fenster_der_wand("rechts")]
    glas = next(t for t in teile if t.name == "Glas")
    for a0, a1, z0, z1 in ([] if dach else rechts):                      # Fenster der rechten Aussenwand: Glas, in der Bibliothek rechteckig mit Gewaende und Sandsteinbank
        if raum == "bibliothek":
            R.bibliotheksfenster(teile[0], "moebel", "rechts", a0, a1, z0, z1, glas_teil=glas)
            p0 = R.wandpunkt("rechts", a0 - 0.06, z0 - 0.03, 0.06); p1 = R.wandpunkt("rechts", a1 + 0.06, z0, -0.02)
            teile[0].kasten(R.S, (min(p0.x, p1.x), min(p0.y, p1.y), z0 - 0.05), (max(p0.x, p1.x), max(p0.y, p1.y), z0), "quader")
        else:
            R.rechteckglas(glas, "rechts", a0, a1, z0, z1)
    budget = {t.name: t.dreiecke() for t in teile}
    print(f"DREIECKE {raum}:", budget, "Summe", sum(budget.values()))
    objekte = [t.objekt(st, mit_farbe=False) for t in teile]
    rest = [o for o in objekte[1:] if o.name != "Glas"]
    R.uv2_und_ao([([objekte[0]], []), (rest, [objekte[0]])], "bibliothek_dach" if dach else f"{raum}_s23", mit_ao="ohne-ao" not in opt, proben=int(opt.get("proben", 64)))
    anker = RD.anker() if dach else Q.anker()
    # an den Platz im Haus (um RAUM_VERSATZ verschoben) und schreiben
    versatz = Matrix.Translation(Vector((HM.FORMEN[form]["dx"], 0.0, HM.Z_DG - M.Z_BODEN) if dach else HM.raum_versatz(form, stufe, raum)))
    bpy.context.view_layer.update()          # matrix_world der eben angelegten Anker (location) auffrischen
    for o in objekte + anker:
        o.matrix_world = versatz @ o.matrix_world
    R.exportiere(objekte + anker, os.path.join(ZIEL, f"{haus}_{raum}_innen.glb"))
    if "bilder" in opt:      # Kontrollbilder im Rahmen des Moduls (kein Spielbild); die Uebersicht schneidet die Huelle auf, darum nach dem Export
        zurueck = versatz.inverted()
        bpy.context.view_layer.update()
        for o in objekte + anker:
            o.matrix_world = zurueck @ o.matrix_world
        orte = getattr(Q, "_flammen", None) or getattr(Q, "_orte", {})
        feuer = orte.get("ofen", orte.get("kamin"))
        lichter = [((0.0, -0.4, 2.1), 160, (1.0, 0.9, 0.78))] + ([(tuple(feuer + Vector((-0.3, -0.2, 0.3))), 140, (1.0, 0.55, 0.25))] if feuer is not None else [])
        R.raum_bilder(opt["bilder"], f"{haus}_{raum}", sichten, uebersicht, mass=mass, lichter=lichter, mit_huelle=False, schnitt=schnitt)


if __name__ == "__main__":
    main()
