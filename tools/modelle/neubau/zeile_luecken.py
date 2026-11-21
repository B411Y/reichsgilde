"""Pruefung der Zeilenmodule (Blender): Durchblick und Rahmen, gegen die exportierte GLB eines Satzes.

    blender -b --python tools/modelle/neubau/zeile_luecken.py -- --satz=<satz>[,<satz>...] [--quelle=glb|skript]

Durchblick: Je Nachbarschaft (zwei Koerper nebeneinander, mit den Seitenstuecken nach der Nachbarregel wie im Spiel) zielen Strahlen
von vorn und hinten (Hoehenwinkel -40 bis +40 Grad, seitlich -40 bis +40 Grad) auf Punkte im Inneren der Haeuser (unter der
Dachunterseite). Kommt ein Strahl ohne Treffer an, geht er durch die Huelle: gezaehlt mit der Hoehe, auf der er die Flucht kreuzt. Soll 0.
Rueckseiten: Strahlen von vorn und hinten auf die Naht zwischen den Haeusern (Grenze +-0,6 m, Ortgang bis Ortgang, bis ueber den First);
trifft ein Strahl zuerst die Rueckseite einer Flaeche (und ebenso zwei um 2 mm versetzte Nachbarstrahlen), sieht man in ein Bauteil
hinein. Alpha-Karten (Strohkante) zaehlen nicht (im Pixel-Look aus). Paare mit der Umgebungsvariable ZEILE_PAARE=„k_t6|k_t6,...“, --debug
nennt Einzelfaelle. Ausgabe zusaetzlich „RUECK <satz> <paar>: …“.
Rahmen: Wie weit ragt ein Koerper ueber seine Parzelle (x < 0 bzw. x > Breite), getrennt nach Dach (Stroh, Strohkante) und Wand. Soll:
Wand 0 cm; Dach nur, wo das Konzept einen Ueberstand vorsieht (keiner zur Seite).
Ausgabe: Zeilen „DURCH <satz> <links|rechts>: <n> Strahlen, Hoehe a-b m“ und „RAHMEN <satz> <modul>: Dach x cm, Wand y cm“.
"""
import math
import os
import sys

import bpy
from mathutils import Matrix, Vector
from mathutils.bvhtree import BVHTree

HIER = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HIER)
import zeile_module as Z      # noqa: E402
import zeile_bilder as ZB     # noqa: E402

DEBUG = "--debug" in sys.argv
PAARE = [p for p in (os.environ.get("ZEILE_PAARE") or "").split(",") if p] or ["k_t6|k_t6", "k_g6|k_g6", "k_g4|k_g4", "k_t6|eck", "k_t6|k_g6", "k_g4|k_t8", "k_g8|k_g4", "k_t8|k_t6", "k_g6|eck",
                 "k_g6_a|k_g4_b", "k_g4_c|k_g8_a", "k_g8_b|k_g6_c", "k_g6_b|eck_a", "k_g8_c|eck_b", "k_g4_a|k_g6_b"]
KANTE = {"Strohkante"}     # Alpha-Karten: im Pixel-Look aus, beidseitig – sie verdecken nichts und zaehlen nicht als Rueckseite
DACH = {"Stroh", "Strohkante"}


def module_laden(satz, quelle):
    for o in list(bpy.data.objects): bpy.data.objects.remove(o, do_unlink=True)
    netze = {}
    if quelle == "glb":
        bpy.ops.import_scene.gltf(filepath=os.path.join(Z.ZIEL_MODELL, f"zeile_{satz}.glb"))
        for o in bpy.data.objects:
            if o.type == "MESH": netze[o.name.split(".")[0]] = o
    else:
        stoffe = Z.stoffe_anlegen(Z.S["blatt"])
        for m in Z.MODULE:
            t, _ = Z.BAUER[m](m)
            netze[m] = t.objekt(stoffe)
    return netze


def dreiecke(o, matrix):
    pts = [matrix @ (o.matrix_world @ v.co) for v in o.data.vertices]
    polys = [tuple(p.vertices) for p in o.data.polygons]
    return pts, polys, [o.data.materials[p.material_index].name.split(".")[0] if o.data.materials else "" for p in o.data.polygons]


