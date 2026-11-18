"""Grundformen 2 bis 4 des Buergerhauses (Stufe 2): was aussen am gemeinsamen Kern dazukommt (Vorlagen lvl2_v02..v04).

Wird von wohnhaus_mensch_s23.py benutzt (nach dem Kern gebaut, im Rahmen des Kerns, danach mit ihm um dx verschoben):
  * Grundform 2: Fachwerkfluegel rechts hinten (Stall bzw. Kammer, nicht begehbar) mit Satteldach, Giebel zur Gasse, Brettertuer.
  * Grundform 3: offener Holzschuppen rechts unter der Auskragung (Pultdach aus Ziegeln auf Pfosten, Rueckwand aus Brettern, Scheitholz).
  * Grundform 4: Erker am Flur des Obergeschosses (links) auf Knaggen, mit drei Butzenfenstern und eigenem Satteldach.
Alle drei sind Huelle; die Innenraeume (Wohnstube, Raum im Obergeschoss) sind in allen Grundformen gleich.
"""
from mathutils import Vector

import wohnhaus_mensch_s23_masse as M
from netzbau import ROT

EZ = Vector((0, 0, 1))
NEIG_FLUEGEL = 1.0          # 45 Grad


def _bretter_tuer(h, x0, x1, y, z0, z1, n):
    """Brettertuer auf der Wand (Blatt 4 cm vor), zwei Querriegel, Bander."""
    nv = Vector(n)
    for i in range(3):
        a = x0 + i * (x1 - x0) / 3
        h.rechteck("Fachwerk", (a, y + n[1] * 0.04, z0), (1, 0, 0), EZ, (x1 - x0) / 3, z1 - z0, "planken", laengs="v", zu=Vector((a, y, z0 + 1)) + nv * 3)
    for z in (z0 + 0.35, z1 - 0.35):
        h.brett("Fachwerk", (x0 + 0.04, y + n[1] * 0.04, z), (x1 - 0.04, y + n[1] * 0.04, z), 0.13, n, "balken_c", tiefe=0.03)
        h.rechteck("Bruchstein", (x0 + 0.02, y + n[1] * 0.075, z - 0.03), (1, 0, 0), EZ, 0.45, 0.06, "eisen", zu=Vector((x0, y, z)) + nv * 3)
    for x in (x0 - 0.07, x1 + 0.07):
        h.brett("Fachwerk", (x, y, z0), (x, y, z1 + 0.1), 0.13, n, "balken_c", laengs="v", tiefe=0.05)
    h.brett("Fachwerk", (x0 - 0.12, y, z1 + 0.06), (x1 + 0.12, y, z1 + 0.06), 0.14, n, "balken_c", tiefe=0.06)


