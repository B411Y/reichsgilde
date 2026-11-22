"""Messung 1-24 an den fertigen GLB der Menschenhaeuser Stufe 1 bis 3 (je Grundform 1-4): die Pruefungen der Mess-Skripte je Stufe
und dazu Lueckenpruefung, Wappen, Raeume, Dreiecksgrenze, Tueren, Fahnenmast und Glas innen (wie wohnhaus_zwerg_neu_messen.py der
Zwerg-Schicht, Mensch-Nachtrag 16.11.2026).

    blender -b --factory-startup --python tools/modelle/neubau/wohnhaus_mensch_messen.py -- --haus=wohnhaus_mensch_s<1|2|3>_g<1-4>
            [--ordner=<glb-ordner>] [--texturen=<ordner>] [--atlas-json=<ao_atlas.json>] [--raeume-json=<raeume/ao_atlas.json>] [--gegenprobe]

Prueft:
  Stufe 1: wohnhaus_mensch_s1_messen.pruefe (1 Holz gegen Fenster, 2 Giebel, 3 Fenster innen = aussen, 4 Moebel vor Fenstern, 5 gleiche
           Fensterbreite, 6 Fenster buendig im Gefach, 7 Staender im Raster, 8 Haustuer, 12-15 aussen und Wohnstube), dazu
           16-18 Lueckenpruefung und 19 Wappen frei (genauer Flaechentest).
  Stufe 2/3: wohnhaus_mensch_s23_messen.pruefe (1-11, 12-15 aussen und Wohnstube, 16-18 Lueckenpruefung, 19 Wappen frei).
  Alle: 12-15 auch fuer Hinterzimmer und Bibliothek (Blatt raeume_innen_trim, AO-Atlanten der Raeume), 12b Rueckseite offen an der
        Huelle und 12c Licht im Standardblick in Wohnstube und Raeumen (blick_licht_haeuser.py), 20 Dreiecke der Huelle
        (Haus, Dach, Scheiben) hoechstens 4.000 (Nutzer 15.11.2026), 21 Haustuer und Zimmertueren fuer einen Menschen (1,75 m + 0,20
        hoch, Schulter 0,46 + 0,30 breit; Elb zur Kenntnis), 22 Fahnenmast steht auf dem Dach (Luft hoechstens 1 cm), 23 entfaellt bei
        den Menschen (kein Legschindeldach mit Schwerstangen), 24 jedes Glas innen liegt in einer Oeffnung der Huelle.
--gegenprobe: dieselben Pruefungen an einem aelteren Stand (--ordner, --texturen, --atlas-json und --raeume-json dieses Stands), die
Fehler finden muessen. Rueckgabewert 1 bei Fehlern (ohne --gegenprobe).
"""
import os
import sys

import bpy
from mathutils import Vector

HIER = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HIER)
import uv2_pruefung as U                                                  # noqa: E402
import luecken_pruefung as L                                              # noqa: E402

WURZEL = os.path.dirname(os.path.dirname(os.path.dirname(HIER)))
RAEUME_JSON = os.path.join(os.path.dirname(HIER), "raeume", "ao_atlas.json")
DREIECKE_HUELLE = 4000
DREIECKE_FACHWERK = 6000              # Fachwerkhaeuser Mensch/Halbling (Nutzer 21.11.2026: echtes Fachwerk); alle anderen 4.000
DREIECKE_HOECHSTENS = 8000            # allgemeine Hoechstgrenze fuer Wohnhaeuser (Nutzer 21.11.2026); keine Grenze liegt darueber
FACHWERK_STUFEN = (1, 2)              # Mensch: Stufe 1 Fachwerkhaus, Stufe 2 Fachwerk-Obergeschoss (Stufe 3 Stein)
ATLAS_JSON = None                     # --atlas-json (Gegenprobe: Atlasplan des alten Stands)
# Figuren (Hoehe, Schulterbreite) wie zwerg_masse.py der Zwerg-Schicht; Tuer = Hoehe + 0,20 und Schulter + 0,30
FIGUREN = {"Mensch": (1.75, 0.46), "Elb": (1.85, 0.44)}
KOPF, SCHULTER_LUFT = 0.20, 0.30

_alt_atlas = U.atlas_von


