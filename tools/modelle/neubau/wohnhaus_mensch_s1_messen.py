"""Messung an den fertigen GLB der Wohnhaeuser Mensch Stufe 1 (Grundformen 1-4) nach der Konstruktionsregel Fachwerk
(Rueckmeldung des Nutzers 14.11.2026; dieselben Pruefungen wie wohnhaus_mensch_s23_messen.py fuer Stufe 2/3).

    blender -b --factory-startup --python tools/modelle/neubau/wohnhaus_mensch_s1_messen.py -- --haus=wohnhaus_mensch_s1_g1 [--ordner=<glb-ordner>] [--gegenprobe]

Die GLB stehen in der Lage im Spiel; die Messung dreht sie zuerst in den Baurahmen der Bauskripte zurueck (First entlang y,
Giebel bei vorn/hinten, Giebelachse x = 0). Prueft:
  1. Kein Holz schneidet ein Fenster (Holz "Fachwerk" in Haus und Dach in der Ebene eines Fensters, das in die Oeffnung ragt;
     Fensterstock und Rahmen, ganz innerhalb der Oeffnung plus 6 cm, ausgenommen). Holz, das die Oeffnung nicht beruehrt, haelt
     mindestens 0,05 m Abstand; anliegende Rahmenhoelzer (Staender und Riegel als Laibung) werden gezaehlt.
  2. Giebelseiten (ab der Schwelle des Obergeschosses bzw. Kniestocks bis zum First): Holz spiegelgleich zur Giebelachse (2 cm;
     Fensterlaeden, Band "farbholz", gehoeren zum Fenster und zaehlen nicht) und bis unter den First (hoechstens 0,35 m darunter).
  3. Fenster innen = aussen: jedes Aussenfenster hat im Innenraum (ein Raum mit Galerie) ein Glas in derselben Ebene, Lage und
     Groesse (3 cm).
  4. Kein Moebel vor einem Fenster (Oeffnung plus 5 cm, 0,12 bis 0,45 m in den Raum).
  5. Gleiche Fensterbreite je Geschoss und Seite (1 cm).
  6. Fenster buendig im Gefach: in Fachwerkwaenden (Grundform 1 und 3: Obergeschoss und Giebel; 2 und 4: alle Waende) Abstand
     zum Holz links, rechts, unten, oben je hoechstens 0,02 m und untereinander gleich (1 cm).
  7. Staender im festen Raster (zusaetzlich zu Stufe 2, weil 6 allein Staender neben jedem Fenster nicht erkennt): die
     senkrechten Hoelzer jeder Wand und jedes Geschosses (Erdgeschoss, Obergeschoss/Kniestock, Giebel) haben gleiche
     Achsabstaende (2 cm).
  8. Haustuer licht mindestens 1,95 m hoch und 0,85 m breit, bei Grundform 2 und 4 0,80 m (die Tuer fuellt ein Gefach;
     --tuer-soll=<m> setzt ein anderes Soll, z. B. fuer die Gegenprobe).
--gegenprobe: dieselben Pruefungen an einem aelteren Stand (--ordner), die Fehler finden muessen.
Rueckgabewert 1 bei Fehlern (ohne --gegenprobe).
"""
import os
import sys

import bpy
from mathutils import Vector

HIER = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HIER)
import wohnhaus_mensch_s1_g1 as G1                                        # noqa: E402
import wohnhaus_mensch_s1 as S1                                           # noqa: E402

WURZEL = os.path.dirname(os.path.dirname(os.path.dirname(HIER)))
TOL = 0.005


SOLL_TUER = None                          # --tuer-soll=<m>: anderes Soll fuer die Tuerbreite (Gegenprobe)
WELT = (((1, 0), (0, 1)), (0.0, 0.0))     # Baurahmen -> Spiel der gemessenen Grundform (wird in pruefe gesetzt)


def zurueck(v, ist_normale=False):
    """Punkt bzw. Normale aus der Lage im Spiel zurueck in den Baurahmen (Umkehrung von WELT: Drehung/Spiegelung und Versatz)."""
    ((a, b), (c, d)), (tx, ty) = WELT
    x, y = (v.x, v.y) if ist_normale else (v.x - tx, v.y - ty)
    return Vector((a * x + c * y, b * x + d * y, v.z))        # transponiert = invers (orthogonal)


def lade(pfad):
    """GLB in eine leere Szene: Liste (Objektname ohne Endung, Material, Eckpunkte im Baurahmen, Normale) je Flaeche."""
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
            pts = [zurueck(mw @ o.data.vertices[i].co) for i in p.vertices]
            n = zurueck((mw.to_3x3() @ p.normal).normalized(), True)
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


