"""Modulpruefung der Haeuserzeilen (modulares Wohnhaussystem): prueft die exportierte GLB eines Satzes und seinen AO-Atlas.

    blender -b --factory-startup --python tools/modelle/neubau/zeile_pruefung.py -- --satz=mensch_s1 [--raster=0.03] [--nur=naht]

Geprueft wird, was das Spiel laedt (game/assets/modelle/gebaeude/zeilen/zeile_<satz>.glb), nicht der Bau im Speicher:

1. Module: alle Module aus zeile_module.MODULE sind da, Koerper mit Anker „Tuer_<modul>“.
2. Dreiecke je Modul (Grenzen DREIECKE) und je Haus (Koerper mit den zwei teuersten Seitenstuecken hoechstens 4.000).
3. Rahmen: Koerper bleiben in ihrer Parzelle (x von 0 bis Breite, auf 1 mm) und reichen mit dem Dach bis an beide Grenzen;
   Eckhaus buendig links und hinten; Seitenstuecke (linke Seite) ragen nicht in die Nachbarparzelle (Brandwand: Abdeckung 5 cm).
4. Bandueberlauf (luecken_pruefung.band_ueberlauf): keine Flaeche liest ueber ihr Band des Trim-Sheets hinaus.
5. Verdeckung: UV2 jedes Moduls liegt in seiner Zelle des Atlas; die gebackene Verdeckung ist nicht schwarz (Mittel je Modul
   mindestens AO_MITTEL, hoechstens DUNKEL_ANTEIL der Flaeche unter 0,12).
6. Naehte: jede Nachbarschaft (Koerper an Koerper, Koerper am Eckhaus, Haus um die Ecke hinter dem Eckhaus) wird nach der
   Nachbarregel gestellt (zeile_bilder.stuecke, wie Zeilenplan.cs) und in einem Streifen von 0,6 m beiderseits der Naht mit
   Strahlen abgetastet: waagerecht von vorn und hinten (bis unter die Traufgasse), senkrecht von oben und schraeg wie die
   Spielkamera (40 Grad von oben, 20 Grad seitlich, von vorn und hinten). „Durchblick“: ein Strahl geht durch die Zeile hindurch
   bzw. trifft im Grundriss den Boden. „Rueckseite“: ein Strahl trifft zuerst die Rueckseite einer Flaeche (im Spiel unsichtbar).
   Gemeldet ab NAHT_MIN Strahlen je Fall und Blick. Strohkanten (Alpha-Karten) zaehlen nicht.

Fenster-Varianten (Lochbilder): Heissen Objekte in der GLB <modul>_a, <modul>_b ... (lueckenlos ab _a), gelten die Grenzen des Moduls
fuer jede Variante; Rahmen und Naehte laufen je Variantenrunde (Runde i: von jedem Modul die Variante i), Verdeckung je Objekt nach
den Plaetzen in ao_atlas.json. Ohne Varianten aendert sich nichts.

--selbsttest=1 eicht die Nahtabtastung an Quadern mit bekannter Antwort (geschlossen, mit Spalt, mit gedrehter Wand); --fall=<a>|<b>
und --proben=1 geben fuer eine Nachbarschaft Stichproben der Fundstellen aus.

Ausgabe mit Praefix ZEILE, Rueckgabewert 1 bei Fehlern.
"""
import json
import math
import os
import sys

import bpy
import numpy as np
from mathutils import Matrix, Vector
from mathutils.bvhtree import BVHTree

HIER = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HIER)
import zeile_module as Z                 # noqa: E402
import zeile_bilder as ZB                # noqa: E402
import luecken_pruefung as L             # noqa: E402

WURZEL = os.path.dirname(os.path.dirname(os.path.dirname(HIER)))
TEXTUREN = os.path.join(WURZEL, "game", "assets", "texturen", "neubau")

# Hoechstzahl Dreiecke je Modul. Koerper nach Breite, Eckhaus wie ein freistehendes Haus; blinde Stuecke klein, die offenen Seiten
# sind ganze Fassaden mit Fenstern (kappe_offen dazu mit dem Dachstreifen des Ortgangs).
DREIECKE = {"k_g4": 2000, "k_g6": 2500, "k_g8": 3000, "k_t6": 2500, "k_t8": 3000, "eck": 4000, "seite_blind": 300, "kappe_brand": 300,
            "gasse": 300, "seite_offen": 800, "kappe_offen": 800, "mauer": 100, "pfeiler": 100, "tor": 100, "hinterhaus": 300}
