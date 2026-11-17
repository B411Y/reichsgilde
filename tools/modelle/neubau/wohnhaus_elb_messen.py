"""Messung an den fertigen GLB der Elbenhaeuser Stufe 1 (Grundform 1-4): die Pruefungen der Menschenhaeuser (1-8, 12-15 aussen und
Wohnstube) und dazu 16-18 Luecken, 19 Wappen, 20 Dreiecke, 21 Tueren, 22 Fahnenmast, 23 (entfaellt: kein Legschindeldach), 24 Glas,
Rahmen und Oeffnung je Wand, 25 Spitzbogen nur aussen, 26 Dachschwung – mit den Funktionen der Zwergen-Messung (wohnhaus_zwerg_neu_messen.py), aber mit den Blaettern der Elben.

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


def grundform_masse(form):
    """Was 25 und 26 von der Grundform wissen muessen (Baurahmen): Oeffnungen mit Spitzbogen (Wandachse, Ebene, Aussenrichtung,
    u0, u1, z0, z1) und die Dachmasse (OX, Traufe xe, DV, DH, Giebelwaende, dach_z, Walm vorn)."""
    import wohnhaus_mensch_s1_g1 as G1
    import wohnhaus_mensch_s1 as S1
    import zwerg_teile as ZT
    bogen = []
    if form in (1, 3):
        wand, (a0, a1), (z0, z1), _ = next(f for f in G1.FENSTER if f[3] == "tuer")
        bogen.append(("y", G1.VORN, -1, a0, a1, z0, z1))
        (lx0, lx1), (lz0, lz1) = G1.LUKE
        bogen.append(("y", G1.OVORN, -1, lx0, lx1, lz0, lz1))
        dach = dict(ox=G1.OX, dv=G1.DACH_VORN, dh=G1.DACH_HINTEN, yw=(G1.OVORN, G1.HINTEN), dach_z=G1.dach_z, walm=False)
    else:
        S1.form_setzen(form)
        F = S1.F
        for wand, (a0, a1), (z0, z1), art in F.FENSTER:
            if art != "tuer" and not wand.endswith("_gb"): continue
            (ox, oy), (ex, ey), _, (nx, ny) = F.WAENDE[wand]
            bogen.append(("y", oy, ny, a0, a1, z0, z1) if ex else ("x", ox, nx, a0, a1, z0, z1))
        dach = dict(ox=F.OX, dv=F.DACH_VORN, dh=F.DACH_HINTEN, yw=(F.VORN, F.HINTEN), dach_z=S1.dach_z, walm=bool(F.WALM))
    dach["xe"] = dach["ox"] + ZT.UEBER
    return bogen, dach


def spitzbogen_pruefen(hull, bogen):
    """Pruefung 25 (Spitzbogen nur aussen, innen = aussen): vor jeder Haustuer und Giebeloeffnung liegt ein Spitzbogen – Holz
    (Bogenbrett, mindestens 6 Flaechen) und Putz (Zwickel) ganz in der Oeffnung (4 cm Rand fuer das Brett), zwischen Wandflucht und
    15 cm davor. Die Oeffnung der Huelle und die Innenseite bleiben rechteckig (Glas innen = aussen: Pruefungen 3 und 24)."""
    zeilen, fehler = [], 0
    for ax, e, aussen, u0, u1, z0, z1 in bogen:
        holz = putz = 0
        for name, mat, pts, n, v in hull:
            if name != "Haus" or mat not in ("Fachwerk", "Lehmputz"): continue
            uu = [p.x if ax == "y" else p.y for p in pts]; ee = [(p.y if ax == "y" else p.x) for p in pts]
            if min(uu) < u0 - 0.045 or max(uu) > u1 + 0.045 or min(p.z for p in pts) < z0 + 0.25 or max(p.z for p in pts) > z1 + 0.005: continue
            d = [(x - e) * aussen for x in ee]
            if min(d) < -0.005 or max(d) > 0.15: continue        # nur vor der Wandflucht (Fensterstock und Tuerblatt liegen dahinter)
            if max(p.z for p in pts) < z1 - 1.3 * (u1 - u0) / 2 - 0.01: continue
            if mat == "Fachwerk": holz += 1
            else: putz += 1
        ok = holz >= 6 and putz >= 2
        fehler += 0 if ok else 1
        zeilen.append(f"   {ax}={e:+.2f} u {u0:.2f}..{u1:.2f} bis z {z1:.2f}: Bogenbrett {holz}, Zwickel {putz} -> {'ok' if ok else 'FEHLER'}")
    return zeilen, fehler


def dachschwung_pruefen(hull, dach):
    """Pruefung 26 (Dachschwung): (a) die Dachecken stehen hoeher als die Traufmitte (mindestens 15 cm; Schindeln „Stroh“ am Rand der
    Traufe), (b) ueber der Wandflucht liegt das Dach wie vorher auf der Dachlinie (-6 bis +12 cm ueber dach_z): dort keine Luecke zu
    Wand und Giebel. Geschlossenheit: Pruefung 16, Mast: 22."""
    ox, xe, dv, dh, (yw0, yw1), dz = dach["ox"], dach["xe"], dach["dv"], dach["dh"], dach["yw"], dach["dach_z"]
    rand = [p for name, mat, pts, n, v in hull if name == "Dach" and mat == "Stroh" for p in pts if abs(p.x) > xe - 0.12]
    ecke = max((p.z for p in rand if p.y > dh - 0.4), default=0.0)
    if not dach["walm"]: ecke = max(ecke, max((p.z for p in rand if p.y < dv + 0.4), default=0.0))
    ym = (dv + dh) / 2
    mitte = max((p.z for p in rand if abs(p.y - ym) < 0.5), default=0.0)
    ab = [p.z - dz(abs(p.x)) for name, mat, pts, n, v in hull if name == "Dach" and mat == "Stroh" for p in pts
          if abs(p.x) < ox - 0.02 and yw0 + 0.02 < p.y < yw1 - 0.02]
    ok_a, ok_b = ecke - mitte >= 0.15, bool(ab) and -0.06 <= min(ab) and max(ab) <= 0.12
    zeilen = [f"   Ecke {ecke:.2f}, Traufmitte {mitte:.2f}: Schwung {ecke - mitte:+.2f} m (Soll >= 0,15) -> {'ok' if ok_a else 'FEHLER'}",
              f"   ueber der Wandflucht {len(ab)} Punkte, Abstand zur Dachlinie {min(ab, default=0):+.3f}..{max(ab, default=0):+.3f} m (Soll -0,06..+0,12) -> {'ok' if ok_b else 'FEHLER'}"]
    return zeilen, (0 if ok_a else 1) + (0 if ok_b else 1)


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
    bogen, dach = grundform_masse(form)
    MS1.WELT = MS1.masse(form)["welt"]
    hull = MS1.lade(glb)
    z25, f25 = spitzbogen_pruefen(hull, bogen)
    zeilen.append(f"25. Spitzbogen nur aussen vor Haustuer und Giebeloeffnung (innen = aussen): {'ok' if not f25 else str(f25) + ' FEHLER'}"); zeilen += z25; fehler += f25
    z26, f26 = dachschwung_pruefen(hull, dach)
    zeilen.append(f"26. Dachschwung: Ecken gehoben, ueber den Waenden unveraendert: {'ok' if not f26 else str(f26) + ' FEHLER'}"); zeilen += z26; fehler += f26
    z24, f24 = Z.fenster_innen(haus, ordner, MS1)
    zeilen.append(f"24. Jedes Glas innen liegt in einer Oeffnung der Huelle, mit Rahmen: {'ok' if not f24 else str(f24) + ' FEHLER'}"); zeilen += z24; fehler += f24
    kopf = ("GEGENPROBE (anderer Stand, muss Fehler finden): " if "gegenprobe" in opt else "") + f"Messung {haus} ({ordner})"
    print("MESSUNG " + kopf)
    for z in zeilen: print("MESSUNG " + z)
    print(f"MESSUNG Ergebnis: {fehler} Fehler")
    if "gegenprobe" not in opt and fehler: sys.exit(1)


if __name__ == "__main__":
    main()
