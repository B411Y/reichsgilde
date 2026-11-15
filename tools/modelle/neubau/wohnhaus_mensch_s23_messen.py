"""Messung an den fertigen GLB der Wohnhaeuser Mensch Stufe 2/3: Fenster gegen Holz, Giebelfachwerk, Fenster innen = aussen,
Moebel vor Fenstern.

    blender -b --factory-startup --python tools/modelle/neubau/wohnhaus_mensch_s23_messen.py -- --haus=wohnhaus_mensch_s2_g1 [--ordner=<glb-ordner>] [--gegenprobe]

Prueft (Nutzer, Rueckmeldung zur Probe 14.11.2026):
  1. Kein Holz schneidet ein Fenster: Jede Holzflaeche („Fachwerk“ in Haus und Dach), die in der Ebene eines Fensters liegt (bis
     0,30 m davor/dahinter) und in die Oeffnung ragt, ist ein Fehler – ausser den Teilen des Fensters selbst (Fensterstock, Rahmen:
     ganz innerhalb der Oeffnung plus 6 cm). Holz, das die Oeffnung nicht beruehrt, haelt mindestens FENSTER_ABSTAND (0,05 m);
     anliegende Rahmenhoelzer (Fachwerkstiele und Riegel als Laibung, Abstand 0) werden gezaehlt, aber nicht als Fehler gewertet.
  2. Giebelfachwerk: spiegelgleich zur Mitte (jeder Eckpunkt des Holzes hat sein Spiegelbild, 2 cm Toleranz) und bis unter den
     First (hoechstes Holz hoechstens 0,35 m unter der Unterseite des Firsts). Fensterlaeden (Band „farbholz“) gehoeren zum Fenster.
  3. Fenster innen = aussen: jedes Aussenfenster, das zu einem Raum gehoert (Wohnstube; Raum im Obergeschoss), hat im Modell des
     Raums ein Glas in derselben Ebene, Lage und Groesse (3 cm); Fenster von Flur, Treppengang und Dachboden werden genannt.
  4. Kein Moebel vor einem Fenster: keine Ecke eines Moebels im Raum vor der Oeffnung (Oeffnung plus 5 cm, bis 0,45 m in den Raum).
  5. Gleiche Fensterbreite je Geschoss und Seite (1 cm).
  7. Steinhaus: genau ein Fenster im Treppengiebel, mittig zur Giebelachse (1 cm).
  8. Steinhaus: jedes Moebel der Bibliothek im Dachgeschoss bleibt 3 cm unter den Sparren; begehbare Zone fuer Mensch und Elb.
  9. Steinhaus: zwei Vollgeschosse, Traufe und First wie die Vorlagen (wohnhaus_mensch_s23_masse.SOLL_*).
  10. Steinhaus ganz aus Stein (Nutzer 14.11.2026): kein Holz der Fachwerkbaender an Haus und Dach (ueber 0,7 m); statt Pruefung 2 und
      6: Fenster der Giebelseiten spiegelgleich, 11. Laibung aus Stein an allen vier Seiten jedes Fensters, Tiefe je Fenster gleich.
      10 erlaubt seit 15.11.2026 dunkle Holzakzente (Balkenkoepfe, Laeden, Traufe), verboten bleiben Staender, Riegel, Streben.
  12.-15. Gemeinsame Pruefungen aller Stufen aus uv2_pruefung.py (Grenzwerte dort im Kopf), Huelle und Wohnstube: 12 keine
      schwarze Flaeche (schwarze_flaechen), 13 UV2-Inseln (uv2_inseln), 14 UV2-Dichte und Inselrand (uv2_dichte), 15 Normalmap und
      ORM (normal_orm); Blatt aussen trim_s<stufe>, innen innen_trim_s23.
  16.-18. Lueckenpruefung der Huelle (luecken_pruefung.py, aus phase5/stein-ebenen): 16 kein Durchblick durch die Huelle,
      17 kein Bandueberlauf, 18 kein Eisenteil, das sich als Spalt liest; Rueckseiten werden nur gezaehlt (Hinweis).
  19. Wappen frei (Regel wohnhaus_mensch_s23.wappen_ort): in der Ansicht der Gassenwand mindestens 5 cm zu Laden, Fenster,
      Gewaende, Pfosten, Fachwerkholz, Tuer und Vordach.
  6. Fenster buendig im Gefach (Konstruktionsregel Fachwerk): in Fachwerkwaenden Abstand zum Holz links, rechts, unten, oben je
     hoechstens 0,02 m und untereinander gleich (1 cm). Die Giebelseiten (Pruefung 2) gelten ab dem Obergeschoss als spiegelgleich.
--gegenprobe: dieselben Pruefungen an einem aelteren Stand (--ordner, fuer 12-15 mit --texturen=<ordner der Trim-Sheets und Atlanten
dieses Stands>), die Fehler finden muessen.
Rueckgabewert 1 bei Fehlern (ohne --gegenprobe).
"""
import os
import sys

import bpy
from mathutils import Vector

HIER = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HIER)
import wohnhaus_mensch_s23_masse as M                                     # noqa: E402
import uv2_pruefung as U                                                  # noqa: E402
import luecken_pruefung as L                                              # noqa: E402

WURZEL = os.path.dirname(os.path.dirname(os.path.dirname(HIER)))
TOL = 0.005


