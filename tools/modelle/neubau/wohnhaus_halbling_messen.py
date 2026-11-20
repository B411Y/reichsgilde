"""Messung an den fertigen GLB der Halblingshaeuser Stufe 1 (Grundform 1-4): die Pruefungen der Menschenhaeuser (1-8, 12-15 aussen
und Wohnstube) und dazu 16-18 Luecken, 19 Wappen, 20 Dreiecke, 21 Tueren, 22 Fahnenmast, 23 (entfaellt: Strohdach), 24 Glas, Rahmen und
Oeffnung je Wand, 25 Rundbogen nur aussen, 26 Strohdach (Zwerchhaus und Wulst nicht im Dachgeschoss, Stroh obenauf), 27 Fussabdruck,
28-32 Wohnstube (Dach innen, Haustuer innen, Schlot ueber dem Herd, kein Holz im Kamin und in der Einfassung, Hauswappen im
Dachgeschoss), 33 Laterne frei vor Tuer, Fenster, Herd und Wappen, 34 Hauswappen im Standard-Innenblick frei, 35 Haustuer im
Standard-Innenblick, 36 Fahne frei vom Schlot im Stadtblick, dazu 12b/12c (blick_licht_pruefung.py, auch in Stufe 1), 37 Pixel-Look reine
Baender (pixel_mip_pruefung.py, nur Stufe 2/3; --alter-shader rechnet den Shader vor BAND_REIN), 38 Standardblick nicht dicht vor einer
Wand (Wohnstube; Klicktest NAS 19.11.2026) – Muster: wohnhaus_elb_messen.py (Funktionen der Zwergen-Messung
wohnhaus_zwerg_neu_messen.py), mit den Blaettern der Halblinge.

    blender -b --factory-startup --python tools/modelle/neubau/wohnhaus_halbling_messen.py -- --haus=wohnhaus_halbling_s1_g<1-4>
            [--ordner=<glb-ordner>] [--gegenprobe] [--kleine-tuer]

Die Halblingsmasse (wohnhaus_halbling_s1.masse_anlegen: Kniestock, Kehlbalken, Grundformen; Haustuer und Herdhaube der Menschen) gelten
auch hier, damit die Pruefungen der Menschen dieselben Oeffnungen erwarten, die das Bauskript gebaut hat. Blaetter: trim_halbling
(aussen), innen_trim_halbling (Wohnstube). Die Huellen liegen im AO-Atlas „wohnhaus_halbling“ mit Zellen von 1024 Bildpunkten (Pruefung
14 im Verhaeltnis wie bei den Elben). 21: Haustuer gegen Halbling (bis 1,15 m), Halblingin, Zwerg und Mensch (halbling_masse.py, Pflicht).
--gegenprobe: dieselben Pruefungen an einem anderen Stand (--ordner), die Fehler finden muessen; Rueckgabewert 1 bei Fehlern sonst.
--kleine-tuer (nur mit --gegenprobe): eine Haustuer nur fuer Halblinge (1,40 m) – Pruefung 21 muss fuer Zwerg und Mensch anschlagen.
"""
import os
import sys

import bpy                                                                # noqa: F401
from mathutils import Vector

HIER = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HIER)
import wohnhaus_zwerg_neu_messen as Z                                     # noqa: E402  (setzt die Zwergen-Blaetter, hier zurueckgesetzt)
import uv2_pruefung as U                                                  # noqa: E402
import halbling_masse as EM                                                   # noqa: E402
import wohnhaus_halbling_s1 as HB                                             # noqa: E402

WURZEL = Z.WURZEL


# Stufen 2 und 3: die Blaetter der Menschen mit "_halbling" (trim_s2_halbling, trim_s3_halbling, innen_trim_s23_halbling)
BLATT_S23 = {}


def _blatt(glb, blatt):
    """Blatt der Halblinge: das der Menschen mit "_halbling"."""
    if blatt is None:
        name = os.path.basename(glb)
        stufe = int(name.split("_s", 1)[1].split("_")[0]) if "_s" in name else 1
        if stufe >= 2:
            blatt = "innen_trim_s23" if name.endswith("_innen.glb") else f"trim_s{stufe}"
        else:
            blatt = "innen_trim" if name.endswith("_innen.glb") else "trim"
    return blatt if blatt.endswith("_halbling") else BLATT_S23.get(blatt, blatt + "_halbling")


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
    """Pruefung 21: Haustuer gegen die Figuren (halbling_masse: Hoehe + 0,20, Schulter + 0,30), alle Pflicht."""
    import wohnhaus_mensch_s1_g1 as G1
    import wohnhaus_mensch_s1 as S1
    S1.form_setzen(form); fen = S1.F.FENSTER                   # alle Grundformen der Halblinge entstehen im Rahmen von wohnhaus_mensch_s1
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
    """Was 25 und 26 von der Grundform wissen muessen (Baurahmen): Haustueren mit Rundbogen (Wandachse, Ebene, Aussenrichtung, u0, u1,
    z0, z1) und die Dachmasse (OX, Traufe xe, DV, DH, Giebelwaende, dach_z, Walm vorn, Traufhoehe, Schlot)."""
    import wohnhaus_mensch_s1 as S1
    bogen = []
    S1.form_setzen(form)
    F = S1.F
    for wand, (a0, a1), (z0, z1), art in F.FENSTER:
        if art != "tuer": continue
        (ox, oy), (ex, ey), _, (nx, ny) = F.WAENDE[wand]
        bogen.append(("y", oy, ny, a0, a1, z0, z1) if ex else ("x", ox, nx, a0, a1, z0, z1))
    dach = dict(ox=F.OX, dv=F.DACH_VORN, dh=F.DACH_HINTEN, yw=(F.VORN, F.HINTEN), dach_z=S1.dach_z, walm=bool(F.WALM), xe=F.OX + S1.UEBER,
                zt=S1.Z_TRAUFE, kamin=F.KAMIN, walm_bis=(F.DACH_VORN + F.D_WALM + 0.30) if F.WALM else None)
    return bogen, dach


