"""Messung an den fertigen GLB der Elbenhaeuser Stufe 1 (Grundform 1-4): die Pruefungen der Menschenhaeuser (1-8, 12-15 aussen und
Wohnstube) und dazu 16-18 Luecken, 19 Wappen, 20 Dreiecke, 21 Tueren, 22 Fahnenmast, 23 (entfaellt: kein Legschindeldach), 24 Glas,
Rahmen und Oeffnung je Wand – mit den Funktionen der Zwergen-Messung (wohnhaus_zwerg_neu_messen.py), aber mit den Blaettern der Elben.

    blender -b --factory-startup --python tools/modelle/neubau/wohnhaus_elb_messen.py -- --haus=wohnhaus_elb_s1_g<1-4>
            [--ordner=<glb-ordner>] [--gegenprobe] [--mensch-tuer]

Die Elbenmasse (Haustuer 2,10 m, Herdhaube; wohnhaus_elb_s1.masse_anlegen) gelten auch hier, damit die Pruefungen der Menschen dieselben
Oeffnungen erwarten, die das Bauskript gebaut hat. Blaetter: trim_elb (aussen), innen_trim_elb (Wohnstube). Die Huellen liegen im
AO-Atlas „wohnhaus_elb“ mit Zellen von 1024 Bildpunkten: die Grenzen des Inselrands (Pruefung 14) gelten im Verhaeltnis wie bei den
Stufen 2 und 3 der Menschen. 21: Haustuer gegen Elb (bis 1,90 m), Elbin, Mensch, Zwerg und Zwergin (elb_masse.py; alles Pflicht).
--gegenprobe: dieselben Pruefungen an einem anderen Stand (--ordner), die Fehler finden muessen; Rueckgabewert 1 bei Fehlern sonst.
--mensch-tuer (nur mit --gegenprobe): die Masse der Menschen (Haustuer 2,00 m) statt der Elbenmasse – Pruefung 21 muss anschlagen.
"""
import os
import sys

import bpy                                                                # noqa: F401

HIER = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HIER)
import wohnhaus_zwerg_neu_messen as Z                                     # noqa: E402  (setzt die Zwergen-Blaetter, hier zurueckgesetzt)
import uv2_pruefung as U                                                  # noqa: E402
import elb_masse as EM                                                    # noqa: E402
import wohnhaus_elb_s1 as ELB                                             # noqa: E402

WURZEL = Z.WURZEL


def _blatt(glb, blatt):
    """Blatt der Elben: das der Menschen mit "_elb"."""
    if blatt is None:
        blatt = "innen_trim" if os.path.basename(glb).endswith("_innen.glb") else "trim"
    return blatt if blatt.endswith("_elb") else blatt + "_elb"


_alt_dichte = U.uv2_dichte


def _dichte(glb, atlas=None, rand_min=None, rand_max=None, **kw):
    """Pruefung 14 mit Inselrand-Grenzen im Verhaeltnis zur Zellgroesse (512 px = Grenzen wie bei den Menschen)."""
    if rand_min is None:
        atl = atlas or U.atlas_von(glb)
        k = (atl["kante"] / atl["zellen"]) / 512.0
        rand_min, rand_max = U.RAND_TYP_MIN * k, U.RAND_TYP_MAX * k
    return _alt_dichte(glb, atlas=atlas, rand_min=rand_min, rand_max=rand_max, **kw)


U.schwarze_flaechen = lambda glb, texturen=None, blatt=None, **kw: Z._alt_schwarz(glb, texturen=texturen, blatt=_blatt(glb, blatt), **kw)
U.normal_orm = lambda glb, texturen=None, blatt=None, **kw: Z._alt_normal(glb, texturen=texturen, blatt=_blatt(glb, blatt), **kw)
U.uv2_dichte = _dichte


def tueren(form):
    """Pruefung 21: Haustuer gegen die Figuren (elb_masse: Hoehe + 0,20, Schulter + 0,30), alle Pflicht."""
    import wohnhaus_mensch_s1_g1 as G1
    import wohnhaus_mensch_s1 as S1
    if form in (1, 3): fen = G1.FENSTER
    else:
        S1.form_setzen(form); fen = S1.F.FENSTER
    t = next(f for f in fen if f[3] == "tuer")
    b, h = t[1][1] - t[1][0], t[2][1] - t[2][0]
    soll = EM.soll()
    teile, fehler = [], 0
    for fig in EM.PFLICHT:
        ok = b + 1e-6 >= soll[fig]["tuer_b"] and h + 1e-6 >= soll[fig]["tuer_h"]
        teile.append(f"{fig} {'ja' if ok else 'nein'}")
        fehler += 0 if ok else 1
    return [f"21. Haustuer licht {b:.2f} x {h:.2f} m: passt " + ", ".join(teile) + (" -> ok" if not fehler else " -> FEHLER")], fehler


def main():
    args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    opt = dict((a[2:].split("=", 1) + ["1"])[:2] for a in args if a.startswith("--"))
    haus = opt["haus"]
    ordner = opt.get("ordner", os.path.join(WURZEL, "game", "assets", "modelle", "gebaeude", "neubau"))
    form = int(haus.rsplit("_g", 1)[1])
    glb = os.path.join(ordner, f"{haus}.glb")
    if "mensch-tuer" not in opt: ELB.masse_anlegen()        # Gegenprobe 21: mit den Massen der Menschen (Haustuer 2,00 m)
    import wohnhaus_mensch_s1_messen as MS1
    zeilen, fehler = MS1.pruefe(haus, ordner)
    z, f = Z.luecken(glb); zeilen += z; fehler += f
    z19, f19 = Z.wappen_frei(glb)
    zeilen.append(f"19. Wappen frei (5 cm zu Laden, Fenster, Gewaende, Holz, Vordach): {'ok' if not f19 else str(f19) + ' FEHLER'}"); zeilen += z19; fehler += f19
    je, summe = Z.dreiecke(glb)
    zeilen.append(f"20. Dreiecke der Huelle {je} Summe {summe} (Grenze {Z.DREIECKE_HUELLE}) -> {'ok' if summe <= Z.DREIECKE_HUELLE else 'FEHLER'}")
    fehler += 0 if summe <= Z.DREIECKE_HUELLE else 1
    z, f = tueren(form); zeilen += z; fehler += f
    z22, f22 = Z.fahne_steht(glb)
    zeilen.append(f"22. Fahnenmast steht auf dem Dach: {'ok' if not f22 else 'FEHLER'}"); zeilen += z22; fehler += f22
    zeilen.append("23. Schwerstangen: entfaellt (Schindeldach ohne Stangen und Findlinge, wie im Kunstwerk)")
    z24, f24 = Z.fenster_innen(haus, ordner, MS1)
    zeilen.append(f"24. Jedes Glas innen liegt in einer Oeffnung der Huelle, mit Rahmen: {'ok' if not f24 else str(f24) + ' FEHLER'}"); zeilen += z24; fehler += f24
    kopf = ("GEGENPROBE (anderer Stand, muss Fehler finden): " if "gegenprobe" in opt else "") + f"Messung {haus} ({ordner})"
    print("MESSUNG " + kopf)
    for z in zeilen: print("MESSUNG " + z)
    print(f"MESSUNG Ergebnis: {fehler} Fehler")
    if "gegenprobe" not in opt and fehler: sys.exit(1)


if __name__ == "__main__":
    main()
