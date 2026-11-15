"""Gemeinsame Pruefungen fuer gebackene Neubau-Modelle (Huelle und Innenraum mit AO-Atlas auf UV2): keine schwarze Flaeche
und eine gesunde UV2 (Rueckmeldung des Nutzers 15.11.2026: tiefschwarze Flaechen am Steinhaus; Ursachen deckungsgleiche
Flaechen, fehlende Mauerstuecke und eine entartete UV2 mit ganzen Mauerstuecken auf einem Bildpunkt).

Aus einem Messskript (laeuft in Blender, laedt das GLB selbst in eine leere Szene):

    import uv2_pruefung as U
    zeilen, fehler = U.schwarze_flaechen(glb)          # Pruefung 12
    zeilen, fehler, werte = U.uv2_inseln(glb)          # UV2-Pruefung (werte: Zahlen fuer die Tabelle)

Allein (Ausgabe mit Praefix PRUEFUNG, Rueckgabewert 1 bei Fehlern):

    blender -b --factory-startup --python tools/modelle/neubau/uv2_pruefung.py -- --glb=<datei.glb> [--texturen=<ordner>]
        [--blatt=<trim>] [--atlas-json=<datei>] [--gruppe=<gruppe>] [--modell=<name>]

Wovon die Pruefungen ausgehen (Standard, jeder Wert laesst sich als Schluesselwort uebergeben):
  * modell = Dateiname ohne ".glb" und ohne "_innen"; Innenraum, wenn der Dateiname auf "_innen.glb" endet.
  * atlas_json = ao_atlas.json neben dieser Datei; gruppe = die Gruppe, deren "plaetze" das Modell enthalten und deren Name
    genau dann auf "_innen" endet, wenn das GLB der Innenraum ist. Daraus: Atlasdatei, Kante, Zellen je Seite, Zelle.
  * texturen = game/assets/texturen/neubau (Trim-Sheets und Atlanten; fuer eine Gegenprobe den Ordner des alten Stands).
  * blatt = "innen_trim" fuer den Innenraum, sonst "trim" (andere Stufen uebergeben ihr Blatt, z. B. "trim_s3").
  * trim_frei = Stoffe ohne Trim-Sheet (eigene Farbe, keine Albedo-Probe): Fenster, Bleinetz, Hausfarbe, Holz, Eisen, Innenglas, Glas.

Grenzwerte schwarze_flaechen (je Flaeche eines Objekts mit UV2, gemessen an den gebackenen Dateien):
  * ein Stoff je Flaeche ("ohne Stoff"), UV und UV2 vorhanden ("ohne UV");
  * UV im Blatt (v in 0..1; u darf kacheln) und UV2 in der Zelle des Modells im Atlas (Toleranz 0,001);
  * Albedo des Blatts im Mittel ueber die Flaeche mindestens 0,025 (dunkles Holz liegt bei 0,04; Eisen im Eisenband ausgenommen);
  * gebackene Verdeckung im Mittel ueber den sichtbaren Teil mindestens 0,12 (Flaeche ab 100 cm2, mindestens 3 sichtbare
    Stichproben; sichtbar = auf 8 cm vor dem Punkt liegt nichts und die Stelle liegt nicht auf einer entgegengesetzt gerichteten
    Flaeche derselben Ebene auf (Fensterstock auf der Laibung), so zaehlen verdeckte Stuecke hinter Gewaende, Stufe, Moebel nicht;
    der Shader dunkelt mit 75 % davon ab – deckungsgleiche Flaechen backen 0) – es sei denn, die Nachrechnung (48 feste
    Strahlen ueber der Halbkugel je Stichprobe der ganzen Flaeche, Reichweite 1,1 m wie beim Backen, aussen mit Boden) ist
    hoechstens 0,25 heller:
    dann ist die Stelle wirklich so verdeckt (Kontaktschatten unter Traufe, zwischen Balken) und schwarz ist richtig;
  * UV2 nicht entartet: eine Flaeche ab 0,05 m2 hat mindestens 2 Bildpunkte des Atlas;
  * keine zwei deckungsgleichen Flaechen (gleiche Ebene auf 2 mm, gleiche Richtung, sichtbar ueberdeckt > 20 cm2).

Grenzwerte uv2_inseln (Inseln = Flaechen, die in der UV2 eine Kante teilen; alle Objekte des GLB liegen in einer Zelle):
  * kleinste Insel ab 0,05 m2 mindestens 2 Bildpunkte (Bildpunkte = Flaeche in der UV2 mal Kante des Atlas zum Quadrat);
  * jede Insel ab 0,05 m2 hat mindestens ein Viertel der mittleren Dichte (Bildpunkte je m2, Median ueber die Flaeche) –
    Mauerstuecke bekommen Inseln nach ihrer Groesse, keines wird zusammengepresst;
  * kleinster Inselrand (Abstand zweier Inseln in Bildpunkten) mindestens 1,0: dazwischen bleibt ein Bildpunkt, den das Backen
    mit dem Rand der Nachbarinsel fuellt; ueberlappende Inseln (Abstand 0) sind immer Fehler.
  Smart UV Project mit Inselrand 0,003 (wohnhaus_mensch_s1_g1.uv2_und_ao) ergibt in einer Zelle von 512 Bildpunkten gemessen
  3,9 bis 4 Bildpunkte (Mensch Stufe 1, 15.11.2026); mit 0,02 waren es ueber 4, dafuer lagen ganze Mauerstuecke auf 1–2 Bildpunkten.
"""
import json
import math
import os
import re
import sys