def holz_gegen_fenster(fl, fenster):
    fehler, anliegend, min_abstand = [], 0, 9.0
    for ax, e, u0, u1, z0, z1 in fenster:
        for name, mat, pts, n, v in fl:
            if name not in ("Haus", "Dach") or mat != "Fachwerk" or achse(n) != ax: continue
            if LADEN[0] - 0.01 <= v <= LADEN[1] + 0.01: continue
            pe, pu0, pu1, pz0, pz1 = rechteck(pts, ax)
            if abs(pe - e) > 0.30 or not aussen_davor(ax, e, pe): continue
            if pu0 >= u0 - 0.06 and pu1 <= u1 + 0.06 and pz0 >= z0 - 0.06 and pz1 <= z1 + 0.06: continue     # Teil des Fensters
            poly = [((p.y if ax == "x" else p.x), p.z) for p in pts]
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
        if LADEN[0] - 0.01 <= v <= LADEN[1] + 0.01: continue          # Fensterlaeden gehoeren zum Fenster
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
        if min(werte) > 1.0:                                   # kein Fachwerk der Huelle in dieser Ebene (Gaube auf dem Dach)
            zeilen.append(f"   {ax}={e:.2f} u {u0:.2f}..{u1:.2f} z {z0:.2f}..{z1:.2f}: kein Fachwerk der Waende (Gaube), nicht geprueft")
            continue
        ok = max(werte) <= 0.02 and max(werte) - min(werte) <= 0.01
        fehler += 0 if ok else 1
        zeilen.append(f"   {ax}={e:.2f} u {u0:.2f}..{u1:.2f} z {z0:.2f}..{z1:.2f}: " + ", ".join(f"{k} {w:.2f}" for k, w in luecke.items()) + ("" if ok else "  FEHLER"))
    return zeilen, fehler


def staender_raster(fl, geschoss, z_spitze, grenzen):
    """Staender im festen Raster: die senkrechten Hoelzer der Wandflaechen (Vorderseite 0,15-0,19 m breit, mindestens 0,35 m
    hoch; Laeden, die Spitzsaeule ueber dem Kehlbalken (Unterkante ueber z_spitze) und Hoelzer ausserhalb der Wand (`grenzen` je
    Achse, z. B. die Pfosten der Laube) ausgenommen) je Wand und Geschoss haben
    gleiche Achsabstaende (2 cm)."""
    gruppen = {}
    for name, mat, pts, n, v in fl:
        if name != "Haus" or mat != "Fachwerk": continue
        ax = achse(n)
        if ax is None or LADEN[0] - 0.01 <= v <= LADEN[1] + 0.01: continue
        e, u0, u1, z0, z1 = rechteck(pts, ax)
        if not (0.15 <= u1 - u0 <= 0.19 and z1 - z0 >= 0.35) or z0 > z_spitze: continue
        if not grenzen[ax][0] - 0.05 <= (u0 + u1) / 2 <= grenzen[ax][1] + 0.05: continue
        g = gruppen.setdefault((ax, round(e, 1), geschoss(z0 + 0.05, z1 - 0.05)), [])
        um = (u0 + u1) / 2
        if not any(abs(um - x) < 0.02 for x in g): g.append(um)
    zeilen, fehler = [], 0
    for (ax, e, gs), xs in sorted(gruppen.items()):
        xs.sort()
        if len(xs) < 3: continue
        d = [b - a for a, b in zip(xs[:-1], xs[1:])]
        ok = max(d) - min(d) <= 0.02
        fehler += 0 if ok else 1
        zeilen.append(f"   {gs:4} {ax}={e:5.1f}: {len(xs)} Staender, Achsabstaende " + ", ".join(f"{x:.2f}" for x in d) + ("" if ok else "  FEHLER"))
    return zeilen, fehler


TUER_SOLL = {1: 0.85, 2: 0.80, 3: 0.85, 4: 0.80}    # lichte Breite der Haustuer (G2/G4: ein Gefach, Entscheidung des Nutzers 14.11.2026)
TUER_HOEHE_SOLL = 1.95


