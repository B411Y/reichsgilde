"""Messung an den fertigen GLB der Zwergenhaeuser Stufe 1 bis 3 (je Grundform 1-4): die Pruefungen der Menschenhaeuser und dazu
Lueckenpruefung, Wappen, Dreiecksgrenze und die gemeinsamen Pruefungen 12-15 auch in den Raeumen.

    blender -b --factory-startup --python tools/modelle/neubau/wohnhaus_zwerg_neu_messen.py -- --haus=wohnhaus_zwerg_s<1|2|3>_g<1-4>
            [--ordner=<glb-ordner>] [--texturen=<ordner>] [--atlas-json=<ao_atlas.json>] [--raeume-json=<raeume/ao_atlas.json>] [--gegenprobe]

Prueft (Nummern wie bei den Menschen):
  Stufe 1: wohnhaus_mensch_s1_messen.pruefe (1 Holz gegen Fenster, 2 Giebel, 3 Fenster innen = aussen, 4 Moebel vor Fenstern, 5 gleiche
           Fensterbreite, 6 Fenster buendig im Gefach - bei den Zwergen erst ab dem Obergeschoss, das Erdgeschoss ist Stein -, 7 Staender
           im Raster, 8 Haustuer licht fuer einen Menschen, 12-15 aussen und Wohnstube), dazu 16-18 Lueckenpruefung und 19 Wappen frei.
  Stufe 2/3: wohnhaus_mensch_s23_messen.pruefe (1-11, 12-15 aussen und Wohnstube, 16-18 Lueckenpruefung, 19 Wappen frei an der Gassenwand).
  Alle: 12-15 auch fuer Hinterzimmer und Bibliothek (Blatt raeume_innen_trim_zwerg, AO-Atlanten der Raeume), 20 Dreiecke der Huelle
        (Haus, Dach, Scheiben) hoechstens 4.000 (Nutzer 15.11.2026), 21 Haustuer und Zimmertueren mindestens fuer einen Menschen
        (1,75 m + 0,20 hoch, Schulter 0,46 + 0,30 breit; Elb zur Kenntnis; Ork und Oger werden nicht betrachtet, Nutzer 16.11.2026),
        22 Fahnenmast steht auf dem Dach (Luft hoechstens 1 cm); Stufe 1: 23 Schwerstangen liegen auf den Schindeln, Findlinge auf den Stangen.
Die Blaetter heissen wie bei den Menschen mit "_zwerg" (trim_zwerg, trim_s2_zwerg, innen_trim_s23_zwerg ...).
--gegenprobe: dieselben Pruefungen am alten Stand (Branch phase5/neubau-zwerg-st23: --ordner, --texturen, --atlas-json und --raeume-json
dieses Stands), die Fehler finden muessen. Rueckgabewert 1 bei Fehlern (ohne --gegenprobe).
"""
import os
import sys

import bpy
from mathutils import Vector

HIER = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HIER)
import uv2_pruefung as U                                                  # noqa: E402
import luecken_pruefung as L                                              # noqa: E402
import zwerg_masse as ZMA                                                 # noqa: E402

WURZEL = os.path.dirname(os.path.dirname(os.path.dirname(HIER)))
RAEUME_JSON = os.path.join(os.path.dirname(HIER), "raeume", "ao_atlas.json")
DREIECKE_HUELLE = 4000
ATLAS_JSON = None                     # --atlas-json (Gegenprobe: Atlasplan des alten Stands)

_alt_atlas, _alt_schwarz, _alt_normal = U.atlas_von, U.schwarze_flaechen, U.normal_orm


def _atlas(glb, atlas_json=None, gruppe=None, modell=None):
    return _alt_atlas(glb, atlas_json or ATLAS_JSON, gruppe, modell)


def _blatt(glb, blatt):
    """Blatt der Zwerge: das der Menschen mit "_zwerg" (die Mensch-Messskripte nennen das Blatt der Menschen oder keins)."""
    if blatt is None:
        blatt = "innen_trim" if os.path.basename(glb).endswith("_innen.glb") else "trim"
    return blatt if blatt.endswith("_zwerg") else blatt + "_zwerg"


U.atlas_von = _atlas
U.schwarze_flaechen = lambda glb, texturen=None, blatt=None, **kw: _alt_schwarz(glb, texturen=texturen, blatt=_blatt(glb, blatt), **kw)
U.normal_orm = lambda glb, texturen=None, blatt=None, **kw: _alt_normal(glb, texturen=texturen, blatt=_blatt(glb, blatt), **kw)