def fluegel(h, box):
    """Grundform 2: Fachwerkfluegel mit Bruchsteinsockel, Satteldach (First entlang y), Giebel zur Gasse mit Brettertuer."""
    x0, x1, y0, y1, zw = box
    xm, hw = (x0 + x1) / 2, (x1 - x0) / 2
    zf = zw + hw * NEIG_FLUEGEL
    mitte = Vector((xm, (y0 + y1) / 2, 1.2))
    tuer = (xm - 0.45, xm + 0.45, M.Z_BODEN, M.Z_BODEN + 1.95)
    fenster = (y0 + 1.0, y0 + 1.6, 1.15, 1.75)       # kleines Fenster rechts (Laeden zu: Stall)
    for name, o, eu, laenge, loch in (("vorn", (x0, y0), (1, 0), x1 - x0, [(tuer[0] - x0, tuer[1] - x0, tuer[2], tuer[3])]),
                                      ("hinten", (x0, y1), (1, 0), x1 - x0, []),
                                      ("rechts", (x1, y0), (0, 1), y1 - y0, [(fenster[0] - y0, fenster[1] - y0, fenster[2], fenster[3])])):
        h.wand("Bruchstein", (o[0], o[1], 0), (eu[0], eu[1], 0), laenge, -0.12, M.Z_SOCKEL, "bruchstein",
               loecher=[(a, b, c, min(d, M.Z_SOCKEL)) for a, b, c, d in loch if c < M.Z_SOCKEL], zbasis=-0.12, hoehe=0.57, weg_von=mitte)
        h.wand("Lehmputz", (o[0], o[1], 0), (eu[0], eu[1], 0), laenge, M.Z_SOCKEL, zw, "putz", loecher=loch, zbasis=M.Z_SOCKEL, hoehe=zw - M.Z_SOCKEL,
               laibung=("Fachwerk", "balken_c"), laibung_tiefe=0.18, weg_von=mitte)
        n = (0, -1, 0) if name == "vorn" else (0, 1, 0) if name == "hinten" else (1, 0, 0)
        P = lambda s, z, o=o, eu=eu: Vector((o[0] + eu[0] * s, o[1] + eu[1] * s, z))
        # Schwelle und Rahm an den Ecken wie im Hauptbau: Giebelseiten (vorn, hinten) laufen um die Tiefe ueber die Ecke und bekommen
        # eine Stirn, die Traufseite endet buendig (sonst blieb an der Ecke ein offenes Balkenende – Durchblick, luecken_pruefung)
        e = 0.06 if name != "rechts" else 0.0
        for zm, band in ((M.Z_SOCKEL + 0.08, "balken_c"), (zw - 0.08, "balken_b")):
            # (am Hauptbau, s = 0, beginnen alle buendig: dort steht die Hauswand)
            h.brett("Fachwerk", P(0.0, zm), P(laenge + e, zm), 0.16, n, band, tiefe=0.06)
            if e:
                h.rechteck("Fachwerk", Vector(P(laenge + e, zm - 0.08)), n, (0, 0, 1), 0.06, 0.16, "balken_c", weg_von=Vector(P(laenge + e - 1, zm)))
        stiele = [0.08, laenge - 0.08] + [k * laenge / round(laenge / 1.1) for k in range(1, round(laenge / 1.1))]
        frei = [(a - 0.12, b + 0.12) for a, b, *_ in loch]
        for s in stiele:
            if any(a < s < b for a, b in frei): continue
            h.brett("Fachwerk", P(s, M.Z_SOCKEL + 0.16), P(s, zw - 0.16), 0.15, n, "balken_a", laengs="v")
        z_r = 1.40
        stuecke = [(0.08, laenge - 0.08)]
        for a, b in frei: stuecke = [(u, min(v, a)) for u, v in stuecke] + [(b, laenge - 0.08)]
        for u, v in stuecke:
            if v - u > 0.2: h.brett("Fachwerk", P(u, z_r), P(v, z_r), 0.12, n, "balken_b", tiefe=0.04)
        if name != "vorn" and not loch:
            h.brett("Fachwerk", P(0.12, M.Z_SOCKEL + 0.2), P(0.9, z_r - 0.06), 0.12, n, "balken_a", tiefe=0.04)
    _bretter_tuer(h, tuer[0], tuer[1], y0, tuer[2], tuer[3], (0, -1, 0))
    # Laeden vor dem Fenster rechts (geschlossen, Akzentfarbe)
    for a, b in ((fenster[0], (fenster[0] + fenster[1]) / 2), ((fenster[0] + fenster[1]) / 2, fenster[1])):
        h.rechteck("Fachwerk", (x1 + 0.04, a, fenster[2]), (0, 1, 0), EZ, b - a, fenster[3] - fenster[2], "farbholz", laengs="v", zu=(x1 + 3, a, 1.4), farbe=ROT)
    # Giebel vorn und hinten
    for y, s in ((y0, -1), (y1, 1)):
        h.welt("Lehmputz", [(x0, y, zw), (x1, y, zw), (xm, y, zf)], "putz", (x0, y, 0), (1, 0, 0), zw, zf - zw, weg_von=mitte)
        h.brett("Fachwerk", (xm, y, zw), (xm, y, zf - 0.1), 0.13, (0, s, 0), "balken_a", laengs="v")
        h.brett("Fachwerk", (x0 + 0.1, y, zw + 0.05), (xm - 0.05, y, zf - 0.15), 0.12, (0, s, 0), "balken_a", tiefe=0.04)
    return dict(x0=x0, x1=x1, y0=y0, y1=y1, zw=zw, zf=zf, xm=xm, hw=hw)


