"""Messung an den fertigen GLB der Elbenhaeuser Stufe 1 (Grundform 1-4): die Pruefungen der Menschenhaeuser (1-8, 12-15 aussen und
Wohnstube) und dazu 16-18 Luecken, 19 Wappen, 20 Dreiecke, 21 Tueren, 22 Fahnenmast, 23 (entfaellt: kein Legschindeldach), 24 Glas,
Rahmen und Oeffnung je Wand, 25 Spitzbogen nur aussen, 26 Dachschwung, 27 Fussabdruck, 28-31 Wohnstube (Dach innen, Haustuer innen, Schlot ueber dem Herd, kein Holz im Kamin) – mit den Funktionen der Zwergen-Messung (wohnhaus_zwerg_neu_messen.py), aber mit den Blaettern der Elben.

    blender -b --factory-startup --python tools/modelle/neubau/wohnhaus_elb_messen.py -- --haus=wohnhaus_elb_s1_g<1-4>
            [--ordner=<glb-ordner>] [--gegenprobe] [--mensch-tuer]

Die Elbenmasse (Haustuer 2,10 m, Herdhaube; wohnhaus_elb_s1.masse_anlegen) gelten auch hier, damit die Pruefungen der Menschen dieselben
Oeffnungen erwarten, die das Bauskript gebaut hat. Blaetter: trim_elb (aussen), innen_trim_elb (Wohnstube). Die Huellen liegen im
AO-Atlas „wohnhaus_elb“ mit Zellen von 1024 Bildpunkten: die Grenzen des Inselrands (Pruefung 14) gelten im Verhaeltnis wie bei den
Stufen 2 und 3 der Menschen. 21: Haustuer gegen Elb (bis 1,90 m), Elbin und Mensch (elb_masse.py; alles Pflicht; Zwerge gehen nicht in Elbenhaeuser); 27 Fussabdruck.
--gegenprobe: dieselben Pruefungen an einem anderen Stand (--ordner), die Fehler finden muessen; Rueckgabewert 1 bei Fehlern sonst.
--mensch-tuer (nur mit --gegenprobe): die Masse der Menschen (Haustuer 2,00 m) statt der Elbenmasse – Pruefung 21 muss anschlagen.
"""
import os
import sys