def lade(pfad):
    """GLB in eine leere Szene: Liste (Objektname ohne Endung, Material, Eckpunkte in Welt, Normale) je Flaeche."""
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=pfad)
    flaechen = []
    for o in bpy.data.objects:
        if o.type != "MESH": continue
        mw = o.matrix_world
        name = o.name.split(".")[0]
        uvl = o.data.uv_layers.get("UVMap") or (o.data.uv_layers[0] if o.data.uv_layers else None)
        for p in o.data.polygons:
            mat = o.data.materials[p.material_index].name.split(".")[0] if o.data.materials else ""
            pts = [mw @ o.data.vertices[i].co for i in p.vertices]
            n = (mw.to_3x3() @ p.normal).normalized()
            v = (1.0 - sum(uvl.data[i].uv[1] for i in p.loop_indices) / len(p.loop_indices)) if uvl else -1.0   # UV.y wie in Godot
            flaechen.append((name, mat, pts, n, v))
    return flaechen


def achse(n):
    return "x" if abs(n.x) > 0.9 else "y" if abs(n.y) > 0.9 else None


def rechteck(pts, ax):
    """(Ebene, u0, u1, z0, z1) einer senkrechten Flaeche: u entlang der Wand."""
    u = [p.y if ax == "x" else p.x for p in pts]
    e = sum(p.x if ax == "x" else p.y for p in pts) / len(pts)
    return e, min(u), max(u), min(p.z for p in pts), max(p.z for p in pts)


RICHTUNG = {}          # (Achse, Ebene) eines Aussenfensters -> Richtung nach aussen (+1/-1)


def aussen_davor(ax, e, pe):
    """Liegt das Holz vor dem Glas oder in seiner Ebene (nicht in der Laibung einer Nachbarwand dahinter)?"""
    return (pe - e) * RICHTUNG.get((ax, round(e, 2)), 0) >= -0.02 or (ax, round(e, 2)) not in RICHTUNG


def fenster_aussen(fl):
    """Aussenfenster aus den Scheiben (Material „Fenster“): (Achse, Ebene, u0, u1, z0, z1)."""
    out = []
    for name, mat, pts, n, *_ in fl:
        if name != "Scheiben" or mat != "Fenster": continue
        ax = achse(n)
        if ax is None: continue
        e, u0, u1, z0, z1 = rechteck(pts, ax)
        RICHTUNG[(ax, round(e, 2))] = 1 if (n.x if ax == "x" else n.y) > 0 else -1
        for i, (a, e2, v0, v1, w0, w1) in enumerate(out):    # Dreiecke derselben Scheibe zusammenfassen
            if a == ax and abs(e2 - e) < 0.01 and min(u1, v1) - max(u0, v0) > -0.01 and min(z1, w1) - max(z0, w0) > -0.01:
                out[i] = (a, e2, min(u0, v0), max(u1, v1), min(z0, w0), max(z1, w1)); break
        else:
            out.append((ax, e, u0, u1, z0, z1))
    return out


def _schnitt(poly, u0, u1, z0, z1):
    """Polygon (u, z) auf das Rechteck zuschneiden (Sutherland-Hodgman); Flaeche des Rests."""
    def klipp(pts, innen, schnitt):
        aus = []
        for i, p in enumerate(pts):
            q = pts[i - 1]
            if innen(p):
                if not innen(q): aus.append(schnitt(q, p))
                aus.append(p)
            elif innen(q): aus.append(schnitt(q, p))
        return aus
    def an_u(c):
        return lambda q, p: (c, q[1] + (p[1] - q[1]) * (c - q[0]) / (p[0] - q[0]))
    def an_z(c):
        return lambda q, p: (q[0] + (p[0] - q[0]) * (c - q[1]) / (p[1] - q[1]), c)
    pts = list(poly)
    for innen, sch in ((lambda p: p[0] >= u0, an_u(u0)), (lambda p: p[0] <= u1, an_u(u1)), (lambda p: p[1] >= z0, an_z(z0)), (lambda p: p[1] <= z1, an_z(z1))):
        pts = klipp(pts, innen, sch)
        if not pts: return 0.0
    return abs(sum(pts[i - 1][0] * p[1] - p[0] * pts[i - 1][1] for i, p in enumerate(pts))) / 2


def _abstand(poly, u0, u1, z0, z1):
    """Kleinster Abstand der Ecken eines Polygons zur Oeffnung (0, wenn eine Ecke drin oder auf dem Rand liegt; grob: Eckpunkte)."""
    d = 9.0
    for u, z in poly:
        du = max(u0 - u, 0.0, u - u1); dz = max(z0 - z, 0.0, z - z1)
        d = min(d, (du * du + dz * dz) ** 0.5)
    return d


LADEN = (0.775, 0.85)          # Band „farbholz“ (Fensterlaeden): gehoert zum Fenster


