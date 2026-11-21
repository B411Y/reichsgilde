"""Messung an den fertigen GLB der Elbenhaeuser Stufe 1 (Grundform 1-4): die Pruefungen der Menschenhaeuser (1-8, 12-15 aussen und
Wohnstube) und dazu 16-18 Luecken, 19 Wappen, 20 Dreiecke, 21 Tueren, 22 Fahnenmast, 23 (entfaellt: kein Legschindeldach), 24 Glas,
Rahmen und Oeffnung je Wand, 25 Spitzbogen nur aussen, 26 Dachschwung, 27 Fussabdruck, 28-32 Wohnstube (Dach innen, Haustuer innen, Schlot ueber dem Herd, kein Holz im Kamin und in der Einfassung, Hauswappen im Dachgeschoss), 33 Laterne frei vor Tuer, Fenster, Herd und Wappen, 34 Hauswappen im
Standard-Innenblick frei, 35 Haustuer im Standard-Innenblick, 36 Fahne frei vom Schlot im Stadtblick, 38 Standardblick nicht dicht vor einer Wand (blick_licht_pruefung.blick_wand, seit 20.11.2026 auch hier) – mit den Funktionen der Zwergen-Messung (wohnhaus_zwerg_neu_messen.py), aber mit den Blaettern der Elben.

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
        name = os.path.basename(glb)
        stufe = int(name.split("_s", 1)[1].split("_")[0]) if "_s" in name else 1
        if stufe >= 2:
            blatt = "innen_trim_s23" if name.endswith("_innen.glb") else f"trim_s{stufe}"
        else:
            blatt = "innen_trim" if name.endswith("_innen.glb") else "trim"
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


def dach_verdeckung(glb, ordner, stoff="Stroh"):
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
            if not me.materials[p.material_index].name.startswith(stoff) or me.materials[p.material_index].name.startswith("Strohkante"): continue
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
      30 Schlot ueber der Feuerstelle: der Grundriss des Schlots ueber der Haube liegt im Grundriss des Herdsteins (2 cm);
      31 kein Holz im Kamin: keine Holzflaeche (Dielen, Balken) mit einer Ecke im Grundriss des Schlots oberhalb der Herdhaube und keine
         Holzflaeche ganz im Grundriss des Herdsteins bis unter die Haube (Einfassung der Feuerstelle: Sturz, Seiten);
      32 Hauswappen innen im Dachgeschoss (Unterkante ueber dem Galerieboden), nicht im Erdgeschoss an der Wand mit der Haustuer."""
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
        bh = (min(p.x for p in herd), max(p.x for p in herd), min(p.y for p in herd), max(p.y for p in herd))
        bs = (min(p.x for p in schlot), max(p.x for p in schlot), min(p.y for p in schlot), max(p.y for p in schlot))
        ok = bs[0] >= bh[0] - 0.02 and bs[1] <= bh[1] + 0.02 and bs[2] >= bh[2] - 0.02 and bs[3] <= bh[3] + 0.02
        text = f"Herdstein x {bh[0]:.2f}..{bh[1]:.2f} y {bh[2]:.2f}..{bh[3]:.2f}, Schlot x {bs[0]:.2f}..{bs[1]:.2f} y {bs[2]:.2f}..{bs[3]:.2f}"
    else:
        ok, text, bh = False, "Herd oder Schlot nicht gefunden", (0, 0, 0, 0)
    zeilen.append(f"30. Schlot ueber der Feuerstelle (Schlot im Grundriss des Herdsteins): {text} -> {'ok' if ok else 'FEHLER'}"); fehler += 0 if ok else 1
    kx0, kx1, ky0, ky1 = F.KAMIN
    funde = []
    for name, mat, pts, n, v in fl:
        if not any(in_band(v, b) for b in holz): continue
        if any(kx0 + 0.02 < p.x < kx1 - 0.02 and ky0 + 0.02 < p.y < ky1 - 0.02 and S1.Z_HAUBE < p.z < F.Z_FIRST for p in pts):
            funde.append(name)
    for name, mat, pts, n, v in fl:                       # Einfassung der Feuerstelle
        if not any(in_band(v, b) for b in holz): continue
        r = ELB.EINFASSUNG_RAND                           # 8 cm: auch der vorstehende Sturz unter der Haube (Runde 5 hatte 3 cm und liess ihn Holz)
        if all(bh[0] - r <= p.x <= bh[1] + r and bh[2] - r <= p.y <= bh[3] + r and p.z <= S1.Z_HAUBE + 0.35 for p in pts):
            funde.append(name + " (Einfassung)")
    zeilen.append(f"31. Kein Holz im Kamin (Schlot ueber der Haube, Einfassung der Feuerstelle): {len(funde)} Holzflaechen -> {'ok' if not funde else 'FEHLER ' + ', '.join(sorted(set(funde)))}"); fehler += len(funde)
    wap = [p for name, mat, pts, n, v in fl if name.startswith("Einrichtung_Hauswappen") for p in pts]
    if wap:
        unten = min(p.z for p in wap)
        tw = (oy if ex else ox)                              # Wand der Haustuer (Baurahmen)
        an_tuer = all(abs((p.y if ex else p.x) - tw) < 0.45 for p in wap) and unten < S1.Z_OG
        ok = unten >= S1.Z_GALERIE - 0.05 and not an_tuer
        text = f"Unterkante {unten:.2f} m (Galerieboden {S1.Z_GALERIE:.2f})" + (", an der Wand der Haustuer" if an_tuer else "")
    else:
        ok, text = True, "kein Hauswappen im Modell"
    zeilen.append(f"32. Hauswappen innen im Dachgeschoss: {text} -> {'ok' if ok else 'FEHLER'}"); fehler += 0 if ok else 1
    z, f = laterne_frei(fl, F, MS1, S1); zeilen += z; fehler += f
    z, f = blick_frei(fl, baum, F, MS1, S1); zeilen += z; fehler += f
    return zeilen, fehler