import bpy
from mathutils import Vector

HIER = os.path.dirname(os.path.abspath(__file__))
WURZEL = os.path.dirname(os.path.dirname(os.path.dirname(HIER)))
TEXTUREN = os.path.join(WURZEL, "game", "assets", "texturen", "neubau")
TRIM_FREI = ("Fenster", "Bleinetz", "Hausfarbe", "Holz", "Eisen", "Innenglas", "Glas")
BAND_EISEN = 0.95                     # Eisen liegt im Blatt oberhalb v = 0,95 (Godot-Zaehlung); in Blender-UV unterhalb 0,05

# Grenzwerte (siehe Kopf)
ALBEDO_MIN = 0.025
AO_MIN = 0.12
AO_ABSTAND = 0.25                     # Verdeckung schwarz nur, wenn die Nachrechnung um mehr als 0,25 heller ist
AO_REICHWEITE = 1.1                   # wie beim Backen (light_settings.distance in uv2_und_ao)
AO_FLAECHE_MIN = 0.01
SICHT_TIEFE = 0.08
ENTARTET_FLAECHE = 0.05
ENTARTET_BILDPUNKTE = 2.0
DECKUNG_MIN = 0.002
EBENE_TOL = 0.002                     # zwei Flaechen gelten als eine Ebene bis 2 mm Abstand
DICHTE_ANTEIL_MIN = 0.25
RAND_MIN = 1.0
LISTE_MAX = 5                         # so viele Fundstellen je Fehlerart in der Ausgabe (--alle: alle)


def _optionen():
    args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    return dict((a[2:].split("=", 1) + ["1"])[:2] for a in args if a.startswith("--"))


def atlas_von(glb, atlas_json=None, gruppe=None, modell=None):
    """Platz des Modells im AO-Atlas: dict(datei, kante, zellen, spalte, zeile, modell, innen)."""
    name = os.path.basename(glb)[:-4]
    innen = name.endswith("_innen")
    modell = modell or (name[:-6] if innen else name)
    daten = json.load(open(atlas_json or os.path.join(HIER, "ao_atlas.json"), encoding="utf-8"))
    if gruppe is None:
        passend = [g for g, d in daten.items() if modell in d["plaetze"] and g.endswith("_innen") == innen]
        if len(passend) != 1: raise SystemExit(f"Atlasgruppe fuer {modell} (innen={innen}) nicht eindeutig: {passend}")
        gruppe = passend[0]
    d = daten[gruppe]
    nummer = d["plaetze"][modell]
    return dict(datei=d["datei"], kante=d["kante"], zellen=d["zellen"], spalte=nummer % d["zellen"], zeile=nummer // d["zellen"],
                modell=modell, innen=innen, gruppe=gruppe)


def _lade_glb(glb):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=glb)


def _bild(pfad):
    import numpy as np
    im = bpy.data.images.load(pfad); w, h = im.size
    a = np.empty(w * h * 4, dtype=np.float32); im.pixels.foreach_get(a)
    bpy.data.images.remove(im)
    return a.reshape(h, w, 4)


