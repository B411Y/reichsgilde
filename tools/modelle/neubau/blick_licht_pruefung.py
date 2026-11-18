"""Pruefung 12b und 12c (Klicktest der Elbenhaeuser Stufe 2/3, 18.11.2026): schwarze Stellen, die das Backen und die Verdeckung nicht
zeigen, weil sie erst im Spiel entstehen.

  12b Rueckseite offen (Huelle): Eine nach unten zeigende Flaeche (Normale z < -0,3, ab 100 cm2), ueber deren Rueckseite nichts liegt
      (Strahlen von der Rueckseite nach oben treffen auf 30 m nichts), ist von oben nur von hinten zu sehen. Das Spiel zeichnet Rueckseiten
      nicht (Culling): dort ist ein Loch, dunkel oder durchsichtig. So fehlte am Vordach eine Dachflaeche – Dachflaeche und Untersicht lagen
      deckungsgleich, beim Verschmelzen der Punkte loeschte Blender eine von beiden (Pruefung 12 sah nur die Untersicht, gut belichtet).
      Fehler, wenn mindestens die Haelfte der Stichproben frei nach oben sieht.
  12c Ohne Licht im Standardblick (Innenraum): Eine helle Flaeche des Raums (Albedo im Mittel ab 0,30; Objekte Raum, Decke, Fensterseiten,
      Herd_Set, ab 500 cm2), die die Raumkamera (Blick_Auge, Blick_Ziel, Oeffnungswinkel 66 Grad hoch, 16:9) sieht, bekommt von keinem
      Licht-Anker (Licht_*) direktes Licht: dann bleibt im Spiel nur das Umgebungslicht, und heller Stein oder helles Holz wirken fast
      schwarz neben den beleuchteten Flaechen (rechte Herdseiten, Seite des Unterzugs, Balkenseiten zur Kamera). Direktes Licht = der Anker
      liegt vor der Flaeche mit Kosinus ab LICHT_COS_MIN, hoechstens LICHT_WEITE entfernt, und – bei Lichtern mit Schatten, also nicht
      beim Grundlicht Licht_Raum – der Weg ist frei. Fehler, wenn mindestens
      die Haelfte der sichtbaren Stichproben ohne Licht ist (mindestens 3 sichtbare). Unterseiten (Normale z < -0,7: Rauchfang im Herd,
      Untersichten) bleiben unbewertet.
Aufruf aus wohnhaus_elb_messen.py (main_s23); Gegenprobe: der Stand vor dem Fix (Branch phase5/neubau-elb-s23b) findet 12b am Vordach
(G2, S3 G2) und 12c an Feuerstelle, Schlot, Deckenbalken der Wohnstube und an den Balken in Hinterzimmer und Bibliothek.
"""
import math
import os

import bpy
from mathutils import Vector

RUECK_FLAECHE_MIN = 0.01
RUECK_NZ_MAX = -0.3
RUECK_WEITE = 30.0
LICHT_FLAECHE_MIN = 0.05
LICHT_ALBEDO_MIN = 0.30
LICHT_COS_MIN = 0.02                  # der Anker liegt vor der Flaeche (streifendes Licht genuegt; dahinter = kein direktes Licht)
LICHT_WEITE = 6.5
LICHT_NZ_MIN = -0.7                   # Unterseiten (Rauchfang im Herd, Untersichten) bleiben unbewertet: gewollt dunkel bzw. vom Boden aufgehellt
UMSCHAUEN = True                      # sichtbar = vom Blickpunkt frei zu sehen (Umschauen im Spiel), nicht nur im Standardbild
AO_BLICK_MIN = 0.12                  # wie AO_MIN von Pruefung 12 (uv2_pruefung.py)
LICHT_OBJEKTE = ("Raum", "Decke", "Fensterseiten", "Herd_Set")
FOV_HOCH = 66.0
SEITEN = 16 / 9
GEW = [(a, b, 1 - a - b) for a in (0.1, 0.3, 0.5, 0.7) for b in (0.1, 0.3, 0.5, 0.7) if a + b <= 0.9]