def _objekte(glb):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=glb)
    return [o for o in bpy.data.objects if o.type == "MESH"]


def dreiecke(glb):
    """Pruefung 20: Dreiecke von Haus, Dach und Scheiben (ohne Zustaende, Wappen, Fahne, Anker)."""
    je = {}
    for o in _objekte(glb):
        name = o.name.split(".")[0]
        if name in ("Haus", "Dach", "Scheiben"):
            je[name] = je.get(name, 0) + sum(len(p.vertices) - 2 for p in o.data.polygons)
    return je, sum(je.values())


def _vieleck_trifft_rechteck(pts2, u0, u1, z0, z1):
    """Schneidet ein ebenes, konvexes Vieleck (Punkte in der Wandansicht u, z) das Rechteck? Trennende Achsen: die Rechteckseiten
    und die Kanten des Vielecks (genauer als der Vergleich der Umrisskaesten: schraege Dachkanten)."""
    if min(p[0] for p in pts2) >= u1 or max(p[0] for p in pts2) <= u0 or min(p[1] for p in pts2) >= z1 or max(p[1] for p in pts2) <= z0:
        return False
    ecken = ((u0, z0), (u1, z0), (u1, z1), (u0, z1))
    for (ax, ay), (bx, by) in zip(pts2, pts2[1:] + pts2[:1]):
        nx, ny = by - ay, ax - bx
        if abs(nx) + abs(ny) < 1e-9: continue
        a = [nx * x + ny * y for x, y in pts2]; b = [nx * x + ny * y for x, y in ecken]
        if max(a) <= min(b) + 1e-9 or max(b) <= min(a) + 1e-9:
            return False
    return True


def _dach_baum(obs, stoffe=None):
    """BVH der Flaechen des Objekts „Dach“ (optional nur bestimmter Stoffe)."""
    from mathutils.bvhtree import BVHTree
    pkt, fl = [], []
    for o in obs:
        if o.name.split(".")[0] != "Dach": continue
        for p in o.data.polygons:
            mat = o.data.materials[p.material_index].name.split(".")[0] if o.data.materials else ""
            if stoffe and mat not in stoffe: continue
            b = len(pkt)
            pkt += [o.matrix_world @ o.data.vertices[i].co for i in p.vertices]
            fl.append(list(range(b, b + len(p.vertices))))
    return BVHTree.FromPolygons(pkt, fl) if fl else None


def fahne_steht(glb):
    """Pruefung 22: Der Fahnenmast steht auf dem Dach (Rueckmeldung des Nutzers 16.11.2026): der tiefste Punkt des Masts (Knoten
    „Fahne“, Stoff „Holz“) liegt hoechstens 1 cm ueber der Dachoberflaeche an seiner Stelle (Strahl von oben auf das Dach)."""
    obs = _objekte(glb)
    holz = [o.matrix_world @ o.data.vertices[i].co for o in obs if o.name.split(".")[0] == "Fahne"
            for p in o.data.polygons if o.data.materials and o.data.materials[p.material_index].name.split(".")[0] == "Holz" for i in p.vertices]
    if not holz:
        return ["   FEHLER kein Fahnenmast"], 1
    unten = min(p.z for p in holz)
    fuss = [p for p in holz if p.z < unten + 1e-3]
    x, y = sum(p.x for p in fuss) / len(fuss), sum(p.y for p in fuss) / len(fuss)
    baum = _dach_baum(obs)
    treffer = baum.ray_cast(Vector((x, y, unten + 3.0)), Vector((0, 0, -1))) if baum else (None,)
    if treffer[0] is None:
        return [f"   FEHLER unter dem Mast bei ({x:.2f}, {y:.2f}) kein Dach"], 1
    luft = unten - treffer[0].z
    ok = luft <= 0.01
    return [f"   Mastfuss z {unten:.3f}, Dach darunter z {treffer[0].z:.3f}: Luft {luft * 100:+.1f} cm (Soll <= 1 cm)" + ("" if ok else "  FEHLER")], 0 if ok else 1