def _oeffnungen(F, S1):
    """Oeffnungen im Baurahmen: (Art, Wand, Normalenachse, Ebene innen, Richtung in den Raum, a0, a1, z0, z1)."""
    out = []
    for wand, (a0, a1), (z0, z1), art in F.FENSTER:
        (ox, oy), (ex, ey), _, (nx, ny) = F.WAENDE[wand]
        ax = "y" if ex else "x"
        e = (oy if ex else ox) - (ny if ex else nx) * S1.T
        out.append((art, wand, ax, e, -(ny if ex else nx), a0, a1, z0, z1))
    return out


def laterne_frei(fl, F, MS1, S1):
    """33 Laterne des Hauswaechters frei (Rueckmeldung 17.11.2026, Grundform 2: sie hing vor dem Tuerblatt): die Laterne (Hornscheiben
    im Farbfeld „glut“ des Hauswaechters und alles bis 15 cm daneben) haengt mindestens 0,30 m neben der Oeffnung der Haustuer und
    0,10 m neben jedem Fenster derselben Wand (bei Hoehenueberlappung), nicht ueber dem Herdstein (0,30 m Rand) und nicht vor dem
    Hauswappen."""
    import wohnhaus_mensch_s1_g1 as G1
    from trimsheet import BAENDER_INNEN
    u0, u1 = G1.feld("glut")
    k0, k1, _ = BAENDER_INNEN["kleinteile"]
    glut = []
    for o in bpy.data.objects:
        if o.type != "MESH" or not o.name.startswith("Einrichtung_Hauswaechter"): continue
        uvl = o.data.uv_layers[0]
        for p in o.data.polygons:
            uu = sum(uvl.data[i].uv[0] for i in p.loop_indices) / len(p.loop_indices)
            vv = 1.0 - sum(uvl.data[i].uv[1] for i in p.loop_indices) / len(p.loop_indices)
            if u0 - 1e-3 <= uu <= u1 + 1e-3 and k0 - 1e-3 <= vv <= k1 + 1e-3:
                glut += [MS1.zurueck(o.matrix_world @ o.data.vertices[i].co) for i in p.vertices]
    if not glut:
        return ["33. Laterne frei vor Tuer, Fenster, Herd und Wappen: keine Laterne im Modell -> FEHLER"], 1
    c = sum(glut, Vector()) / len(glut)
    lat = [p for name, mat, pts, n, v in fl if name.startswith("Einrichtung_Hauswaechter")
           and all(abs(q.x - c.x) < 0.15 and abs(q.y - c.y) < 0.15 and c.z - 0.35 < q.z < c.z + 0.45 for q in pts) for p in pts]
    lx0, lx1, ly0, ly1 = min(p.x for p in lat), max(p.x for p in lat), min(p.y for p in lat), max(p.y for p in lat)
    lz0, lz1 = min(p.z for p in lat), max(p.z for p in lat)
    zeilen, fehler = [], 0
    for art, wand, ax, e, rein, a0, a1, z0, z1 in _oeffnungen(F, S1):
        ebene = c.y if ax == "y" else c.x
        if abs(ebene - e) > 0.40 or lz1 < z0 or lz0 > z1: continue
        b0, b1 = (lx0, lx1) if ax == "y" else (ly0, ly1)
        abstand = max(a0 - b1, b0 - a1)
        soll = 0.30 if art == "tuer" else 0.10
        ok = abstand >= soll
        zeilen.append(f"   {('Haustuer' if art == 'tuer' else 'Fenster')} {wand} {a0:.2f}..{a1:.2f}: Laterne {b0:.2f}..{b1:.2f}, Abstand {abstand:+.2f} m (Soll >= {soll:.2f}) -> {'ok' if ok else 'FEHLER'}")
        fehler += 0 if ok else 1
    herd = [p for name, mat, pts, n, v in fl if name.startswith("Herd") for p in pts]
    hx0, hx1, hy0, hy1 = min(p.x for p in herd), max(p.x for p in herd), min(p.y for p in herd), max(p.y for p in herd)
    ueber_herd = lx1 > hx0 - 0.3 and lx0 < hx1 + 0.3 and ly1 > hy0 - 0.3 and ly0 < hy1 + 0.3
    wap = [p for name, mat, pts, n, v in fl if name.startswith("Einrichtung_Hauswappen") for p in pts]
    vor_wappen = bool(wap) and lx1 > min(p.x for p in wap) - 0.3 and lx0 < max(p.x for p in wap) + 0.3 and ly1 > min(p.y for p in wap) - 0.3 \
        and ly0 < max(p.y for p in wap) + 0.3 and lz1 > min(p.z for p in wap) and lz0 < max(p.z for p in wap)
    fehler += int(ueber_herd) + int(vor_wappen)
    kopf = (f"33. Laterne frei vor Tuer, Fenster, Herd und Wappen (Laterne x {lx0:.2f}..{lx1:.2f} y {ly0:.2f}..{ly1:.2f} z {lz0:.2f}..{lz1:.2f}"
            f"{', ueber dem Herd' if ueber_herd else ''}{', vor dem Wappen' if vor_wappen else ''}): {'ok' if not fehler else str(fehler) + ' FEHLER'}")
    return [kopf] + zeilen, fehler