def _laden(glb):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=glb, disable_bone_shape=True)
    for o in list(bpy.data.objects):          # Zustaende (Leerstand, Geruest) sind im Spiel aus
        if o.type == "MESH" and (o.get("sichtbar") is False or o.name.startswith(("Leerstand", "Geruest"))):
            bpy.data.objects.remove(o)
    return bpy.context.evaluated_depsgraph_get(), bpy.context.scene


def _stiche(o, p):
    mw = o.matrix_world
    e = [mw @ o.data.vertices[o.data.loops[i].vertex_index].co for i in p.loop_indices]
    return [g0 * e[0] + g1 * e[k] + g2 * e[k + 1] for k in range(1, len(e) - 1) for g0, g1, g2 in GEW], e


def _melden(fehler, art, ort):
    fehler.setdefault(art, []).append(ort)


def _zeilen(fehler, geprueft, was):
    zeilen, anzahl = [f"   {geprueft} {was} geprueft"], 0
    for art, liste in sorted(fehler.items()):
        anzahl += len(liste)
        zeilen.append(f"   FEHLER {art}: {len(liste)} (" + "; ".join(liste[:5]) + (" ..." if len(liste) > 5 else "") + ")")
    return zeilen, anzahl


def rueckseite_offen(glb):
    """12b an einer Huelle. Rueckgabe (Zeilen, Fehlerzahl)."""
    dg, sz = _laden(glb)
    fehler, geprueft = {}, 0
    for o in bpy.data.objects:
        if o.type != "MESH" or len(o.data.uv_layers) < 2: continue          # nur Teile mit Verdeckung (Scheiben, Fahne: eigene Stoffe)
        n3 = o.matrix_world.to_3x3()
        for p in o.data.polygons:
            n = (n3 @ p.normal).normalized()
            if n.z > RUECK_NZ_MAX or p.area < RUECK_FLAECHE_MIN: continue
            geprueft += 1
            stiche, _e = _stiche(o, p)
            frei = sum(1 for q in stiche if not sz.ray_cast(dg, q - n * 1e-3, -n, distance=RUECK_WEITE)[0])
            if frei * 2 >= len(stiche):
                c = o.matrix_world @ p.center
                mat = o.data.materials[p.material_index].name.split(".")[0] if o.data.materials else "-"
                _melden(fehler, "Rueckseite offen (von oben nur die Rueckseite sichtbar)",
                        f"{o.name.split('.')[0]} ({c.x:.2f}, {c.y:.2f}, {c.z:.2f}) {mat} {p.area * 1e4:.0f} cm2")
    return _zeilen(fehler, geprueft, "Flaechen nach unten")


def _albedo(o, p, alb):
    import uv2_pruefung as U
    uv = o.data.uv_layers[0]
    u = [uv.data[i].uv for i in p.loop_indices]
    werte = [U._probe(alb, (g0 * u[0].x + g1 * u[k].x + g2 * u[k + 1].x) % 1.0, g0 * u[0].y + g1 * u[k].y + g2 * u[k + 1].y)
             for k in range(1, len(u) - 1) for g0, g1, g2 in GEW]
    return sum(werte) / len(werte)


def _verdeckung(o, p, ao, idx):
    """Gebackene Verdeckung (UV2, Atlas `ao`) an den Stichproben `idx` (Reihenfolge wie _stiche)."""
    import uv2_pruefung as U
    uv2 = o.data.uv_layers[1]
    w = [uv2.data[i].uv for i in p.loop_indices]
    alle = [(g0 * w[0].x + g1 * w[k].x + g2 * w[k + 1].x, g0 * w[0].y + g1 * w[k].y + g2 * w[k + 1].y)
            for k in range(1, len(w) - 1) for g0, g1, g2 in GEW]
    werte = [U._bilinear(ao, *alle[i]) for i in idx]
    return sum(werte) / max(len(werte), 1)