def rundbogen_pruefen(hull, bogen):
    """Pruefung 25 (Rundbogen nur aussen, innen = aussen): vor jeder Haustuer liegt ein Rundbogen – Holz (Bogenbrett, mindestens 6
    Flaechen) und Putz (Zwickel) ganz in der Oeffnung (4,5 cm Rand fuer das Brett), zwischen Wandflucht und 15 cm davor, nicht tiefer als
    der Kaempfer (Oberkante minus halbe Weite). Die Oeffnung der Huelle und die Innenseite bleiben rechteckig (Pruefungen 3 und 24)."""
    zeilen, fehler = [], 0
    for ax, e, aussen, u0, u1, z0, z1 in bogen:
        holz = putz = 0
        for name, mat, pts, n, v in hull:
            if name != "Haus" or mat not in ("Fachwerk", "Lehmputz"): continue
            uu = [p.x if ax == "y" else p.y for p in pts]; ee = [(p.y if ax == "y" else p.x) for p in pts]
            if min(uu) < u0 - 0.045 or max(uu) > u1 + 0.045 or min(p.z for p in pts) < z0 + 0.25 or max(p.z for p in pts) > z1 + 0.005: continue
            d = [(x - e) * aussen for x in ee]
            if min(d) < -0.005 or max(d) > 0.15: continue        # nur vor der Wandflucht (Fensterstock und Tuerblatt liegen dahinter)
            if min(p.z for p in pts) < z1 - (u1 - u0) / 2 - 0.05: continue
            if mat == "Fachwerk": holz += 1
            else: putz += 1
        ok = holz >= 6 and putz >= 2
        fehler += 0 if ok else 1
        zeilen.append(f"   {ax}={e:+.2f} u {u0:.2f}..{u1:.2f} bis z {z1:.2f}: Bogenbrett {holz}, Zwickel {putz} -> {'ok' if ok else 'FEHLER'}")
    return zeilen, fehler


def strohdach_pruefen(hull, dach):
    """Pruefung 26 (Strohdach der Halblinge): (a) im Grundriss des Hauses liegt kein Stroh unter der Dachlinie (dach_z, 1 cm): Wulst und
    Zwerchdach ragen nicht ins Dachgeschoss; (b) die Waende des Zwerchhauses (Haus ueber der Traufe, ohne Schlot und Giebel) ebenso;
    (c) am Giebel steht der Wulst vor (Stroh vor der Ortgangkante), am Walm nicht; (d) die Strohflaechen liegen obenauf (dach_verdeckung)."""
    ox, dv, dh, (yw0, yw1), dz, zt = dach["ox"], dach["dv"], dach["dh"], dach["yw"], dach["dach_z"], dach["zt"]
    kx0, kx1, ky0, ky1 = dach["kamin"]
    wb = dach["walm_bis"]                                    # unter dem Walm liegt das Dach tiefer als dach_z: dort nicht pruefen
    innen = lambda p: abs(p.x) < ox - 0.02 and yw0 + 0.02 < p.y < yw1 - 0.02 and (wb is None or p.y > wb)
    stroh = [p.z - dz(abs(p.x)) for name, mat, pts, n, v in hull if name == "Dach" and mat in ("Stroh", "Strohkante") for p in pts if innen(p)]
    im_schlot = lambda p: kx0 - 0.15 < p.x < kx1 + 0.15 and ky0 - 0.15 < p.y < ky1 + 0.15
    wand = [p.z - dz(abs(p.x)) for name, mat, pts, n, v in hull if name == "Haus" and mat != "Bruchstein" for p in pts
            if innen(p) and p.z > zt + 0.05 and yw0 + 0.35 < p.y < yw1 - 0.35 and not im_schlot(p)]
    vor = [p for name, mat, pts, n, v in hull if name == "Dach" and mat == "Stroh" for p in pts]
    vorn = sum(1 for p in vor if p.y < dv - 0.10)
    hinten = sum(1 for p in vor if p.y > dh + 0.10)
    ok_a = not stroh or min(stroh) >= -0.01
    ok_b = not wand or min(wand) >= -0.01
    ok_c = hinten > 0 and (vorn > 0 or dach["walm"])
    zeilen = [f"   (a) Stroh im Grundriss: {len(stroh)} Punkte, tiefster {min(stroh, default=0):+.3f} m zur Dachlinie (Soll >= -0,01) -> {'ok' if ok_a else 'FEHLER'}",
              f"   (b) Zwerchhaus im Grundriss: {len(wand)} Punkte, tiefster {min(wand, default=0):+.3f} m zur Dachlinie (Soll >= -0,01) -> {'ok' if ok_b else 'FEHLER'}",
              f"   (c) Wulst vor dem Ortgang: vorn {vorn}, hinten {hinten} Punkte{' (vorn Walm)' if dach['walm'] else ''} -> {'ok' if ok_c else 'FEHLER'}"]
    return zeilen, (0 if ok_a else 1) + (0 if ok_b else 1) + (0 if ok_c else 1)