def _probe(img, u, v):
    h, w = img.shape[:2]
    return float(img[min(max(int(v * h), 0), h - 1), min(max(int(u * w), 0), w - 1), :3].mean())


def _bilinear(img, u, v):
    """Wie das Spiel (filter_linear): zwischen den vier naechsten Bildpunkten gemittelt."""
    h, w = img.shape[:2]
    x, y = u * w - 0.5, v * h - 0.5
    x0, y0 = int(math.floor(x)), int(math.floor(y)); fx, fy = x - x0, y - y0
    g = lambda xx, yy: float(img[min(max(yy, 0), h - 1), min(max(xx, 0), w - 1), :3].mean())
    return (g(x0, y0) * (1 - fx) + g(x0 + 1, y0) * fx) * (1 - fy) + (g(x0, y0 + 1) * (1 - fx) + g(x0 + 1, y0 + 1) * fx) * fy


def _achse3(n):
    return max(range(3), key=lambda k: abs(n[k]))


def _schnittflaeche(p1, p2, n):
    """Flaeche, in der sich zwei konvexe Vielecke derselben Ebene ueberdecken (Sutherland-Hodgman in der Ebene), und ihre Mitte."""
    k = _achse3(n)
    a, b = [i for i in range(3) if i != k]
    zwei = lambda pts: [(q[a], q[b]) for q in pts]

    def flaeche(v):
        return abs(sum(v[i][0] * v[(i + 1) % len(v)][1] - v[(i + 1) % len(v)][0] * v[i][1] for i in range(len(v)))) / 2 if len(v) > 2 else 0.0

    def links(v):
        s = sum(v[i][0] * v[(i + 1) % len(v)][1] - v[(i + 1) % len(v)][0] * v[i][1] for i in range(len(v)))
        return v if s > 0 else v[::-1]
    aus, klinge = links(zwei(p1)), links(zwei(p2))
    for i in range(len(klinge)):
        c0, c1 = klinge[i], klinge[(i + 1) % len(klinge)]
        innen = lambda q: (c1[0] - c0[0]) * (q[1] - c0[1]) - (c1[1] - c0[1]) * (q[0] - c0[0]) > 1e-9

        def schnitt(s_, e_):
            dx, dy = e_[0] - s_[0], e_[1] - s_[1]
            ex, ey = c1[0] - c0[0], c1[1] - c0[1]
            nenner = dx * ey - dy * ex
            t = ((c0[0] - s_[0]) * ey - (c0[1] - s_[1]) * ex) / nenner if abs(nenner) > 1e-12 else 0.0
            return (s_[0] + t * dx, s_[1] + t * dy)
        ein, aus = aus, []
        for j in range(len(ein)):
            s_, e_ = ein[j - 1], ein[j]
            if innen(e_):
                if not innen(s_): aus.append(schnitt(s_, e_))
                aus.append(e_)
            elif innen(s_):
                aus.append(schnitt(s_, e_))
        if not aus: return 0.0, None
    ma, mb = sum(q[0] for q in aus) / len(aus), sum(q[1] for q in aus) / len(aus)
    m3 = [0.0, 0.0, 0.0]; m3[a], m3[b] = ma, mb; m3[k] = sum(q[k] for q in p1) / len(p1)
    return flaeche(aus), Vector(m3)


def _richtungen(anzahl=48):
    """Feste, kosinusgewichtete Richtungen ueber der Halbkugel (z oben; Fibonacci-Scheibe, kein Zufall)."""
    gold = math.pi * (3 - math.sqrt(5))
    aus = []
    for i in range(anzahl):
        r = math.sqrt((i + 0.5) / anzahl); phi = i * gold
        aus.append(Vector((r * math.cos(phi), r * math.sin(phi), math.sqrt(max(0.0, 1 - r * r)))))
    return aus


RICHTUNGEN = _richtungen()