def unterseite(modul, x, y):
    """Dachunterseite eines Koerpers (Modulrahmen) oder None ausserhalb."""
    S = Z.S
    z_t, krag = S["z_traufe"], S["krag"]
    if modul.startswith("k_g"):
        w = Z.BREITE[modul]
        if not (Z.EIN < x < w - Z.EIN and 0 < y < Z.TIEFE): return None
        return z_t + min(x - Z.EIN, w - Z.EIN - x) * S["tg"]
    if modul.startswith("k_t"):
        w = Z.BREITE[modul]
        if not (0 < x < w and 0 < y < Z.TIEFE): return None
        return z_t + min(y + krag, Z.TIEFE - y) * S["tt"]
    if modul.startswith("eck"):
        Q, oy = Z.TIEFE + krag, -krag
        if not (0 < x < Z.TIEFE and 0 < y < Z.TIEFE): return None
        return z_t + S["tg"] * min(x, Q - x, y - oy, Q - (y - oy))
    return None


def durchblick(netze, satz, paar):
    links, rechts = paar.split("|")
    parzellen = [{"modul": links}, {"modul": rechts}]
    liste, _ = ZB.stuecke(parzellen)
    pts, polys, herkunft = [], [], []
    for modul, x0, spiegel in liste:
        o = netze[modul]
        w = Z.BREITE.get(ZB.modul_von_x(liste, x0), 0)
        m = Matrix.Translation((x0 + (w if spiegel else 0), 0, 0)) @ Matrix.Diagonal((-1 if spiegel else 1, 1, 1, 1))
        p, f, stoffe = dreiecke(o, m)
        start = len(pts); pts += p
        for q, stoff in zip(f, stoffe):
            if stoff in KANTE: continue
            q = tuple(i + start for i in q)
            polys.append(q[::-1] if spiegel else q)          # gespiegelt: Umlauf umkehren, sonst zeigt die Normale nach innen
            herkunft.append(f"{modul}{'/r' if spiegel else ''}:{stoff}")
    # Boden (die Module haben keinen Fussboden): eine grosse Flaeche bei z = 0
    start = len(pts); pts += [Vector((-200, -200, 0)), Vector((200, -200, 0)), Vector((200, 200, 0)), Vector((-200, 200, 0))]
    polys.append((start, start + 1, start + 2, start + 3))
    baum = BVHTree.FromPolygons(pts, polys, all_triangles=False, epsilon=0.0)
    ziele, x0 = [], 0.0
    for modul in (links, rechts):
        w = Z.BREITE[modul]
        for ix in range(1, int(w / 0.5)):
            for y in (0.3, 1.0, 4.0, 7.0, 7.7):
                u = unterseite(modul, ix * 0.5, y)
                if u is None: continue
                z = 0.02
                while z < u - 0.15:
                    ziele.append(Vector((x0 + ix * 0.5, y, z))); z += 0.25
        x0 += w
    durch, hoehen = 0, []
    for ziel in ziele:
        for seite in (1, -1):                     # 1: von vorn (Strahl in +y), -1: von hinten
            for e in range(-40, 41, 10):
                for a in range(-40, 41, 20):
                    er, ar = math.radians(e), math.radians(a)
                    d = Vector((math.sin(ar) * math.cos(er), seite * math.cos(ar) * math.cos(er), -math.sin(er))).normalized()
                    ursprung = ziel - d * 40.0
                    treffer = baum.ray_cast(ursprung, d, 40.0 - 0.02)
                    if treffer[0] is None:
                        durch += 1
                        y_flucht = 0.0 if seite > 0 else Z.TIEFE
                        t = (y_flucht - ursprung.y) / d.y
                        hoehen.append((ursprung + d * t).z)
                        if DEBUG and durch <= 4:
                            # Eintritt: letzter Punkt des Strahls ausserhalb der Wandflucht des Zielhauses (Abstand 2 cm Schritte)
                            k, p = 40.0, ziel
                            while k > 0:
                                q = ursprung + d * k
                                if not (0.0 < q.y < Z.TIEFE and q.z > 0): break
                                p = q; k -= 0.02
                            print(f"  STRAHL ziel {tuple(round(c, 2) for c in ziel)} richtung {tuple(round(c, 2) for c in d)} eintritt {tuple(round(c, 2) for c in p)}")
    if durch:
        print(f"DURCH {satz} {paar}: {durch} Strahlen, Hoehe {min(hoehen):.2f}-{max(hoehen):.2f} m (von {len(ziele) * 90})")
    # Rueckseiten: Strahlen von vorn und hinten auf die Naht zwischen den Haeusern (x = Grenze +-0,6 m, vom Ortgang vorn bis hinten,
    # bis ueber den First); trifft ein Strahl zuerst die Rueckseite einer Flaeche, sieht man in ein Bauteil hinein
    grenze = Z.BREITE[links]
    hoch = Z.S["z_traufe"] + 6.0
    rueck, orte, wer = 0, [], {}
    for ix in range(-6, 7):
        for y in [i * 0.5 - 1.0 for i in range(21)]:
            z = 0.1
            while z < hoch:
                ziel = Vector((grenze + ix * 0.1 + 0.05, y, z)); z += 0.25   # nicht genau in der Grenzebene (Strahlen streifend)
                for seite in (1, -1):
                    for e in range(-40, 41, 20):
                        for a in range(-40, 41, 20):
                            er, ar = math.radians(e), math.radians(a)
                            d = Vector((math.sin(ar) * math.cos(er), seite * math.cos(ar) * math.cos(er), -math.sin(er))).normalized()
                            ursprung = ziel - d * 40.0
                            ort, normale, nr, _ = baum.ray_cast(ursprung, d, 40.0)
                            if ort is not None and normale.dot(d) > 1e-4 and ort.z > 0.001:
                                # nur echte Loecher zaehlen: zwei um 2 mm versetzte Nachbarstrahlen muessen ebenfalls eine Rueckseite treffen
                                # (sonst streift der Strahl genau eine Kante zweier Flaechen)
                                echt = True
                                for dv in (Vector((0.002, 0.0, 0.0011)), Vector((-0.0013, 0.0, -0.002))):
                                    o2, n2, _, _ = baum.ray_cast(ursprung + dv, d, 40.0)
                                    if o2 is None or n2.dot(d) <= 1e-4: echt = False
                                if not echt: continue
                                rueck += 1; orte.append(ort)
                                if nr < len(herkunft): wer[herkunft[nr]] = wer.get(herkunft[nr], 0) + 1
                                if DEBUG and rueck <= 6:
                                    ecken = [tuple(round(c, 2) for c in pts[i]) for i in polys[nr]]
                                    print(f"  RUECKFALL {herkunft[nr] if nr < len(herkunft) else '?'} von {tuple(round(c, 2) for c in ursprung)} d {tuple(round(c, 2) for c in d)} trifft {tuple(round(c, 2) for c in ort)} flaeche {ecken}")
    if rueck:
        zs, ys = [o.z for o in orte], [o.y for o in orte]
        print(f"RUECK {satz} {paar}: {rueck} Strahlen auf Rueckseiten, Hoehe {min(zs):.2f}-{max(zs):.2f} m, y {min(ys):.2f}-{max(ys):.2f}")
        if DEBUG:
            for o in orte[:4]: print(f"  RUECKORT {tuple(round(c, 2) for c in o)}")
            for k, v in sorted(wer.items(), key=lambda kv: -kv[1])[:8]: print(f"  RUECKTEIL {k}: {v}")
    return durch + rueck