HAUS_HOECHSTENS = 4000
KOERPER = ["k_g4", "k_g6", "k_g8", "k_t6", "k_t8"]
AO_MITTEL, AO_DUNKEL, DUNKEL_ANTEIL = 0.45, 0.12, 0.10
NAHT_MIN = 4
FALL = ""             # --fall=k_t6|eck: nur diese Nachbarschaft
PROBEN = False        # --proben=1: je Fall Stichproben der Fundstellen ausgeben (u quer zur Naht, v Tiefe bzw. Hoehe)


def basis(name):
    """Modulname ohne Variantenzusatz: k_g6_b -> k_g6 (Fenster-Lochbilder heissen <modul>_a, _b, _c ...)."""
    teile = name.rsplit("_", 1)
    return teile[0] if len(teile) == 2 and len(teile[1]) == 1 and teile[1].isalpha() and teile[0] in DREIECKE else name


def varianten(netze, modul):
    """Die Objekte eines Moduls in der GLB: seine Varianten <modul>_a ... (lueckenlos ab _a) oder das Modul ohne Zusatz."""
    aus, i = [], 0
    while f"{modul}_{chr(ord('a') + i)}" in netze:
        aus.append(f"{modul}_{chr(ord('a') + i)}"); i += 1
    return aus or ([modul] if modul in netze else [])


def runden(netze):
    """Sichten auf die Module fuer Rahmen- und Nahtpruefung: Runde i nimmt von jedem Modul die Variante i (gibt es weniger, die
    letzte; ohne Varianten das Modul selbst), unter dem Namen ohne Zusatz. Ohne Varianten genau eine Runde."""
    alle = {m: varianten(netze, m) for m in DREIECKE}
    zahl = max([len(v) for v in alle.values()] + [1])
    for i in range(zahl):
        sicht = {m: netze[v[min(i, len(v) - 1)]] for m, v in alle.items() if v}
        yield ("" if zahl == 1 else f"Variante {chr(ord('a') + i)}: "), sicht


def laden(satz):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=os.path.join(Z.ZIEL_MODELL, f"zeile_{satz}.glb"))
    netze = {o.name: o for o in bpy.data.objects if o.type == "MESH"}
    anker = {o.name for o in bpy.data.objects if o.type == "EMPTY"}
    return netze, anker


def dreiecke(o):
    return sum(len(p.vertices) - 2 for p in o.data.polygons)


def grenzen(o):
    pts = [o.matrix_world @ v.co for v in o.data.vertices]
    return [(min(p[i] for p in pts), max(p[i] for p in pts)) for i in range(3)]


def pruefe_module(netze, anker, melde):
    for m in Z.MODULE:
        if not varianten(netze, basis(m)):
            melde(f"FEHLER Modul {m} fehlt in der GLB")
    for m in KOERPER + ["eck"]:
        for v in varianten(netze, m):
            if "Tuer_" + v not in anker:
                melde(f"FEHLER Anker Tuer_{v} fehlt")
    zahl = {m: dreiecke(o) for m, o in netze.items() if basis(m) in DREIECKE}
    for m in sorted(zahl, key=lambda n: (list(DREIECKE).index(basis(n)), n)):
        zu_viel = zahl[m] > DREIECKE[basis(m)]
        melde(f"{'FEHLER ' if zu_viel else ''}Dreiecke {m}: {zahl[m]} (hoechstens {DREIECKE[basis(m)]})", fehler=zu_viel)
    groesste = lambda modul: max((zahl[v] for v in varianten(netze, modul)), default=0)
    for m in KOERPER:
        for v in varianten(netze, m):
            seiten = ("seite_offen", "seite_offen") if m.startswith("k_g") else ("kappe_offen", "kappe_brand")
            summe = zahl[v] + sum(groesste(s) for s in seiten)
            melde(f"{'FEHLER ' if summe > HAUS_HOECHSTENS else ''}Haus {v} mit {seiten[0]} und {seiten[1]}: {summe} Dreiecke (hoechstens {HAUS_HOECHSTENS})",
                  fehler=summe > HAUS_HOECHSTENS)


DACH_STOFFE = ("Stroh", "Strohkante")