def _atlas(glb, atlas_json=None, gruppe=None, modell=None):
    return _alt_atlas(glb, atlas_json or ATLAS_JSON, gruppe, modell)


U.atlas_von = _atlas


def _objekte(glb):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=glb, disable_bone_shape=True)   # ohne Knochenformen (Icosphere) des Wandbehangs
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


def _dach_baum(obs):
    """BVH der Flaechen des Objekts „Dach“."""
    from mathutils.bvhtree import BVHTree
    pkt, fl = [], []
    for o in obs:
        if o.name.split(".")[0] != "Dach": continue
        for p in o.data.polygons:
            b = len(pkt)
            pkt += [o.matrix_world @ o.data.vertices[i].co for i in p.vertices]
            fl.append(list(range(b, b + len(p.vertices))))
    return BVHTree.FromPolygons(pkt, fl) if fl else None


def fahne_steht(glb):
    """Pruefung 22: Der Fahnenmast steht auf dem Dach (Rueckmeldung des Nutzers 16.11.2026 zu den Zwergen, ebenso bei den Menschen):
    der tiefste Punkt des Masts (Knoten „Fahne“, Stoff „Holz“) liegt hoechstens 1 cm ueber der Dachoberflaeche an seiner Stelle
    (Strahl von oben auf das Dach)."""
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


def fenster_innen(haus, ordner, MOD):
    """Pruefung 24 (Klicktest der Zwerge 16.11.2026: „ein Fenster schwebt mitten im Raum“): jede Glasflaeche jedes Innenmodells
    (Wohnstube, Hinterzimmer, Bibliothek) liegt in einer Oeffnung der Huelle (gleiche Wandebene auf 3 cm, Mitte in der Oeffnung) –
    Glas ohne Aussenfenster ist ein Fehler. `MOD` = das Messmodul dieser Stufe (lade, fenster_aussen, glas, rechteck); seine Lage
    (Baurahmen) ist durch pruefe gesetzt.
    Seit dem Haeuser-Feinschliff (17.11.2026) dieselbe Pruefung wie bei den Zwergen (wohnhaus_zwerg_neu_messen.fenster_innen): dazu
    Rahmen bzw. Laibung innen je Fenster und die Tabelle je Wand (Glas, Rahmen, Oeffnungen); der Wandbehang wird genannt."""
    gesichert = (U.atlas_von, U.schwarze_flaechen, U.normal_orm)
    import wohnhaus_zwerg_neu_messen as Z                    # setzt beim Laden die Blattnamen der Zwerge: gleich zuruecknehmen
    U.atlas_von, U.schwarze_flaechen, U.normal_orm = gesichert
    return Z.fenster_innen(haus, ordner, MOD)


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
    """Pruefungen 16-18 (luecken_pruefung.py) wie bei Stufe 2/3."""
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
    """12-15 in Hinterzimmer und Bibliothek (Blatt raeume_innen_trim; Atlas der Raeume: Stufe 1 Plaetze hinterzimmer/bibliothek,
    Stufe 2/3 hinterzimmer_s23/bibliothek_s23, Bibliothek des Steinhauses im Dachgeschoss bibliothek_dach)."""
    stufe = int(haus.rsplit("_s", 1)[1].split("_")[0])
    zeilen, fehler = [], 0
    for raum in ("hinterzimmer", "bibliothek"):
        glb = os.path.join(ordner, f"{haus}_{raum}_innen.glb")
        if not os.path.exists(glb):
            continue
        dach = raum == "bibliothek" and stufe == 3
        gruppe = "wohnhaus_mensch_dachraeume_innen" if dach else "wohnhaus_mensch_raeume_innen"
        platz = "bibliothek_dach" if dach else (raum if stufe == 1 else f"{raum}_s23")
        atl = _alt_atlas(glb, raeume_json, gruppe, platz)
        blatt = "raeume_innen_trim"
        z12, f12 = U.schwarze_flaechen(glb, texturen=texturen, blatt=blatt, atlas=atl)
        zeilen.append(f"12. Keine schwarze Flaeche {raum}: {'ok' if not f12 else str(f12) + ' FEHLER'}"); zeilen += z12; fehler += f12
        zu, fu, _w = U.uv2_inseln(glb, atlas=atl)
        zeilen.append(f"13. UV2-Inseln {raum}: {'ok' if not fu else str(fu) + ' FEHLER'}"); zeilen += zu; fehler += fu
        k = (atl["kante"] / atl["zellen"]) / 512.0
        zd, fd, _w = U.uv2_dichte(glb, atlas=atl, rand_min=U.RAND_TYP_MIN * k, rand_max=U.RAND_TYP_MAX * k)
        zeilen.append(f"14. UV2-Dichte und Inselrand {raum}: {'ok' if not fd else str(fd) + ' FEHLER'}"); zeilen += zd; fehler += fd
        zn, fn = U.normal_orm(glb, texturen=texturen, blatt=blatt, atlas=atl)
        zeilen.append(f"15. Normalmap und ORM {raum}: {'ok' if not fn else str(fn) + ' FEHLER'}"); zeilen += zn; fehler += fn
    return zeilen, fehler