def holz_gegen_fenster(fl, fenster, stoffe=("Fachwerk",)):
    fehler, anliegend, min_abstand = [], 0, 9.0
    for ax, e, u0, u1, z0, z1 in fenster:
        for name, mat, pts, n, v in fl:
            if name not in ("Haus", "Dach") or mat not in stoffe or achse(n) != ax: continue
            if LADEN[0] - 0.01 <= v <= LADEN[1] + 0.01: continue
            pe, pu0, pu1, pz0, pz1 = rechteck(pts, ax)
            if abs(pe - e) > 0.30 or not aussen_davor(ax, e, pe): continue
            if pu0 >= u0 - 0.06 and pu1 <= u1 + 0.06 and pz0 >= z0 - 0.06 and pz1 <= z1 + 0.06: continue     # Teil des Fensters
            poly = [((p.y if ax == "x" else p.x), p.z) for p in pts]
            # Mittelpfosten eines Fensterpaars (25 cm, deckt die Laibungskante mit hoechstens 4 cm): gilt als anliegend
            if pu1 - pu0 <= 0.26 and ((pu0 < u0 and pu1 - u0 <= 0.045) or (pu1 > u1 and u1 - pu0 <= 0.045)):
                anliegend += 1; continue
            flaeche = _schnitt(poly, u0 + TOL, u1 - TOL, z0 + TOL, z1 - TOL)
            if flaeche > 1e-5:
                fehler.append(f"{ax}={e:.2f} Fenster u {u0:.2f}..{u1:.2f} z {z0:.2f}..{z1:.2f}: Holz u {pu0:.2f}..{pu1:.2f} z {pz0:.2f}..{pz1:.2f}")
                continue
            d = _abstand(poly, u0, u1, z0, z1)
            if _schnitt(poly, u0 - TOL, u1 + TOL, z0 - TOL, z1 + TOL) > 1e-6 or d < TOL:
                anliegend += 1
            elif d < 0.5:
                min_abstand = min(min_abstand, d)
    return fehler, anliegend, min_abstand


def giebel_symmetrie(fl, y, zt, zf, mitte=0.0, breite=9.0):
    """Eckpunkte des Holzes in der Giebelebene oberhalb der Traufe: jeder hat sein Spiegelbild an der Mitte (x = 0, 2 cm)."""
    pkt = set()
    for name, mat, pts, n, v in fl:
        if name != "Haus" or mat != "Fachwerk" or achse(n) != "y": continue
        if abs(sum(p.y for p in pts) / len(pts) - y) > 0.15: continue
        for p in pts:
            if p.z >= zt + 0.10 and abs(p.x - mitte) <= breite: pkt.add((round(p.x - mitte, 3), round(p.z, 3)))
    pkt = sorted(pkt)
    ohne = [p for p in pkt if not any(abs(q[0] + p[0]) < 0.02 and abs(q[1] - p[1]) < 0.02 for q in pkt)]
    hoch = max((z for _, z in pkt), default=0.0)
    return len(pkt), ohne, hoch


def glas(fl):
    return [(achse(n), pts) for name, mat, pts, n, *_ in fl if mat == "Innenglas" and achse(n)]


def glas_an(glaeser, ax, e, u0, u1, z0, z1):
    """Vereinigung der Glasflaechen in dieser Ebene, deren Mitte in der Oeffnung liegt: (u0, u1, z0, z1) oder None."""
    sel = []
    for gax, pts in glaeser:
        if gax != ax: continue
        ge, gu0, gu1, gz0, gz1 = rechteck(pts, ax)
        if abs(ge - e) > 0.03: continue
        cu, cz = (gu0 + gu1) / 2, (gz0 + gz1) / 2
        if u0 - 0.02 <= cu <= u1 + 0.02 and z0 - 0.02 <= cz <= z1 + 0.02: sel.append((gu0, gu1, gz0, gz1))
    if not sel: return None
    return min(s[0] for s in sel), max(s[1] for s in sel), min(s[2] for s in sel), max(s[3] for s in sel)


def moebel_vor_fenster(fl, fenster_innen, ausnahmen):
    fehler = []
    for ax, e, u0, u1, z0, z1, innen_richtung in fenster_innen:
        for name, mat, pts, n, *_ in fl:
            if any(name.startswith(a) for a in ausnahmen) or mat == "Innenglas": continue
            for p in pts:
                pe = p.x if ax == "x" else p.y
                pu = p.y if ax == "x" else p.x
                tiefe = (pe - e) * innen_richtung
                if 0.12 < tiefe < 0.45 and u0 - 0.05 < pu < u1 + 0.05 and z0 - 0.05 < p.z < z1 + 0.05:
                    fehler.append(f"{name} vor Fenster {ax}={e:.2f} u {u0:.2f}..{u1:.2f}")
                    break
    return sorted(set(fehler))


BALKEN = (0.25, 0.50)         # Baender balken_a..balken_c (UV.y): Fachwerkholz


def kein_fachwerk(fl, dx=0.0):
    """Steinhaus: kein Fachwerk (Nutzer 14.11.2026), aber dunkle Holzakzente erlaubt (Nutzer 15.11.2026: Balkenkoepfe, Laeden,
    Traufe und Ortgang wie in den Vorlagen wohnhaus_lvl3_*). Verboten ist das Fachwerkraster: Holz der Baender balken_* in einer
    Fassadenebene an Haus oder Dach oberhalb 0,7 m, das ein Wandglied ist –
      Staender oder Riegel: lang (mindestens 0,8 m) und schmal (hoechstens 0,25 m),
      Strebe: eine schraege Kante ueber 0,4 m,
    ausser es liegt an der Dachkante (Ortgangbrett, bis 0,6 m unter der Dachunterseite). Balkenkoepfe (0,4 x 0,17 m), Laeden
    (0,35 x 1 m), Sparrenkoepfe und Bretter der Tuer (Band planken) sind damit erlaubt."""
    glieder = set()
    for name, mat, pts, n, v in fl:
        if name not in ("Haus", "Dach") or mat != "Fachwerk" or not BALKEN[0] - 1e-3 <= v <= BALKEN[1] + 1e-3: continue
        if max(p.z for p in pts) <= 0.7 or abs(n.z) > 0.3: continue
        ax = achse(n)
        if ax is None: continue
        uu = [p.y if ax == "x" else p.x for p in pts]
        du, dz = max(uu) - min(uu), max(p.z for p in pts) - min(p.z for p in pts)
        if all(-0.15 <= M.z_dach(min(abs(p.x - dx), M.OX + M.UEBER)) - p.z <= 0.6 for p in pts) and max(p.z for p in pts) > M.Z_TRAUFE - 0.3:
            continue                                                # Ortgangbrett an der Dachkante
        kanten = [(pts[i], pts[(i + 1) % len(pts)]) for i in range(len(pts))]
        if len(pts) == 3:                                           # Dreieck eines Rechtecks: die laengste Kante ist dessen Diagonale
            kanten.remove(max(kanten, key=lambda k: (k[0] - k[1]).length))
        schraeg = any((a - b).length > 0.4 and abs(a.z - b.z) > 0.05 and abs((a.y - b.y) if ax == "x" else (a.x - b.x)) > 0.05 for a, b in kanten)
        if schraeg or (max(du, dz) >= 0.8 and min(du, dz) <= 0.25):
            glieder.add(f"{name} {'Strebe' if schraeg else 'Staender' if dz > du else 'Riegel'} z {max(p.z for p in pts):.2f}")
    return sorted(glieder)