def dach_verdeckung(glb, ordner, texturen=None, stoff="Stroh"):
    """Pruefung 26 d: die Strohflaechen liegen obenauf – die gebackene Verdeckung in der Mitte jeder Schindelflaeche (Stoff „Stroh“,
    Normale nach oben) ist im Median mindestens 0,5 (kippt eine lange Grundflaeche ueber die Schindeln, backt das Dach schwarz)."""
    import numpy as np
    atl = U.atlas_von(glb)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=glb)
    tex = texturen or os.path.join(ordner, "..", "..", "..", "texturen", "neubau")                # Atlas neben dem Modellordner (Gegenprobe: --texturen)
    im = bpy.data.images.load(os.path.normpath(os.path.join(tex, atl["datei"]))); W, H = im.size
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
    return [f"   Verdeckung des Strohs (Median, {len(werte)} Flaechen): {m:.2f} (Soll >= 0,50) -> {'ok' if ok else 'FEHLER'}"], 0 if ok else 1


def fussabdruck(glb, haus, ordner):
    """Pruefung 27 (Fussabdruck der Grundform, Muster der Elben 17.11.2026): Haus und Dach des Halblingshauses bleiben in Lage im
    Spiel innerhalb des Umrisses (x, y) von Haus und Dach des Menschenhauses derselben Grundform (2 cm), damit es auf jeden Bauplatz
    passt, der diese Grundform bekommt. Die Hoehe ist frei."""
    def umriss(pfad):
        bpy.ops.wm.read_factory_settings(use_empty=True)
        bpy.ops.import_scene.gltf(filepath=pfad)
        pts = [o.matrix_world @ v.co for o in bpy.data.objects if o.type == "MESH" and o.name.split(".")[0] in ("Haus", "Dach") for v in o.data.vertices]
        return min(p.x for p in pts), max(p.x for p in pts), min(p.y for p in pts), max(p.y for p in pts), max(p.z for p in pts)
    m = umriss(os.path.join(WURZEL, "game", "assets", "modelle", "gebaeude", "neubau", haus.replace("_halbling_", "_mensch_") + ".glb"))
    e = umriss(glb)
    ok = e[0] >= m[0] - 0.02 and e[1] <= m[1] + 0.02 and e[2] >= m[2] - 0.02 and e[3] <= m[3] + 0.02
    return [f"   Halbling x {e[0]:.2f}..{e[1]:.2f} y {e[2]:.2f}..{e[3]:.2f} Hoehe {e[4]:.2f}; Mensch x {m[0]:.2f}..{m[1]:.2f} y {m[2]:.2f}..{m[3]:.2f} Hoehe {m[4]:.2f} -> {'ok' if ok else 'FEHLER'}"], 0 if ok else 1


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
        r = HB.EINFASSUNG_RAND                           # 8 cm: auch der vorstehende Sturz unter der Haube (Runde 5 hatte 3 cm und liess ihn Holz)
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


# ── Stufe 2 und 3 (wohnhaus_halbling_s23.py) ─────────────────────────────────────────────────────────────────────────────

RAEUME_JSON = os.path.join(os.path.dirname(HIER), "raeume", "ao_atlas.json")


class _Rahmen23:
    """Was die Pruefungen 33-36 der Stufe 1 (laterne_frei, blick_frei, fahne_frei) vom Baurahmen wissen: Oeffnungen und Waende in Lage
    im Modell (der Kern ist um dx verschoben), Raummitte, Schornstein, Traufe und Wandstaerke; die Modelle der Stufen 2/3 sind nicht
    gedreht (zurueck = Identitaet)."""

    def __init__(self, S23, dx):
        M = S23.M
        self.WAENDE = {w: ((ox + dx, oy), e, l, n) for w, ((ox, oy), e, l, n) in S23.WAENDE.items()}
        self.FENSTER = [(w, (a0 + dx, a1 + dx) if S23.WAENDE[w][1][0] else (a0, a1), z, art) for w, (a0, a1), z, art in M.FENSTER]
        self.RAUM = Vector((S23.RAUM.x + dx, S23.RAUM.y, S23.RAUM.z))
        x0, x1, y0, y1 = M.SCHORNSTEIN_RECHTS
        self.KAMIN = (x0 + dx, x1 + dx, y0, y1)
        self.T = M.T
        self.Z_TRAUFE = M.Z_TRAUFE
        self.zurueck = lambda v: v