import bpy                                                                # noqa: F401
from mathutils import Vector

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
    S1.form_setzen(form); fen = S1.F.FENSTER                   # alle Grundformen der Elben entstehen im Rahmen von wohnhaus_mensch_s1
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
    if True:
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
    Wand und Giebel, (c) die Schindeln liegen obenauf (dach_verdeckung). Geschlossenheit: Pruefung 16, Mast: 22."""
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


def dach_verdeckung(glb, ordner):
    """Pruefung 26 c: die Schindelflaechen liegen obenauf – die gebackene Verdeckung in der Mitte jeder Schindelflaeche (Stoff „Stroh“,
    Normale nach oben) ist im Median mindestens 0,5 (kippt eine lange Grundflaeche ueber die Schindeln, backt das Dach schwarz)."""
    import numpy as np
    atl = U.atlas_von(glb)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=glb)
    im = bpy.data.images.load(os.path.normpath(os.path.join(ordner, "..", "..", "..", "texturen", "neubau", atl["datei"]))); W, H = im.size   # Atlas neben dem Modellordner
    px = np.empty(W * H * 4, dtype=np.float32); im.pixels.foreach_get(px); px = px.reshape(H, W, 4)
    werte = []
    for o in bpy.data.objects:
        if o.type != "MESH" or not o.name.startswith("Dach"): continue
        me = o.data; uv2 = me.uv_layers[1].data
        for p in me.polygons:
            if not me.materials[p.material_index].name.startswith("Stroh") or me.materials[p.material_index].name.startswith("Strohkante"): continue
            if (o.matrix_world.to_3x3() @ p.normal).z < 0.5: continue
            u = sum(uv2[i].uv[0] for i in p.loop_indices) / p.loop_total; v = sum(uv2[i].uv[1] for i in p.loop_indices) / p.loop_total
            werte.append(float(px[min(int(v * H), H - 1), min(int(u * W), W - 1), 0]))
    m = float(np.median(werte)) if werte else 0.0
    ok = m >= 0.5
    return [f"   Verdeckung der Schindeln (Median, {len(werte)} Flaechen): {m:.2f} (Soll >= 0,50) -> {'ok' if ok else 'FEHLER'}"], 0 if ok else 1


def fussabdruck(glb, haus, ordner):
    """Pruefung 27 (Fussabdruck der Grundform, eigene Form der Elben 17.11.2026): Haus und Dach des Elbenhauses bleiben in Lage im
    Spiel innerhalb des Umrisses (x, y) von Haus und Dach des Menschenhauses derselben Grundform (2 cm), damit es auf jeden Bauplatz
    passt, der diese Grundform bekommt. Die Hoehe ist frei (die Elben bauen niedriger)."""
    def umriss(pfad):
        bpy.ops.wm.read_factory_settings(use_empty=True)
        bpy.ops.import_scene.gltf(filepath=pfad)
        pts = [o.matrix_world @ v.co for o in bpy.data.objects if o.type == "MESH" and o.name.split(".")[0] in ("Haus", "Dach") for v in o.data.vertices]
        return min(p.x for p in pts), max(p.x for p in pts), min(p.y for p in pts), max(p.y for p in pts), max(p.z for p in pts)
    m = umriss(os.path.join(WURZEL, "game", "assets", "modelle", "gebaeude", "neubau", haus.replace("_elb_", "_mensch_") + ".glb"))
    e = umriss(glb)
    ok = e[0] >= m[0] - 0.02 and e[1] <= m[1] + 0.02 and e[2] >= m[2] - 0.02 and e[3] <= m[3] + 0.02
    return [f"   Elb x {e[0]:.2f}..{e[1]:.2f} y {e[2]:.2f}..{e[3]:.2f} Hoehe {e[4]:.2f}; Mensch x {m[0]:.2f}..{m[1]:.2f} y {m[2]:.2f}..{m[3]:.2f} Hoehe {m[4]:.2f} -> {'ok' if ok else 'FEHLER'}"], 0 if ok else 1


def innen_pruefen(haus, ordner, form, MS1):
    """Pruefungen 28-31 der Wohnstube (Rueckmeldung des Nutzers 17.11.2026 zum Innenbild):
      28 Dach innen geschlossen: von einem Raster ueber dem Boden des Dachgeschosses (0,5 m) trifft jeder Strahl nach oben eine Flaeche
         des Innenmodells (kein Blick ins Leere zwischen den Sparren);
      29 Haustuer innen: in der Oeffnung der Haustuer liegt innen ein Tuerblatt (Flaechen bis 0,15 m vor bzw. hinter der Innenwand
         decken mindestens 60 % der Oeffnung), an derselben Stelle wie aussen;
      30 Schlot ueber der Feuerstelle: Mitte (x, y) des Steins am Herd und des Schlots ueber der Haube hoechstens 0,10 m auseinander;
      31 kein Holz im Kamin: keine Holzflaeche (Dielen, Balken) mit einer Ecke im Grundriss des Schlots oberhalb der Herdhaube."""
    import wohnhaus_mensch_s1 as S1
    from mathutils.bvhtree import BVHTree
    from trimsheet import BAENDER_INNEN
    S1.form_setzen(form)
    F = S1.F
    MS1.WELT = MS1.masse(form)["welt"]
    fl = MS1.lade(os.path.join(ordner, f"{haus}_innen.glb"))
    zeilen, fehler = [], 0
    ecken, polys = [], []
    for name, mat, pts, n, v in fl:
        polys.append(list(range(len(ecken), len(ecken) + len(pts)))); ecken += [tuple(p) for p in pts]
    baum = BVHTree.FromPolygons(ecken, polys)
    frei = gesamt = 0
    x = -F.IX + 0.3
    while x <= F.IX - 0.3:
        y = F.IV + 0.3
        while y <= F.IH - 0.3:
            gesamt += 1
            if baum.ray_cast(Vector((x, y, S1.Z_TRAUFE + 0.05)), Vector((0, 0, 1)), 10.0)[0] is None: frei += 1
            y += 0.5
        x += 0.5
    zeilen.append(f"28. Dach innen geschlossen: {gesamt} Strahlen nach oben, {frei} ins Leere -> {'ok' if not frei else 'FEHLER'}"); fehler += 1 if frei else 0
    wand, (a0, a1), (z0, z1), _ = next(f for f in F.FENSTER if f[3] == "tuer")
    (ox, oy), (ex, ey), _, (nx, ny) = F.WAENDE[wand]
    ax = "y" if ex else "x"
    e_innen = (oy if ex else ox) - (ny if ex else nx) * S1.T
    vielecke = []
    for name, mat, pts, n, v in fl:
        if mat in ("Innenglas",) or MS1.achse(n) != ax: continue
        ee = [p.y if ax == "y" else p.x for p in pts]
        if any(abs(q - e_innen) > 0.15 for q in ee): continue
        vielecke.append([((p.x if ax == "y" else p.y), p.z) for p in pts])

    def drin(u, z, q):
        an = False
        for (ua, za), (ub, zb) in zip(q, q[1:] + q[:1]):
            if (za > z) != (zb > z) and u < ua + (z - za) * (ub - ua) / (zb - za): an = not an
        return an
    probe = [(a0 + (a1 - a0) * (i + 0.5) / 12, z0 + (z1 - z0) * (j + 0.5) / 20) for i in range(12) for j in range(20)]
    anteil = sum(1 for u, z in probe if any(drin(u, z, q) for q in vielecke)) / len(probe)
    ok = anteil >= 0.6
    zeilen.append(f"29. Haustuer innen an der Stelle der Oeffnung ({wand}, {a0:.2f}..{a1:.2f}): gedeckt {anteil * 100:.0f} % (Soll >= 60 %) -> {'ok' if ok else 'FEHLER'}"); fehler += 0 if ok else 1
    stein = BAENDER_INNEN["bruchstein"]; holz = [BAENDER_INNEN[b] for b in ("bohlen", "balken")]
    in_band = lambda v, b: b[0] - 1e-3 <= v <= b[1] + 1e-3
    herd = [p for name, mat, pts, n, v in fl if name.startswith("Herd") and in_band(v, stein) for p in pts]
    schlot = [p for name, mat, pts, n, v in fl if name == "Raum" and in_band(v, stein) for p in pts if p.z > S1.Z_HAUBE + 0.3]
    if herd and schlot:
        mh = ((min(p.x for p in herd) + max(p.x for p in herd)) / 2, (min(p.y for p in herd) + max(p.y for p in herd)) / 2)
        ms = ((min(p.x for p in schlot) + max(p.x for p in schlot)) / 2, (min(p.y for p in schlot) + max(p.y for p in schlot)) / 2)
        d = ((mh[0] - ms[0]) ** 2 + (mh[1] - ms[1]) ** 2) ** 0.5
        ok = d <= 0.10
    else:
        d, ok, mh, ms = -1.0, False, (0, 0), (0, 0)
    zeilen.append(f"30. Schlot ueber der Feuerstelle: Herd ({mh[0]:.2f}, {mh[1]:.2f}), Schlot ({ms[0]:.2f}, {ms[1]:.2f}), Abstand {d:.2f} m (Soll <= 0,10) -> {'ok' if ok else 'FEHLER'}"); fehler += 0 if ok else 1
    kx0, kx1, ky0, ky1 = F.KAMIN
    funde = []
    for name, mat, pts, n, v in fl:
        if not any(in_band(v, b) for b in holz): continue
        if any(kx0 + 0.02 < p.x < kx1 - 0.02 and ky0 + 0.02 < p.y < ky1 - 0.02 and S1.Z_HAUBE < p.z < F.Z_FIRST for p in pts):
            funde.append(name)
    zeilen.append(f"31. Kein Holz im Kamin (Grundriss des Schlots ueber der Haube): {len(funde)} Holzflaechen -> {'ok' if not funde else 'FEHLER ' + ', '.join(sorted(set(funde)))}"); fehler += len(funde)
    return zeilen, fehler


def main():
    args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    opt = dict((a[2:].split("=", 1) + ["1"])[:2] for a in args if a.startswith("--"))
    haus = opt["haus"]
    ordner = opt.get("ordner", os.path.join(WURZEL, "game", "assets", "modelle", "gebaeude", "neubau"))
    form = int(haus.rsplit("_g", 1)[1])
    glb = os.path.join(ordner, f"{haus}.glb")
    import wohnhaus_mensch_s1 as S1
    ELB.masse_anlegen()                                      # Formen und Masse der Elben
    if "mensch-tuer" in opt:                                 # Gegenprobe 21: Haustuer der Menschen (2,00 m)
        ELB.TUER_HOCH = S1.TUER_HOCH = 2.00
    import wohnhaus_mensch_s1_messen as MS1
    alt_masse = MS1.masse

    def masse(f):                                            # alle Grundformen im Rahmen von wohnhaus_mensch_s1 (eigene Form der Elben)
        S1.form_setzen(f)
        F = S1.F
        return dict(giebel=[("vorn", F.VORN), ("hinten", F.HINTEN)], ox=F.OX, z_og=S1.Z_OG, zt=S1.Z_TRAUFE, zf=F.Z_FIRST,
                    z_fachwerk=0.0, ymitte=(F.VORN + F.HINTEN) / 2, welt=F.WELT, giebel_ab={}, laenge=(F.VORN, F.HINTEN))
    MS1.masse = masse
    MS1.TUER_SOLL = {1: 0.80, 2: 0.80, 3: 0.80, 4: 0.80}     # jede Haustuer fuellt ein Gefach (wie Grundform 2 und 4 der Menschen)
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
    z26c, f26c = dach_verdeckung(glb, ordner); z26 += z26c; f26 += f26c
    zeilen.append(f"26. Dachschwung: Ecken gehoben, ueber den Waenden unveraendert: {'ok' if not f26 else str(f26) + ' FEHLER'}"); zeilen += z26; fehler += f26
    z27, f27 = fussabdruck(glb, haus, ordner)
    zeilen.append(f"27. Fussabdruck der Grundform (Haus und Dach im Umriss des Menschenhauses): {'ok' if not f27 else 'FEHLER'}"); zeilen += z27; fehler += f27
    z28, f28 = innen_pruefen(haus, ordner, form, MS1); zeilen += z28; fehler += f28
    z24, f24 = Z.fenster_innen(haus, ordner, MS1)
    zeilen.append(f"24. Jedes Glas innen liegt in einer Oeffnung der Huelle, mit Rahmen: {'ok' if not f24 else str(f24) + ' FEHLER'}"); zeilen += z24; fehler += f24
    kopf = ("GEGENPROBE (anderer Stand, muss Fehler finden): " if "gegenprobe" in opt else "") + f"Messung {haus} ({ordner})"
    print("MESSUNG " + kopf)
    for z in zeilen: print("MESSUNG " + z)
    print(f"MESSUNG Ergebnis: {fehler} Fehler")
    if "gegenprobe" not in opt and fehler: sys.exit(1)


if __name__ == "__main__":
    main()