def stangen_liegen(glb):
    """Pruefung 23 (Legschindeldach, Stufe 1): jede Schwerstange (Holz im Dach, ueber 2 m lang entlang des Firsts) liegt auf den
    Schindeln (Strahl von der Mitte ihrer Unterseite entlang deren Normale trifft eine Schindelflaeche nach hoechstens 1,5 cm), und
    jeder Findling (Stoff „Lehmputz“ im Dach) liegt auf einer Stange (Strahl von seinem Boden nach unten trifft Holz nach hoechstens 2 cm)."""
    obs = _objekte(glb)
    schindeln, holz = _dach_baum(obs, ("Stroh",)), _dach_baum(obs, ("Fachwerk",))
    stangen = funde = steine = 0
    zeilen, boden = [], []
    dv = [o.matrix_world @ v.co for o in obs if o.name.split(".")[0] == "Dach" for v in o.data.vertices]
    mx, my = sum(v.x for v in dv) / len(dv), sum(v.y for v in dv) / len(dv)       # Mitte des Dachs (der First liegt darueber)
    for o in obs:
        if o.name.split(".")[0] != "Dach": continue
        for p in o.data.polygons:
            mat = o.data.materials[p.material_index].name.split(".")[0] if o.data.materials else ""
            pts = [o.matrix_world @ o.data.vertices[i].co for i in p.vertices]
            n = (o.matrix_world.to_3x3() @ p.normal).normalized()
            c = sum(pts, Vector()) / len(pts)
            dx_, dy_ = max(q.x for q in pts) - min(q.x for q in pts), max(q.y for q in pts) - min(q.y for q in pts)
            flach = max(q.z for q in pts) - min(q.z for q in pts) < 0.15
            zum_first = (n.x * (c.x - mx) < 0) if dy_ > dx_ else (n.y * (c.y - my) < 0)     # Unterseite: Normale zeigt hangaufwaerts
            if mat == "Fachwerk" and flach and max(dx_, dy_) > 2.0 and n.z < -0.3 and zum_first:   # Unterseite einer Stange
                stangen += 1
                t = schindeln.ray_cast(c - n * 0.05, n, 0.20)                 # von 5 cm innen: eine eingesenkte Stange zaehlt als aufliegend
                if t[0] is None or t[3] > 0.065:
                    funde += 1; zeilen.append(f"   FEHLER Stange bei ({c.x:.2f}, {c.y:.2f}, {c.z:.2f}) liegt nicht auf ({'nichts' if t[0] is None else f'{(t[3] - 0.05) * 100:.1f} cm Luft'})")
            if mat == "Lehmputz" and n.z < -0.9:                                                          # Boden eines Findlings
                boden.append([tuple(round(c_, 3) for c_ in o.matrix_world @ o.data.vertices[i].co) for i in p.vertices])
    # Findlinge: Bodenfaecher nach gemeinsamen Ecken zusammenfassen; die Ecke, die in den meisten Dreiecken steckt, ist die Mitte
    gruppe = {}
    def wurzel(a):
        while gruppe.setdefault(a, a) != a: a = gruppe[a]
        return a
    for tri in boden:
        for v in tri[1:]: gruppe[wurzel(v)] = wurzel(tri[0])
    mitten = {}
    for tri in boden:
        for v in tri:
            w = wurzel(v); mitten.setdefault(w, {}); mitten[w][v] = mitten[w].get(v, 0) + 1
    for w, ecken in mitten.items():
        v = max(ecken, key=ecken.get)
        steine += 1
        t = holz.ray_cast(Vector(v) + Vector((0, 0, 0.05)), Vector((0, 0, -1)), 0.20)
        if t[0] is None or t[3] > 0.075:
            funde += 1; zeilen.append(f"   FEHLER Findling bei ({v[0]:.2f}, {v[1]:.2f}, {v[2]:.2f}) liegt nicht auf einer Stange")
    zeilen.insert(0, f"   {stangen} Stangen-Unterseiten, {steine} Findlinge")
    return zeilen[:10], funde + (1 if stangen == 0 else 0)