def grenzen_x_nach_stoff(o):
    """x-Grenzen der Ecken getrennt nach Dach (Deckung, Fransen) und allem anderen (Waende, Holz, Stein, Fenster)."""
    me, mw = o.data, o.matrix_world
    aus = {"dach": [1e9, -1e9], "wand": [1e9, -1e9], "unten": [1e9, -1e9]}
    z_unten = Z.S["z_traufe"] - Z.EIN * Z.S["tg"] - 0.4       # sicher unter Traufe, Ortgang und Windbrettern
    for p in me.polygons:
        stoff = me.materials[p.material_index].name.split(".")[0] if me.materials else ""
        ziel = aus["dach" if stoff in DACH_STOFFE else "wand"]
        for vi in p.vertices:
            w = mw @ me.vertices[vi].co
            ziel[0], ziel[1] = min(ziel[0], w.x), max(ziel[1], w.x)
            if w.z < z_unten:       # alles unterhalb der Traufzone, gleich welcher Stoff: was dort uebersteht, steckt im Nachbarn
                aus["unten"][0], aus["unten"][1] = min(aus["unten"][0], w.x), max(aus["unten"][1], w.x)
    return aus


def pruefe_rahmen(netze, melde):
    t = 0.001
    for m in KOERPER + ["eck"]:
        if m not in netze: continue
        g = grenzen_x_nach_stoff(netze[m])
        melde(f"Rahmen {m} getrennt: Dach x {g['dach'][0]:+.4f} bis {g['dach'][1]:.4f}, Wand und Holz x {g['wand'][0]:+.4f} bis {g['wand'][1]:.4f}, "
              f"unter der Traufzone (alle Stoffe) x {g['unten'][0]:+.4f} bis {g['unten'][1]:.4f}", fehler=False)
    for m in KOERPER:
        if m not in netze: continue
        (x0, x1), _, _ = grenzen(netze[m])
        w = Z.BREITE[m]
        ok = x0 >= -t and x1 <= w + t and x0 <= t and x1 >= w - t
        melde(f"{'' if ok else 'FEHLER '}Rahmen {m}: x {x0:+.4f} bis {x1:.4f} (Parzelle 0 bis {w})", fehler=not ok)
    if "eck" in netze:
        (x0, _), (_, y1), _ = grenzen(netze["eck"])
        ok = x0 >= -t and y1 <= Z.TIEFE + t
        melde(f"{'' if ok else 'FEHLER '}Rahmen eck: links x {x0:+.4f}, hinten y {y1:.4f} (buendig bei 0 und {Z.TIEFE})", fehler=not ok)
    for m, links in (("seite_blind", 0.0), ("seite_offen", 0.0), ("gasse", 0.0), ("kappe_brand", -0.05)):
        if m not in netze: continue
        (x0, x1), _, _ = grenzen(netze[m])
        ok = x0 >= links - t
        melde(f"{'' if ok else 'FEHLER '}Rahmen {m}: x {x0:+.4f} bis {x1:.4f} (nicht unter {links})", fehler=not ok)


def pruefe_verdeckung(netze, satz, melde):
    daten = json.load(open(os.path.join(HIER, "ao_atlas.json"), encoding="utf-8"))[f"zeile_{satz}"]
    pfad = os.path.join(TEXTUREN, daten["datei"])
    bild = bpy.data.images.load(pfad)
    k = bild.size[0]
    feld = np.empty(k * k * 4, dtype=np.float32); bild.pixels.foreach_get(feld)
    grau = feld.reshape(k, k, 4)[..., 0]
    n = daten["zellen"]
    for m, nummer in daten["plaetze"].items():
        if m not in netze: continue
        me = netze[m].data
        if len(me.uv_layers) < 2:
            melde(f"FEHLER Verdeckung {m}: keine UV2"); continue
        uv2 = me.uv_layers[1].data
        spalte, zeile = nummer % n, nummer // n
        u0, u1, v0, v1 = spalte / n, (spalte + 1) / n, zeile / n, (zeile + 1) / n
        draussen, summe, flaeche, dunkel = 0, 0.0, 0.0, 0.0
        for p in me.polygons:
            stoff = me.materials[p.material_index].name.split(".")[0] if me.materials else ""
            uvs = [uv2[li].uv for li in p.loop_indices]
            if any(u.x < u0 - 0.001 or u.x > u1 + 0.001 or u.y < v0 - 0.001 or u.y > v1 + 0.001 for u in uvs): draussen += 1
            if stoff in ("Fenster", "Strohkante"): continue
            mu, mv = sum(u.x for u in uvs) / len(uvs), sum(u.y for u in uvs) / len(uvs)
            wert = float(grau[min(k - 1, max(0, int(mv * k))), min(k - 1, max(0, int(mu * k)))])
            summe += wert * p.area; flaeche += p.area
            if wert < AO_DUNKEL: dunkel += p.area
        mittel, anteil = summe / max(flaeche, 1e-9), dunkel / max(flaeche, 1e-9)
        schlecht = draussen > 0 or mittel < AO_MITTEL or anteil > DUNKEL_ANTEIL
        melde(f"{'FEHLER ' if schlecht else ''}Verdeckung {m} (Zelle {spalte},{zeile}): Mittel {mittel:.2f} (mindestens {AO_MITTEL}), "
              f"unter {AO_DUNKEL}: {anteil * 100:.1f} % der Flaeche (hoechstens {DUNKEL_ANTEIL * 100:.0f} %), {draussen} Flaechen ausserhalb der Zelle",
              fehler=schlecht)