def rahmen(netze, satz):
    schlecht = 0
    for modul in [m for m in Z.MODULE if m in Z.BREITE]:
        w = Z.BREITE[modul]
        p, f, stoffe = dreiecke(netze[modul], Matrix.Identity(4))
        werte = {"Dach": 0.0, "Wand": 0.0}
        for q, stoff in zip(f, stoffe):
            art = "Dach" if stoff in DACH else "Wand"
            for i in q:
                x, y = p[i].x, p[i].y
                # Eckhaus: vorn und rechts Schauseiten (Auskragung, Ueberstand erlaubt), links und hinten Nachbarn
                ueber = max(-x, y - Z.TIEFE, 0.0) if modul.startswith("eck") else max(-x, x - w, 0.0)
                werte[art] = max(werte[art], ueber)
        if werte["Dach"] > 0.001 or werte["Wand"] > 0.001: schlecht += 1
        print(f"RAHMEN {satz} {modul}: Dach {werte['Dach'] * 100:.1f} cm, Wand {werte['Wand'] * 100:.1f} cm")
    return schlecht


def main():
    args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    opt = dict((a[2:].split("=", 1) + ["1"])[:2] for a in args if a.startswith("--"))
    summe = 0
    for satz in opt.get("satz", "mensch_s1").split(","):
        Z.stil(satz)
        netze = module_laden(satz, opt.get("quelle", "glb"))
        d = sum(durchblick(netze, satz, paar) for paar in PAARE)
        r = rahmen(netze, satz)
        print(f"ERGEBNIS {satz}: Durchblick {d} Strahlen, Rahmen {r} Module ueber der Grenze")
        summe += d + r
    print("GESAMT", "0 Fehler" if summe == 0 else f"{summe} Fehler")


if __name__ == "__main__":
    main()