def fenster_innen(haus, ordner, MOD):
    """Pruefung 24 (Klicktest 16.11.2026: „ein Fenster schwebt mitten im Raum“, „blaues Fenster ohne Rahmen“), je Innenmodell
    (Wohnstube, Hinterzimmer, Bibliothek) und Wand:
      a) jede Glasflaeche liegt in einer Oeffnung der Huelle (gleiche Wandebene auf 3 cm, Mitte in der Oeffnung);
      b) jedes Fenster mit Glas hat innen einen Rahmen bzw. eine Laibung: Flaechen des Innenmodells (nicht Glas), die bis 6 cm um die
         Oeffnung und bis 0,40 m vor der Glasebene liegen;
      c) wandgrosse farbige Flaechen ohne Glas (Stoff „Hausfarbe“, ab 0,3 m2: Wandbehang) werden genannt, damit sie nicht als Fenster
         gelesen werden; sie liegen auf einer Wand ohne Oeffnung (sonst Fehler).
    Tabelle je Wand: Glas, Rahmen, Oeffnungen. `MOD` = das Messmodul der Menschen (lade, fenster_aussen, glas, rechteck, achse) dieser
    Stufe; seine Lage (Baurahmen) ist durch pruefe gesetzt."""
    fa = MOD.fenster_aussen(MOD.lade(os.path.join(ordner, f"{haus}.glb")))
    zeilen, fehler = [], 0
    for teil in ("_innen", "_hinterzimmer_innen", "_bibliothek_innen"):
        pfad = os.path.join(ordner, f"{haus}{teil}.glb")
        if not os.path.exists(pfad): continue
        fl = MOD.lade(pfad)
        gl = MOD.glas(fl)
        getroffen, frei, funde = {}, 0, []
        for ax, pts in gl:
            ge, gu0, gu1, gz0, gz1 = MOD.rechteck(pts, ax)
            cu, cz = (gu0 + gu1) / 2, (gz0 + gz1) / 2
            f = next((i for i, (fax, e, u0, u1, z0, z1) in enumerate(fa)
                      if fax == ax and abs(ge - e) <= 0.03 and u0 - 0.02 <= cu <= u1 + 0.02 and z0 - 0.02 <= cz <= z1 + 0.02), None)
            if f is None:
                frei += 1
                funde.append(f"   FEHLER {teil[1:]}: Glas ohne Aussenfenster bei Ebene {ax} = {ge:.2f}, u {gu0:.2f}..{gu1:.2f}, z {gz0:.2f}..{gz1:.2f}")
            else:
                getroffen[f] = getroffen.get(f, 0) + 1
        ohne_rahmen = 0
        je_wand = {}
        for f in getroffen:
            fax, e, u0, u1, z0, z1 = fa[f]
            rahmen = 0
            for name, mat, pts, n, *_ in fl:
                if mat in ("Innenglas", "Hausfarbe") or name == "Glas": continue
                uu = [p.y if fax == "x" else p.x for p in pts]; ee = [p.x if fax == "x" else p.y for p in pts]
                if (min(uu) < u1 + 0.06 and max(uu) > u0 - 0.06 and min(p.z for p in pts) < z1 + 0.06 and max(p.z for p in pts) > z0 - 0.06
                        and all(abs(x - e) < 0.40 for x in ee)
                        and not (min(uu) > u0 + 0.06 and max(uu) < u1 - 0.06 and min(p.z for p in pts) > z0 + 0.06 and max(p.z for p in pts) < z1 - 0.06)):
                    rahmen += 1
            wand = f"{fax}={e:.2f}"
            g, r, o = je_wand.get(wand, (0, 0, 0)); je_wand[wand] = (g + 1, r + (1 if rahmen else 0), o + 1)
            if not rahmen:
                ohne_rahmen += 1
                funde.append(f"   FEHLER {teil[1:]}: Fenster ohne Rahmen/Laibung innen bei {wand}, u {u0:.2f}..{u1:.2f}, z {z0:.2f}..{z1:.2f}")
        behang = []
        for name, mat, pts, n, *_ in fl:
            ax = MOD.achse(n)
            if mat != "Hausfarbe" or ax is None: continue
            e, u0, u1, z0, z1 = MOD.rechteck(pts, ax)
            if (u1 - u0) * (z1 - z0) < 0.3: continue
            loch = any(fax == ax and abs(fe - e) < 0.40 and min(u1, fu1) - max(u0, fu0) > 0.05 and min(z1, fz1) - max(z0, fz0) > 0.05
                       for fax, fe, fu0, fu1, fz0, fz1 in fa)
            behang.append(f"{name} {ax}={e:.2f} {u1 - u0:.2f} x {z1 - z0:.2f} m" + (" FEHLER: vor einer Oeffnung" if loch else " (Wand ohne Oeffnung)"))
            if loch: ohne_rahmen += 1
        fehler += frei + ohne_rahmen
        zeilen.append(f"   {teil[1:]}: {len(getroffen)} Fenster innen in Oeffnungen der Huelle, {frei} Glas ohne Oeffnung, {ohne_rahmen} ohne Rahmen; je Wand (Glas, Rahmen, Oeffnungen): "
                      + ", ".join(f"{w} {g}/{r}/{o}" for w, (g, r, o) in sorted(je_wand.items())))
        if behang: zeilen.append(f"   {teil[1:]}: farbige Wandflaechen ohne Glas (Wandbehang in der Hausfarbe, kein Fenster): " + "; ".join(sorted(set(behang))))
        zeilen += funde
    return zeilen, fehler