def fluegel_dach(d, f, ziegelflaeche):
    x0, x1, y0, y1, zw, zf, xm = f["x0"], f["x1"], f["y0"], f["y1"], f["zw"], f["zf"], f["xm"]
    ue = 0.22
    dick = 0.08
    yv, yh = y0 - 0.18, y1 + 0.05
    for seite, xe in ((-1, x0 + 0.02), (1, x1 + ue)):
        ze = zw - (abs(xe - xm) - (xm - x0)) * NEIG_FLUEGEL + dick
        unten = lambda y, xe=xe, ze=ze: Vector((xe, y, ze))
        oben = lambda y: Vector((xm, y, zf + dick))
        ziegelflaeche(d, "Dachziegel", unten, oben, (yv, yh), saat=7 + seite)
        d.vieleck("Fachwerk", [Vector((xe, yv, ze - dick)), Vector((xe, yh, ze - dick)), Vector((xm, yh, zf)), Vector((xm, yv, zf))],
                  [(0, 0.775), ((yh - yv) / 4, 0.775), ((yh - yv) / 4, 0.70), (0, 0.70)], zu=(xm, 0, 0))
        for y, aussen in ((yv, -1), (yh, 1)):
            vor = aussen * (0.01 + 0.005 * (seite > 0))           # rechts 5 mm weiter vorn: am First nicht deckungsgleich
            d.brett("Fachwerk", Vector((xe, y + vor, ze - 0.04)), Vector((xm, y + vor, zf - 0.04)), 0.18, (0, aussen, 0), "balken_c", tiefe=0.04)
    d.vieleck("Firstziegel", [Vector((xm - 0.12, yv, zf + 0.12)), Vector((xm + 0.12, yv, zf + 0.12)), Vector((xm + 0.12, yh, zf + 0.12)), Vector((xm - 0.12, yh, zf + 0.12))],
              [(0, 0.18), (0.06, 0.18), (0.06, 0.15), (0, 0.15)], zu=(xm, 0, 99))


def schuppen(h, d, box, ziegelflaeche):
    """Grundform 3: offener Holzschuppen unter der Auskragung: Pfosten, Rueckwand aus Brettern, Pultdach aus Ziegeln, Scheitholz."""
    x0, x1, y0, y1, z_hoch, z_tief = box
    z_dach = lambda x: z_hoch + (z_tief - z_hoch) * (x - x0) / (x1 - x0)
    for x in (x1 - 0.08,):
        for y in (y0 + 0.08, (y0 + y1) / 2, y1 - 0.08):
            h.kasten("Fachwerk", (x - 0.07, y - 0.07, 0.0), (x + 0.07, y + 0.07, z_dach(x) - 0.12), "balken_a", ohne=("-z",), laengs="z")
            h.kasten("Bruchstein", (x - 0.13, y - 0.13, -0.05), (x + 0.13, y + 0.13, 0.12), "bruchstein", ohne=("-z",), laengs="x")
    h.brett("Fachwerk", (x1 - 0.08, y0 - 0.1, z_dach(x1) - 0.12), (x1 - 0.08, y1 + 0.1, z_dach(x1) - 0.12), 0.16, (1, 0, 0), "balken_b", tiefe=0.08)   # 1 cm vor den Pfosten (nicht deckungsgleich)
    # Pfette an der Hauswand und Sparren
    h.brett("Fachwerk", (x0 + 0.02, y0 - 0.1, z_hoch - 0.12), (x0 + 0.02, y1 + 0.1, z_hoch - 0.12), 0.14, (1, 0, 0), "balken_c", tiefe=0.08)
    for k in range(5):
        y = y0 + 0.05 + k * (y1 - y0 - 0.1) / 4
        h.stab("Fachwerk", (x0, y, z_hoch - 0.05), (x1 + 0.15, y, z_dach(x1 + 0.15) - 0.05), 0.09, "balken_c", breite=0.07)
    # Rueckwand aus Brettern (stehend)
    import wohnhaus_mensch_s1 as s1
    s1.bretterwand(h, "Fachwerk", (x0, y1, 0.0), (x1, y1, 0.0), 0.0, lambda p: z_dach(p.x) - 0.1, "planken", weg_von=Vector(((x0 + x1) / 2, y1 - 1, 1)))
    # Scheitholz in drei Lagen entlang der Rueckwand
    for i, (dy, z) in enumerate([(0.19 * k, 0.09) for k in range(8)] + [(0.09 + 0.19 * k, 0.26) for k in range(7)] + [(0.19 + 0.19 * k, 0.43) for k in range(5)]):
        y = y1 - 0.2 - dy
        a, b = Vector((x0 + 0.05, y, z)), Vector((x0 + 0.65, y + 0.02 * (i % 3 - 1), z))
        h.stab("Fachwerk", a, b, 0.17, "balken_a", kappen=False)
        h.rechteck("Fachwerk", b - Vector((0, 0.085, 0.085)), (0, 1, 0), EZ, 0.17, 0.17, "farbholz", zu=b + Vector((3, 0, 0)), anteil=(0.1, 0.9), u_bereich=(0.1 + 0.07 * (i % 9), 0.14 + 0.07 * (i % 9)))
    # Pultdach aus Ziegeln
    dick = 0.06
    xe = x1 + 0.25
    unten = lambda y: Vector((xe, y, z_dach(xe) + dick))
    oben = lambda y: Vector((x0, y, z_hoch + dick))
    # Lagen von einem Punkt unter dem Schuppen weg (Pruefung 12b, 18.11.2026): mit der Hausmitte (z 3,0, ueber dem Pultdach) zeigten
    # sie nach unten, von oben sah man nur ihre Rueckseiten (im Spiel durchsichtig)
    ziegelflaeche(d, "Dachziegel", unten, oben, (y0 - 0.2, y1 + 0.2), saat=9, weg_von=((x0 + x1) / 2, (y0 + y1) / 2, 0.0))
    d.vieleck("Fachwerk", [Vector((xe, y0 - 0.2, z_dach(xe))), Vector((xe, y1 + 0.2, z_dach(xe))), Vector((x0, y1 + 0.2, z_hoch)), Vector((x0, y0 - 0.2, z_hoch))],
              [(0, 0.775), (1, 0.775), (1, 0.70), (0, 0.70)], zu=(x0, 0, 0))
    for y, aussen in ((y0 - 0.2, -1), (y1 + 0.2, 1)):
        d.brett("Fachwerk", Vector((xe, y, z_dach(xe) - 0.02)), Vector((x0, y, z_hoch - 0.02)), 0.14, (0, aussen, 0), "balken_c", tiefe=0.03)