def raeume_s23(haus, ordner, texturen):
    """12-15 in Hinterzimmer und Bibliothek (Blatt raeume_innen_trim_halbling, Atlas wohnhaus_halbling_raeume_innen, Plaetze <raum>_s23;
    die Bibliothek des Steinhauses liegt wie im Buergerhaus rechteckig im Obergeschoss) und die Dreiecke der Raeume (nicht in der Huelle)."""
    zeilen, fehler = [], 0
    for raum in ("hinterzimmer", "bibliothek"):
        glb = os.path.join(ordner, f"{haus}_{raum}_innen.glb")
        if not os.path.exists(glb):
            zeilen.append(f"12. Raum {raum}: Modell fehlt -> FEHLER"); fehler += 1
            continue
        atl = Z._alt_atlas(glb, RAEUME_JSON, "wohnhaus_halbling_raeume_innen", f"{raum}_s23")
        blatt = "raeume_innen_trim_halbling"
        z12, f12 = Z._alt_schwarz(glb, texturen=texturen, blatt=blatt, atlas=atl)
        zeilen.append(f"12. Keine schwarze Flaeche {raum}: {'ok' if not f12 else str(f12) + ' FEHLER'}"); zeilen += z12; fehler += f12
        zu, fu, _w = U.uv2_inseln(glb, atlas=atl)
        zeilen.append(f"13. UV2-Inseln {raum}: {'ok' if not fu else str(fu) + ' FEHLER'}"); zeilen += zu; fehler += fu
        k = (atl["kante"] / atl["zellen"]) / 512.0
        zd, fd, _w = _alt_dichte(glb, atlas=atl, rand_min=U.RAND_TYP_MIN * k, rand_max=U.RAND_TYP_MAX * k)
        zeilen.append(f"14. UV2-Dichte und Inselrand {raum}: {'ok' if not fd else str(fd) + ' FEHLER'}"); zeilen += zd; fehler += fd
        zn, fn = Z._alt_normal(glb, texturen=texturen, blatt=blatt, atlas=atl)
        zeilen.append(f"15. Normalmap und ORM {raum}: {'ok' if not fn else str(fn) + ' FEHLER'}"); zeilen += zn; fehler += fn
        n = sum(len(p.vertices) - 2 for o in Z._objekte(glb) for p in o.data.polygons)
        zeilen.append(f"20. Dreiecke Raum {raum} (eigenes Modell, nicht in der Huelle): {n}")
    return zeilen, fehler


def blick_licht_s23(haus, ordner, texturen):
    """12b Rueckseite offen an der Huelle, 12c ohne Licht im Standardblick in Wohnstube, Hinterzimmer und Bibliothek
    (blick_licht_pruefung.py, aus dem Klicktest der Elben 18.11.2026)."""
    import blick_licht_pruefung as B
    texturen = texturen or U.TEXTUREN
    zeilen, fehler = [], 0
    z, f = B.rueckseite_offen(os.path.join(ordner, f"{haus}.glb"))
    zeilen.append(f"12b. Keine offene Rueckseite aussen (von oben nur die Rueckseite sichtbar): {'ok' if not f else str(f) + ' FEHLER'}")
    zeilen += z; fehler += f
    for raum, blatt in (("", "innen_trim_s23_halbling"), ("_hinterzimmer", "raeume_innen_trim_halbling"), ("_bibliothek", "raeume_innen_trim_halbling")):
        glb = os.path.join(ordner, f"{haus}{raum}_innen.glb")
        if not os.path.exists(glb): continue
        z, f = B.licht_im_blick(glb, os.path.join(texturen, f"{blatt}_albedo.png"))
        zeilen.append(f"12c. Helle Flaechen im Standardblick mit direktem Licht ({raum[1:] or 'wohnstube'}): {'ok' if not f else str(f) + ' FEHLER'}")
        zeilen += z; fehler += f
        if raum: continue                                      # 38 nur in der Wohnstube (Hinterzimmer und Bibliothek sind enger)
        z, f = B.blick_wand(glb)
        zeilen.append(f"38. Standardblick nicht dicht vor einer Wand (wohnstube): {'ok' if not f else 'FEHLER'}"); zeilen += z; fehler += f
    return zeilen, fehler