def blick_frei(fl, baum, F, MS1, S1):
    """34 Hauswappen im Standard-Innenblick frei (Grundform 4: ein Galeriebalken verdeckte die obere Haelfte): neun Sichtstrahlen vom
    Anker Blick_Auge auf die Vorderseite des Wappens (je 15/50/85 % von Breite und Hoehe, 2 cm davor) treffen vorher keine Flaeche.
    35 Haustuer im Standard-Innenblick (Grundform 3: sie lag neben der Kamera im Schatten hinter dem Herd): ihre Mitte liegt im Bild
    der Raumkamera (Oeffnungswinkel 66 Grad senkrecht, 16:9; mit Rand: hoechstens 45 Grad zur Seite, 28 Grad nach oben/unten) und von
    neun Sichtstrahlen auf die Innenseite der Oeffnung (5 cm davor) sind die Mitte und mindestens sechs frei."""
    an = {o.name.split(".")[0]: MS1.zurueck(o.matrix_world.translation.copy()) for o in bpy.data.objects}
    auge, ziel = an.get("Blick_Auge"), an.get("Blick_Ziel")
    if auge is None or ziel is None:
        return ["34./35. Standard-Innenblick: Anker Blick_Auge/Blick_Ziel fehlen -> FEHLER"], 1
    namen = [name for name, *_ in fl]

    def frei(p):
        d = p - auge
        h = baum.ray_cast(auge, d.normalized(), d.length - 0.03)
        return h[0] is None, (namen[h[2]] if h[0] is not None else "")
    zeilen, fehler = [], 0
    wap = [p for name, mat, pts, n, v in fl if name.startswith("Einrichtung_Hauswappen") for p in pts]
    if wap:
        x0, x1, y0, y1 = min(p.x for p in wap), max(p.x for p in wap), min(p.y for p in wap), max(p.y for p in wap)
        z0, z1 = min(p.z for p in wap), max(p.z for p in wap)
        if x1 - x0 < y1 - y0:                                   # haengt an einer Wand quer zu x
            xv = x1 + 0.02 if F.RAUM.x > (x0 + x1) / 2 else x0 - 0.02
            probe = [Vector((xv, y0 + (y1 - y0) * a, z0 + (z1 - z0) * b)) for a in (0.15, 0.5, 0.85) for b in (0.15, 0.5, 0.85)]
        else:
            yv = y1 + 0.02 if F.RAUM.y > (y0 + y1) / 2 else y0 - 0.02
            probe = [Vector((x0 + (x1 - x0) * a, yv, z0 + (z1 - z0) * b)) for a in (0.15, 0.5, 0.85) for b in (0.15, 0.5, 0.85)]
        zu = [frei(p)[1] for p in probe if not frei(p)[0]]
        ok = not zu
        zeilen.append(f"34. Hauswappen im Standard-Innenblick frei (Auge {auge.x:.2f}, {auge.y:.2f}, {auge.z:.2f}): {9 - len(zu)} von 9 Sichtstrahlen frei"
                      + (f", verdeckt von {', '.join(sorted(set(zu)))}" if zu else "") + f" -> {'ok' if ok else 'FEHLER'}")
        fehler += 0 if ok else 1
    art, wand, ax, e, rein, a0, a1, z0, z1 = next(o for o in _oeffnungen(F, S1) if o[0] == "tuer")
    ev = e + rein * 0.05
    punkt = (lambda s, z: Vector((s, ev, z))) if ax == "y" else (lambda s, z: Vector((ev, s, z)))
    probe = [punkt(a0 + (a1 - a0) * a, z0 + (z1 - z0) * b) for a in (0.2, 0.5, 0.8) for b in (0.2, 0.5, 0.8)]
    mitte = probe[4]
    vorn = (ziel - auge).normalized()
    d = (mitte - auge).normalized()
    import math
    seite = math.degrees(math.atan2(abs(vorn.x * d.y - vorn.y * d.x), vorn.x * d.x + vorn.y * d.y))
    hoch = math.degrees(abs(math.asin(max(-1.0, min(1.0, d.z))) - math.asin(max(-1.0, min(1.0, vorn.z)))))
    im_bild = seite <= 45.0 and hoch <= 28.0
    zu = [frei(p)[1] for p in probe if not frei(p)[0]]
    ok = im_bild and frei(mitte)[0] and len(zu) <= 3
    zeilen.append(f"35. Haustuer im Standard-Innenblick: Mitte {seite:.0f} Grad zur Seite, {hoch:.0f} Grad hoch/tief ({'im Bild' if im_bild else 'NICHT im Bild'}),"
                  f" {9 - len(zu)} von 9 Sichtstrahlen frei" + (f" (verdeckt von {', '.join(sorted(set(zu)))})" if zu else "") + f" -> {'ok' if ok else 'FEHLER'}")
    fehler += 0 if ok else 1
    return zeilen, fehler


def _huelle2d(pts):
    pts = sorted(set(pts))
    if len(pts) < 3: return pts
    kreuz = lambda o, a, b: (a[0] - o[0]) * (b[1] - o[1]) - (a[1] - o[1]) * (b[0] - o[0])
    unten, oben = [], []
    for p in pts:
        while len(unten) >= 2 and kreuz(unten[-2], unten[-1], p) <= 0: unten.pop()
        unten.append(p)
    for p in reversed(pts):
        while len(oben) >= 2 and kreuz(oben[-2], oben[-1], p) <= 0: oben.pop()
        oben.append(p)
    return unten[:-1] + oben[:-1]


def _poly_abstand(a, b):
    """Abstand zweier konvexer Vielecke in der Ebene (0 bei Ueberschneidung)."""
    def drin(p, q):
        return all((q[(i + 1) % len(q)][0] - q[i][0]) * (p[1] - q[i][1]) - (q[(i + 1) % len(q)][1] - q[i][1]) * (p[0] - q[i][0]) >= 0 for i in range(len(q)))
    if any(drin(p, b) for p in a) or any(drin(p, a) for p in b): return 0.0

    def seg(p, s, t):
        dx, dy = t[0] - s[0], t[1] - s[1]
        k = max(0.0, min(1.0, ((p[0] - s[0]) * dx + (p[1] - s[1]) * dy) / (dx * dx + dy * dy or 1e-12)))
        return ((p[0] - s[0] - k * dx) ** 2 + (p[1] - s[1] - k * dy) ** 2) ** 0.5

    def schneiden(p1, p2, p3, p4):
        d = lambda a, b, c: (b[0] - a[0]) * (c[1] - a[1]) - (b[1] - a[1]) * (c[0] - a[0])
        return d(p1, p2, p3) * d(p1, p2, p4) < 0 and d(p3, p4, p1) * d(p3, p4, p2) < 0
    ka = list(zip(a, a[1:] + a[:1])); kb = list(zip(b, b[1:] + b[:1]))
    if any(schneiden(*x, *y) for x in ka for y in kb): return 0.0
    return min([seg(p, *k) for p in a for k in kb] + [seg(p, *k) for p in b for k in ka])