def wappen_frei(glb, abstand=0.05):
    """Pruefung 19 fuer jede Wand (Stufe 1 haengt das Wappen je Grundform an verschiedene Waende): Das Wappen haelt in der Ansicht
    seiner Wand mindestens `abstand` zu allem, was vor der Mauerflucht steht (Laden, Gewaende, Fachwerkholz, Vordach, Tuer), und zu
    den Scheiben. Die Wand ist die Ebene der Rueckseite des Wappens; ihre Richtung die duennste Ausdehnung des Wappens."""
    obs = _objekte(glb)
    wap = [o for o in obs if o.name.split(".")[0] == "Wappen"]
    if not wap:
        return ["   FEHLER kein Wappen im Modell"], 1
    pw = [o.matrix_world @ v.co for o in wap for v in o.data.vertices]
    ph = [o.matrix_world @ v.co for o in obs if o.name.split(".")[0] == "Haus" for v in o.data.vertices]
    ausd = [max(p[i] for p in pw) - min(p[i] for p in pw) for i in (0, 1)]
    ax = 0 if ausd[0] < ausd[1] else 1
    au = 1 - ax
    mitte_h = (max(p[ax] for p in ph) + min(p[ax] for p in ph)) / 2
    s = 1.0 if sum(p[ax] for p in pw) / len(pw) > mitte_h else -1.0
    wand = min(s * p[ax] for p in pw)
    u0, u1 = min(p[au] for p in pw) - abstand, max(p[au] for p in pw) + abstand
    z0, z1 = min(p.z for p in pw) - abstand, max(p.z for p in pw) + abstand
    funde = []
    for o in obs:
        if o.name.split(".")[0] not in ("Haus", "Dach", "Scheiben"): continue
        for p in o.data.polygons:
            pts = [o.matrix_world @ o.data.vertices[i].co for i in p.vertices]
            mat = o.data.materials[p.material_index].name.split(".")[0] if o.data.materials else ""
            d = [s * q[ax] for q in pts]
            glas = mat in ("Fenster", "Bleinetz") and abs(sum(d) / len(d) - wand) < 0.4
            vor = max(d) > wand + 0.004 and min(d) < wand + 0.40
            if not (glas or vor): continue
            if _vieleck_trifft_rechteck([(q[au], q.z) for q in pts], u0, u1, z0, z1):
                c = sum(pts, Vector()) / len(pts)
                funde.append(f"{o.name.split('.')[0]} {mat} ({c.x:.2f}, {c.y:.2f}, {c.z:.2f})")
    zeilen = [f"   Wappen an der Wand {'xy'[ax]} = {s * wand:+.2f}, {'yx'[ax]} {u0 + abstand:.2f}..{u1 - abstand:.2f}, z {z0 + abstand:.2f}..{z1 - abstand:.2f}"]
    if funde:
        zeilen.append(f"   FEHLER zu nah am Wappen: {len(funde)} Flaechen (" + "; ".join(sorted(set(funde))[:6]) + ")")
    return zeilen, len(funde)


def luecken(glb):
    """Pruefungen 16-18 (luecken_pruefung.py) wie bei Stufe 2/3 der Menschen."""
    zeilen, fehler = [], 0
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
    return zeilen, fehler