def _verdeckung_soll(szene, dg, punkte, n):
    """Unverdeckter Anteil der Halbkugel ueber den Punkten (Reichweite AO_REICHWEITE) im Mittel – wie Cycles' AO-Backen."""
    t = n.orthogonal().normalized(); b = n.cross(t)
    frei = 0
    for q in punkte:
        start = q + n * 1e-4
        for d in RICHTUNGEN:
            r = t * d.x + b * d.y + n * d.z
            if not szene.ray_cast(dg, start, r, distance=AO_REICHWEITE)[0]: frei += 1
    return frei / max(len(punkte) * len(RICHTUNGEN), 1)


def _lagen():
    """Alle achsparallelen Flaechen der Szene nach Ebene: {(Achse, Fach): [(Lage, Vorzeichen der Normale, Umriss in der Ebene)]}."""
    lagen = {}
    for o in bpy.data.objects:
        if o.type != "MESH" or o.name == "Pruefboden": continue
        mw = o.matrix_world
        for p in o.data.polygons:
            n = (mw.to_3x3() @ p.normal).normalized()
            k = _achse3(n)
            if abs(n[k]) < 0.99: continue
            a, b = [i for i in range(3) if i != k]
            pts = [mw @ o.data.vertices[i].co for i in p.vertices]
            lage = sum(q[k] for q in pts) / len(pts)
            lagen.setdefault((k, math.floor(lage / EBENE_TOL)), []).append((lage, 1 if n[k] > 0 else -1, [(q[a], q[b]) for q in pts]))
    return lagen


def _aufliegend(lagen, x, n):
    """Liegt der Punkt x (auf einer Flaeche mit Normale n) auf einer entgegengesetzt gerichteten Flaeche derselben Ebene? Dann
    beruehren sich zwei Teile (Fensterstock auf der Laibung, Brett auf Brett): die Stelle ist nicht zu sehen, schwarz ist richtig."""
    k = _achse3(n)
    if abs(n[k]) < 0.99: return False
    a, b = [i for i in range(3) if i != k]
    vz = 1 if n[k] > 0 else -1
    pu, pv = x[a], x[b]
    for fach in range(math.floor(x[k] / EBENE_TOL) - 1, math.floor(x[k] / EBENE_TOL) + 2):
        for lage, vz2, umriss in lagen.get((k, fach), []):
            if vz2 == vz or abs(lage - x[k]) > EBENE_TOL: continue
            seiten = [(umriss[(i + 1) % len(umriss)][0] - umriss[i][0]) * (pv - umriss[i][1]) - (umriss[(i + 1) % len(umriss)][1] - umriss[i][1]) * (pu - umriss[i][0]) for i in range(len(umriss))]
            if all(s_ >= -1e-9 for s_ in seiten) or all(s_ <= 1e-9 for s_ in seiten): return True
    return False


def _mit_uv2(o):
    return o.type == "MESH" and len(o.data.uv_layers) > 1