def haustuer(fl):
    """Lichte Masse der Haustuer aus dem Tuerblatt (Bahnen auf dem Boden, rund 2 m hoch, in der Wand zurueckgesetzt): (Breite, Hoehe)."""
    gruppen = {}
    for name, mat, pts, n, v in fl:
        ax = achse(n)
        if name != "Haus" or mat != "Fachwerk" or ax is None: continue
        e, u0, u1, z0, z1 = rechteck(pts, ax)
        if 0.10 <= z0 <= 0.20 and 1.8 <= z1 - z0 <= 2.2 and u1 - u0 < 0.5:
            gruppen.setdefault((ax, round(e, 2)), []).append((u0, u1, z0, z1))
    if not gruppen: return None
    bahnen = max(gruppen.values(), key=len)
    return max(b[1] for b in bahnen) - min(b[0] for b in bahnen), max(b[3] for b in bahnen) - min(b[2] for b in bahnen)


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


def masse(form):
    """Masse der Grundform im Baurahmen: Giebel (Name, y), halbe Breite, Schwelle des Fachwerks ueber dem Erdgeschoss, Traufe,
    First, unterste Hoehe der Fachwerkwaende (Pruefung 6), Mitte der Laengsrichtung, Lage im Spiel."""
    if form in (1, 3):
        welt = (((1, 0), (0, 1)), (0.0, 0.0))
        if form == 3:
            S1.form_setzen(3)
            welt = S1.F.WELT
        g = G1
        # Grundform 3: am hinteren Giebel steht der Schuppen (Pultdach bis z_oben an der Seitenwand); gemessen wird darueber
        ab = {"hinten": S1.SCHUPPEN["z_oben"] + 0.10} if form == 3 else {}
        return dict(giebel=[("vorn", g.OVORN), ("hinten", g.HINTEN)], ox=g.OX, z_og=g.Z_OG, zt=g.Z_TRAUFE, zf=g.Z_FIRST,
                    z_fachwerk=g.Z_OG, ymitte=(g.VORN + g.HINTEN) / 2, welt=welt, giebel_ab=ab, laenge=(g.OVORN, g.HINTEN))
    S1.form_setzen(form)
    F = S1.F
    return dict(giebel=[("vorn", F.VORN), ("hinten", F.HINTEN)], ox=F.OX, z_og=S1.Z_OG, zt=S1.Z_TRAUFE, zf=F.Z_FIRST,
                z_fachwerk=0.0, ymitte=(F.VORN + F.HINTEN) / 2, welt=F.WELT, giebel_ab={}, laenge=(F.VORN, F.HINTEN))