def fahne_frei(hull, F, S1):
    """36 Fahne frei vom Schlot im Stadtblick (Rueckmeldung 17.11.2026, Grundform 4: „die Fahne sitzt direkt am Schlot“): Die
    Stadtkamera blickt 40 Grad schraeg von oben und dreht in Vierteldrehungen (Nordost, Nordwest, Suedwest, Suedost). In allen vier
    Blicken liegen Fahne (Mast, Knauf, Tuch) und Schlot (Haustein ueber dem Dach) im Bild mindestens 0,40 m auseinander (gemessen in der
    Bildebene, in Metern). Die Lage im Spiel ist eine Drehung um Vielfache von 90 Grad bzw. Spiegelung: die vier Blicke bleiben dieselben."""
    import math
    kx0, kx1, ky0, ky1 = F.KAMIN
    fahne = [p for name, mat, pts, n, v in hull if name == "Fahne" for p in pts]
    schlot = [p for name, mat, pts, n, v in hull if name == "Haus" and mat == "Bruchstein"
              for p in pts if p.z > S1.Z_TRAUFE + 1.0 and kx0 - 0.15 <= p.x <= kx1 + 0.15 and ky0 - 0.15 <= p.y <= ky1 + 0.15]
    if not fahne or not schlot:
        return [f"36. Fahne frei vom Schlot im Stadtblick: {'keine Fahne' if not fahne else 'kein Schlot'} gefunden -> FEHLER"], 1
    werte = []
    for gier in (45, 135, 225, 315):
        t, n = math.radians(gier), math.radians(40)
        d = Vector((math.cos(t) * math.cos(n), math.sin(t) * math.cos(n), -math.sin(n)))
        r = d.cross(Vector((0, 0, 1))).normalized(); u = r.cross(d)
        a = _huelle2d([(round(p.dot(r), 4), round(p.dot(u), 4)) for p in fahne])
        b = _huelle2d([(round(p.dot(r), 4), round(p.dot(u), 4)) for p in schlot])
        werte.append((_poly_abstand(a, b), gier))
    m, g = min(werte)
    ok = m >= 0.40
    return [f"36. Fahne frei vom Schlot im Stadtblick (40 Grad, vier Drehungen): kleinster Abstand im Bild {m:.2f} m (Blick {g} Grad; "
            + ", ".join(f"{w:.2f}" for w, _ in werte) + f") (Soll >= 0,40) -> {'ok' if ok else 'FEHLER'}"], 0 if ok else 1

# ── Stufe 2 und 3 (wohnhaus_elb_s23.py) ─────────────────────────────────────────────────────────────────────────────────

RAEUME_JSON = os.path.join(os.path.dirname(HIER), "raeume", "ao_atlas.json")


def raeume_s23(haus, ordner, texturen):
    """12-15 in Hinterzimmer und Bibliothek (Blatt raeume_innen_trim_elb, Atlas wohnhaus_elb_raeume_innen, Plaetze <raum>_s23; die Bibliothek
    des Steinhauses liegt wie im Buergerhaus rechteckig im Obergeschoss) und die Dreiecke der Raeume (getrennt von der Huelle)."""
    zeilen, fehler = [], 0
    for raum in ("hinterzimmer", "bibliothek"):
        glb = os.path.join(ordner, f"{haus}_{raum}_innen.glb")
        if not os.path.exists(glb):
            zeilen.append(f"12. Raum {raum}: Modell fehlt -> FEHLER"); fehler += 1
            continue
        atl = Z._alt_atlas(glb, RAEUME_JSON, "wohnhaus_elb_raeume_innen", f"{raum}_s23")
        blatt = "raeume_innen_trim_elb"
        z12, f12 = Z._alt_schwarz(glb, texturen=texturen, blatt=blatt, atlas=atl)
        zeilen.append(f"12. Keine schwarze Flaeche {raum}: {'ok' if not f12 else str(f12) + ' FEHLER'}"); zeilen += z12; fehler += f12
        zu, fu, _w = U.uv2_inseln(glb, atlas=atl)
        zeilen.append(f"13. UV2-Inseln {raum}: {'ok' if not fu else str(fu) + ' FEHLER'}"); zeilen += zu; fehler += fu
        k = (atl["kante"] / atl["zellen"]) / 512.0
        zd, fd, _w = U.uv2_dichte(glb, atlas=atl, rand_min=U.RAND_TYP_MIN * k, rand_max=U.RAND_TYP_MAX * k)
        zeilen.append(f"14. UV2-Dichte und Inselrand {raum}: {'ok' if not fd else str(fd) + ' FEHLER'}"); zeilen += zd; fehler += fd
        zn, fn = Z._alt_normal(glb, texturen=texturen, blatt=blatt, atlas=atl)
        zeilen.append(f"15. Normalmap und ORM {raum}: {'ok' if not fn else str(fn) + ' FEHLER'}"); zeilen += zn; fehler += fn
        n = sum(len(p.vertices) - 2 for o in Z._objekte(glb) for p in o.data.polygons)
        zeilen.append(f"20. Dreiecke Raum {raum} (eigenes Modell, nicht in der Huelle): {n}")
    return zeilen, fehler