def blick_licht_s1(haus, ordner, texturen):
    """Stufe 1 (Klicktest NAS 19.11.2026): 12b an der Huelle, 12c und 38 in der Wohnstube (Blatt innen_trim_halbling)."""
    import blick_licht_pruefung as B
    texturen = texturen or U.TEXTUREN
    zeilen, fehler = [], 0
    z, f = B.rueckseite_offen(os.path.join(ordner, f"{haus}.glb"))
    zeilen.append(f"12b. Keine offene Rueckseite aussen (von oben nur die Rueckseite sichtbar): {'ok' if not f else str(f) + ' FEHLER'}")
    zeilen += z; fehler += f
    glb = os.path.join(ordner, f"{haus}_innen.glb")
    z, f = B.licht_im_blick(glb, os.path.join(texturen, "innen_trim_halbling_albedo.png"))
    zeilen.append(f"12c. Helle Flaechen im Standardblick mit direktem Licht (wohnstube): {'ok' if not f else str(f) + ' FEHLER'}")
    zeilen += z; fehler += f
    z, f = B.blick_wand(glb)
    zeilen.append(f"38. Standardblick nicht dicht vor einer Wand (wohnstube): {'ok' if not f else 'FEHLER'}"); zeilen += z; fehler += f
    return zeilen, fehler


def pixel_baender(glb, stufe, texturen, opt):
    """37 Pixel-Look, reine Baender (pixel_mip_pruefung.py, Klicktest NAS 19.11.2026): Deckung blau und matt, Steinbaender ohne
    Streifen und ohne Metall, wie der Shader mit BAND_REIN im Pixel-Look liest (Pixel 2 und 3, Zoom 25/50/100). --alter-shader:
    Gegenprobe mit dem Shader vor BAND_REIN."""
    import pixel_mip_pruefung as PM
    tex = texturen or U.TEXTUREN
    stein = (0.85, 0.95) if stufe == 2 else (0.50, 0.70, 0.85, 0.95)       # STEIN_EBENE 10 und 11 (building_trim.gdshaderinc)
    z, f = PM.pruefe(glb, os.path.join(tex, f"trim_s{stufe}_halbling_albedo.png"), stein, "alter-shader" not in opt, stroh_teil=0.125 if stufe == 3 else 0.0)
    zeilen = [f"37. Pixel-Look: Deckung blau und matt, Stein ohne Streifen{' (Shader vor BAND_REIN)' if 'alter-shader' in opt else ''}: {'ok' if not f else str(f) + ' FEHLER'}"]
    return zeilen + z, f


def tueren_s23(M, form, stufe):
    """Pruefung 21: Haustuer und Zimmertueren gegen Halbling (bis 1,15 m), Halblingin, Zwerg und Mensch (halbling_masse; alles Pflicht)."""
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


def rundbogen_s23(hull, S23, form, stufe):
    """Pruefung 25 fuer Stufe 2/3: Rundbogen nur aussen vor der Haustuer wie Stufe 1 (rundbogen_pruefen); im Steinhaus sind Zwickel und
    Bogen aus Werkstein (Stoff Haustein): mindestens 8 Flaechen vor der Wand in der Oeffnung ueber dem Kaempfer."""
    M = S23.M
    dx = M.FORMEN[form]["dx"]
    bogen = []
    for wand, (a0, a1), (z0, z1), art in M.fenster(form, stufe):
        if art != "tuer": continue
        (ox, oy), (ex, ey), _, (nx, ny) = S23.WAENDE[wand]
        bogen.append(("y", oy, ny, a0 + dx, a1 + dx, z0, z1) if ex else ("x", ox + dx, nx, a0, a1, z0, z1))
    if stufe == 2:
        return rundbogen_pruefen(hull, bogen)
    zeilen, fehler = [], 0
    for ax, e, aussen, u0, u1, z0, z1 in bogen:
        n = 0
        for name, mat, pts, nn, v in hull:
            if name != "Haus" or mat != "Haustein": continue
            uu = [p.x if ax == "y" else p.y for p in pts]; ee = [(p.y if ax == "y" else p.x) for p in pts]
            if min(uu) < u0 - 0.045 or max(uu) > u1 + 0.045 or min(p.z for p in pts) < z0 + 0.25 or max(p.z for p in pts) > z1 + 0.005: continue
            d = [(x - e) * aussen for x in ee]
            if min(d) < -0.005 or max(d) > 0.15: continue
            if min(p.z for p in pts) < z1 - (u1 - u0) / 2 - 0.05: continue
            n += 1
        ok = n >= 8
        fehler += 0 if ok else 1
        zeilen.append(f"   {ax}={e:+.2f} u {u0:.2f}..{u1:.2f} bis z {z1:.2f}: Werkstein im Bogen {n} -> {'ok' if ok else 'FEHLER'}")
    return zeilen, fehler