def baum(objekte):
    """BVH aller gestellten Module in Weltkoordinaten (ohne Strohkanten) und die Normale je Dreieck, wie das Spiel sie sieht
    (gespiegelte Module mit umgedrehtem Umlauf)."""
    ecken, flaechen, normalen = [], [], []
    for o in objekte:
        me, mw = o.data, o.matrix_world
        spiegel = mw.to_3x3().determinant() < 0
        basis = len(ecken)
        ecken += [mw @ v.co for v in me.vertices]
        me.calc_loop_triangles()
        for t in me.loop_triangles:
            stoff = me.materials[t.material_index].name.split(".")[0] if me.materials else ""
            if stoff == "Strohkante": continue
            a, b, c = (ecken[basis + i] for i in t.vertices)
            nn = (b - a).cross(c - a)
            if nn.length < 1e-12: continue
            flaechen.append(tuple(basis + i for i in t.vertices))
            normalen.append(-nn.normalized() if spiegel else nn.normalized())
    return BVHTree.FromPolygons(ecken, flaechen), normalen


def naht_abtasten(objekte, s0, quer, tief, raster, z_wand, proben=None):
    """Strahlen im Streifen um die Naht: s0 = Punkt der Naht an der Flucht, quer = entlang der Zeile, tief = in die Haeuser.
    Rueckgabe {(Blick, Art): Zahl der Strahlen}."""
    bvh, normalen = baum(objekte)
    funde = {}

    def strahl(blick, start, richtung, boden_ist_loch, ort=None):
        treffer = bvh.ray_cast(start, richtung, 200.0)
        if treffer[0] is None or treffer[0].z < 0.03:
            # nichts oder erst den Boden getroffen
            if boden_ist_loch:
                funde[(blick, "Durchblick")] = funde.get((blick, "Durchblick"), 0) + 1
                if proben is not None:
                    # Durchgang durch die Flucht, durch die der Strahl kommt: vorn (Tiefe 0) bzw. hinten (Tiefe TIEFE)
                    rt = richtung.dot(tief)
                    if abs(rt) > 1e-6:
                        k = ((0.0 if rt > 0 else Z.TIEFE) - (start - s0).dot(tief)) / rt
                        d = start + richtung * k - s0
                        proben.setdefault((blick, "Durchblick"), []).append((ort, ("Flucht", round(d.dot(quer), 2), round(d.z, 2))))
                    else:
                        proben.setdefault((blick, "Durchblick"), []).append((ort, None))
            return
        if normalen[treffer[2]].dot(richtung) > 1e-4:
            funde[(blick, "Rueckseite")] = funde.get((blick, "Rueckseite"), 0) + 1
            if proben is not None: proben.setdefault((blick, "Rueckseite"), []).append((ort, tuple(round(c, 2) for c in treffer[0])))

    us = np.arange(-0.6, 0.6 + 1e-6, raster)
    hoch = Vector((0, 0, 1))
    for u in us:
        for z in np.arange(0.1, z_wand, raster):                         # waagerecht von vorn und hinten
            p = s0 + quer * float(u) + hoch * float(z)
            strahl("vorn", p - tief * 6.0, tief, True, (round(float(u), 2), round(float(z), 2)))
            strahl("hinten", p + tief * (Z.TIEFE + 6.0), -tief, True, (round(float(u), 2), round(float(z), 2)))
        for v in np.arange(0.4, Z.TIEFE - 0.4 + 1e-6, raster):            # von oben und schraeg auf den Grundriss
            ziel = s0 + quer * float(u) + tief * float(v)
            ort = (round(float(u), 2), round(float(v), 2))
            strahl("oben", ziel + hoch * 40.0, -hoch, True, ort)
            for name, vorn in (("schraeg vorn", -1.0), ("schraeg hinten", 1.0)):
                for seite in (-1.0, 1.0):
                    waag = (tief * -vorn * math.cos(math.radians(20)) + quer * seite * math.sin(math.radians(20))).normalized()
                    richtung = (waag * math.cos(math.radians(40)) - hoch * math.sin(math.radians(40))).normalized()
                    strahl(name, ziel - richtung * 60.0, richtung, True, ort)
    return funde