def blick_licht_s23(haus, ordner, texturen):
    """12b Rueckseite offen an der Huelle, 12c ohne Licht im Standardblick in Wohnstube, Hinterzimmer und Bibliothek
    (blick_licht_pruefung.py; Klicktest 18.11.2026: Vordach, Feuerstelle und Balken fast schwarz, Pruefung 12 sah es nicht)."""
    import blick_licht_pruefung as B
    texturen = texturen or U.TEXTUREN
    zeilen, fehler = [], 0
    z, f = B.rueckseite_offen(os.path.join(ordner, f"{haus}.glb"))
    zeilen.append(f"12b. Keine offene Rueckseite aussen (von oben nur die Rueckseite sichtbar): {'ok' if not f else str(f) + ' FEHLER'}")
    zeilen += z; fehler += f
    for raum, blatt, atlas in (("", "innen_trim_s23_elb", "wohnhaus_elb_innen_ao_atlas.png"),
                               ("_hinterzimmer", "raeume_innen_trim_elb", "wohnhaus_elb_raeume_innen_ao_atlas.png"),
                               ("_bibliothek", "raeume_innen_trim_elb", "wohnhaus_elb_raeume_innen_ao_atlas.png")):
        glb = os.path.join(ordner, f"{haus}{raum}_innen.glb")
        if not os.path.exists(glb): continue
        z, f = B.licht_im_blick(glb, os.path.join(texturen, f"{blatt}_albedo.png"), os.path.join(texturen, atlas))
        zeilen.append(f"12c. Helle Flaechen im Standardblick mit direktem Licht ({raum[1:] or 'wohnstube'}): {'ok' if not f else str(f) + ' FEHLER'}")
        zeilen += z; fehler += f
        if raum: continue                                      # 38 nur in der Wohnstube (wie bei den Halblingen)
        z, f = B.blick_wand(glb)
        zeilen.append(f"38. Standardblick nicht dicht vor einer Wand (wohnstube): {'ok' if not f else 'FEHLER'}"); zeilen += z; fehler += f
    return zeilen, fehler


def tueren_s23(M, form, stufe):
    """Pruefung 21: Haustuer und Zimmertueren gegen Elb (bis 1,90 m), Elbin und Mensch (elb_masse; alles Pflicht)."""
    fen = M.fenster(form, stufe)
    t = next(f for f in fen if f[3] == "tuer")
    soll = EM.soll()
    zeilen, fehler = [], 0
    for name, (b, h) in (("Haustuer", (t[1][1] - t[1][0], t[2][1] - t[2][0])), ("Zimmertuer", M.ZIMMERTUER)):
        teile, f = [], 0
        for fig in EM.PFLICHT:
            ok = b + 1e-6 >= soll[fig]["tuer_b"] and h + 1e-6 >= soll[fig]["tuer_h"]
            teile.append(f"{fig} {'ja' if ok else 'nein'}"); f += 0 if ok else 1
        zeilen.append(f"21. {name} licht {b:.2f} x {h:.2f} m: passt " + ", ".join(teile) + (" -> ok" if not f else " -> FEHLER")); fehler += f
    return zeilen, fehler


def masse_s23(S23, form, stufe):
    """Was 25 und 26 von Stufe 2 und 3 wissen muessen: Oeffnungen mit Spitzbogen (Tuer, Giebel, Schildgiebel; um dx verschoben) und die
    Dachmasse der Menschen (Wandflucht OX, Traufe xe, Giebelenden); vorn im Steinhaus kein Schwung (Schildgiebel)."""
    M = S23.M
    dx = M.FORMEN[form]["dx"]
    bogen = []
    for wand, (a0, a1), (z0, z1), art in M.fenster(form, stufe):
        if art != "tuer" and not wand.endswith(("_gb", "_tg")): continue
        (ox, oy), (ex, ey), _, (nx, ny) = S23.WAENDE[wand]
        bogen.append(("y", oy, ny, a0 + dx, a1 + dx, z0, z1) if ex else ("x", ox + dx, nx, a0, a1, z0, z1))
    dach = dict(ox=M.OX, xe=M.OX + M.UEBER, dv=M.OVORN - M.UEBER_ORTGANG_VORN, dh=M.HINTEN + M.UEBER_ORTGANG, yw=(M.OVORN, M.HINTEN),
                dach_z=M.z_dach, vorn=stufe == 2, dx=dx, quergiebel=S23.GAUBE["y"] if stufe == 2 else None)
    return bogen, dach


def spitzbogen_s23(hull, bogen, stein):
    """Pruefung 25 fuer Stufe 2/3: wie Stufe 1 (Bogenbrett und Zwickel ganz in der Oeffnung, vor der Wandflucht); im Steinhaus sind beide
    aus Werkstein (Stoff Haustein): mindestens 8 Flaechen (3 Abschnitte je Seite: 6 Zwickel, 6 Bogenstuecke)."""
    if not stein:
        return spitzbogen_pruefen(hull, bogen)
    zeilen, fehler = [], 0
    for ax, e, aussen, u0, u1, z0, z1 in bogen:
        n = 0
        for name, mat, pts, nn, v in hull:
            if name != "Haus" or mat != "Haustein": continue
            uu = [p.x if ax == "y" else p.y for p in pts]; ee = [(p.y if ax == "y" else p.x) for p in pts]
            if min(uu) < u0 - 0.045 or max(uu) > u1 + 0.045 or min(p.z for p in pts) < z0 + 0.25 or max(p.z for p in pts) > z1 + 0.005: continue
            d = [(x - e) * aussen for x in ee]
            if min(d) < -0.005 or max(d) > 0.15: continue
            if max(p.z for p in pts) < z1 - 1.3 * (u1 - u0) / 2 - 0.01: continue
            n += 1
        ok = n >= 8
        fehler += 0 if ok else 1
        zeilen.append(f"   {ax}={e:+.2f} u {u0:.2f}..{u1:.2f} bis z {z1:.2f}: Werkstein im Bogen {n} -> {'ok' if ok else 'FEHLER'}")
    return zeilen, fehler