def dach_s23(glb, hull, S23, form, stufe, texturen=None, ebenen_datei=None):
    """Pruefung 26 fuer Stufe 2/3 nach den Vorlagen (Nutzer 18.11.2026: Backstein, blaues Dach, kein Stroh, im Steinhaus Kupfergauben):
      (a) kein Stroh an Haus und Dach (Stoffe Stroh, Strohkante);
      (b) ueber der Wandflucht liegt die Deckung wie bei den Menschen auf der Dachlinie (Unterseite dach_z, Deckung 0,10, Lagenstufe 0,03:
          0 bis +0,16 m; ohne Gaube bzw. Quergiebel);
      (c) die Deckung ist blau: Mittel der Albedo der Dachflaechen im Band „stroh_franse“ des Blatts trim_s<n>_halbling, Blau ueber Rot
          und Gruen;
      (d) Steinhaus: die Gaube hat ein Kupferdach mit Gruenspan (Dachflaechen der Gaube im Band „stroh“: Gruen ueber Rot);
      (e) Backstein: im Blatt ist das Band „bruchstein“ rot (Rot mindestens 1,25 x Gruen und 1,4 x Blau), und die Steinebene
          halbling_s<n> setzt dort den Backstein K6 ein (trimsheet_ebenen.py: Eintrag und Stein medieval_red_brick)."""
    import re
    import uv2_pruefung as U
    from trimsheet import BAENDER_AUSSEN as BA
    M = S23.M
    dx = M.FORMEN[form]["dx"]
    zeilen, fehler = [], 0
    stroh = sum(1 for name, mat, pts, n, v in hull if name in ("Haus", "Dach") and mat.startswith("Stroh"))
    ok = stroh == 0
    zeilen.append(f"   (a) Stroh an Haus und Dach: {stroh} Flaechen (Soll 0) -> {'ok' if ok else 'FEHLER'}"); fehler += 0 if ok else 1
    stoff = "Schiefer" if stufe == 3 else "Dachziegel"
    qy = S23.GAUBE["y"]
    dz = M.z_dach
    ab = [p.z - dz(p.x - dx) for name, mat, pts, n, v in hull if name == "Dach" and mat == stoff and all(abs(q.x - dx) < M.OX - 0.02 for q in pts)
          and min(q.z for q in pts) > dz(M.OX) - 0.1 and not any(qy[0] - 0.3 < q.y < qy[1] + 0.3 and abs(q.x - dx) > 0.8 for q in pts) for p in pts]
    ok = bool(ab) and -0.01 <= min(ab) and max(ab) <= 0.16
    zeilen.append(f"   (b) Deckung ueber der Wandflucht: {len(ab)} Punkte, Abstand zur Dachlinie {min(ab, default=0):+.3f}..{max(ab, default=0):+.3f} m (Soll 0..+0,16) -> {'ok' if ok else 'FEHLER'}")
    fehler += 0 if ok else 1
    tex = texturen or U.TEXTUREN
    alb = U._bild(os.path.join(tex, f"trim_s{stufe}_halbling_albedo.png"))
    H, W = alb.shape[:2]
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=glb)
    sf, st, bs = BA["stroh_franse"], BA["stroh"], BA["bruchstein"]
    deck, kupfer = [], []
    for o in bpy.data.objects:
        if o.type != "MESH" or not o.name.startswith("Dach"): continue
        me = o.data; uvl = me.uv_layers["UVMap"].data
        for p in me.polygons:
            if me.materials[p.material_index].name.split(".")[0] != stoff: continue
            u = sum(uvl[i].uv[0] for i in p.loop_indices) / p.loop_total; vb = sum(uvl[i].uv[1] for i in p.loop_indices) / p.loop_total
            px = alb[min(max(int(vb * H), 0), H - 1), int((u % 1.0) * W) % W, :3]
            c = o.matrix_world @ p.center
            vg = 1.0 - vb
            if sf[0] <= vg <= sf[1]: deck.append(px)
            elif st[0] <= vg <= st[1] and (o.matrix_world.to_3x3() @ p.normal).z > 0.3 and qy[0] - 0.4 < c.y < qy[1] + 0.4 and c.z > M.Z_TRAUFE + 0.5:
                kupfer.append(px)
    mittel = lambda l: [sum(x[k] for x in l) / len(l) for k in range(3)] if l else [0.0, 0.0, 0.0]
    r, g, b = mittel(deck)
    ok = bool(deck) and b > r * 1.05 and b > g
    zeilen.append(f"   (c) Dach blau: {len(deck)} Flaechen, Mittel R {r:.2f} G {g:.2f} B {b:.2f} (Soll B > 1,05 R und B > G) -> {'ok' if ok else 'FEHLER'}")
    fehler += 0 if ok else 1
    if stufe == 3:
        r, g, b = mittel(kupfer)
        ok = len(kupfer) >= 2 and g > r * 1.1
        zeilen.append(f"   (d) Gaube mit Kupferdach (Gruenspan): {len(kupfer)} Flaechen, Mittel R {r:.2f} G {g:.2f} B {b:.2f} (Soll >= 2, G > 1,1 R) -> {'ok' if ok else 'FEHLER'}")
        fehler += 0 if ok else 1
    a0, a1 = int((1.0 - bs[1]) * H), int((1.0 - bs[0]) * H)
    band = alb[a0:a1, :, :3].reshape(-1, 3).mean(axis=0)
    ok_blatt = band[0] >= band[1] * 1.25 and band[0] >= band[2] * 1.4
    text = open(ebenen_datei or os.path.join(HIER, "trimsheet_ebenen.py"), encoding="utf-8").read()
    stein = re.search(r'\("halbling_s%d", "trim_s%d_halbling", \{"bruchstein": "(k\d)"' % (stufe, stufe), text)
    k = stein.group(1) if stein else None
    ok_ebene = bool(k) and re.search(r'"%s": dict\(name="[a-z_]*brick' % k, text) is not None
    ok = ok_blatt and ok_ebene
    zeilen.append(f"   (e) Backstein: Band „bruchstein“ im Blatt R {band[0]:.2f} G {band[1]:.2f} B {band[2]:.2f}, Steinebene halbling_s{stufe} Stein {k} "
                  f"{'(Backstein)' if ok_ebene else '(kein Backstein)'} -> {'ok' if ok else 'FEHLER'}")
    fehler += 0 if ok else 1
    return zeilen, fehler