def schwarze_flaechen(glb, texturen=None, blatt=None, atlas=None, trim_frei=TRIM_FREI):
    """Pruefung 12: keine schwarze Flaeche (Grenzwerte im Kopf). Rueckgabe (Zeilen, Fehlerzahl)."""
    atlas = atlas or atlas_von(glb)
    texturen = texturen or TEXTUREN
    blatt = blatt or ("innen_trim" if atlas["innen"] else "trim")
    _lade_glb(glb)
    alb = _bild(os.path.join(texturen, f"{blatt}_albedo.png"))
    ao = _bild(os.path.join(texturen, atlas["datei"]))
    nz, sp, ze = atlas["zellen"], atlas["spalte"], atlas["zeile"]
    kante = ao.shape[0]
    fehler = {}

    def melde(art, text):
        fehler.setdefault(art, []).append(text)
    if not atlas["innen"]:
        # Boden unter dem Haus wie beim Backen (der Verdecker „boden“ steht nicht im GLB)
        zmin = min((o.matrix_world @ v.co).z for o in bpy.data.objects if o.type == "MESH" for v in o.data.vertices)
        bpy.ops.mesh.primitive_plane_add(size=60, location=(0, 0, zmin - 0.001))
        boden = bpy.context.active_object; boden.name = "Pruefboden"
    dg = bpy.context.evaluated_depsgraph_get()
    szene = bpy.context.scene
    lagen = _lagen()
    gew = [(a_, b_, 1 - a_ - b_) for a_ in (0.1, 0.3, 0.5, 0.7) for b_ in (0.1, 0.3, 0.5, 0.7) if a_ + b_ <= 0.9]
    ebenen = {}
    geprueft = 0
    for o in bpy.data.objects:
        if o.type != "MESH" or o.name == "Pruefboden": continue
        mw = o.matrix_world
        name = o.name.split(".")[0]
        hat_uv2 = _mit_uv2(o)
        uv1 = o.data.uv_layers[0] if o.data.uv_layers else None
        uv2 = o.data.uv_layers[1] if hat_uv2 else None
        for p in o.data.polygons:
            c = mw @ p.center
            ort = f"{name} ({c.x:.2f}, {c.y:.2f}, {c.z:.2f})"
            if not o.data.materials or o.data.materials[p.material_index] is None:
                melde("ohne Stoff", ort); continue
            if not hat_uv2: continue                       # Scheiben, Fahne, Glas: eigene Stoffe ohne Verdeckung
            geprueft += 1
            mat = o.data.materials[p.material_index].name.split(".")[0]
            if uv1 is None:
                melde("ohne UV", ort); continue
            u = [uv1.data[i].uv.copy() for i in p.loop_indices]
            w2 = [uv2.data[i].uv.copy() for i in p.loop_indices]
            ecken = [mw @ o.data.vertices[o.data.loops[i].vertex_index].co for i in p.loop_indices]
            n = (mw.to_3x3() @ p.normal).normalized()
            stich = []
            for k in range(1, len(u) - 1):
                for g0, g1, g2 in gew:
                    stich.append((g0 * u[0].x + g1 * u[k].x + g2 * u[k + 1].x, g0 * u[0].y + g1 * u[k].y + g2 * u[k + 1].y,
                                  g0 * w2[0].x + g1 * w2[k].x + g2 * w2[k + 1].x, g0 * w2[0].y + g1 * w2[k].y + g2 * w2[k + 1].y,
                                  g0 * ecken[0] + g1 * ecken[k] + g2 * ecken[k + 1]))
            if not (sp / nz - 1e-3 <= min(x.x for x in w2) and max(x.x for x in w2) <= (sp + 1) / nz + 1e-3
                    and ze / nz - 1e-3 <= min(x.y for x in w2) and max(x.y for x in w2) <= (ze + 1) / nz + 1e-3):
                melde("UV2 ausserhalb der Zelle", f"{ort} {mat}")
            # UV2 entartet: eine Flaeche ab 0,05 m2 auf weniger als 2 Bildpunkte gepresst (backt Unsinn, meist schwarz)
            bp = abs(sum(w2[i].x * w2[(i + 1) % len(w2)].y - w2[(i + 1) % len(w2)].x * w2[i].y for i in range(len(w2)))) / 2 * kante ** 2
            if p.area >= ENTARTET_FLAECHE and bp < ENTARTET_BILDPUNKTE:
                melde("UV2 entartet", f"{ort} {mat} {p.area * 1e4:.0f} cm2 auf {bp:.1f} Bildpunkte")
            if mat in trim_frei: albedo_pruefen = False
            else:
                albedo_pruefen = True
                if not (-1e-3 <= min(x.y for x in u) and max(x.y for x in u) <= 1 + 1e-3):
                    melde("UV ausserhalb des Blatts", f"{ort} {mat}")
            eisen = all(x.y <= 1.0 - BAND_EISEN + 1e-3 for x in u)          # Beschlaege, Anker: dunkles Eisen ist gewollt
            if albedo_pruefen and not eisen:
                a = sum(_probe(alb, x[0] % 1.0, x[1]) for x in stich) / len(stich)
                if a < ALBEDO_MIN: melde("Albedo schwarz", f"{ort} {mat} {a:.3f}")
            # sichtbar: auf 8 cm vor dem Punkt liegt nichts und die Stelle liegt nicht auf einem anderen Teil auf; deckungsgleiche
            # Flaechen (gleiche Richtung) zaehlen als sichtbar - genau sie backen schwarz
            sicht = [x for x in stich if not szene.ray_cast(dg, x[4] + n * 1e-4, n, distance=SICHT_TIEFE)[0] and not _aufliegend(lagen, x[4], n)]
            if p.area >= AO_FLAECHE_MIN and len(sicht) >= 3 and not eisen:
                mittel = sum(_bilinear(ao, x[2], x[3]) for x in sicht) / len(sicht)
                if mittel < AO_MIN:
                    # nachrechnen: so hell muesste die Verdeckung dort sein (Strahlen ueber die Halbkugel, Reichweite wie beim Backen);
                    # dunkel ist nur ein Fehler, wenn die Rechnung deutlich heller ist (Kontaktschatten unter Traufe und Balken sind echt)
                    # ueber die ganze Flaeche wie das Backen (ein Bildpunkt mittelt auch ihre verdeckten Teile, etwa einen Spalt)
                    soll = _verdeckung_soll(szene, dg, [x[4] for x in stich], n)
                    if soll - mittel > AO_ABSTAND:
                        melde("Verdeckung schwarz", f"{ort} {mat} {mittel:.2f} im Mittel (nachgerechnet {soll:.2f}), {p.area * 1e4:.0f} cm2")
            if sicht and p.area > DECKUNG_MIN:
                ebenen.setdefault((_achse3(n), math.floor((c @ n) / EBENE_TOL)), []).append(([mw @ o.data.vertices[i].co for i in p.vertices], n, f"{ort} {mat}", c))
    # deckungsgleich: zwei Flaechen in derselben Ebene (Abstand bis 2 mm) mit derselben Richtung ueberdecken sich (> 20 cm2) -
    # flackern im Spiel und backen beide schwarz (so die Gewaende zweier Nachbarfenster am Steinhaus)
    for (ax_, fach), fl_ in ebenen.items():
        nachbar = ebenen.get((ax_, fach + 1), [])
        for i, (p1, n1, ort1, d1) in enumerate(fl_):
            for p2, n2, ort2, d2 in fl_[i + 1:] + nachbar:
                # Abstand der Mitte der zweiten Flaeche von der Ebene der ersten (nicht c @ n je Flaeche: bei leicht verschiedenen
                # Normalen weit vom Ursprung ergaebe das falsche Naehe)
                if n1.dot(n2) < 0.99 or abs(n1.dot(d2 - d1)) > EBENE_TOL: continue
                f, mitte_ = _schnittflaeche(p1, p2, n1)
                if f > DECKUNG_MIN and not szene.ray_cast(dg, mitte_ + n1 * 1e-3, n1, distance=SICHT_TIEFE)[0]:
                    melde("deckungsgleich", f"{ort1} mit {ort2}, {f * 1e4:.0f} cm2")
    zeilen, anzahl = [f"   {geprueft} Flaechen mit UV2 geprueft (Blatt {blatt}, Atlas {atlas['datei']} Zelle {sp}/{ze})"], 0
    for art, liste in sorted(fehler.items()):
        anzahl += len(liste)
        zeilen.append(f"   FEHLER {art}: {len(liste)} (" + "; ".join(liste[:LISTE_MAX]) + (" ..." if len(liste) > LISTE_MAX else "") + ")")
    return zeilen, anzahl