def dachschwung_s23(hull, dach, stoff):
    """Pruefung 26 fuer Stufe 2/3: (a) die Traufecken (am hinteren Giebel, im Buergerhaus auch vorn) stehen mindestens 15 cm hoeher als die
    Traufmitte, (b) ueber der Wandflucht liegt die Deckung wie bei den Menschen auf der Dachlinie (Unterseite dach_z, Deckung 0,10 und
    Lagenstufe 0,03: 0 bis +0,16 m), (c) die Deckung liegt obenauf (dach_verdeckung)."""
    ox, xe, dv, dh, (yw0, yw1), dx, dz = dach["ox"], dach["xe"], dach["dv"], dach["dh"], dach["yw"], dach["dx"], dach["dach_z"]
    rand = [p for name, mat, pts, n, v in hull if name == "Dach" and mat == stoff for p in pts if abs(p.x - dx) > xe - 0.12]
    ecke = max((p.z for p in rand if p.y > dh - 0.4), default=0.0)
    if dach["vorn"]: ecke = max(ecke, max((p.z for p in rand if p.y < dv + 0.4), default=0.0))
    ym = (dv + dh) / 2
    mitte = max((p.z for p in rand if abs(p.y - ym) < 0.5), default=0.0)
    # Flaechen ganz innerhalb der Wandflucht (die Lagen reichen von Giebel zu Giebel, ihre Ecken liegen an den Enden), ohne den Quergiebel
    qy = dach.get("quergiebel")
    ab = [p.z - dz(p.x - dx) for name, mat, pts, n, v in hull if name == "Dach" and mat == stoff and all(abs(q.x - dx) < ox - 0.02 for q in pts)
          and min(q.z for q in pts) > dz(ox) - 0.1                                     # nur das Hauptdach (nicht Fluegel und Schuppen)
          and not (qy and any(qy[0] - 0.3 < q.y < qy[1] + 0.3 and abs(q.x - dx) > 1.0 for q in pts)) for p in pts]
    ok_a, ok_b = ecke - mitte >= 0.15, bool(ab) and -0.01 <= min(ab) and max(ab) <= 0.16
    zeilen = [f"   Ecke {ecke:.2f}, Traufmitte {mitte:.2f}: Schwung {ecke - mitte:+.2f} m (Soll >= 0,15) -> {'ok' if ok_a else 'FEHLER'}",
              f"   ueber der Wandflucht {len(ab)} Punkte, Abstand zur Dachlinie {min(ab, default=0):+.3f}..{max(ab, default=0):+.3f} m (Soll 0..+0,16) -> {'ok' if ok_b else 'FEHLER'}"]
    return zeilen, (0 if ok_a else 1) + (0 if ok_b else 1)