def innen_s23(haus, ordner, M, dx, lade, S23):
    """Pruefungen 28-35 der Wohnstube (Stufe 2/3; wie bei den Elben und in Stufe 1):
      28 Decke geschlossen: von einem Raster 0,3 m unter der Decke (0,5 m) trifft jeder Strahl nach oben eine Flaeche;
      29 Haustuer innen an der Stelle der Oeffnung (60 %);
      30 Schlot ueber der Feuerstelle: Grundriss des Schlots (Stein ueber der Haube) im Grundriss des Herdsteins (2 cm);
      31 kein Holz im Schlot (oberhalb der Haube bis zur Decke) und keine Holzflaeche ganz in der Einfassung der Feuerstelle;
      32 Hauswappen hoch (Unterkante mindestens 1,85 m ueber dem Fussboden: ueber dem Kopf eines Menschen) und nicht an der Haustuerwand;
      33-35 wie Stufe 1 (laterne_frei, blick_frei)."""
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
    R = _Rahmen23(S23, dx)
    z, f = laterne_frei(fl, R, R, R); zeilen += z; fehler += f
    z, f = blick_frei(fl, baum, R, R, R); zeilen += z; fehler += f
    return zeilen, fehler


def main_s23(haus, ordner, form, stufe, opt):
    """Messung der Stufen 2 und 3: die Pruefungen der Menschen (wohnhaus_mensch_s23_messen: 1-11, 12-15 Huelle und Wohnstube, 16-19),
    Raeume (12-15), 12b/12c, 20 Dreiecke (Huelle; Raeume getrennt), 21 Tueren, 22 Fahnenmast, 24 Glas innen, 25 Rundbogen, 26 Dach und Stein nach den Vorlagen (dach_s23),
    27 Fussabdruck, 28-35 Wohnstube, 36 Fahne frei vom Schlot (Hinweis). 23 entfaellt (Strohdach)."""
    import wohnhaus_halbling_s23 as H23
    import wohnhaus_mensch_s23 as S23
    import wohnhaus_mensch_s23_messen as MS23
    glb = os.path.join(ordner, f"{haus}.glb")
    H23.masse_anlegen(stufe)
    M = S23.M
    M.stufe_setzen(stufe)
    S23.waende_setzen()
    M.FENSTER = M.fenster(form, stufe)
    if "kleine-tuer" in opt:                                 # Gegenprobe 21: Haustuer und Zimmertueren nur fuer Halblinge (1,40 m)
        M.FENSTER = [(w, a, (z[0], z[0] + 1.40), art) if art == "tuer" else (w, a, z, art) for w, a, z, art in M.FENSTER]
        alt_fenster = M.fenster
        M.fenster = lambda f, s=2: [(w, a, (z[0], z[0] + 1.40), art) if art == "tuer" else (w, a, z, art) for w, a, z, art in alt_fenster(f, s)]
        M.ZIMMERTUER = (M.ZIMMERTUER[0], 1.40)
    MS23.DACH_BIBLIOTHEK = False                              # die Bibliothek der Halblinge liegt auch im Steinhaus im Obergeschoss
    MS23.wappen_frei = Z.wappen_frei
    dx = M.FORMEN[form]["dx"]
    texturen = opt.get("texturen")
    zeilen, fehler = MS23.pruefe(haus, ordner, texturen)
    z, f = raeume_s23(haus, ordner, texturen); zeilen += z; fehler += f
    z, f = blick_licht_s23(haus, ordner, texturen); zeilen += z; fehler += f
    z, f = pixel_baender(glb, stufe, texturen, opt); zeilen += z; fehler += f
    je, summe = Z.dreiecke(glb)
    zeilen.append(f"20. Dreiecke der Huelle {je} Summe {summe} (Grenze {Z.DREIECKE_HUELLE}) -> {'ok' if summe <= Z.DREIECKE_HUELLE else 'FEHLER'}")
    fehler += 0 if summe <= Z.DREIECKE_HUELLE else 1
    z, f = tueren_s23(M, form, stufe); zeilen += z; fehler += f
    z22, f22 = Z.fahne_steht(glb)
    zeilen.append(f"22. Fahnenmast steht auf dem Dach: {'ok' if not f22 else 'FEHLER'}"); zeilen += z22; fehler += f22
    zeilen.append("23. Schwerstangen: entfaellt (Strohdach)")
    hull = MS23.lade(glb)
    z25, f25 = rundbogen_s23(hull, S23, form, stufe)
    zeilen.append(f"25. Rundbogen nur aussen vor der Haustuer (innen = aussen): {'ok' if not f25 else str(f25) + ' FEHLER'}"); zeilen += z25; fehler += f25
    z26, f26 = dach_s23(glb, hull, S23, form, stufe, texturen, opt.get("ebenen"))
    z26d, f26d = dach_verdeckung(glb, ordner, texturen, "Schiefer" if stufe == 3 else "Dachziegel"); z26 += z26d; f26 += f26d
    zeilen.append(f"26. Dach und Stein nach den Vorlagen: kein Stroh, Deckung auf der Dachlinie, blau, Kupfergaube (Stufe 3), Backstein, Deckung obenauf: {'ok' if not f26 else str(f26) + ' FEHLER'}"); zeilen += z26; fehler += f26
    z27, f27 = fussabdruck(glb, haus, ordner)
    zeilen.append(f"27. Fussabdruck der Grundform (Haus und Dach im Umriss des Menschenhauses): {'ok' if not f27 else 'FEHLER'}"); zeilen += z27; fehler += f27
    hull = MS23.lade(glb)
    R = _Rahmen23(S23, dx)
    hull36 = [(name, "Bruchstein" if mat == "Haustein" else mat, pts, n, v) for name, mat, pts, n, v in hull]
    z36, f36 = fahne_frei(hull36, R, R)
    if f36:                                                  # Pruefung 36 nur Hinweis (Auftrag 18.11.2026)
        z36 = [z36[0].replace("-> FEHLER", "-> Hinweis (nur Hinweis in Stufe 2/3)")]
        f36 = 0
    zeilen += z36
    z28, f28 = innen_s23(haus, ordner, M, dx, MS23.lade, S23); zeilen += z28; fehler += f28
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
    HB.masse_anlegen()                                      # Formen und Masse der Halblinge
    if "kleine-tuer" in opt:                                 # Gegenprobe 21: Haustuer nur fuer Halblinge (1,40 m)
        S1.TUER_HOCH = 1.40
    import wohnhaus_mensch_s1_messen as MS1
    alt_masse = MS1.masse

    def masse(f):                                            # alle Grundformen im Rahmen von wohnhaus_mensch_s1 (eigene Form der Halblinge)
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
    zeilen.append("23. Schwerstangen: entfaellt (Strohdach)")
    bogen, dach = grundform_masse(form)
    MS1.WELT = MS1.masse(form)["welt"]
    hull = MS1.lade(glb)
    z25, f25 = rundbogen_pruefen(hull, bogen)
    zeilen.append(f"25. Rundbogen nur aussen vor der Haustuer (innen = aussen): {'ok' if not f25 else str(f25) + ' FEHLER'}"); zeilen += z25; fehler += f25
    z26, f26 = strohdach_pruefen(hull, dach)
    z26d, f26d = dach_verdeckung(glb, ordner); z26 += z26d; f26 += f26d
    zeilen.append(f"26. Strohdach: Zwerchhaus und Wulst nicht im Dachgeschoss, Wulst am Giebel, Stroh obenauf: {'ok' if not f26 else str(f26) + ' FEHLER'}"); zeilen += z26; fehler += f26
    z27, f27 = fussabdruck(glb, haus, ordner)
    zeilen.append(f"27. Fussabdruck der Grundform (Haus und Dach im Umriss des Menschenhauses): {'ok' if not f27 else 'FEHLER'}"); zeilen += z27; fehler += f27
    z36, f36 = fahne_frei(hull, S1.F, S1)
    if form != 4 and f36:                                    # Pflicht nur fuer Grundform 4 (Rueckmeldung); sonst Hinweis (Bericht, Offen)
        z36 = [z36[0].replace("-> FEHLER", "-> Hinweis (Pflicht nur Grundform 4)")]
        f36 = 0
    zeilen += z36; fehler += f36
    z28, f28 = innen_pruefen(haus, ordner, form, MS1); zeilen += z28; fehler += f28
    z, f = blick_licht_s1(haus, ordner, opt.get("texturen")); zeilen += z; fehler += f
    z24, f24 = Z.fenster_innen(haus, ordner, MS1)
    zeilen.append(f"24. Jedes Glas innen liegt in einer Oeffnung der Huelle, mit Rahmen: {'ok' if not f24 else str(f24) + ' FEHLER'}"); zeilen += z24; fehler += f24
    kopf = ("GEGENPROBE (anderer Stand, muss Fehler finden): " if "gegenprobe" in opt else "") + f"Messung {haus} ({ordner})"
    print("MESSUNG " + kopf)
    for z in zeilen: print("MESSUNG " + z)
    print(f"MESSUNG Ergebnis: {fehler} Fehler")
    if "gegenprobe" not in opt and fehler: sys.exit(1)


if __name__ == "__main__":
    main()
