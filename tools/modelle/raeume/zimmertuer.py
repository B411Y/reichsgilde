"""Zimmertuer mit Klinke: Blatt aus senkrechten Brettern mit Querriegeln, Langbaender, Klinke mit Schild, Bekleidung um die Oeffnung.

Wird von der Wohnstube (tools/modelle/neubau/wohnhaus_mensch_s1_g1.py, Trim-Sheet innen_trim) und von den Raeumen
(raum_hinterzimmer.py, raum_bibliothek.py, Trim-Sheet raeume_innen_trim) benutzt; deshalb nimmt sie Baender und Eisenfeld als
Parameter. Die Tuer sitzt auf der Wandflaeche (sie geht im Spiel nicht auf: der Raumwechsel laeuft mit Klinke und Schwarzblende).
"""
import math

from mathutils import Vector

EZ = Vector((0, 0, 1))


def zimmertuer(teil, stoff, holz, rahmen, eisen_band, eisen_ub, wand_x, y0, y1, z0, hoehe, innen_richtung, klinke_rechts=True):
    """Tuer in einer Wand quer zur X-Achse (Wandflaeche bei x = wand_x, der Raum liegt in Richtung `innen_richtung` = +1 oder -1).
    y0..y1 ist die lichte Breite, z0 der Fussboden, `hoehe` die lichte Hoehe. Gibt den Mittelpunkt der Tuer zurueck (fuer den Anker)."""
    n = Vector((innen_richtung, 0, 0))                       # Sichtseite (in den Raum)
    eu = Vector((0, 1, 0))
    breite = y1 - y0
    ym = (y0 + y1) / 2
    x = wand_x + innen_richtung * 0.012                      # das Blatt liegt 12 mm vor der Wand (Anschlag auf der Bekleidung)
    v0, v1, mm = teil.baender[holz]
    # Blatt: fuenf Bretter, Maserung senkrecht
    bretter = 5
    b = breite / bretter
    for i in range(bretter):
        s0, s1 = y0 + i * b, y0 + (i + 1) * b
        u = (i * 0.23) % 1
        pts = [Vector((x, s0, z0)), Vector((x, s1, z0)), Vector((x, s1, z0 + hoehe)), Vector((x, s0, z0 + hoehe))]
        teil.vieleck(stoff, pts, [(u, v1), (u, v0), (u + hoehe / mm, v0), (u + hoehe / mm, v1)], zu=Vector((x, ym, z0 + 1)) + n)
    # zwei Querriegel und eine Strebe aufgedoppelt
    for z in (z0 + 0.32, z0 + hoehe - 0.32):
        teil.brett(stoff, (x, y0 + 0.04, z), (x, y1 - 0.04, z), 0.13, n, holz, tiefe=0.03)
    unten, oben = (y1 - 0.12, z0 + 0.40), (y0 + 0.12, z0 + hoehe - 0.40)
    if not klinke_rechts: unten, oben = (y0 + 0.12, z0 + 0.40), (y1 - 0.12, z0 + hoehe - 0.40)
    teil.brett(stoff, (x, unten[0], unten[1]), (x, oben[0], oben[1]), 0.11, n, holz, tiefe=0.026)
    # Bekleidung (Rahmen) um die Oeffnung, auf der Wand
    xr = wand_x
    for yy in (y0 - 0.035, y1 + 0.035):
        teil.brett(stoff, (xr, yy, z0), (xr, yy, z0 + hoehe + 0.07), 0.07, n, rahmen, tiefe=0.03)
    teil.brett(stoff, (xr, y0 - 0.07, z0 + hoehe + 0.035), (xr, y1 + 0.07, z0 + hoehe + 0.035), 0.07, n, rahmen, tiefe=0.034)
    # Eisen: zwei Langbaender auf der Bandseite, Klinke mit Schild auf der Schlossseite
    k0, k1, _ = teil.baender[eisen_band]
    eis = [(eisen_ub[0], k1), (eisen_ub[1], k1), (eisen_ub[1], k0), (eisen_ub[0], k0)]
    band_seite, schloss = (y0, y1) if klinke_rechts else (y1, y0)
    rich = 1 if band_seite == y0 else -1
    xe = x + innen_richtung * 0.034
    for z in (z0 + 0.32, z0 + hoehe - 0.32):
        pts = [Vector((xe, band_seite, z - 0.025)), Vector((xe, band_seite + rich * 0.52, z - 0.018)), Vector((xe, band_seite + rich * 0.52, z + 0.018)), Vector((xe, band_seite, z + 0.025))]
        teil.vieleck(stoff, pts, eis, zu=Vector((xe, ym, z)) + n)
    ys = schloss - rich * 0.085
    zk = z0 + 1.02
    xs = x + innen_richtung * 0.004
    teil.vieleck(stoff, [Vector((xs, ys - 0.035, zk - 0.11)), Vector((xs, ys + 0.035, zk - 0.11)), Vector((xs, ys + 0.035, zk + 0.07)), Vector((xs, ys - 0.035, zk + 0.07))], eis, zu=Vector((xs, ys, zk)) + n)  # Schild
    griff = Vector((x + innen_richtung * 0.055, ys, zk))
    teil.stab(stoff, Vector((xs, ys, zk)), griff, 0.022, eisen_band, u_bereich=eisen_ub)                                   # Hals der Klinke
    teil.stab(stoff, griff, griff + Vector((0, -rich * 0.12, -0.012)), 0.022, eisen_band, u_bereich=eisen_ub, kappen=True)   # Druecker
    return Vector((x, ym, z0 + hoehe / 2))
