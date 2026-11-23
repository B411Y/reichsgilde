"""Karren ohne Zugtier und ohne Figur: Handkarren, Ochsenkarren, Pferdekarren (gemalt-realistisch, Material-Bibliothek).

    blender -b --python tools/modelle/karren.py [-- --ohne-bericht]
    python tools/modelle/karren.py

Schreibt game/assets/modelle/karren/karren_hand.glb, karren_ochse.glb und karren_pferd.glb (die alten Dateien in fahrzeuge/ bleiben,
bis der Spiel-Code umgestellt ist; der alte Bau steht in karren_alt.py). Zugtiere und die Menschen am Karren sind eigene Modelle,
das Spiel hängt sie an die Anker.

Maßstab: MPFB-Figur (Mann 1,64 m, Hüfte 0,93 m). Blender: Z oben, Fahrtrichtung −Y (im Spiel Y oben, Fahrtrichtung +Z). Ursprung auf dem Boden
unter der (hinteren) Radachse, Mitte des Wagens. „Links“ und „rechts“ in Fahrtrichtung: links = +X (Blender und Spiel).

Knoten je Datei (alle auf oberster Ebene):
    Karren      Wagenkasten aus Bohlen, Pfosten, Eisenbändern, Achsbalken, Langhölzern, Deichsel bzw. Holme (Holz, Holz alt, Eisen)
    Rad_L, Rad_R (Handkarren, Ochsenkarren) bzw. Rad_VL, Rad_VR, Rad_HL, Rad_HR (Pferdekarren, vorn und hinten)
                Räder als eigene Teile: der Ursprung des Knotens ist die Achse, gedreht wird um die lokale X-Achse (Querachse). Felgen,
                Speichen, Nabe und Eisenreifen sind rundum geschlossen (auch die Unterseite), weil sie sich drehen. Extras: „radius“ (m),
                „drehachse“ = "X". Rollweg je Umdrehung 2π · radius.
    Ladung1–3   Kisten, Säcke, Fässer (Handkarren 1, Ochsenkarren 2, Pferdekarren 3), Zustand: standardmäßig aus
    Plane       Plane über der Ladung in der Hausfarbe (Material „Hausfarbe“, eigener Knoten), Zustand: standardmäßig aus
    Wimpel      Wimpel an einem Pfosten in der Hausfarbe (Ochsen-, Pferdekarren), sichtbar
    Decke       Sitzdecke in der Hausfarbe auf dem Bock (nur Pferdekarren), sichtbar
    Waage       Waagscheit am Ende der Deichsel (nur Pferdekarren), sichtbar
    Stuetze     Stützbock bzw. Stützbeine zum Abstellen (alle), Zustand: standardmäßig aus – zeigen, wenn der Karren steht
Hausfarbe nur an Plane, Wimpel und Decke. Kein Fahrer, kein Tier im Modell.

Anker (leere Knoten „Anker_…“, Ursprung am Boden bzw. auf der Sitzfläche; alle Blickrichtung Fahrtrichtung; Einbau in Stadtansicht.Karren.cs):
    Anker_Ziehen             Handkarren: Boden 0,285 m vor der Zugstange, dort steht der Zieher (Blick in Fahrtrichtung, Hände hinter dem Körper)
    Anker_Zug_L, _R          Handkarren: Zugstange links und rechts (0,22 m aus der Mitte, 0,86 m hoch), dort greifen die Hände
    Anker_Deichsel           Ochsen- und Pferdekarren: Mitte des Zuggeschirrs (Jochmitte bzw. Mitte zwischen den beiden Pferden) in
                             Halshöhe; das Spiel setzt den Anker „Geschirr“ des Zugtiers dorthin
    Anker_Pferd_L, _R        Pferdekarren: Geschirr des linken und rechten Pferds (Abstand ±0,62 m)
    Anker_Fahrer_Seite       Ochsenkarren: Boden links neben dem Ochsen, auf Höhe der Schulter des Tiers, dort geht der Läufer
    Anker_Bock               Pferdekarren: Oberkante der Sitzfläche (die Hüfte der sitzenden Figur kommt dorthin; Sitzhöhe 0,446 m über
                             dem Fußbrett, wie der Clip „Sitzen“ der Passanten)
"""

import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bausteine as bs  # noqa: E402
from bausteine import Matrix, Vector  # noqa: E402

bs.FARBEN.update({
    "Sack": (0.70, 0.62, 0.46),
    "Leder dunkel": (0.17, 0.10, 0.06),    # Zugstränge und Riemen der neuen Karren: sollen neben dem Holz als Leder lesbar sein
})

ARTEN = ("hand", "ochse", "pferd", "pferd_gross")
ORDNER = "karren"


# ─────────────────────────────────────────────────────────────────────────────
#  Grundteile
# ─────────────────────────────────────────────────────────────────────────────

def rohr(n, st, a, b, r0, r1, stoff, seiten=8, kappen=(True, True)):
    """Rundholz als n-eckiger Kegelstumpf von a (Halbmesser r0) nach b (r1)."""
    a, b = Vector(a), Vector(b)
    achse = (b - a).normalized()
    hilf = Vector((1, 0, 0)) if abs(achse.x) < 0.9 else Vector((0, 1, 0))
    u = achse.cross(hilf).normalized()
    v = achse.cross(u)

    def ring(p, r):
        return [p + (u * math.cos(2 * math.pi * k / seiten + math.pi / seiten) + v * math.sin(2 * math.pi * k / seiten + math.pi / seiten)) * r
                for k in range(seiten)]
    ra, rb = ring(a, r0), ring(b, r1)
    for k in range(seiten):
        j = (k + 1) % seiten
        q = [ra[k], ra[j], rb[j], rb[k]]
        n.vieleck_nach(q, sum(q, Vector()) / 4 - (a + b) / 2, st[stoff])
    if kappen[0]:
        n.vieleck_nach(ra, -achse, st[stoff])
    if kappen[1]:
        n.vieleck_nach(rb, achse, st[stoff])