def innen_s23(haus, ordner, M, dx, lade):
    """Pruefungen 28-32 der Wohnstube (Stufe 2/3; wie Stufe 1, Rueckmeldungen des Nutzers 17.11.2026):
      28 Decke geschlossen: von einem Raster 0,3 m unter der Decke (0,5 m) trifft jeder Strahl nach oben eine Flaeche;
      29 Haustuer innen an der Stelle der Oeffnung (60 %);
      30 Schlot ueber der Feuerstelle: Grundriss des Schlots (Stein ueber der Haube) im Grundriss des Herdsteins (2 cm);
      31 kein Holz im Schlot (oberhalb der Haube bis zur Decke) und keine Holzflaeche ganz in der Einfassung der Feuerstelle;
      32 Hauswappen hoch (Unterkante mindestens 1,85 m ueber dem Fussboden: ueber dem Kopf eines Elben) und nicht an der Wand der Haustuer."""
    from mathutils.bvhtree import BVHTree
    from trimsheet import BAENDER_INNEN
    import wohnhaus_mensch_s1_g1 as G1
    fl = lade(os.path.join(ordner, f"{haus}_innen.glb"))
    zeilen, fehler = [], 0
    ecken, polys = [], []
    for name, mat, pts, n, v in fl:
        polys.append(list(range(len(ecken), len(ecken) + len(pts)))); ecken += [tuple(p) for p in pts]
    baum = BVHTree.FromPolygons(ecken, polys)
    frei = gesamt = 0
    x = -M.IX + 0.3
    while x <= M.IX - 0.3:
        y = M.IV + 0.3
        while y <= M.STUBE_HINTEN - 0.3:
            gesamt += 1
            if baum.ray_cast(Vector((x + dx, y, M.Z_EG_DECKE - 0.3)), Vector((0, 0, 1)), 10.0)[0] is None: frei += 1
            y += 0.5
        x += 0.5
    zeilen.append(f"28. Decke der Wohnstube geschlossen: {gesamt} Strahlen nach oben, {frei} ins Leere -> {'ok' if not frei else 'FEHLER'}"); fehler += 1 if frei else 0
    tuer = next(f for f in M.FENSTER if f[3] == "tuer")
    a0, a1 = tuer[1][0] + dx, tuer[1][1] + dx
    z0, z1 = tuer[2]
    e_innen = M.VORN + M.T
    vielecke = [[(p.x, p.z) for p in pts] for name, mat, pts, n, v in fl
                if mat != "Innenglas" and abs(n.y) > 0.9 and all(abs(p.y - e_innen) <= 0.15 for p in pts)]

    def drin(u, z, q):
        an = False
        for (ua, za), (ub, zb) in zip(q, q[1:] + q[:1]):
            if (za > z) != (zb > z) and u < ua + (z - za) * (ub - ua) / (zb - za): an = not an
        return an
    probe = [(a0 + (a1 - a0) * (i + 0.5) / 12, z0 + (z1 - z0) * (j + 0.5) / 20) for i in range(12) for j in range(20)]
    anteil = sum(1 for u, z in probe if any(drin(u, z, q) for q in vielecke)) / len(probe)
    ok = anteil >= 0.6
    zeilen.append(f"29. Haustuer innen an der Stelle der Oeffnung (vorn, {a0:.2f}..{a1:.2f}): gedeckt {anteil * 100:.0f} % (Soll >= 60 %) -> {'ok' if ok else 'FEHLER'}"); fehler += 0 if ok else 1
    stein = BAENDER_INNEN["bruchstein"]; holz = [BAENDER_INNEN[b] for b in ("bohlen", "balken")]
    in_band = lambda v, b: b[0] - 1e-3 <= v <= b[1] + 1e-3
    z_haube = G1.Z_HAUBE
    herd = [p for name, mat, pts, n, v in fl if name.startswith("Herd") and in_band(v, stein) for p in pts]
    schlot = [p for name, mat, pts, n, v in fl if name == "Fensterseiten" and in_band(v, stein) for p in pts if p.z > z_haube + 0.3]
    bh = bs = None
    if herd and schlot:
        bh = (min(p.x for p in herd), max(p.x for p in herd), min(p.y for p in herd), max(p.y for p in herd))
        bs = (min(p.x for p in schlot), max(p.x for p in schlot), min(p.y for p in schlot), max(p.y for p in schlot))
        ok = bs[0] >= bh[0] - 0.02 and bs[1] <= bh[1] + 0.02 and bs[2] >= bh[2] - 0.02 and bs[3] <= bh[3] + 0.02
        text = f"Herdstein x {bh[0]:.2f}..{bh[1]:.2f} y {bh[2]:.2f}..{bh[3]:.2f}, Schlot x {bs[0]:.2f}..{bs[1]:.2f} y {bs[2]:.2f}..{bs[3]:.2f}"
    else:
        ok, text = False, "Herd oder Schlot (Stein) nicht gefunden"
    zeilen.append(f"30. Schlot ueber der Feuerstelle (Schlot im Grundriss des Herdsteins): {text} -> {'ok' if ok else 'FEHLER'}"); fehler += 0 if ok else 1
    funde = []
    if bh and bs:
        for name, mat, pts, n, v in fl:
            if not any(in_band(v, b) for b in holz): continue
            if any(bs[0] + 0.02 < p.x < bs[1] - 0.02 and bs[2] + 0.02 < p.y < bs[3] - 0.02 and z_haube < p.z < M.Z_EG_DECKE - 0.005 for p in pts):
                funde.append(name)
            if all(bh[0] - 0.03 <= p.x <= bh[1] + 0.03 and bh[2] - 0.03 <= p.y <= bh[3] + 0.03 and p.z <= z_haube + 0.35 for p in pts):
                funde.append(name + " (Einfassung)")
            if name.startswith("Herd") and all(bh[0] - 0.15 <= p.x <= bh[1] + 0.15 and bh[2] - 0.15 <= p.y <= bh[3] + 0.15 and p.z >= z_haube - 0.40 for p in pts):
                funde.append(name + " (Sturz, Haube)")
    zeilen.append(f"31. Kein Holz im Kamin (Schlot ueber der Haube, Einfassung der Feuerstelle mit Sturz und Haube): {len(funde)} Holzflaechen -> "
                  + ('ok' if not funde and bh else 'FEHLER ' + ', '.join(sorted(set(funde)))))
    fehler += len(funde) + (0 if bh else 1)
    wap = [p for name, mat, pts, n, v in fl if name.startswith("Einrichtung_Hauswappen") for p in pts]
    if wap:
        unten = min(p.z for p in wap) - M.Z_BODEN
        an_tuer = all(abs(p.y - M.IV) < 0.30 for p in wap)
        ok = unten >= 1.85 and not an_tuer
        text = f"Unterkante {unten:.2f} m ueber dem Fussboden (Soll >= 1,85)" + (", an der Wand der Haustuer" if an_tuer else "")
    else:
        ok, text = False, "kein Hauswappen im Modell"
    zeilen.append(f"32. Hauswappen innen hoch, nicht an der Haustuerwand: {text} -> {'ok' if ok else 'FEHLER'}"); fehler += 0 if ok else 1
    return zeilen, fehler