def erker(h, d, box, s, ziegelflaeche):
    """Grundform 4: Erker am Flur (linke Wand des Obergeschosses) auf zwei Knaggen: Fachwerk mit Putz, Butzenfenster vorn und an den
    Seiten, Satteldach mit dem First quer zur Wand. `s` ist das Teil der Scheiben."""
    y0, y1, z0, z1, t = box
    xw = -M.OX
    xa = xw - t
    mitte = Vector((xw + 0.3, (y0 + y1) / 2, (z0 + z1) / 2))
    # Konstruktionsregel (wohnhaus_mensch_s23_masse.py): jedes Fenster fuellt sein Gefach – vorn zwischen den Eckstielen, an den
    # Seiten zwischen Eckstiel und Wandstiel, unten Brustriegel, oben Sturzriegel auf der Hoehe der Fenster des Obergeschosses
    b, r = M.BW / 2, M.RIEGEL
    zbr, zst = M.OG_FENSTER_Z
    fv = (y0 + b, y1 - b, zbr, zst)                             # Fenster vorn
    fs = (xa + b, xw - b, zbr, zst)                             # Fenster der Seiten (x)
    h.wand("Lehmputz", (xa, y0, 0), (0, 1, 0), y1 - y0, z0, z1, "putz", loecher=[(fv[0] - y0, fv[1] - y0, fv[2], fv[3])], zbasis=z0, hoehe=z1 - z0,
           laibung=("Fachwerk", "balken_c"), laibung_tiefe=0.12, weg_von=mitte)
    for y in (y0, y1):
        h.wand("Lehmputz", (xa, y, 0), (1, 0, 0), t, z0, z1, "putz", loecher=[(fs[0] - xa, fs[1] - xa, fs[2], fs[3])], zbasis=z0, hoehe=z1 - z0,
               laibung=("Fachwerk", "balken_c"), laibung_tiefe=0.12, weg_von=mitte)
    h.rechteck("Fachwerk", (xa, y0, z0), (0, 1, 0), (1, 0, 0), y1 - y0, t, "balken_c", zu=(xa, 0, z0 - 5))    # Boden
    # Holz: Eckstiele und Wandstiele, Schwelle, Rahm, Brust- und Sturzriegel rundum (buendig an den Fenstern)
    # Stiele und Riegel liegen ganz vor der Putzflaeche (Eckstiel als Winkel aus zwei Bohlen, Wandstiel und Riegel als Bohle): so liegt
    # kein Holz in der Ebene der Laibungen (deckungsgleich, backt schwarz) und die Hoelzer bleiben buendig an den Fenstern
    for y, sy in ((y0, 1), (y1, -1)):
        ya, yb = y - sy * 0.02, y + sy * b
        h.kasten("Fachwerk", (xa - 0.02, min(ya, yb), z0), (xa, max(ya, yb), z1), "balken_a", laengs="z")                # Eckstiel vorn
        h.kasten("Fachwerk", (xa, min(ya, y), z0), (xa + b, max(ya, y), z1), "balken_a", laengs="z")                    # Eckstiel Seite
        h.kasten("Fachwerk", (xw - b, min(ya, y), z0), (xw, max(ya, y), z1), "balken_a", laengs="z")                    # Wandstiel
    for z in (z0 + 0.06, z1 - 0.06):
        h.kasten("Fachwerk", (xa - 0.03, y0 - 0.03, z - 0.07), (xw, y1 + 0.03, z + 0.07), "balken_b", laengs="y")
    for z in (zbr - r / 2, zst + r / 2):
        h.kasten("Fachwerk", (xa - 0.02, y0 + b, z - r / 2), (xa, y1 - b, z + r / 2), "balken_b", laengs="y")
        for y, sy in ((y0, -1), (y1, 1)):
            h.kasten("Fachwerk", (xa + b, min(y, y + sy * 0.04), z - r / 2), (xw - b, max(y, y + sy * 0.04), z + r / 2), "balken_b", laengs="x")
    # Knaggen unter dem Erker
    for y in (y0 + 0.12, y1 - 0.12):
        a, b, c = Vector((xw, y, z0 - 0.75)), Vector((xw, y, z0)), Vector((xa + 0.05, y, z0))
        h.stab("Fachwerk", a, c + Vector((0, 0, -0.04)), 0.11, "balken_c")
        h.stab("Fachwerk", b + Vector((0, 0, -0.06)), c + Vector((0, 0, -0.06)), 0.12, "balken_c")
    # Scheiben: vorn und an beiden Seiten
    for stoff, r in (("Fenster", 0.0), ("Bleinetz", 0.006)):
        x = xa + 0.03 - r                                          # Glas in der Wand (3 cm hinter der Aussenflaeche)
        b, hh = fv[1] - fv[0], fv[3] - fv[2]
        s.vieleck(stoff, [(x, fv[0], fv[2]), (x, fv[1], fv[2]), (x, fv[1], fv[3]), (x, fv[0], fv[3])], [(0, 1), (b, 1), (b, 1 - hh), (0, 1 - hh)], zu=(x - 3, (y0 + y1) / 2, fv[2]))
        for y, sy in ((y0 + 0.03 - r, -1), (y1 - 0.03 + r, 1)):
            b = fs[1] - fs[0]
            s.vieleck(stoff, [(fs[0], y, fs[2]), (fs[1], y, fs[2]), (fs[1], y, fs[3]), (fs[0], y, fs[3])], [(0, 1), (b, 1), (b, 1 - hh), (0, 1 - hh)], zu=(fs[0], y + sy * 3, fs[2]))
    # Satteldach: First quer zur Wand (entlang x), Giebel nach aussen
    ym, hw = (y0 + y1) / 2, (y1 - y0) / 2 + 0.12
    zf = z1 + hw * 1.1
    for seite in (-1, 1):
        ye = ym + seite * hw
        unten = lambda x, ye=ye: Vector((x, ye, z1 + 0.02))
        oben = lambda x: Vector((x, ym, zf))
        ziegelflaeche(d, "Dachziegel", unten, oben, (xa - 0.15, xw), saat=11 + seite, lage=0.22)
    h.welt("Lehmputz", [(xa, y0, z1), (xa, y1, z1), (xa, ym, zf - 0.05)], "putz", (xa, y0, 0), (0, 1, 0), z1, zf - z1, weg_von=mitte)
    d.brett("Fachwerk", (xa - 0.16, y0 - 0.12, z1 + 0.0), (xa - 0.16, ym, zf - 0.02), 0.12, (-1, 0, 0), "balken_c", tiefe=0.03)
    d.brett("Fachwerk", (xa - 0.165, y1 + 0.12, z1 + 0.0), (xa - 0.165, ym, zf - 0.02), 0.12, (-1, 0, 0), "balken_c", tiefe=0.03)   # 5 mm versetzt: am First nicht deckungsgleich