def balken(n, st, a, b, breite, dicke, stoff, quer=(1, 0, 0), ohne=()):
    """Vierkantholz von a nach b; `dicke` liegt entlang `quer`."""
    n.stab(a, b, breite, dicke, st[stoff], quer=quer, ohne=ohne)


# ─────────────────────────────────────────────────────────────────────────────
#  Rad
# ─────────────────────────────────────────────────────────────────────────────

def rad_netz(st, name, r, breite, nr=0, segmente=16, speichen=8):
    """Speichenrad um die X-Achse, Ursprung = Achse. Felge aus Segmenten (Holz, einzelne Segmente „Holz alt“), Eisenreifen (breiter als die
    Felge), Nabe mit Eisenringen, Speichen. Rundum geschlossen (alle=True), die Unterseite sieht die Kamera nach einer Drehung."""
    n = bs.Netz(name, alle=True)
    eisen = 0.03
    ro = r - eisen
    ri = ro - (0.085 if r > 0.55 else 0.07)
    hb = breite / 2
    tb = hb + 0.008
    nabe, nabe_l = (0.085 if r > 0.55 else 0.07), breite + 0.1

    def p(rad, w, x):
        return Vector((x, rad * math.cos(w), rad * math.sin(w)))
    for k in range(segmente):
        w0, w1 = 2 * math.pi * k / segmente, 2 * math.pi * (k + 1) / segmente
        radial = Vector((0, math.cos((w0 + w1) / 2), math.sin((w0 + w1) / 2)))
        holz = st["Holz alt"] if (k + nr) % 3 == 0 else st["Holz"]
        n.vieleck_nach([p(ri, w0, -hb), p(ri, w1, -hb), p(ri, w1, hb), p(ri, w0, hb)], -radial, holz)
        for s in (-1, 1):
            n.vieleck_nach([p(ri, w0, s * hb), p(ri, w1, s * hb), p(ro, w1, s * hb), p(ro, w0, s * hb)], Vector((s, 0, 0)), holz)
        n.vieleck_nach([p(r, w0, -tb), p(r, w1, -tb), p(r, w1, tb), p(r, w0, tb)], radial, st["Eisen"])
        for s in (-1, 1):
            n.vieleck_nach([p(ro, w0, s * tb), p(ro, w1, s * tb), p(r, w1, s * tb), p(r, w0, s * tb)], Vector((s, 0, 0)), st["Eisen"])
    for k in range(speichen):
        w = 2 * math.pi * (k + 0.5 * (nr % 2)) / speichen
        n.stab(p(nabe * 0.8, w, 0), p(ri + 0.012, w, 0), 0.05, 0.042, st["Holz"], quer=(1, 0, 0), ohne=("+x", "-x"))
    rohr(n, st, (-nabe_l / 2 + 0.004, 0, 0), (nabe_l / 2 - 0.004, 0, 0), nabe, nabe, "Holz", seiten=8)
    for s in (-1, 1):
        rohr(n, st, (s * nabe_l / 2, 0, 0), (s * (nabe_l / 2 - 0.035), 0, 0), nabe + 0.012, nabe + 0.012, "Eisen", seiten=8, kappen=(True, False))
    return n


def rad_objekt(st, name, r, breite, ort, nr=0):
    o = rad_netz(st, name, r, breite, nr).objekt()
    o.location = Vector(ort)
    o["radius"] = r
    o["drehachse"] = "X"
    return o


# ─────────────────────────────────────────────────────────────────────────────
#  Wagenkasten, Ladung, Plane
# ─────────────────────────────────────────────────────────────────────────────

def kasten(n, st, halb, y0, y1, zf, hw, bretter, d=0.045, pfosten=True):
    """Wagenkasten: Boden (Oberkante zf), Seitenwände aus `bretter` übereinanderliegenden Bohlen (jede zweite 6 mm weiter außen, damit sich
    die Kanten abzeichnen), Stirnwände, Eckpfosten und mittlere Pfosten, senkrechte Eisenbänder."""
    ym = (y0 + y1) / 2
    n.quader((0, ym, zf - 0.03), (2 * (halb + d), y1 - y0, 0.06), st["Holz alt"])
    hb = hw / bretter
    for sx in (-1, 1):
        for i in range(bretter):
            versatz = 0.006 * (i % 2)
            n.quader((sx * (halb + d / 2 + versatz), ym, zf + hb * i + hb / 2), (d, y1 - y0, hb - 0.004),
                     st["Bretter"] if i % 2 else st["Holz alt"])
    for sy, y in ((-1, y0), (1, y1)):
        for i in range(bretter):
            versatz = 0.006 * (i % 2)
            n.quader((0, y + sy * (d / 2 + versatz), zf + hb * i + hb / 2), (2 * (halb + d), d, hb - 0.004),
                     st["Holz alt"] if i % 2 else st["Bretter"])
    if pfosten:
        pz = (zf - 0.04, zf + hw + 0.06)
        ph = pz[1] - pz[0]
        mitten = [y0, y1] + ([ym] if (y1 - y0) > 2.0 else [])
        for sx in (-1, 1):
            for y in mitten:
                yy = y + (-d if y == y0 else d if y == y1 else 0)
                n.quader((sx * (halb + d + 0.012), yy, pz[0] + ph / 2), (0.07, 0.07, ph), st["Holz"])
        for sx in (-1, 1):                                          # senkrechte Eisenbänder an den Seitenwänden
            for y in (y0 + (y1 - y0) * 0.28, y0 + (y1 - y0) * 0.72):
                n.quader((sx * (halb + d + 0.012), y, zf + hw / 2 - 0.02), (0.012, 0.06, hw + 0.02), st["Eisen"])
        for sx in (-1, 1):                                          # Eisenwinkel an den vorderen Ecken
            n.quader((sx * (halb + d + 0.047), y0 - d, zf + hw + 0.05), (0.012, 0.14, 0.03), st["Eisen"])