def tueren(stufe, form):
    """Pruefung 21: Haustuer und Zimmertueren gegen die Figuren (Hoehe + 0,20, Schulter + 0,30); Pflicht ist der Mensch."""
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
    zeilen, fehler = [], 0
    for name, (b, h) in (("Haustuer", (tb, th)),) + ((("Zimmertuer", zimmer),) if zimmer else ()):
        teile, f_tuer = [], 0
        for fig, (fh, fb) in FIGUREN.items():
            ok = b + 1e-6 >= round(fb + SCHULTER_LUFT, 2) and h + 1e-6 >= round(fh + KOPF, 2)
            teile.append(f"{fig} {'ja' if ok else 'nein'}")
            if not ok and fig == "Mensch": f_tuer += 1
        fehler += f_tuer
        zeilen.append(f"21. {name} licht {b:.2f} x {h:.2f} m: passt " + ", ".join(teile) + (" -> ok" if not f_tuer else " -> FEHLER"))
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
        zeilen, fehler = MS1.pruefe(haus, ordner)
        z, f = luecken(glb); zeilen += z; fehler += f
        z19, f19 = wappen_frei(glb)
        zeilen.append(f"19. Wappen frei (5 cm zu Laden, Fenster, Gewaende, Holz, Vordach): {'ok' if not f19 else str(f19) + ' FEHLER'}"); zeilen += z19; fehler += f19
        MOD = MS1
    else:
        import wohnhaus_mensch_s23_messen as MS23
        zeilen, fehler = MS23.pruefe(haus, ordner, texturen)
        MOD = MS23
    z, f = raeume(haus, ordner, texturen, opt.get("raeume-json", RAEUME_JSON)); zeilen += z; fehler += f
    import blick_licht_haeuser as BL
    z, f = BL.blick_licht(haus, ordner, texturen); zeilen += z; fehler += f
    je, summe = dreiecke(glb)
    grenze = min(DREIECKE_FACHWERK if stufe in FACHWERK_STUFEN else DREIECKE_HUELLE, DREIECKE_HOECHSTENS)
    zeilen.append(f"20. Dreiecke der Huelle {je} Summe {summe} (Grenze {grenze}) -> {'ok' if summe <= grenze else 'FEHLER'}")
    fehler += 0 if summe <= grenze else 1
    z, f = tueren(stufe, form); zeilen += z; fehler += f
    z22, f22 = fahne_steht(glb)
    zeilen.append(f"22. Fahnenmast steht auf dem Dach: {'ok' if not f22 else 'FEHLER'}"); zeilen += z22; fehler += f22
    zeilen.append("23. Schwerstangen: entfaellt bei den Menschen (Stroh-, Ziegel- und Schieferdach)")
    z24, f24 = fenster_innen(haus, ordner, MOD)
    zeilen.append(f"24. Jedes Glas innen liegt in einer Oeffnung der Huelle: {'ok' if not f24 else str(f24) + ' FEHLER'}"); zeilen += z24; fehler += f24
    kopf = ("GEGENPROBE (aelterer Stand, muss Fehler finden): " if "gegenprobe" in opt else "") + f"Messung {haus} ({ordner})"
    print("MESSUNG " + kopf)
    for z in zeilen: print("MESSUNG " + z)
    print(f"MESSUNG Ergebnis: {fehler} Fehler")
    if "gegenprobe" not in opt and fehler: sys.exit(1)


if __name__ == "__main__":
    main()