def pruefe(haus, ordner):
    global WELT
    form = int(haus.rsplit("_g", 1)[1])
    m = masse(form)
    WELT = m["welt"]
    RICHTUNG.clear()
    zeilen, fehler = [], 0
    hull = lade(os.path.join(ordner, f"{haus}.glb"))
    fa = fenster_aussen(hull)
    # 1. Holz gegen Fenster
    f1, anl, mind = holz_gegen_fenster(hull, fa)
    zeilen.append(f"1. Holz gegen Fenster ({len(fa)} Aussenfenster): {len(f1)} Schnitte, {anl} anliegende Rahmenhoelzer, kleinster Abstand sonst {mind:.2f} m (Soll >= 0.05)")
    zeilen += ["   FEHLER " + x for x in f1]
    fehler += len(f1) + (1 if mind < 0.05 - 1e-6 else 0)
    # 2. Giebelseiten
    for name, y in m["giebel"]:
        anz, ohne, hoch = giebel_symmetrie(hull, y, m["giebel_ab"].get(name, m["z_og"]), m["zf"], mitte=0.0, breite=m["ox"] + 0.05)
        ok_sym, ok_hoch = not ohne, hoch >= m["zf"] - 0.35
        zeilen.append(f"2. Giebelseite {name} (ab z = {m['giebel_ab'].get(name, m['z_og']) + 0.10:.2f} bis zum First): {anz} Eckpunkte Holz, ohne Spiegelbild {len(ohne)}, hoechstes Holz {hoch:.2f} (First {m['zf']:.2f}) -> {'ok' if ok_sym and ok_hoch else 'FEHLER'}")
        if ohne: zeilen.append("   ohne Spiegelbild (x, z): " + ", ".join(f"{x:.2f}/{z:.2f}" for x, z in ohne[:8]))
        fehler += (0 if ok_sym else 1) + (0 if ok_hoch else 1)
    # 5. gleiche Fenster je Geschoss und Seite, 6. Fenster buendig im Gefach
    gesch = lambda z0, z1: "EG" if z1 <= m["z_og"] else ("Dach" if z0 >= m["zt"] else "OG")
    z5, f5 = breiten_je_seite(fa, gesch)
    zeilen.append(f"5. Gleiche Fensterbreite je Geschoss und Seite: {'ok' if not f5 else str(f5) + ' FEHLER'}"); zeilen += z5; fehler += f5
    z6, f6 = gefach_buendig(hull, fa, m["z_fachwerk"])
    zeilen.append(f"6. Fenster buendig im Gefach (Fachwerkwaende ab z = {m['z_fachwerk']:.2f}, Abstand zum Holz je Seite <= 0,02 und gleich): {'ok' if not f6 else str(f6) + ' FEHLER'}"); zeilen += z6; fehler += f6
    # 7. Staender im festen Raster (Wandflaechen und Giebel getrennt)
    z7, f7 = staender_raster(hull, lambda z0, z1: "EG" if z1 <= m["z_og"] + 0.01 else ("Dach" if z0 >= m["zt"] - 0.01 else "OG"), m["zt"] + 0.6,
                          {"x": m["laenge"], "y": (-m["ox"], m["ox"])})
    zeilen.append(f"7. Staender im festen Raster (gleiche Achsabstaende je Wand und Geschoss, 2 cm): {'ok' if not f7 else str(f7) + ' FEHLER'}"); zeilen += z7; fehler += f7
    # 8. Haustuer: lichte Masse gegen das Soll fuer eine Figur von 1,75 m
    soll_b = float(SOLL_TUER) if SOLL_TUER else TUER_SOLL[form]
    tm = haustuer(hull)
    if tm is None:
        zeilen.append("8. Haustuer: Tuerblatt nicht gefunden  FEHLER"); fehler += 1
    else:
        ok = tm[0] >= soll_b - 0.005 and tm[1] >= TUER_HOEHE_SOLL - 0.005
        zeilen.append(f"8. Haustuer licht {tm[1]:.2f} x {tm[0]:.2f} m (Soll mindestens {TUER_HOEHE_SOLL:.2f} x {soll_b:.2f}) -> {'ok' if ok else 'FEHLER'}")
        fehler += 0 if ok else 1
    # 3. Fenster innen = aussen (ein Raum mit Galerie: jedes Fenster gehoert zu ihm)
    innen = lade(os.path.join(ordner, f"{haus}_innen.glb"))
    glaeser = glas(innen)
    zeilen.append("3. Fenster innen = aussen (Wand, Lage, Geschoss, Glas innen, Abweichung)")
    fenster_innen = []
    for ax, e, u0, u1, z0, z1 in sorted(fa, key=lambda f: (f[4], f[0], f[2])):
        g = glas_an(glaeser, ax, e, u0, u1, z0, z1)
        if g is None:
            befund = "KEIN GLAS  FEHLER"; fehler += 1
        else:
            d = max(abs(g[0] - u0), abs(g[1] - u1), abs(g[2] - z0), abs(g[3] - z1))
            befund = f"Glas, Abweichung {d:.3f}" + ("" if d <= 0.03 else "  FEHLER"); fehler += 0 if d <= 0.03 else 1
        mitte = 0.0 if ax == "x" else m["ymitte"]
        fenster_innen.append((ax, e, u0, u1, z0, z1, -1 if e > mitte else 1))
        zeilen.append(f"   {ax}={e:6.2f} u {u0:5.2f}..{u1:5.2f} z {z0:4.2f}..{z1:4.2f}  {gesch(z0, z1):4} {befund}")
    # 4. Moebel vor Fenstern
    f4 = moebel_vor_fenster(innen, fenster_innen, ("Raum", "Glas"))
    zeilen.append("4. Moebel vor Fenstern: " + ("keine" if not f4 else f"{len(f4)} FEHLER"))
    zeilen += ["   " + x for x in f4]
    fehler += len(f4)
    return zeilen, fehler


def main():
    args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    opt = dict((a[2:].split("=", 1) + ["1"])[:2] for a in args if a.startswith("--"))
    global SOLL_TUER
    SOLL_TUER = opt.get("tuer-soll")
    ordner = opt.get("ordner", os.path.join(WURZEL, "game", "assets", "modelle", "gebaeude", "neubau"))
    zeilen, fehler = pruefe(opt["haus"], ordner)
    kopf = ("GEGENPROBE (aelterer Stand, muss Fehler finden): " if "gegenprobe" in opt else "") + f"Messung {opt['haus']} ({ordner})"
    print("MESSUNG " + kopf)
    for z in zeilen: print("MESSUNG " + z)
    print(f"MESSUNG Ergebnis: {fehler} Fehler")
    if "gegenprobe" not in opt and fehler: sys.exit(1)


if __name__ == "__main__":
    main()