def laibungen(fl, fenster):
    """Steinhaus: jede Oeffnung hat auf allen vier Seiten eine Laibung aus Stein (Flaechen quer zur Wand an der Kante der Oeffnung);
    Tiefe je Fenster gleich (2 cm)."""
    zeilen, fehler = [], 0
    for ax, e, u0, u1, z0, z1 in fenster:
        tiefen = {}
        for name, mat, pts, n, v in fl:
            if name not in ("Haus", "Dach") or mat not in ("Haustein", "Bruchstein"): continue
            a = achse(n)
            koord = (lambda p: p.x) if ax == "x" else (lambda p: p.y)
            ucoord = (lambda p: p.y) if ax == "x" else (lambda p: p.x)
            ts = [koord(p) for p in pts]
            if max(abs(t - e) for t in ts) > 0.45: continue
            if a is not None and a != ax:                       # senkrechte Laibung links/rechts
                uu = sum(ucoord(p) for p in pts) / len(pts)
                zz = [p.z for p in pts]
                if min(zz) > (z0 + z1) / 2 or max(zz) < (z0 + z1) / 2: continue
                for seite, k in (("links", u0), ("rechts", u1)):
                    if abs(uu - k) < 0.012: tiefen[seite] = max(tiefen.get(seite, 0.0), max(ts) - min(ts))
            elif a is None and abs(n.z) > 0.9:                  # Sohle und Sturz
                zz = sum(p.z for p in pts) / len(pts)
                us = [ucoord(p) for p in pts]
                if min(us) > (u0 + u1) / 2 or max(us) < (u0 + u1) / 2: continue
                for seite, k in (("unten", z0), ("oben", z1)):
                    if abs(zz - k) < 0.012: tiefen[seite] = max(tiefen.get(seite, 0.0), max(ts) - min(ts))
        ok = len(tiefen) == 4 and max(tiefen.values()) - min(tiefen.values()) <= 0.02 and min(tiefen.values()) >= 0.10
        fehler += 0 if ok else 1
        zeilen.append(f"   {ax}={e:.2f} u {u0:.2f}..{u1:.2f} z {z0:.2f}..{z1:.2f}: " + (", ".join(f"{k} {w:.2f}" for k, w in sorted(tiefen.items())) or "keine Laibung") + ("" if ok else "  FEHLER"))
    return zeilen, fehler


def fenster_symmetrie(fenster, y, dx):
    """Steinhaus: die Fenster einer Giebelseite (Obergeschoss und Giebel) liegen spiegelgleich zur Achse."""
    auf = [((u0 + u1) / 2 - dx, z0) for ax, e, u0, u1, z0, z1 in fenster if ax == "y" and abs(e - y) < 0.45 and z0 > M.Z_EG_DECKE]
    return [p for p in auf if not any(abs(q[0] + p[0]) < 0.01 and abs(q[1] - p[1]) < 0.01 for q in auf)], len(auf)


def gefach_buendig(fl, fenster, z_min):
    """Fenster in Fachwerkwaenden (Unterkante ueber z_min): Abstand zum naechsten Holz links, rechts, unten, oben – das Fenster sitzt
    buendig in seinem Gefach, wenn alle vier Abstaende hoechstens 0,02 m und gleich (1 cm) sind."""
    zeilen, fehler = [], 0
    for ax, e, u0, u1, z0, z1 in fenster:
        if z0 < z_min: continue
        luecke = {"links": 9.0, "rechts": 9.0, "unten": 9.0, "oben": 9.0}
        um, zm = (u0 + u1) / 2, (z0 + z1) / 2
        for name, mat, pts, n, v in fl:
            if name != "Haus" or mat != "Fachwerk" or achse(n) != ax: continue
            if LADEN[0] - 0.01 <= v <= LADEN[1] + 0.01: continue
            pe, pu0, pu1, pz0, pz1 = rechteck(pts, ax)
            if abs(pe - e) > 0.30 or not aussen_davor(ax, e, pe): continue
            if pu0 >= u0 - 0.06 and pu1 <= u1 + 0.06 and pz0 >= z0 - 0.06 and pz1 <= z1 + 0.06: continue
            if pz0 <= zm <= pz1:
                if pu1 <= u0 + TOL: luecke["links"] = min(luecke["links"], u0 - pu1)
                if pu0 >= u1 - TOL: luecke["rechts"] = min(luecke["rechts"], pu0 - u1)
            if pu0 <= um <= pu1:
                if pz1 <= z0 + TOL: luecke["unten"] = min(luecke["unten"], z0 - pz1)
                if pz0 >= z1 - TOL: luecke["oben"] = min(luecke["oben"], pz0 - z1)
        werte = list(luecke.values())
        if sum(w < 1.0 for w in werte) < 3:                     # kein Fachwerk der Huelle um dieses Fenster (Gaube, Treppengiebel aus Stein)
            zeilen.append(f"   {ax}={e:.2f} u {u0:.2f}..{u1:.2f} z {z0:.2f}..{z1:.2f}: kein Fachwerk der Waende (Gaube, Treppengiebel), nicht geprueft")
            continue
        ok = max(werte) <= 0.02 and max(werte) - min(werte) <= 0.01
        fehler += 0 if ok else 1
        zeilen.append(f"   {ax}={e:.2f} u {u0:.2f}..{u1:.2f} z {z0:.2f}..{z1:.2f}: " + ", ".join(f"{k} {w:.2f}" for k, w in luecke.items()) + ("" if ok else "  FEHLER"))
    return zeilen, fehler