def fass(n, st, fuss, r, h):
    """Fass: bauchiger Achtkant aus Holz (drei Abschnitte) mit drei Eisenreifen, Deckel oben."""
    x, y, z = fuss
    stufen = ((0.0, 0.92), (0.25, 0.99), (0.5, 1.0), (0.75, 0.99), (1.0, 0.92))
    for (t0, f0), (t1, f1) in zip(stufen, stufen[1:]):
        rohr(n, st, (x, y, z + h * t0), (x, y, z + h * t1), r * f0, r * f1, "Holz", seiten=8, kappen=(False, t1 == 1.0))
    for t in (0.14, 0.5, 0.86):
        f = 0.92 + 0.08 * (1 - abs(t - 0.5) * 2) ** 0.5 if t != 0.5 else 1.0
        rohr(n, st, (x, y, z + h * t - 0.025), (x, y, z + h * t + 0.025), r * f + 0.012, r * f + 0.012, "Eisen", seiten=8, kappen=(False, False))


def ladung(art, i, st, maße):
    """Ladung einer Sorte: ein Abschnitt des Wagenbetts. Sorte 0 Kisten, 1 Säcke, 2 Fässer."""
    halb, y0, y1, zf, hw = maße
    abschnitt = (y1 - y0) / ARTEN_SORTEN[art]
    ya, yb = y0 + abschnitt * i + 0.1, y0 + abschnitt * (i + 1) - 0.1
    ym, br = (ya + yb) / 2, halb - 0.06
    n = bs.Netz(f"Ladung{i + 1}")
    z0 = zf + 0.01
    if i % 3 == 0:                                                  # Kisten, zwei unten, eine oben
        tiefe = (yb - ya) * 0.8
        for k, x in enumerate((-br / 2, br / 2)):
            n.quader((x, ym, z0 + 0.2), (br * 0.92, tiefe, 0.4), st["Holz"])
            n.quader((x, ym - tiefe * 0.3, z0 + 0.2), (br * 0.93, 0.05, 0.41), st["Eisen"])
            n.quader((x, ym + tiefe * 0.3, z0 + 0.2), (br * 0.93, 0.05, 0.41), st["Eisen"])
        if art != "hand":
            n.quader((0, ym, z0 + 0.4 + 0.15), (br * 0.8, tiefe * 0.8, 0.3), st["Holz alt"])
            n.quader((0, ym - tiefe * 0.25, z0 + 0.4 + 0.15), (br * 0.81, 0.04, 0.31), st["Eisen"])
    elif i % 3 == 1:                                                # Säcke
        for k in range(4):
            x = (-0.5 + (k % 2)) * br * 0.95
            y = ya + (yb - ya) * (0.3 + 0.4 * (k // 2))
            for a, b, r0, r1 in (((x, y, z0), (x, y, z0 + 0.2), 0.17, 0.24), ((x, y, z0 + 0.2), (x, y, z0 + 0.4), 0.24, 0.22),
                                 ((x, y, z0 + 0.4), (x, y, z0 + 0.52), 0.22, 0.09)):
                rohr(n, st, a, b, r0, r1, "Sack", seiten=7, kappen=(False, a[2] == z0 + 0.4))
        if art == "pferd":
            for x in (-br * 0.25, br * 0.25):
                rohr(n, st, (x, ym - 0.2, z0 + 0.5), (x, ym + 0.2, z0 + 0.5), 0.2, 0.2, "Sack", seiten=7)
    else:                                                           # Fässer
        for k in range(2):
            fass(n, st, ((-0.5 + k) * br * 0.95, ym, z0), min(0.3, br * 0.45), 0.72)
    o = n.objekt()
    return bs.zustand(o)


def plane(art, st, maße, hoch, verlaengerung=0.04):
    """Plane (Hausfarbe) über der ganzen Ladung: Gewölbe mit sieben Seiten, rundum zu, nur die Unterseite fehlt."""
    halb, y0, y1, zf, hw = maße
    n = bs.Netz("Plane")
    bw = halb + 0.075
    z0 = zf + hw - 0.04
    zr = zf + hoch
    h = zr - z0
    profil = [(-bw, z0), (bw, z0), (bw * 0.94, z0 + h * 0.28), (bw * 0.66, z0 + h * 0.74), (0, zr),
              (-bw * 0.66, z0 + h * 0.74), (-bw * 0.94, z0 + h * 0.28)]
    m = Matrix.Rotation(math.radians(90), 4, "Z")
    n.prisma(profil, y0 - verlaengerung, y1 + verlaengerung, st["Hausfarbe"], matrix=m)
    return bs.zustand(n.objekt())


def wimpel(st, fuss, hoehe):
    """Pfosten am Wagenkasten und Wimpel in der Hausfarbe; der Pfosten gehört zum Wagenkasten (Aufruf in karren_netz), das Tuch ist der Knoten."""
    n = bs.Netz("Wimpel")
    f = Vector(fuss)
    spitze = f + Vector((0, 0, hoehe))
    a, b, c = spitze, spitze - Vector((0, 0, 0.24)), spitze + Vector((0, 0.62, -0.1))
    for s in (1, -1):                                         # zwei Lagen im Abstand von 6 mm: Vorder- und Rückseite flimmern nicht
        v = Vector((s * 0.003, 0, 0))
        n.vieleck_nach([a + v, b + v, c + v], Vector((s, 0, 0)), st["Hausfarbe"])
    return n.objekt()


def stange(n, st, fuss, hoehe, stoff="Holz", r=0.022):
    rohr(n, st, fuss, (fuss[0], fuss[1], fuss[2] + hoehe), r, r * 0.8, stoff, seiten=6)
    n.quader((fuss[0], fuss[1], fuss[2] + hoehe + 0.012), (0.05, 0.05, 0.024), st["Eisen"])


# ─────────────────────────────────────────────────────────────────────────────
#  Die drei Karren
# ─────────────────────────────────────────────────────────────────────────────

ARTEN_SORTEN = {"hand": 1, "ochse": 2, "pferd": 2, "pferd_gross": 3}


def hand(st):
    zf, halb, y0, y1, hw = 0.64, 0.46, -0.8, 0.8, 0.30
    maße = (halb, y0, y1, zf, hw)
    r, spur = 0.45, 0.62
    n = bs.Netz("Karren")
    kasten(n, st, halb, y0, y1, zf, hw, 2)
    # Achsbalken und Achsnägel
    balken(n, st, (-spur - 0.09, 0, r), (spur + 0.09, 0, r), 0.08, 0.08, "Holz", quer=(0, 1, 0))
    for s in (-1, 1):
        n.quader((s * (spur + 0.115), 0, r), (0.03, 0.05, 0.05), st["Eisen"])
    # Langhölzer unter dem Boden, vorn als Zugholme nach vorn verlängert (der Handkarren wird gezogen); die Holme steigen auf Griffhöhe
    # und laufen zur Zugstange zusammen, an der der Zieher steht (Anker_Ziehen)
    zug_y, zug_z = -2.35, 0.86
    for s in (-1, 1):
        balken(n, st, (s * 0.38, y0 + 0.1, 0.545), (s * 0.38, y1, 0.545), 0.07, 0.07, "Holz")
        balken(n, st, (s * 0.38, y0 + 0.1, 0.545), (s * 0.26, zug_y, zug_z), 0.07, 0.07, "Holz", ohne=("+x",))
        n.quader((s * 0.38, y1, 0.545), (0.085, 0.04, 0.085), st["Eisen"])
    # Zugstange quer zwischen den Holmenden (die Hände des Ziehers), Eisenbeschlag an den Enden
    rohr(n, st, (-0.34, zug_y, zug_z), (0.34, zug_y, zug_z), 0.03, 0.03, "Holz", seiten=8)
    for s in (-1, 1):
        rohr(n, st, (s * 0.255, zug_y, zug_z), (s * 0.285, zug_y, zug_z), 0.04, 0.04, "Eisen", seiten=8)
    # Strebe unter der Zugstange (Quersprosse zwischen den Holmen)
    rohr(n, st, (-0.33, -1.5, 0.74), (0.33, -1.5, 0.74), 0.027, 0.027, "Holz", seiten=6)
    # Eisenbänder um die Achsstütze und Winkel am Boden
    for s in (-1, 1):
        n.quader((s * 0.38, 0.0, 0.545), (0.085, 0.05, 0.085), st["Eisen"])
        n.quader((s * 0.38, y0 + 0.1, 0.545), (0.085, 0.04, 0.085), st["Eisen"])
    karren = n.objekt()
    objekte = [karren, rad_objekt(st, "Rad_L", r, 0.075, (spur, 0, r), 0), rad_objekt(st, "Rad_R", r, 0.075, (-spur, 0, r), 1)]
    for i in range(ARTEN_SORTEN["hand"]):
        objekte.append(ladung("hand", i, st, maße))
    objekte.append(plane("hand", st, maße, 0.7))
    # Stützbeine vorn
    sn = bs.Netz("Stuetze")
    for s in (-1, 1):
        balken(sn, st, (s * 0.38, y0 + 0.14, 0.56), (s * 0.38, y0 - 0.08, 0.0), 0.07, 0.07, "Holz", ohne=("+x", "-x"))
        sn.quader((s * 0.38, y0 - 0.095, 0.015), (0.09, 0.12, 0.03), st["Eisen"])
    balken(sn, st, (-0.38, y0 + 0.06, 0.25), (0.38, y0 + 0.06, 0.25), 0.05, 0.05, "Holz", quer=(0, 1, 0), ohne=("+x", "-x"))
    objekte.append(bs.zustand(sn.objekt()))
    objekte += [bs.anker("Anker_Ziehen", (0, zug_y - 0.285, 0.0), 180.0),
                bs.anker("Anker_Zug_L", (0.22, zug_y, zug_z), 180.0),
                bs.anker("Anker_Zug_R", (-0.22, zug_y, zug_z), 180.0)]
    return objekte


def tiefe_deichsel(n, st, x, ya, yb, za, zb, breite=0.09, dicke=0.08):
    balken(n, st, (x, ya, za), (x, yb, zb), breite, dicke, "Holz", quer=(1, 0, 0))


def ochse(st):
    zf, halb, y0, y1, hw = 0.95, 0.72, -1.2, 1.2, 0.5
    maße = (halb, y0, y1, zf, hw)
    r, spur = 0.72, 0.90
    tip_y, tip_z = -4.7, 1.56          # Höhe des Jochs des Ochsen im Ziehen (tier_ochse_*, anker_ziehen_godot: 1,56 m); 1 m mehr Abstand zum Wagen
    holm_y, holm_z = tip_y + 0.3, 0.85  # die Holme laufen tief und seitlich (x = ±0,62) am Tier vorbei und enden an Zugsträngen zum Joch
    n = bs.Netz("Karren")
    kasten(n, st, halb, y0, y1, zf, hw, 3)
    balken(n, st, (-spur - 0.1, 0, r), (spur + 0.1, 0, r), 0.1, 0.1, "Holz", quer=(0, 1, 0))
    for s in (-1, 1):
        n.quader((s * (spur + 0.125), 0, r), (0.03, 0.06, 0.06), st["Eisen"])
        n.quader((s * (spur - 0.12), 0, r), (0.06, 0.12, 0.12), st["Eisen"])                 # Achskappen innen
    # Gabeldeichsel: Langhölzer unter dem Boden, vorn auf Halshöhe des Ochsen hochgezogen, am Ende das Joch
    for s in (-1, 1):
        tiefe_deichsel(n, st, s * 0.50, y1 - 0.05, y0, 0.845, 0.845, 0.1, 0.09)
        balken(n, st, (s * 0.50, y0, 0.845), (s * 0.62, holm_y, holm_z), 0.1, 0.085, "Holz", quer=(1, 0, 0), ohne=("+x",))
        rohr(n, st, (s * 0.62, holm_y, holm_z), (s * 0.50, tip_y, tip_z), 0.034, 0.034, "Holz alt", seiten=6)      # Zugstrang zum Joch (graubraunes Holz bzw. Hanf, mit Maserung der Bibliothek)
        n.quader((s * 0.50, y0 - 0.2, 0.9), (0.115, 0.05, 0.12), st["Eisen"])                 # Eisenband am Knick
        n.quader((s * 0.50, 0.0, 0.845), (0.115, 0.06, 0.115), st["Eisen"])
    # Querholz zwischen den Gabelarmen
    balken(n, st, (-0.5, y0 - 0.15, 0.9), (0.5, y0 - 0.15, 0.9), 0.07, 0.07, "Holz", quer=(0, 1, 0))      # dicht am Wagen: Platz für den Schwanz des Ochsen
    # Wimpelstange am linken vorderen Eckpfosten
    stange(n, st, (halb + 0.045 + 0.012, y0 - 0.045, zf + hw + 0.06), 0.9)
    objekte = [n.objekt()]
    objekte += [rad_objekt(st, "Rad_L", r, 0.1, (spur, 0, r), 0), rad_objekt(st, "Rad_R", r, 0.1, (-spur, 0, r), 1)]
    for i in range(ARTEN_SORTEN["ochse"]):
        objekte.append(ladung("ochse", i, st, maße))
    objekte.append(plane("ochse", st, maße, 0.98))
    objekte.append(wimpel(st, (halb + 0.045 + 0.012, y0 - 0.045, zf + hw + 0.06 + 0.9 + 0.012), 0.0))
    # Kein Joch am Karren: Das Nackenjoch gehört zum Zugtier (tier_ochse_*), die Gabel endet an seiner Mitte (Anker_Deichsel).
    # Stützbock unter der Gabel
    sn = bs.Netz("Stuetze")
    for s in (-1, 1):
        zg = 0.845 + (holm_z - 0.845) * (-2.4 - y0) / (holm_y - y0)
        balken(sn, st, (s * 0.52, -2.4, zg - 0.03), (s * 0.5, -2.4, 0.0), 0.08, 0.08, "Holz", ohne=("+x", "-x"))
        sn.quader((s * 0.5, -2.4, 0.015), (0.1, 0.12, 0.03), st["Eisen"])
    balken(sn, st, (-0.5, -2.4, 0.4), (0.5, -2.4, 0.4), 0.06, 0.06, "Holz", quer=(0, 1, 0), ohne=("+x", "-x"))
    objekte.append(bs.zustand(sn.objekt()))
    objekte += [bs.anker("Anker_Deichsel", (0, tip_y, tip_z), 180.0),
                bs.anker("Anker_Fahrer_Seite", (1.2, tip_y + 0.2, 0.0), 180.0)]
    return objekte


KRUPPE_ZU_RING = 1.64      # Pferd B: Kruppe 1,64 m hinter der Ringmitte des Kummets
WAAG_ABSTAND = 0.45        # nächstes Wagenteil (Waagscheit bzw. Stirnwand) 0,45 m hinter der Kruppe (IVC: 8 bis 20 Zoll = 0,20 bis 0,51 m)
SPRITZ_ABSTAND = 0.35      # Spritzbrett 0,35 m hinter dem Waagscheit
RING_Z = 1.279             # Zugpunkt der Pferde im Ziehen (Ringmitte des Kummets)
HAKEN = 0.347              # Zughaken am Kummet seitlich der Ringmitte


def ursprung_setzen(obj, punkt):
    """Setzt den Ursprung des Objekts auf `punkt` (Welt), die Gestalt bleibt, wo sie ist."""
    p = Vector(punkt)
    obj.data.transform(Matrix.Translation(-p))
    obj.location = p
    return obj


def unter(kind, eltern):
    """Hängt `kind` an `eltern` (beide ohne Drehung; der Ursprung des Elternknotens ist der Drehpunkt)."""
    kind.parent = eltern
    kind.location = Vector(kind.location) - Vector(eltern.location)
    return kind


STRAENGE = True            # False nur für die Messung (karren_neu_pruefen.py): Abstand der Wagenteile zum Pferd ohne die Zugstränge, die absichtlich am Kummet enden


def zugstrang(n, st, pfad, stoff="Leder dunkel", r=0.03):
    if not STRAENGE:
        return
    for a, b in zip(pfad[:-1], pfad[1:]):
        rohr(n, st, a, b, r, r, stoff, seiten=6)


def pferd_ein(st):
    """Pferdekarren (1 Pferd): Zweirad, Kasten mit Bock für den Fuhrmann, Gabeldeichsel (Schere). Der Ochsenkarren bleibt als karren_ochse."""
    zf, halb, y0, y1, hw = 0.95, 0.72, -1.85, 1.65, 0.5      # Kasten 3,50 m: vorn 1,3 m Bock mit Fußraum, dahinter 2,2 m Ladung (Ladefläche wie Ochsenkarren); Achse 0,25 m weiter vorn, das Pferd trägt etwas Last
    r, spur = 0.72, 0.90
    sx = 0.60                                                   # Schaftbäume 0,21 m außerhalb der Flanke (Flanke 0,385 m aus der Mitte)
    tip_y, tip_z = y0 - WAAG_ABSTAND - KRUPPE_ZU_RING, RING_Z   # Kruppe 0,45 m vor der Stirnwand
    ende_y = tip_y + 0.45
    maße = (halb, y0 + 1.3, y1, zf, hw)                         # Ladung und Plane nur hinter dem Bock
    n = bs.Netz("Karren")
    kasten(n, st, halb, y0, y1, zf, hw, 3)
    balken(n, st, (-spur - 0.1, 0, r), (spur + 0.1, 0, r), 0.1, 0.1, "Holz", quer=(0, 1, 0))
    for s in (-1, 1):
        n.quader((s * (spur + 0.125), 0, r), (0.03, 0.06, 0.06), st["Eisen"])
        n.quader((s * (spur - 0.12), 0, r), (0.06, 0.12, 0.12), st["Eisen"])
        tiefe_deichsel(n, st, s * sx, y1 - 0.05, y0, 0.845, 0.845, 0.09, 0.08)
        balken(n, st, (s * sx, y0, 0.845), (s * sx, ende_y, 1.02), 0.09, 0.08, "Holz", quer=(1, 0, 0))
        n.quader((s * sx, y0 - 0.2, 0.9), (0.115, 0.05, 0.12), st["Eisen"])
        n.quader((s * sx, 0.0, 0.845), (0.115, 0.06, 0.115), st["Eisen"])
        n.quader((s * sx, ende_y + 0.25, 1.0), (0.08, 0.1, 0.08), st["Eisen"])             # Zugnagel am Schaft
        zugstrang(n, st, [(s * HAKEN, tip_y, tip_z), (s * 0.43, tip_y + 0.35, tip_z - 0.12), (s * 0.5, tip_y + 0.7, tip_z - 0.22), (s * sx, ende_y + 0.25, 1.02)])
    balken(n, st, (-sx, y0 - 0.15, 0.9), (sx, y0 - 0.15, 0.9), 0.07, 0.07, "Holz", quer=(0, 1, 0))
    # Bock: Fußbrett auf dem Boden, Sitzbrett auf den Seitenwänden, kleine Lehne
    sitz = zf + 0.446 + 0.06
    n.quader((0, y0 + 0.6, zf + 0.02), (1.2, 0.9, 0.04), st["Holz alt"])                   # Fußbrett (Oberkante 0,06 m über dem Boden)
    n.quader((0, y0 + 1.05, sitz - 0.03), (halb * 2 + 0.06, 0.38, 0.06), st["Holz"])        # Sitzbrett auf den Wänden
    n.quader((0, y0 + 1.27, sitz + 0.22), (0.95, 0.045, 0.34), st["Holz alt"])              # Lehne
    for s in (-1, 1):
        n.quader((s * 0.46, y0 + 1.27, sitz + 0.2), (0.05, 0.07, 0.4), st["Holz"])
    stange(n, st, (halb + 0.045 + 0.012, y0 - 0.045, zf + hw + 0.06), 0.9)
    objekte = [n.objekt()]
    objekte += [rad_objekt(st, "Rad_L", r, 0.1, (spur, 0, r), 0), rad_objekt(st, "Rad_R", r, 0.1, (-spur, 0, r), 1)]
    for i in range(ARTEN_SORTEN["pferd"]):
        objekte.append(ladung("ochse", i, st, maße))
    objekte.append(plane("ochse", st, maße, 0.98))
    objekte.append(wimpel(st, (halb + 0.045 + 0.012, y0 - 0.045, zf + hw + 0.06 + 0.9 + 0.012), 0.0))
    dn = bs.Netz("Decke")
    dn.quader((0, y0 + 1.05, sitz + 0.0), (0.97, 0.4, 0.024), st["Hausfarbe"])
    dn.quader((0, y0 + 1.05 - 0.205, sitz - 0.1), (0.97, 0.024, 0.2), st["Hausfarbe"])
    objekte.append(dn.objekt())
    sn = bs.Netz("Stuetze")                                                                 # Stützbock unter den Schaftbäumen, zeigen wenn der Karren steht
    y_st = ende_y + 0.5
    for s in (-1, 1):
        balken(sn, st, (s * (sx + 0.02), y_st, 0.98), (s * sx, y_st, 0.0), 0.08, 0.08, "Holz", ohne=("+x", "-x"))
        sn.quader((s * sx, y_st, 0.015), (0.1, 0.12, 0.03), st["Eisen"])
    balken(sn, st, (-sx, y_st, 0.4), (sx, y_st, 0.4), 0.06, 0.06, "Holz", quer=(0, 1, 0), ohne=("+x", "-x"))
    objekte.append(bs.zustand(sn.objekt()))
    objekte += [bs.anker("Anker_Deichsel", (0, tip_y, tip_z), 180.0), bs.anker("Anker_Bock", (0, y0 + 1.05, sitz), 180.0)]
    return objekte


def pferd_gross(st):
    """Großer Pferdekarren (2 Pferde): Vierradwagen mit Drehschemel. Knoten „Vorderwagen“ (Ursprung = Reibnagel) trägt Vorderachse, Räder Rad_VL/VR, Mittelbaum zwischen den
    Pferden, Waagscheit mit Zugsträngen und Stütze; das Spiel dreht ihn gegen den Kasten (Anker_Reibnagel liegt auf dem Ursprung). Kruppe → Waagscheit 0,45 m, → Spritzbrett 0,35 m dahinter,
    Mittelbaum 0,21 m neben den Flanken (Pferde ±0,62 m, Flanke ±0,34 m)."""
    zf, halb, y0, y1, hw = 1.04, 0.80, -2.85, 0.55, 0.62       # Kasten 3,40 × 1,60 m
    maße = (halb, y0, y1, zf, hw)
    r_h, r_v, spur_h, spur_v, y_v = 0.66, 0.42, 1.02, 0.98, -2.20   # Radstand 2,20 m; Vorderrad Ø 0,84 m: Oberkante 0,05 m unter den Langhölzern, auch im Einschlag frei
    pole_z = 0.86
    pole_y = y0 - 1.175 - SPRITZ_ABSTAND                       # Waagscheit 0,35 m vor dem Spritzbrett (Vorderkante bei y0 - 1,175)
    tip_y, tip_z = pole_y - KRUPPE_ZU_RING - WAAG_ABSTAND, RING_Z   # Ringmitte der Kummete: Kruppe 0,45 m vor dem Waagscheit
    ende_y = tip_y + 0.5                                       # Mittelbaum endet 0,5 m hinter dem Ring, zwischen den Schultern
    ws = 1.10                                                  # halbe Länge des Waagscheits (Zugstränge außerhalb der Flanken)
    n = bs.Netz("Karren")
    kasten(n, st, halb, y0, y1, zf, hw, 4)
    balken(n, st, (-spur_h - 0.1, 0, r_h), (spur_h + 0.1, 0, r_h), 0.09, 0.09, "Holz", quer=(0, 1, 0))
    for s in (-1, 1):
        n.quader((s * (spur_h + 0.125), 0, r_h), (0.03, 0.06, 0.06), st["Eisen"])
        balken(n, st, (s * 0.55, y1 - 0.1, zf - 0.105), (s * 0.55, y0 + 0.05, zf - 0.105), 0.09, 0.09, "Holz", quer=(1, 0, 0))
    n.quader((0, 0, (zf - 0.105 + r_h + 0.045) / 2), (1.3, 0.14, zf - 0.105 - r_h - 0.045), st["Holz"], ohne=("+z",))
    # Bock auf dem Kasten: Fußbrett, Spritzbrett, Sitzbrett auf zwei Pfosten, Lehne
    boden = zf
    n.quader((0, y0 - 0.6, boden - 0.02), (1.3, 1.15, 0.04), st["Holz alt"])
    n.quader((0, y0 - 1.17, boden + 0.16), (1.3, 0.04, 0.34), st["Bretter"])
    for s in (-1, 1):
        n.quader((s * 0.66, y0 - 0.6, boden + 0.16), (0.05, 1.15, 0.32), st["Holz alt"])
        n.quader((s * 0.4, y0 - 0.55, boden + 0.2), (0.07, 0.07, 0.42), st["Holz"])
        n.quader((s * 0.4, y0 - 0.55, boden + 0.42), (0.1, 0.05, 0.05), st["Eisen"])
    sitz = boden + 0.446
    n.quader((0, y0 - 0.55, sitz - 0.03), (0.95, 0.38, 0.06), st["Holz"])
    n.quader((0, y0 - 0.37, sitz + 0.25), (0.95, 0.045, 0.34), st["Holz alt"])
    for s in (-1, 1):
        n.quader((s * 0.46, y0 - 0.37, sitz + 0.2), (0.05, 0.07, 0.44), st["Holz"])
    stange(n, st, (halb + 0.045 + 0.012, y1 + 0.045, zf + hw + 0.06), 1.0)
    objekte = [n.objekt()]
    objekte += [rad_objekt(st, "Rad_HL", r_h, 0.11, (spur_h, 0, r_h), 0), rad_objekt(st, "Rad_HR", r_h, 0.11, (-spur_h, 0, r_h), 1)]
    for i in range(ARTEN_SORTEN["pferd_gross"]):
        objekte.append(ladung("pferd", i, st, maße))
    objekte.append(plane("pferd", st, maße, 1.15))
    objekte.append(wimpel(st, (halb + 0.045 + 0.012, y1 + 0.045, zf + hw + 0.06 + 1.0 + 0.012), 0.0))
    dn = bs.Netz("Decke")
    dn.quader((0, y0 - 0.55, sitz + 0.0), (0.97, 0.4, 0.024), st["Hausfarbe"])
    dn.quader((0, y0 - 0.55 - 0.205, sitz - 0.1), (0.97, 0.024, 0.2), st["Hausfarbe"])
    objekte.append(dn.objekt())
    # Vorderwagen (Drehschemel): Achsholz, Schemel unter dem Kasten, Reibnagel, Deichsel und Mittelbaum zwischen den Pferden, Waagscheit hinter den Pferden mit Zugsträngen, Kuppelriemen
    v = bs.Netz("Vorderwagen")
    balken(v, st, (-spur_v - 0.1, y_v, r_v), (spur_v + 0.1, y_v, r_v), 0.09, 0.09, "Holz", quer=(0, 1, 0))
    for s in (-1, 1):
        v.quader((s * (spur_v + 0.125), y_v, r_v), (0.03, 0.06, 0.06), st["Eisen"])
    v.quader((0, y_v, (zf - 0.06 + r_v + 0.045) / 2), (1.3, 0.5, zf - 0.06 - r_v - 0.045), st["Holz"], ohne=("+z",))
    v.quader((0, y_v, zf - 0.06), (0.12, 0.12, 0.12), st["Eisen"])
    balken(v, st, (0, y_v + 0.1, 0.62), (0, pole_y, pole_z), 0.14, 0.12, "Holz", quer=(1, 0, 0))          # Deichsel bis zum Waagscheit
    balken(v, st, (0, pole_y, pole_z), (0, ende_y, 1.05), 0.12, 0.10, "Holz", quer=(1, 0, 0))             # Mittelbaum zwischen den Pferden (steigt auf Brusthöhe)
    v.quader((0, ende_y - 0.03, 1.05), (0.16, 0.12, 0.14), st["Eisen"])                                    # Beschlag am Baumende
    rohr(v, st, (-ws, pole_y, pole_z), (ws, pole_y, pole_z), 0.05, 0.05, "Holz", seiten=8)
    v.quader((0, pole_y - 0.02, pole_z), (0.16, 0.18, 0.15), st["Eisen"])
    for s in (-1, 1):
        v.quader((s * ws, pole_y, pole_z), (0.07, 0.11, 0.11), st["Eisen"])
        grund = [(s * ws, pole_y - 0.04, pole_z), (s * 1.09, pole_y - 1.0, pole_z + 0.14), (s * 1.08, tip_y + 0.95, tip_z - 0.22), (s * 1.06, tip_y + 0.40, tip_z - 0.08), (s * (0.62 + HAKEN), tip_y, tip_z)]
        pfad = []
        for i in range(16):
            u = i / 15
            f = u * (len(grund) - 1)
            j = min(int(f), len(grund) - 2)
            q = [grund[j][k] + (grund[j + 1][k] - grund[j][k]) * (f - j) for k in range(3)]
            q[2] -= 0.05 * 4 * u * (1 - u)
            pfad.append(tuple(q))
        zugstrang(v, st, pfad)
        for q in (pfad[4], pfad[8], pfad[12]) if STRAENGE else ():
            v.quader(q, (0.075, 0.075, 0.075), st["Eisen"])
        if STRAENGE:
            v.quader(pfad[-1], (0.08, 0.06, 0.08), st["Eisen"])
        zugstrang(v, st, [(s * (0.62 - HAKEN), tip_y, tip_z - 0.1), (s * 0.3, tip_y + 0.3, tip_z - 0.18), (s * 0.06, ende_y, 1.05)], r=0.02)  # Kuppelriemen Kummet - Baumende
    vw = ursprung_setzen(v.objekt(), (0, y_v, 0))
    raeder_v = [unter(rad_objekt(st, "Rad_VL", r_v, 0.1, (spur_v, y_v, r_v), 2), vw), unter(rad_objekt(st, "Rad_VR", r_v, 0.1, (-spur_v, y_v, r_v), 0), vw)]
    sn = bs.Netz("Stuetze")
    y_st = pole_y + 0.25
    for s in (-1, 1):
        zg = 0.62 + (pole_z - 0.62) * (y_st - (y_v + 0.1)) / (pole_y - (y_v + 0.1))
        balken(sn, st, (s * 0.1, y_st, zg - 0.03), (s * 0.12, y_st, 0.0), 0.08, 0.08, "Holz", ohne=("+x", "-x"))
        sn.quader((s * 0.12, y_st, 0.015), (0.1, 0.12, 0.03), st["Eisen"])
    stuetze = unter(ursprung_setzen(bs.zustand(sn.objekt()), (0, y_v, 0)), vw)
    objekte += [vw] + raeder_v + [stuetze]
    objekte += [bs.anker("Anker_Deichselende", (0, ende_y, 1.05), 180.0), bs.anker("Anker_Pferd_L", (0.62, tip_y, tip_z), 180.0), bs.anker("Anker_Pferd_R", (-0.62, tip_y, tip_z), 180.0),
                bs.anker("Anker_Bock", (0, y0 - 0.55, sitz), 180.0), bs.anker("Anker_Reibnagel", (0, y_v, zf - 0.06), 180.0)]
    return objekte


BAUER = {"hand": hand, "ochse": ochse, "pferd": pferd_ein, "pferd_gross": pferd_gross}


def bauen(art):
    bs.neue_szene()
    return BAUER[art](bs.Stoffe())


def main():
    bs.unterordner = lambda name: ORDNER                      # eigene Dateien unter assets/modelle/karren/
    bs.BIBLIOTHEK_JE_FLAECHE = True
    behalten = set(bs.BENANNTE_STOFFE)
    nur = next((a[6:].split(",") for a in sys.argv if a.startswith("--nur=")), None)
    for art in ARTEN:
        if nur and art not in nur:
            continue
        name = "karren_" + art
        objekte = bauen(art)
        if "--ohne-bericht" not in sys.argv:
            bs.bericht(name, objekte, einzeln=True)
        vorher, nachher = bs.farben_zusammenlegen(objekte, behalten)
        print(f"  Materialien: {vorher} → {nachher}")
        print("geschrieben:", bs.exportieren(name, objekte))


if __name__ == "__main__":
    main()