def pruefe_naehte(netze, raster, melde):
    breiten = dict(Z.BREITE)
    z_wand = Z.S["z_traufe"] - Z.EIN * Z.S["tg"] - 0.35      # bis unter das Brett der Traufgasse
    for o in netze.values(): o.hide_set(True)
    faelle = [(a, b, False) for a in KOERPER for b in KOERPER] + [(a, "eck", False) for a in KOERPER] + [("eck", b, True) for b in KOERPER]
    for a, b, um_die_ecke in faelle:
        if a not in netze or b not in netze: continue
        if FALL and FALL != f"{a}|{b}": continue
        if um_die_ecke:
            # das Eckhaus am Ende seiner Strecke, das naechste Haus beginnt um die Haustiefe versetzt auf der Strecke um die Ecke
            pa, pb = [{"modul": "eck"}], [{"modul": b}]
            pb[0]["links"] = pa[0]
            la, laenge = ZB.stuecke(pa)
            lb, _ = ZB.stuecke(pb)
            stehen = ZB.setzen(netze, la, Vector((0, 0, 0)), Vector((1, 0, 0)), Vector((0, 1, 0)), breiten)
            stehen += ZB.setzen(netze, lb, Vector((laenge, Z.TIEFE, 0)), Vector((0, 1, 0)), Vector((-1, 0, 0)), breiten)
            s0, quer, tief = Vector((laenge, Z.TIEFE, 0)), Vector((0, 1, 0)), Vector((-1, 0, 0))
        else:
            liste, _ = ZB.stuecke([{"modul": a}, {"modul": b}])
            stehen = ZB.setzen(netze, liste, Vector((0, 0, 0)), Vector((1, 0, 0)), Vector((0, 1, 0)), breiten)
            s0, quer, tief = Vector((Z.BREITE[a], 0, 0)), Vector((1, 0, 0)), Vector((0, 1, 0))
        bpy.context.view_layer.update()
        proben = {} if PROBEN else None
        funde = naht_abtasten(stehen, s0, quer, tief, raster, z_wand, proben)
        if proben:
            for schluessel, liste in sorted(proben.items()):
                print("ZEILE PROBE", a, b, schluessel, len(liste), liste[::max(1, len(liste) // 12)][:12])
        for o in stehen: bpy.data.objects.remove(o, do_unlink=True)
        name = f"{a} | {b}" + (" (um die Ecke)" if um_die_ecke else "")
        schlimm = {k: v for k, v in funde.items() if v >= NAHT_MIN}
        if schlimm:
            melde(f"FEHLER Naht {name}: " + ", ".join(f"{art} {blick} {zahl} Strahlen" for (blick, art), zahl in sorted(schlimm.items())))
        else:
            melde(f"Naht {name}: dicht" + (f" (einzelne Strahlen: {sum(funde.values())})" if funde else ""), fehler=False)


def selbsttest(raster):
    """Eichung der Nahtabtastung an Koerpern mit bekannter Antwort (--selbsttest=1): zwei geschlossene Quader 6 x 8 x 5 m Wand an
    Wand muessen „dicht“ sein (kein Befund in keinem Blick); mit 10 cm Spalt an der Naht muss Durchblick gemeldet werden; mit einer
    nach innen gedrehten Vorderwand Rueckseite. Meldet je Fall die Befunde; Rueckgabe: Zahl der Faelle mit falscher Antwort."""
    bpy.ops.wm.read_factory_settings(use_empty=True)
    Z.stil("mensch_s1")

    def quader(name, x0, x1, vorn_gedreht=False):
        e = [(x0, 0, 0), (x1, 0, 0), (x1, Z.TIEFE, 0), (x0, Z.TIEFE, 0), (x0, 0, 5), (x1, 0, 5), (x1, Z.TIEFE, 5), (x0, Z.TIEFE, 5)]
        f = [(0, 1, 5, 4), (1, 2, 6, 5), (2, 3, 7, 6), (3, 0, 4, 7), (4, 5, 6, 7)]      # Waende und Deckel, Umlauf nach aussen
        if vorn_gedreht: f[0] = (4, 5, 1, 0)
        me = bpy.data.meshes.new(name); me.from_pydata(e, [], f); me.update()
        o = bpy.data.objects.new(name, me); bpy.context.scene.collection.objects.link(o)
        return o

    falsch = 0
    faelle = (("geschlossen (soll: dicht)", 6.0, False, False), ("Spalt 10 cm (soll: Durchblick)", 6.1, False, True),
              ("Vorderwand gedreht (soll: Rueckseite)", 6.0, True, True))
    for name, x_b, gedreht, soll_befund in faelle:
        a_, b_ = quader("A", 0.0, 6.0), quader("B", x_b, x_b + 6.0, gedreht)
        bpy.context.view_layer.update()
        funde = naht_abtasten([a_, b_], Vector((6.0, 0, 0)), Vector((1, 0, 0)), Vector((0, 1, 0)), raster, 4.5)
        for o in (a_, b_): bpy.data.objects.remove(o, do_unlink=True)
        hat = any(v >= NAHT_MIN for v in funde.values())
        richtig = hat == soll_befund
        falsch += 0 if richtig else 1
        print("ZEILE SELBSTTEST", "richtig" if richtig else "FALSCH", name, "->", dict(sorted(funde.items())) or "kein Befund")
    return falsch


def main():
    args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    opt = dict((a[2:].split("=", 1) + ["1"])[:2] for a in args if a.startswith("--"))
    satz = opt.get("satz", "mensch_s1")
    Z.stil(satz)
    if "selbsttest" in opt:
        falsch = selbsttest(float(opt.get("raster", "0.03")))
        print("ZEILE SELBSTTEST ERGEBNIS:", falsch, "falsche Antworten")
        sys.exit(1 if falsch else 0)
    nur = opt.get("nur", "")
    global PROBEN, FALL
    PROBEN, FALL = "proben" in opt, opt.get("fall", "")
    fehler = [0]

    def melde(text, fehler_=None):
        ist = text.startswith("FEHLER") if fehler_ is None else fehler_
        if ist: fehler[0] += 1
        print("ZEILE", satz, text)

    netze, anker = laden(satz)
    if nur == "rahmen":
        for vor, sicht in runden(netze):
            pruefe_rahmen(sicht, lambda t, fehler=None, vor=vor: melde(("FEHLER " + vor + t[7:]) if t.startswith("FEHLER ") else vor + t, fehler))
        print("ZEILE", satz, f"ERGEBNIS: {fehler[0]} Fehler"); sys.exit(0)
    if nur != "naht":
        pruefe_module(netze, anker, lambda t, fehler=None: melde(t, fehler))
        pruefe_verdeckung(netze, satz, lambda t, fehler=None: melde(t, fehler))
    for vor, sicht in runden(netze):
        mit = lambda t, fehler=None, vor=vor: melde(("FEHLER " + vor + t[7:]) if t.startswith("FEHLER ") else vor + t, fehler)
        if nur != "naht": pruefe_rahmen(sicht, mit)
        pruefe_naehte(sicht, float(opt.get("raster", "0.03")), mit)
    if nur != "naht":
        zeilen, zahl, _ = L.band_ueberlauf(os.path.join(Z.ZIEL_MODELL, f"zeile_{satz}.glb"))      # laedt die GLB neu, daher zuletzt
        for z in zeilen: print("ZEILE", satz, z.strip())
        fehler[0] += zahl
    print("ZEILE", satz, f"ERGEBNIS: {fehler[0]} Fehler")
    sys.exit(1 if fehler[0] else 0)


if __name__ == "__main__":
    main()