def breiten_je_seite(fenster, geschoss):
    """Gleiche Fensterbreite je Geschoss und Seite."""
    gruppen = {}
    for ax, e, u0, u1, z0, z1 in fenster:
        gruppen.setdefault((ax, round(e, 1), geschoss(z0, z1)), []).append(u1 - u0)
    zeilen, fehler = [], 0
    for (ax, e, g), b in sorted(gruppen.items()):
        ok = max(b) - min(b) <= 0.01
        fehler += 0 if ok else 1
        zeilen.append(f"   {g:5} {ax}={e:5.1f}: {len(b)} Fenster, Breiten " + ", ".join(f"{x:.2f}" for x in b) + ("" if ok else "  FEHLER"))
    return zeilen, fehler


def wappen_frei(glb, abstand=0.05):
    """Pruefung 19: Das Wappen (Knoten „Wappen“) haelt in der Ansicht der Gassenwand (x, z) mindestens `abstand` zu allem, was vor
    der Mauerflucht steht (Laden, Gewaende, Pfosten, Fachwerkholz, Vordach, Tuer) und zu den Scheiben."""
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=glb)
    wap = [o for o in bpy.data.objects if o.type == "MESH" and o.name.split(".")[0] == "Wappen"]
    if not wap:
        return ["   FEHLER kein Wappen im Modell"], 1
    pw = [o.matrix_world @ v.co for o in wap for v in o.data.vertices]
    wx0, wx1 = min(p.x for p in pw) - abstand, max(p.x for p in pw) + abstand
    wz0, wz1 = min(p.z for p in pw) - abstand, max(p.z for p in pw) + abstand
    wand = max(p.y for p in pw)                                        # Rueckseite des Wappens = Mauerflucht
    funde = []
    for o in bpy.data.objects:
        if o.type != "MESH" or o.name.split(".")[0] not in ("Haus", "Dach", "Scheiben"): continue
        for p in o.data.polygons:
            pts = [o.matrix_world @ o.data.vertices[i].co for i in p.vertices]
            mat = o.data.materials[p.material_index].name.split(".")[0] if o.data.materials else ""
            glas = mat in ("Fenster", "Bleinetz")
            vor = min(q.y for q in pts) < wand - 0.004 and max(q.y for q in pts) > wand - 0.40
            if not (glas and abs(sum(q.y for q in pts) / len(pts) - wand) < 0.4) and not vor: continue
            if min(q.x for q in pts) < wx1 and max(q.x for q in pts) > wx0 and min(q.z for q in pts) < wz1 and max(q.z for q in pts) > wz0:
                c = sum(pts, Vector()) / len(pts)
                funde.append(f"{o.name.split('.')[0]} {mat} ({c.x:.2f}, {c.y:.2f}, {c.z:.2f})")
    zeilen = [f"   Wappen x {wx0 + abstand:.2f}..{wx1 - abstand:.2f}, z {wz0 + abstand:.2f}..{wz1 - abstand:.2f}"]
    if funde:
        zeilen.append(f"   FEHLER zu nah am Wappen: {len(funde)} Flaechen (" + "; ".join(sorted(set(funde))[:6]) + ")")
    return zeilen, len(funde)