def raeume(haus, ordner, texturen, raeume_json):
    """12-15 in Hinterzimmer und Bibliothek (Blatt raeume_innen_trim_zwerg; Atlas der Raeume: Stufe 1 Plaetze hinterzimmer/bibliothek,
    Stufe 2/3 hinterzimmer_s23/bibliothek_s23, Bibliothek des Steinhauses im Dachgeschoss bibliothek_dach)."""
    stufe = int(haus.rsplit("_s", 1)[1].split("_")[0])
    zeilen, fehler = [], 0
    for raum in ("hinterzimmer", "bibliothek"):
        glb = os.path.join(ordner, f"{haus}_{raum}_innen.glb")
        if not os.path.exists(glb):
            continue
        dach = raum == "bibliothek" and stufe == 3
        gruppe = "wohnhaus_zwerg_dachraeume_innen" if dach else "wohnhaus_zwerg_raeume_innen"
        platz = "bibliothek_dach" if dach else (raum if stufe == 1 else f"{raum}_s23")
        atl = _alt_atlas(glb, raeume_json, gruppe, platz)
        blatt = "raeume_innen_trim_zwerg"
        z12, f12 = _alt_schwarz(glb, texturen=texturen, blatt=blatt, atlas=atl)
        zeilen.append(f"12. Keine schwarze Flaeche {raum}: {'ok' if not f12 else str(f12) + ' FEHLER'}"); zeilen += z12; fehler += f12
        zu, fu, _w = U.uv2_inseln(glb, atlas=atl)
        zeilen.append(f"13. UV2-Inseln {raum}: {'ok' if not fu else str(fu) + ' FEHLER'}"); zeilen += zu; fehler += fu
        k = (atl["kante"] / atl["zellen"]) / 512.0
        zd, fd, _w = U.uv2_dichte(glb, atlas=atl, rand_min=U.RAND_TYP_MIN * k, rand_max=U.RAND_TYP_MAX * k)
        zeilen.append(f"14. UV2-Dichte und Inselrand {raum}: {'ok' if not fd else str(fd) + ' FEHLER'}"); zeilen += zd; fehler += fd
        zn, fn = _alt_normal(glb, texturen=texturen, blatt=blatt, atlas=atl)
        zeilen.append(f"15. Normalmap und ORM {raum}: {'ok' if not fn else str(fn) + ' FEHLER'}"); zeilen += zn; fehler += fn
    return zeilen, fehler


def tueren(haus, stufe, form):
    """Pruefung 21: Haustuer und Zimmertueren gegen die Figuren (zwerg_masse: Hoehe + 0,20, Schulter + 0,30)."""
    if stufe == 1:
        import wohnhaus_mensch_s1_g1 as G1
        import wohnhaus_mensch_s1 as S1
        if form in (1, 3):
            fen = G1.FENSTER
        else:
            S1.form_setzen(form); fen = S1.F.FENSTER
        zimmer = None
    else:
        import wohnhaus_mensch_s23_masse as M
        M.stufe_setzen(stufe)
        fen = M.fenster(form, stufe)
        zimmer = M.ZIMMERTUER
    t = next(f for f in fen if f[3] == "tuer")
    tb, th = t[1][1] - t[1][0], t[2][1] - t[2][0]
    soll = ZMA.soll()
    zeilen, fehler = [], 0
    for name, (b, h) in (("Haustuer", (tb, th)),) + ((("Zimmertuer", zimmer),) if zimmer else ()):
        teile = []
        for fig in ("Zwergin", "Zwerg", "Mensch", "Elb"):              # Ork und Oger arbeiten nur in Aussenbetrieben (Nutzer 16.11.2026)
            ok = b + 1e-6 >= soll[fig]["tuer_b"] and h + 1e-6 >= soll[fig]["tuer_h"]
            teile.append(f"{fig} {'ja' if ok else 'nein'}")
            if not ok and fig in ("Zwergin", "Zwerg", "Mensch"): fehler += 1
        zeilen.append(f"21. {name} licht {b:.2f} x {h:.2f} m: passt " + ", ".join(teile) + (" -> ok" if not fehler else " -> FEHLER"))
    return zeilen, fehler