def _seg_abstand(a, b, c, d):
    """Kleinster Abstand zweier Strecken ab und cd in der Ebene (0 bei Schnitt)."""
    def kreuz(o, p, q):
        return (p[0] - o[0]) * (q[1] - o[1]) - (p[1] - o[1]) * (q[0] - o[0])
    d1, d2, d3, d4 = kreuz(c, d, a), kreuz(c, d, b), kreuz(a, b, c), kreuz(a, b, d)
    if ((d1 > 0) != (d2 > 0)) and ((d3 > 0) != (d4 > 0)) and d1 * d2 < 0 and d3 * d4 < 0: return 0.0

    def pkt(p, s, e):
        dx, dy = e[0] - s[0], e[1] - s[1]
        l2 = dx * dx + dy * dy
        t = 0.0 if l2 == 0 else max(0.0, min(1.0, ((p[0] - s[0]) * dx + (p[1] - s[1]) * dy) / l2))
        qx, qy = s[0] + t * dx - p[0], s[1] + t * dy - p[1]
        return math.sqrt(qx * qx + qy * qy)
    return min(pkt(a, c, d), pkt(b, c, d), pkt(c, a, b), pkt(d, a, b))


def uv2_inseln(glb, atlas=None):
    """UV2-Pruefung (Grenzwerte im Kopf). Rueckgabe (Zeilen, Fehlerzahl, Werte) mit Werte = dict(inseln, kleinste_bp,
    kleinste_bp_gross, dichte_median, dichte_min_anteil, rand_min_bp, ueberlappend, zu_eng)."""
    atlas = atlas or atlas_von(glb)
    _lade_glb(glb)
    kante = atlas["kante"]
    rund = lambda uv: (round(uv[0] * kante * 64), round(uv[1] * kante * 64))     # 1/64 Bildpunkt
    # Flaechen sammeln (alle Objekte mit UV2: eine gemeinsame Abwicklung je GLB)
    flaechen = []           # (Punkte in Bildpunkten, Schluessel der Ecken, Flaeche UV2 in Bildpunkten, Flaeche 3D, Ort)
    for o in bpy.data.objects:
        if not _mit_uv2(o): continue
        mw = o.matrix_world
        uv2 = o.data.uv_layers[1]
        for p in o.data.polygons:
            pts = [(uv2.data[i].uv[0] * kante, uv2.data[i].uv[1] * kante) for i in p.loop_indices]
            schl = [rund(uv2.data[i].uv) for i in p.loop_indices]
            bp = abs(sum(pts[i][0] * pts[(i + 1) % len(pts)][1] - pts[(i + 1) % len(pts)][0] * pts[i][1] for i in range(len(pts)))) / 2
            ecken = [mw @ o.data.vertices[i].co for i in p.vertices]
            f3 = sum(((ecken[k] - ecken[0]).cross(ecken[k + 1] - ecken[0])).length / 2 for k in range(1, len(ecken) - 1))
            c = mw @ p.center
            flaechen.append((pts, schl, bp, f3, f"{o.name.split('.')[0]} ({c.x:.2f}, {c.y:.2f}, {c.z:.2f})"))
    # Inseln: Flaechen, die in der UV2 eine Kante teilen (Union-Find ueber die Kantenschluessel)
    eltern = list(range(len(flaechen)))

    def wurzel(i):
        while eltern[i] != i:
            eltern[i] = eltern[eltern[i]]; i = eltern[i]
        return i
    kanten = {}
    for fi, (_pts, schl, *_r) in enumerate(flaechen):
        for k in range(len(schl)):
            e = tuple(sorted((schl[k], schl[(k + 1) % len(schl)])))
            if e[0] == e[1]: continue
            if e in kanten: eltern[wurzel(fi)] = wurzel(kanten[e][0])
            kanten.setdefault(e, []).append(fi)
    inseln = {}
    for fi in range(len(flaechen)): inseln.setdefault(wurzel(fi), []).append(fi)
    daten = []              # (Wurzel, Bildpunkte, Flaeche 3D, Ort)
    for w, fl in inseln.items():
        bp = sum(flaechen[i][2] for i in fl); f3 = sum(flaechen[i][3] for i in fl)
        daten.append((w, bp, f3, flaechen[max(fl, key=lambda i: flaechen[i][3])][4]))
    gross = [d for d in daten if d[2] >= ENTARTET_FLAECHE]
    # Dichte (Bildpunkte je m2): Median gewichtet nach Flaeche
    dichten = sorted((d[1] / d[2], d[2]) for d in daten if d[2] > 1e-6)
    halb, s, median = sum(f for _, f in dichten) / 2, 0.0, 0.0
    for dichte, f in dichten:
        s += f
        if s >= halb: median = dichte; break
    fehler = {}

    def melde(art, text):
        fehler.setdefault(art, []).append(text)
    for w, bp, f3, ort in gross:
        if bp < ENTARTET_BILDPUNKTE: melde("Insel zu klein", f"{ort} {f3 * 1e4:.0f} cm2 auf {bp:.1f} Bildpunkte")
        elif median > 0 and bp / f3 < DICHTE_ANTEIL_MIN * median:
            melde("Insel gepresst", f"{ort} {f3 * 1e4:.0f} cm2, Dichte {bp / f3:.0f} statt {median:.0f} Bildpunkte je m2")
    # Inselrand: Randkanten (nur eine Flaeche) je Insel in ein Gitter, Abstand zu Randkanten anderer Inseln
    zelle_g = 4.0
    gitter = {}
    rand = []
    for e, fl in kanten.items():
        if len(fl) != 1: continue
        a = (e[0][0] / 64, e[0][1] / 64); b = (e[1][0] / 64, e[1][1] / 64)
        rand.append((a, b, wurzel(fl[0])))
    for ri, (a, b, _w) in enumerate(rand):
        for gx in range(int(math.floor(min(a[0], b[0]) / zelle_g)) - 1, int(math.floor(max(a[0], b[0]) / zelle_g)) + 2):
            for gy in range(int(math.floor(min(a[1], b[1]) / zelle_g)) - 1, int(math.floor(max(a[1], b[1]) / zelle_g)) + 2):
                gitter.setdefault((gx, gy), []).append(ri)
    paare = {}
    for liste in gitter.values():
        for i in range(len(liste)):
            ai, bi, wi = rand[liste[i]]
            for j in range(i + 1, len(liste)):
                aj, bj, wj = rand[liste[j]]
                if wi == wj: continue
                schl = (min(wi, wj), max(wi, wj))
                if schl in paare and paare[schl] == 0.0: continue
                d = _seg_abstand(ai, bi, aj, bj)
                if d < paare.get(schl, zelle_g): paare[schl] = d
    ort_von = {d[0]: d[3] for d in daten}
    rand_min = min(paare.values()) if paare else zelle_g
    ueber = [(k, v) for k, v in paare.items() if v == 0.0]
    eng = [(k, v) for k, v in paare.items() if 0.0 < v < RAND_MIN]
    for (w1, w2), v in sorted(ueber + eng, key=lambda x: x[1]):
        melde("Inseln ueberlappen" if v == 0.0 else "Inselrand zu schmal", f"{ort_von[w1]} / {ort_von[w2]} {v:.2f} Bildpunkte")
    werte = dict(inseln=len(daten), kleinste_bp=min((d[1] for d in daten), default=0.0),
                 kleinste_bp_gross=min((d[1] for d in gross), default=0.0), dichte_median=median,
                 dichte_min_anteil=min((d[1] / d[2] / median for d in gross), default=0.0) if median else 0.0,
                 rand_min_bp=rand_min, ueberlappend=len(ueber), zu_eng=len(eng))
    zeilen = [f"   {werte['inseln']} Inseln; kleinste {werte['kleinste_bp']:.2f} Bildpunkte, kleinste ab 0,05 m2 "
              f"{werte['kleinste_bp_gross']:.1f} (Soll >= {ENTARTET_BILDPUNKTE:.0f}); Dichte Median {median:.0f} Bildpunkte je m2, "
              f"duennste Insel ab 0,05 m2 {werte['dichte_min_anteil']:.2f} davon (Soll >= {DICHTE_ANTEIL_MIN}); kleinster Inselrand " + (
              f"{rand_min:.2f}" if paare else f"ueber {zelle_g:.0f}") + f" Bildpunkte (Soll >= {RAND_MIN}), ueberlappend {len(ueber)}, zu schmal {len(eng)}"]
    anzahl = 0
    for art, liste in sorted(fehler.items()):
        anzahl += len(liste)
        zeilen.append(f"   FEHLER {art}: {len(liste)} (" + "; ".join(liste[:LISTE_MAX]) + (" ..." if len(liste) > LISTE_MAX else "") + ")")
    return zeilen, anzahl, werte


def main():
    global LISTE_MAX
    opt = _optionen()
    glb = opt["glb"]
    if "alle" in opt: LISTE_MAX = 100000
    atlas = atlas_von(glb, opt.get("atlas-json"), opt.get("gruppe"), opt.get("modell"))
    z1, f1 = schwarze_flaechen(glb, opt.get("texturen"), opt.get("blatt"), atlas)
    z2, f2, _w = uv2_inseln(glb, atlas)
    print(f"PRUEFUNG {glb}")
    print(f"PRUEFUNG 12. Keine schwarze Flaeche: {'ok' if not f1 else str(f1) + ' FEHLER'}")
    for z in z1: print("PRUEFUNG " + z)
    print(f"PRUEFUNG UV2-Inseln: {'ok' if not f2 else str(f2) + ' FEHLER'}")
    for z in z2: print("PRUEFUNG " + z)
    if f1 + f2: sys.exit(1)


if __name__ == "__main__":
    main()