def licht_im_blick(glb, albedo_pfad, atlas_pfad=None):
    """12c an einem Innenraum. Mit `atlas_pfad` (AO-Atlas des Raums) auch: im Standardblick sichtbar und gebackene Verdeckung unter
    AO_BLICK_MIN (Pruefung 12 laesst solche Stellen gelten, wenn die Nachrechnung sie bestaetigt – enge Spalte zwischen Balken und Wand;
    sieht die Raumkamera sie, sind sie im Spiel schwarz: Klicktest 18.11.2026, Balkenseite 0,34 m vor der Rueckwand der Bibliothek).
    Rueckgabe (Zeilen, Fehlerzahl)."""
    import uv2_pruefung as U
    dg, sz = _laden(glb)
    alb = U._bild(albedo_pfad)
    ao = U._bild(atlas_pfad) if atlas_pfad else None
    an = {o.name.split(".")[0]: o.matrix_world.translation.copy() for o in bpy.data.objects if o.type == "EMPTY"}
    if "Blick_Auge" not in an or "Blick_Ziel" not in an:
        return ["   Raumkamera fehlt (Blick_Auge, Blick_Ziel) -> FEHLER"], 1
    auge, ziel = an["Blick_Auge"], an["Blick_Ziel"]
    # (Ort, wirft Schatten): das Grundlicht Licht_Raum hat im Spiel keinen Schatten (Stadtansicht.Neubau.cs, Wohnraeume.cs), die anderen schon
    lichter = [(v, k != "Licht_Raum") for k, v in an.items() if k.startswith("Licht_")]
    vor = (ziel - auge).normalized(); rechts = vor.cross(Vector((0, 0, 1))).normalized(); oben = rechts.cross(vor)
    th = math.tan(math.radians(FOV_HOCH / 2)); tb = th * SEITEN

    def im_bild(q):
        d = q - auge; t = d.dot(vor)
        return t > 0.05 and abs(d.dot(rechts) / t) <= tb and abs(d.dot(oben) / t) <= th

    def frei(a, b):
        d = b - a; l = d.length
        return not sz.ray_cast(dg, a, d / l, distance=max(l - 2e-3, 0.0))[0]
    fehler, geprueft = {}, 0
    for o in bpy.data.objects:
        if o.type != "MESH" or not o.data.uv_layers or o.name.split(".")[0] not in LICHT_OBJEKTE: continue
        n3 = o.matrix_world.to_3x3()
        for p in o.data.polygons:
            if p.area < LICHT_FLAECHE_MIN: continue
            n = (n3 @ p.normal).normalized()
            c = o.matrix_world @ p.center
            if n.dot(auge - c) <= 0 or n.z < LICHT_NZ_MIN: continue
            stiche, _e = _stiche(o, p)
            # sichtbar vom Blickpunkt aus, auch beim Umschauen (Stadtansicht.Neubau: Gier/Nick um den Standardblick), nicht nur im Bild
            idx = [i for i, q in enumerate(stiche) if (UMSCHAUEN or im_bild(q)) and frei(q + n * 1e-3, auge)]
            sichtbar = [stiche[i] for i in idx]
            if len(sichtbar) < 3 or _albedo(o, p, alb) < LICHT_ALBEDO_MIN: continue
            geprueft += 1
            if ao is not None and len(o.data.uv_layers) > 1:
                m = _verdeckung(o, p, ao, idx)
                if m < AO_BLICK_MIN:
                    _melden(fehler, "im Standardblick mit gebackener Verdeckung unter 0,12 (wirkt schwarz)",
                            f"{o.name.split('.')[0]} ({c.x:.2f}, {c.y:.2f}, {c.z:.2f}) n=({n.x:.1f}, {n.y:.1f}, {n.z:.1f}) {m:.2f}")
            dunkel = 0
            for q in sichtbar:
                s = q + n * 1e-3
                if not any((L - s).length <= LICHT_WEITE and n.dot((L - s).normalized()) >= LICHT_COS_MIN and (not schatten or frei(s, L))
                           for L, schatten in lichter):
                    dunkel += 1
            if dunkel * 2 >= len(sichtbar):
                _melden(fehler, "ohne Licht im Standardblick (nur Umgebungslicht, wirkt schwarz)",
                        f"{o.name.split('.')[0]} ({c.x:.2f}, {c.y:.2f}, {c.z:.2f}) n=({n.x:.1f}, {n.y:.1f}, {n.z:.1f}) {p.area * 1e4:.0f} cm2")
    return _zeilen(fehler, geprueft, f"helle Flaechen im Standardblick ({len(lichter)} Licht-Anker)")