def main_s23(haus, ordner, form, stufe, opt):
    """Messung der Stufen 2 und 3: die Pruefungen der Menschen (wohnhaus_mensch_s23_messen: 1-11, 12-15 Huelle und Wohnstube, 16-19), Raeume
    (12-15), 20 Dreiecke (Huelle; Raeume getrennt), 21 Tueren, 22 Fahnenmast, 24 Glas innen, 25 Spitzbogen, 26 Dachschwung, 27 Fussabdruck,
    28-32 Wohnstube. 23 entfaellt (kein Legschindeldach)."""
    import wohnhaus_elb_s23 as E23
    import wohnhaus_mensch_s23 as S23
    import wohnhaus_mensch_s23_messen as MS23
    glb = os.path.join(ordner, f"{haus}.glb")
    E23.masse_anlegen(stufe, tuer_hoch=2.00 if "mensch-tuer" in opt else E23.TUER_HOCH)
    M = S23.M
    M.stufe_setzen(stufe)
    S23.waende_setzen()
    M.FENSTER = M.fenster(form, stufe)
    MS23.DACH_BIBLIOTHEK = False                              # die Bibliothek der Elben liegt auch im Steinhaus im Obergeschoss
    MS23.wappen_frei = Z.wappen_frei
    alt_kein = MS23.kein_fachwerk

    def kein_fachwerk(fl, dx=0.0):
        """Pruefung 10 der Menschen, aber die Holzakzente der Elben sind erlaubt (Nutzer 17.11.2026: Holzunterstand wie in Stufe 1, Ortgang):
        Holz ausserhalb der Mauern unter der Auskragung (Holzunterstand, Schuppen der Grundform 3) und die aufgeschwungenen Ortgangbretter
        hinter dem hinteren Giebel."""
        def elb_holz(f):
            name, mat, pts = f[0], f[1], f[2]
            if mat != "Fachwerk": return False
            if name == "Dach" and min(p.y for p in pts) > M.HINTEN - 0.05: return True
            # Vordach aus Holzschindeln (Entscheidung des Nutzers 18.11.2026): vor der Gassenwand in Hoehe der Erdgeschossdecke
            if name == "Haus" and max(p.y for p in pts) <= M.VORN + 0.01 and all(M.Z_EG_DECKE - 0.8 <= p.z <= M.Z_EG_DECKE + 0.6 for p in pts):
                return True
            return all(abs(p.x - dx) > M.BX - 0.05 for p in pts) and max(p.z for p in pts) < M.Z_EG_DECKE
        return alt_kein([f for f in fl if not elb_holz(f)], dx)
    MS23.kein_fachwerk = kein_fachwerk
    texturen = opt.get("texturen")
    zeilen, fehler = MS23.pruefe(haus, ordner, texturen)
    z, f = raeume_s23(haus, ordner, texturen); zeilen += z; fehler += f
    z, f = blick_licht_s23(haus, ordner, texturen); zeilen += z; fehler += f
    je, summe = Z.dreiecke(glb)
    zeilen.append(f"20. Dreiecke der Huelle {je} Summe {summe} (Grenze {Z.DREIECKE_HUELLE}) -> {'ok' if summe <= Z.DREIECKE_HUELLE else 'FEHLER'}")
    fehler += 0 if summe <= Z.DREIECKE_HUELLE else 1
    z, f = tueren_s23(M, form, stufe); zeilen += z; fehler += f
    z22, f22 = Z.fahne_steht(glb)
    zeilen.append(f"22. Fahnenmast steht auf dem Dach: {'ok' if not f22 else 'FEHLER'}"); zeilen += z22; fehler += f22
    zeilen.append("23. Schwerstangen: entfaellt (Schieferdach ohne Stangen und Findlinge)")
    bogen, dach = masse_s23(S23, form, stufe)
    hull = MS23.lade(glb)
    z25, f25 = spitzbogen_s23(hull, bogen, stufe == 3)
    zeilen.append(f"25. Spitzbogen nur aussen vor Haustuer und Giebeloeffnungen (innen = aussen): {'ok' if not f25 else str(f25) + ' FEHLER'}"); zeilen += z25; fehler += f25
    stoff = "Schiefer" if stufe == 3 else "Dachziegel"          # Stoffnamen der Menschen (Brandschaden, Russ); im Blatt der Elben Schiefer
    z26, f26 = dachschwung_s23(hull, dach, stoff)
    z26c, f26c = dach_verdeckung(glb, ordner, stoff); z26 += z26c; f26 += f26c
    zeilen.append(f"26. Dachschwung: Ecken gehoben, ueber den Waenden unveraendert: {'ok' if not f26 else str(f26) + ' FEHLER'}"); zeilen += z26; fehler += f26
    z27, f27 = fussabdruck(glb, haus, ordner)
    zeilen.append(f"27. Fussabdruck der Grundform (Haus und Dach im Umriss des Menschenhauses): {'ok' if not f27 else 'FEHLER'}"); zeilen += z27; fehler += f27
    z28, f28 = innen_s23(haus, ordner, M, M.FORMEN[form]["dx"], MS23.lade); zeilen += z28; fehler += f28
    z24, f24 = Z.fenster_innen(haus, ordner, MS23)
    zeilen.append(f"24. Jedes Glas innen liegt in einer Oeffnung der Huelle, mit Rahmen: {'ok' if not f24 else str(f24) + ' FEHLER'}"); zeilen += z24; fehler += f24
    kopf = ("GEGENPROBE (anderer Stand, muss Fehler finden): " if "gegenprobe" in opt else "") + f"Messung {haus} ({ordner})"
    print("MESSUNG " + kopf)
    for z in zeilen: print("MESSUNG " + z)
    print(f"MESSUNG Ergebnis: {fehler} Fehler")
    if "gegenprobe" not in opt and fehler: sys.exit(1)


def main():
    args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    opt = dict((a[2:].split("=", 1) + ["1"])[:2] for a in args if a.startswith("--"))
    haus = opt["haus"]
    ordner = opt.get("ordner", os.path.join(WURZEL, "game", "assets", "modelle", "gebaeude", "neubau"))
    form = int(haus.rsplit("_g", 1)[1])
    glb = os.path.join(ordner, f"{haus}.glb")
    stufe = int(haus.rsplit("_s", 1)[1].split("_")[0])
    if stufe >= 2:
        return main_s23(haus, ordner, form, stufe, opt)
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
    z36, f36 = fahne_frei(hull, S1.F, S1)
    if form != 4 and f36:                                    # Pflicht nur fuer Grundform 4 (Rueckmeldung); sonst Hinweis (Bericht, Offen)
        z36 = [z36[0].replace("-> FEHLER", "-> Hinweis (Pflicht nur Grundform 4)")]
        f36 = 0
    zeilen += z36; fehler += f36
    z28, f28 = innen_pruefen(haus, ordner, form, MS1); zeilen += z28; fehler += f28
    z24, f24 = Z.fenster_innen(haus, ordner, MS1)
    zeilen.append(f"24. Jedes Glas innen liegt in einer Oeffnung der Huelle, mit Rahmen: {'ok' if not f24 else str(f24) + ' FEHLER'}"); zeilen += z24; fehler += f24
    import blick_licht_pruefung as B                         # 38 auch in Stufe 1 (Klicktest NAS 19.11.2026: Grundform 3 stand 0,10 m vor der Wand)
    z38, f38 = B.blick_wand(os.path.join(ordner, f"{haus}_innen.glb"))
    zeilen.append(f"38. Standardblick nicht dicht vor einer Wand (wohnstube): {'ok' if not f38 else 'FEHLER'}"); zeilen += z38; fehler += f38
    kopf = ("GEGENPROBE (anderer Stand, muss Fehler finden): " if "gegenprobe" in opt else "") + f"Messung {haus} ({ordner})"
    print("MESSUNG " + kopf)
    for z in zeilen: print("MESSUNG " + z)
    print(f"MESSUNG Ergebnis: {fehler} Fehler")
    if "gegenprobe" not in opt and fehler: sys.exit(1)


if __name__ == "__main__":
    main()