def pruefe(haus, ordner, texturen=None):
    form = int(haus.rsplit("_g", 1)[1]); stufe = int(haus.rsplit("_s", 1)[1].split("_")[0])
    M.stufe_setzen(stufe)
    dx = M.FORMEN[form]["dx"]
    zeilen, fehler = [], 0
    hull = lade(os.path.join(ordner, f"{haus}.glb"))
    fa = fenster_aussen(hull)
    # 1. Holz gegen Fenster
    f1, anl, mind = holz_gegen_fenster(hull, fa, ("Fachwerk", "Haustein") if stufe == 3 else ("Fachwerk",))
    zeilen.append(f"1. Holz gegen Fenster ({len(fa)} Aussenfenster): {len(f1)} Schnitte, {anl} anliegende Rahmenhoelzer, kleinster Abstand sonst {mind:.2f} m (Soll >= {M.FENSTER_ABSTAND:.2f})")
    zeilen += ["   FEHLER " + x for x in f1]
    fehler += len(f1) + (1 if mind < M.FENSTER_ABSTAND - 1e-6 and stufe != 3 else 0)   # Abstandsregel des Fachwerks (Steinhaus: Gewaende der Nachbarfenster duerfen naeher sein)
    # 2. Giebel
    giebel = [] if stufe == 3 else [("hinten", M.HINTEN), ("vorn", M.OVORN)]
    if stufe == 3:                                             # Steinhaus: kein Fachwerk (Nutzer 14.11.2026), Fenster spiegelgleich, Laibungen
        holz = kein_fachwerk(hull, dx)
        zeilen.append("10. Steinhaus ohne Fachwerk (Holzakzente erlaubt): " + ("ok" if not holz else f"{len(holz)} FEHLER (Fachwerkglieder): " + ", ".join(holz[:6])))
        fehler += len(holz)
        for name, y in (("vorn", M.OVORN), ("hinten", M.HINTEN)):
            ohne, anz = fenster_symmetrie(fa, y, dx)
            zeilen.append(f"2. Giebelseite {name}: {anz} Fenster ab dem Obergeschoss, ohne Spiegelbild {len(ohne)} -> {'ok' if not ohne else 'FEHLER'}")
            fehler += 1 if ohne else 0
        z11, f11 = laibungen(hull, fa)
        zeilen.append(f"11. Laibungen aus Stein an allen Fenstern (vier Seiten, Tiefe gleich): {'ok' if not f11 else str(f11) + ' FEHLER'}"); zeilen += z11; fehler += f11
    for name, y in giebel:
        anz, ohne, hoch = giebel_symmetrie(hull, y, M.Z_EG_DECKE, M.Z_FIRST, mitte=dx, breite=M.OX + 0.05)
        stein = stufe == 3 and name == "vorn"                  # Steinhaus: vorn der Treppengiebel aus Haustein (kein Holz ueber der Traufe)
        ok_sym, ok_hoch = not ohne, stein or hoch >= M.Z_FIRST - 0.35
        zeilen.append(f"2. Giebelseite {name} (Obergeschoss und Giebel, Achse x = {dx:.2f}): {anz} Eckpunkte Holz, ohne Spiegelbild {len(ohne)}, hoechstes Holz {hoch:.2f} (First {M.Z_FIRST:.2f}) -> {'ok' if ok_sym and ok_hoch else 'FEHLER'}")
        if ohne: zeilen.append("   ohne Spiegelbild (x, z): " + ", ".join(f"{x:.2f}/{z:.2f}" for x, z in ohne[:8]))
        fehler += (0 if ok_sym else 1) + (0 if ok_hoch else 1)
    # 5. gleiche Fenster je Geschoss und Seite, 6. Fenster buendig im Gefach
    gesch = lambda z0, z1: "EG" if z1 <= M.Z_EG_DECKE else ("Dach" if z0 > M.Z_TRAUFE else "OG")
    z5, f5 = breiten_je_seite(fa, gesch)
    zeilen.append(f"5. Gleiche Fensterbreite je Geschoss und Seite: {'ok' if not f5 else str(f5) + ' FEHLER'}"); zeilen += z5; fehler += f5
    z6, f6 = gefach_buendig(hull, [f for f in fa if f[0] in ("x", "y")] if stufe != 3 else [], M.Z_EG_DECKE)
    zeilen.append(f"6. Fenster buendig im Gefach (Fachwerkwaende, Abstand zum Holz je Seite <= 0,02 und gleich): {'ok' if not f6 else str(f6) + ' FEHLER'}"); zeilen += z6; fehler += f6
    # 7. Steinhaus: Fenster im Treppengiebel (Entscheidung des Nutzers), mittig zur Giebelachse
    if stufe == 3:
        tg = [f for f in fa if f[0] == "y" and f[1] < M.OVORN + M.TG_DICKE and f[4] > M.Z_TRAUFE]
        mittig = [f for f in tg if abs((f[2] + f[3]) / 2 - dx) <= 0.01]
        ok = len(tg) == 1 and len(mittig) == 1
        zeilen.append(f"7. Fenster im Treppengiebel: {len(tg)} gefunden" + (f", Mitte u {(tg[0][2] + tg[0][3]) / 2 - dx:+.3f} zur Achse, {tg[0][3] - tg[0][2]:.2f} breit, z {tg[0][4]:.2f}..{tg[0][5]:.2f}" if tg else "") + (" -> ok" if ok else " -> FEHLER"))
        fehler += 0 if ok else 1
    # 3. Fenster innen = aussen
    innen = {"Wohnstube": lade(os.path.join(ordner, f"{haus}_innen.glb"))}
    for raum in ("hinterzimmer", "bibliothek"):
        pfad = os.path.join(ordner, f"{haus}_{raum}_innen.glb")
        if os.path.exists(pfad): innen[raum.capitalize()] = lade(pfad)
    glaeser = {k: glas(v) for k, v in innen.items()}
    zeilen.append("3. Fenster innen = aussen (Tabelle: Wand, Lage, Geschoss, Raum, innen gefunden, Abweichung)")
    rx0, rx1 = M.RAUM_X[0] + dx, M.RAUM_X[1] + dx
    fenster_innen = {k: [] for k in innen}
    for ax, e, u0, u1, z0, z1 in sorted(fa, key=lambda f: (f[4], f[0], f[2])):
        x_mitte = (u0 + u1) / 2 if ax == "y" else e
        y_mitte = (u0 + u1) / 2 if ax == "x" else e
        if z1 <= M.Z_EG_DECKE:
            geschoss = "EG"
            raeume = ["Wohnstube"] if (y_mitte < M.STUBE_HINTEN and abs(x_mitte - dx) <= M.BX + 0.05) else []
            ort = "Wohnstube" if raeume else "Treppengang"
        elif z1 <= M.Z_OG_DECKE + 0.2:
            geschoss = "OG"
            im_raum = rx0 - 0.05 <= x_mitte <= rx1 + 0.25 and y_mitte <= M.RAUM_Y[1] + 0.05 and (ax == "y" or x_mitte > rx1 - 0.1)
            if stufe == 3: raeume = ["Hinterzimmer"] if im_raum else []         # Steinhaus: die Bibliothek liegt im Dachgeschoss
            else: raeume = ["Hinterzimmer", "Bibliothek"] if im_raum else []
            ort = "/".join(raeume) if raeume else ("Flur" if y_mitte < M.STUBE_HINTEN else "Treppengang")
        elif stufe == 3 and y_mitte < M.STUBE_HINTEN and z0 < M.Z_FIRST:
            geschoss, raeume, ort = "Dach", ["Bibliothek"], "Bibliothek (Dachgeschoss)"
        else:
            geschoss, raeume, ort = "Dach", [], "Dachboden (Schlafkammern, nicht begehbar)"
        befund = []
        for r in raeume:
            if r not in glaeser: befund.append(f"{r}: Modell fehlt"); fehler += 1; continue
            g = glas_an(glaeser[r], ax, e, u0, u1, z0, z1)
            if g is None: befund.append(f"{r}: KEIN GLAS"); fehler += 1; continue
            d = max(abs(g[0] - u0), abs(g[1] - u1), abs(g[2] - z0), abs(g[3] - z1))
            befund.append(f"{r}: Glas, Abweichung {d:.3f}" + ("" if d <= 0.03 else " FEHLER"))
            fehler += 0 if d <= 0.03 else 1
            # Richtung in den Raum: zur Hausmitte
            mitte = (x_mitte - (dx if ax == "x" else 0)) if ax == "x" else (y_mitte if False else e)
            richtung = -1 if (e - (dx if ax == "x" else 0.0)) > 0 else 1
            fenster_innen[r].append((ax, e, u0, u1, z0, z1, richtung))
        wand = f"{ax}={e:.2f}"
        zeilen.append(f"   {wand:9} u {u0:5.2f}..{u1:5.2f} z {z0:4.2f}..{z1:4.2f}  {geschoss:5} {ort:28} " + ("; ".join(befund) if befund else "kein Raum: kein Glas innen erwartet"))
    # 8. Steinhaus: Moebel der Bibliothek unter den Schraegen (Abstand 3 cm), begehbare Zone
    if stufe == 3 and "Bibliothek" in innen:
        zu_hoch = set()
        for name, mat, pts, n, *_ in innen["Bibliothek"]:
            if name in ("Raum", "Glas"): continue
            for p in pts:
                if p.z - M.Z_DG > M.dg_hoehe(p.x - dx) - 0.03: zu_hoch.add(name); break
        zeilen.append(f"8. Bibliothek im Dachgeschoss: begehbare Zone Mensch |x| <= {M.dg_zone(1.75):.2f}, Elb |x| <= {M.dg_zone(1.85):.2f}; Moebel unter den Schraegen: "
                      + ("alle" if not zu_hoch else "FEHLER " + ", ".join(sorted(zu_hoch))))
        fehler += len(zu_hoch)
    # 9. Stufe 3: Geschosse und Hoehe wie die Vorlagen
    if stufe == 3:
        first = max((p.z for name, mat, pts, n, *_ in hull if name == "Dach" for p in pts), default=0.0)
        sy0, sy1 = M.SCHORNSTEIN_RECHTS[2], M.SCHORNSTEIN_RECHTS[3]
        traufe = max((p.z for name, mat, pts, n, *_ in hull
                      if name == "Haus" and mat in ("Lehmputz", "Fachwerk", "Haustein") and achse(n) == "x"
                      and abs(abs(sum(q.x for q in pts) / len(pts) - dx) - M.OX) < 0.03
                      and not (min(q.y for q in pts) >= sy0 - 0.06 and max(q.y for q in pts) <= sy1 + 0.06) for p in pts), default=0.0)   # Wandkrone der Traufseiten (ohne Schornstein)
        baender = sorted({round(f[4], 1) for f in fa if f[4] < traufe})
        geschosse = []
        for z in baender:
            if not geschosse or z - geschosse[-1] > 1.0: geschosse.append(z)
        ok = len(geschosse) == M.SOLL_GESCHOSSE and M.SOLL_TRAUFE[0] - 0.1 <= traufe <= M.SOLL_TRAUFE[1] + 0.1 and M.SOLL_FIRST[0] <= first <= M.SOLL_FIRST[1] + 0.25
        zeilen.append(f"9. Geschosse und Hoehe wie die Vorlagen: {len(geschosse)} Vollgeschosse (Soll {M.SOLL_GESCHOSSE}), Traufe {traufe:.2f} (Soll {M.SOLL_TRAUFE[0]:.2f}..{M.SOLL_TRAUFE[1]:.2f}), "
                      f"First {first:.2f} (Soll {M.SOLL_FIRST[0]:.2f}..{M.SOLL_FIRST[1]:.2f} + Deckung) -> {'ok' if ok else 'FEHLER'}")
        fehler += 0 if ok else 1
    # 4. Moebel vor Fenstern
    ausnahmen = {"Wohnstube": ("Raum", "Glas", "Decke", "Fensterseiten", "Zimmertuer", "Einrichtung_Riegel"), "Hinterzimmer": ("Raum", "Glas"), "Bibliothek": ("Raum", "Glas")}
    for r, fl in innen.items():
        f4 = moebel_vor_fenster(fl, fenster_innen[r], ausnahmen[r])
        zeilen.append(f"4. Moebel vor Fenstern, {r}: " + ("keine" if not f4 else f"{len(f4)} FEHLER"))
        zeilen += ["   " + x for x in f4]
        fehler += len(f4)
    # 12.-15. gemeinsame Pruefungen aller Stufen (uv2_pruefung.py): keine schwarze Flaeche, UV2-Inseln, UV2-Dichte und Inselrand,
    # Normalmap und ORM – Huelle (Blatt trim_s<stufe>) und Wohnstube (innen_trim_s23)
    for teil, blatt in (("", f"trim_s{stufe}"), ("_innen", "innen_trim_s23")):
        glb = os.path.join(ordner, f"{haus}{teil}.glb")
        wo = "innen" if teil else "aussen"
        z12, f12 = U.schwarze_flaechen(glb, texturen=texturen, blatt=blatt)
        zeilen.append(f"12. Keine schwarze Flaeche {wo} (Stoff, UV, UV2, Albedo, Verdeckung, deckungsgleich): {'ok' if not f12 else str(f12) + ' FEHLER'}")
        zeilen += z12; fehler += f12
        zu, fu, _w = U.uv2_inseln(glb)
        zeilen.append(f"13. UV2-Inseln {wo} (kleinste Insel, Dichte, Inselrand): {'ok' if not fu else str(fu) + ' FEHLER'}")
        zeilen += zu; fehler += fu
        # Inselrand: Smart UV Project legt ihn als Anteil der Zelle an (0,003); die Grenzen 2-6 Bildpunkte gelten fuer Zellen von 512 px,
        # die Huellen der Stufen 2 und 3 haben Zellen von 1024 px - Grenzen im selben Verhaeltnis
        atl = U.atlas_von(glb)
        k = (atl["kante"] / atl["zellen"]) / 512.0
        zd, fd, _w = U.uv2_dichte(glb, rand_min=U.RAND_TYP_MIN * k, rand_max=U.RAND_TYP_MAX * k)
        zeilen.append(f"14. UV2-Dichte und Inselrand {wo}: {'ok' if not fd else str(fd) + ' FEHLER'}")
        zeilen += zd; fehler += fd
        zn, fn = U.normal_orm(glb, texturen=texturen, blatt=blatt)
        zeilen.append(f"15. Normalmap und ORM {wo}: {'ok' if not fn else str(fn) + ' FEHLER'}")
        zeilen += zn; fehler += fn
    # 16.-18. Lueckenpruefung der Huelle (luecken_pruefung.py, aus phase5/stein-ebenen): Durchblick, Bandueberlauf, Eisenspalt;
    # Rueckseiten nur als Hinweis (offene Ober- und Stirnseiten von Bauteilen, noch keine harte Grenze)
    glb = os.path.join(ordner, f"{haus}.glb")
    _z, _f, funde = L.luecken(glb)
    durch = [f_ for f_ in funde if f_["art"] == "Durchblick"]
    rueck = [f_ for f_ in funde if f_["art"] == "Rueckseite"]
    zeilen.append(f"16. Kein Durchblick durch die Huelle: {'ok' if not durch else str(len(durch)) + ' FEHLER'} (Hinweis: {len(rueck)} Rueckseiten, "
                  f"davon {sum(1 for f_ in rueck if f_['flaeche'] >= 0.01)} ab 100 cm2)")
    zeilen += [f"   FEHLER Durchblick bei x {f_['godot'][0]:+.2f}, Hoehe {f_['godot'][1]:.2f}, z {f_['godot'][2]:+.2f} (Godot): {f_['breite'] * 100:.0f} x {f_['hoehe'] * 100:.0f} cm"
               for f_ in durch[:8]]
    fehler += len(durch)
    zb, fb, _ = L.band_ueberlauf(glb)
    zeilen.append(f"17. Kein Bandueberlauf: {'ok' if not fb else str(fb) + ' FEHLER'}"); zeilen += [x for x in zb if "FEHLER" in x]; fehler += fb
    ze, fe, _ = L.eisen_spalten(glb)
    zeilen.append(f"18. Kein Eisenteil als Spalt: {'ok' if not fe else str(fe) + ' FEHLER'}"); zeilen += [x for x in ze if "FEHLER" in x]; fehler += fe
    # 19. Wappen frei (Regel wappen_ort): Abstand >= 5 cm zu Laden, Fenster, Gewaende, Holz, Tuer und Vordach
    z19, f19 = wappen_frei(glb)
    zeilen.append(f"19. Wappen frei (5 cm zu Laden, Fenster, Gewaende, Holz, Vordach): {'ok' if not f19 else str(f19) + ' FEHLER'}"); zeilen += z19; fehler += f19
    return zeilen, fehler


def main():
    args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    opt = dict((a[2:].split("=", 1) + ["1"])[:2] for a in args if a.startswith("--"))
    ordner = opt.get("ordner", os.path.join(WURZEL, "game", "assets", "modelle", "gebaeude", "neubau"))
    zeilen, fehler = pruefe(opt["haus"], ordner, opt.get("texturen"))
    kopf = ("GEGENPROBE (aelterer Stand, muss Fehler finden): " if "gegenprobe" in opt else "") + f"Messung {opt['haus']} ({ordner})"
    print("MESSUNG " + kopf)
    for z in zeilen: print("MESSUNG " + z)
    print(f"MESSUNG Ergebnis: {fehler} Fehler")
    if "gegenprobe" not in opt and fehler: sys.exit(1)


if __name__ == "__main__":
    main()