def main():
    global ATLAS_JSON
    args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    opt = dict((a[2:].split("=", 1) + ["1"])[:2] for a in args if a.startswith("--"))
    haus = opt["haus"]
    ordner = opt.get("ordner", os.path.join(WURZEL, "game", "assets", "modelle", "gebaeude", "neubau"))
    texturen = opt.get("texturen")
    ATLAS_JSON = opt.get("atlas-json")
    stufe = int(haus.rsplit("_s", 1)[1].split("_")[0]); form = int(haus.rsplit("_g", 1)[1])
    glb = os.path.join(ordner, f"{haus}.glb")
    if stufe == 1:
        import wohnhaus_mensch_s1_messen as MS1
        MS1.TEXTUREN = texturen
        alt_masse = MS1.masse

        def masse(f):                    # Erdgeschoss der Zwerge ist Stein: Fachwerkpruefungen ab dem Obergeschoss
            m = alt_masse(f); m["z_fachwerk"] = MS1.S1.Z_OG; return m
        MS1.masse = masse
        zeilen, fehler = MS1.pruefe(haus, ordner)
        z, f = luecken(glb); zeilen += z; fehler += f
        z19, f19 = wappen_frei(glb)
        zeilen.append(f"19. Wappen frei (5 cm zu Laden, Fenster, Gewaende, Holz, Vordach): {'ok' if not f19 else str(f19) + ' FEHLER'}"); zeilen += z19; fehler += f19
    else:
        import wohnhaus_mensch_s23_messen as MS23
        MS23.wappen_frei = wappen_frei            # dieselbe Regel, mit dem genauen Flaechentest (schraege Kanten)
        alt_kein = MS23.kein_fachwerk

        def kein_fachwerk(fl, dx=0.0):
            """Pruefung 10 der Menschen, aber der Holzpfosten 25 x 10 cm zwischen den Fenstern eines Paars ist erlaubt (Nutzer
            16.11.2026): Holzflaechen, die ganz im Pfeiler eines Fensterpaars liegen (Fenster derselben Wand, Abstand unter 0,30 m),
            zaehlen nicht als Staender."""
            fa = MS23.fenster_aussen(fl)
            pfeiler = []
            for a in fa:
                for b in fa:
                    if a[0] == b[0] and abs(a[1] - b[1]) < 0.02 and 0 < b[2] - a[3] < 0.30 and abs(a[4] - b[4]) < 0.01:
                        pfeiler.append((a[0], a[1], a[3] - 0.05, b[2] + 0.05, a[4] - 0.03, a[5] + 0.03))

            def im_pfeiler(pts, n):
                ax = MS23.achse(n)
                for pax, e, u0, u1, z0, z1 in pfeiler:
                    uu = [p.y if pax == "x" else p.x for p in pts]
                    ee = [p.x if pax == "x" else p.y for p in pts]
                    if min(uu) >= u0 and max(uu) <= u1 and min(p.z for p in pts) >= z0 and max(p.z for p in pts) <= z1 and all(abs(x - e) < 0.35 for x in ee):
                        return True
                return False
            return alt_kein([f for f in fl if not (f[1] == "Fachwerk" and im_pfeiler(f[2], f[3]))], dx)
        MS23.kein_fachwerk = kein_fachwerk
        zeilen, fehler = MS23.pruefe(haus, ordner, texturen)
    z, f = raeume(haus, ordner, texturen, opt.get("raeume-json", RAEUME_JSON)); zeilen += z; fehler += f
    je, summe = dreiecke(glb)
    zeilen.append(f"20. Dreiecke der Huelle {je} Summe {summe} (Grenze {DREIECKE_HUELLE}) -> {'ok' if summe <= DREIECKE_HUELLE else 'FEHLER'}")
    fehler += 0 if summe <= DREIECKE_HUELLE else 1
    z, f = tueren(haus, stufe, form); zeilen += z; fehler += f
    MOD = MS1 if stufe == 1 else MS23
    z24, f24 = fenster_innen(haus, ordner, MOD)
    zeilen.append(f"24. Jedes Glas innen liegt in einer Oeffnung der Huelle: {'ok' if not f24 else str(f24) + ' FEHLER'}"); zeilen += z24; fehler += f24
    z22, f22 = fahne_steht(glb)
    zeilen.append(f"22. Fahnenmast steht auf dem Dach: {'ok' if not f22 else 'FEHLER'}"); zeilen += z22; fehler += f22
    if stufe == 1:
        z23, f23 = stangen_liegen(glb)
        zeilen.append(f"23. Legschindeldach: Schwerstangen auf den Schindeln, Findlinge auf den Stangen: {'ok' if not f23 else str(f23) + ' FEHLER'}"); zeilen += z23; fehler += f23
    kopf = ("GEGENPROBE (aelterer Stand, muss Fehler finden): " if "gegenprobe" in opt else "") + f"Messung {haus} ({ordner})"
    print("MESSUNG " + kopf)
    for z in zeilen: print("MESSUNG " + z)
    print(f"MESSUNG Ergebnis: {fehler} Fehler")
    if "gegenprobe" not in opt and fehler: sys.exit(1)


if __name__ == "__main__":
    main()
