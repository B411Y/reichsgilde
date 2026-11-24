"""Efeu als Blattkarten fuer beliebige Haeuser (Elben, Nutzer 21.11.2026: „im Pixel-Look nicht zu sehen – deutlich mehr Efeu, gern auch
ueber das Fachwerkholz, Stoff Efeublatt“). Muster: wohnhaus_halbling_s23.efeu_karten (Halbling Stufe 3), hier ohne feste Masse des Hauses:

    bewachsen(haus, teile, ebenen, budget, saat, z_grenze)

Die Karten (je zwei Dreiecke, Stoff „Efeublatt“, Alpha-Schnitt) kommen ins Teil `haus`. Wo eine Karte sitzen darf, liest die Funktion aus
den Flaechen der Huelle (`teile`: Haus, Dach, Wappen ...):
  * hinter allen fuenf Pruefpunkten der Karte (Ecken und Mitte) liegt eine Flaeche in der Wandebene – Mauer, Putz oder Fachwerkholz
    (bis 4 cm vor der Ebene); damit haengt keine Karte in einer Oeffnung oder neben dem Giebel in der Luft;
  * im Raum vor der Karte (9 bis 30 cm vor der Ebene) und knapp dahinter (Laibungen, Glas) liegt kein Punkt einer anderen Flaeche –
    Fensterrahmen, Laeden, Gewaende, Wappen, Vordach, Anbauten und Schornstein bleiben frei;
  * `z_grenze(ebene, s)`: Oberkante je Stelle (unter der Traufe bzw. unter dem Ortgang).
`ebenen`: Liste aus (Ursprung (x, y), Richtung (ex, ey), Laenge, Normale (nx, ny)) – die Aussenwaende. Der Bewuchs waechst aus Wurzeln
(an den Ecken und dazwischen) in Spalten nach oben, unten dichte Bilder, oben lockere, zuoberst ein Trieb; die Wurzeln kommen reihum
ueber alle Waende an die Reihe, bis `budget` Karten gesetzt sind. Die acht Blattbilder liegen im Blatt der Halblinge Stufe 3 (Band
„stroh“, v 0,004 bis 0,121; trimsheet_halbling.efeu_blaetter); im Spiel liest der Stoff Efeublatt immer diese Ebene (efeu_ebene im
Shader building_trim.gdshaderinc, Stadtansicht.Neubau.cs).
"""
import random

from mathutils import Vector

EFEU_V = (0.004, 0.121)
VOR = 0.095                       # erste Lage vor der Wandebene: vor dem Fachwerkholz
HOLZ_VOR = 0.09                   # bis hierhin zaehlt eine Flaeche vor der Ebene als Wand (Fachwerkholz, Schwellen, Giebelsparren)
FREI_VOR, FREI_HINTER = 0.30, 0.25
RAND = 0.03


def _proben(pts):
    """Pruefpunkte einer Flaeche: Ecken, Kantenmitten, Mitte, bei grossen Flaechen ein Raster."""
    n = len(pts)
    mitte = sum(pts, Vector()) / n
    aus = list(pts) + [(pts[i] + pts[(i + 1) % n]) / 2 for i in range(n)] + [mitte]
    gross = max((pts[i] - pts[(i + 1) % n]).length for i in range(n))
    if gross > 0.6 and n in (3, 4):
        k = min(8, int(gross / 0.3) + 1)
        for a in range(1, k):
            for b in range(1, k):
                u, v = a / k, b / k
                if n == 4:
                    aus.append((pts[0] * (1 - u) + pts[1] * u) * (1 - v) + (pts[3] * (1 - u) + pts[2] * u) * v)
                elif u + v < 1:
                    aus.append(pts[0] + (pts[1] - pts[0]) * u + (pts[2] - pts[0]) * v)
    return aus


def _in_vieleck(q, poly):
    """Punkt (s, z) im konvexen Vieleck (Liste aus (s, z)), mit 1 mm Rand."""
    vz = 0
    for i in range(len(poly)):
        ax, ay = poly[i]; bx, by = poly[(i + 1) % len(poly)]
        k = (bx - ax) * (q[1] - ay) - (by - ay) * (q[0] - ax)
        if abs(k) < 1e-6: continue
        if vz == 0: vz = 1 if k > 0 else -1
        elif (k > 0) != (vz > 0): return False
    return True


def bewachsen(haus, teile, ebenen, budget, saat, z_grenze, gross=1.0, z_fuss=0.04, nur_ecken=False, orte_je_ebene=None):
    """Setzt hoechstens `budget` Karten ins Teil `haus`. Rueckgabe: Zahl der Karten."""
    rng = random.Random(saat)
    # (Punkte, farbig): Flaechen in der Akzentfarbe (Fensterlaeden, Luken; Eckenfarbe Rot) sind nie Wand, auch wenn sie flach aufliegen
    flaechen = [([Vector(p) for p in fl[0]], len(fl) > 3 and fl[3] is not None and fl[3][0] > 0.5) for t in teile for fl in t.flaechen]
    karten = []                                                    # (Ebene, s0, s1, z0, z1, Bild)
    je_ebene = []
    for nr, ((ox, oy), (ex, ey), laenge, (nx, ny)) in enumerate(ebenen):
        o, e, n = Vector((ox, oy, 0.0)), Vector((ex, ey, 0.0)), Vector((nx, ny, 0.0))
        wand, stoerer = [], []
        for pts, farbig in flaechen:
            d = [(p - o).dot(n) for p in pts]
            fn = (pts[1] - pts[0]).cross(pts[2] - pts[0])
            if not farbig and fn.length > 1e-9 and abs(fn.normalized().dot(n)) > 0.95 and all(-0.005 <= x <= HOLZ_VOR for x in d):
                wand.append([((p - o).dot(e), p.z) for p in pts])
                continue
            for q in _proben(pts):
                dq = (q - o).dot(n)
                if HOLZ_VOR < dq < FREI_VOR or -FREI_HINTER < dq < -0.02 or (farbig and -0.02 <= dq <= HOLZ_VOR):
                    stoerer.append(((q - o).dot(e), q.z))
        je_ebene.append((o, e, n, laenge, wand, stoerer))

    def frei(nr, s0, s1, z0, z1):
        o, e, n, laenge, wand, stoerer = je_ebene[nr]
        if s0 < -0.02 or s1 > laenge + 0.02 or z1 - z0 < 0.25: return False
        for q in ((s0 + 0.04, z0 + 0.04), (s1 - 0.04, z0 + 0.04), (s1 - 0.04, z1 - 0.04), (s0 + 0.04, z1 - 0.04), ((s0 + s1) / 2, (z0 + z1) / 2)):
            if not any(_in_vieleck(q, w) for w in wand): return False
        return not any(s0 - RAND < s < s1 + RAND and z0 - RAND < z < z1 + RAND for s, z in stoerer)

    # Wurzeln je Wand: beide Ecken, dazu je 2,5 m Wand eine weitere; reihum ueber die Waende
    wurzeln = []
    for nr, (o, e, n, laenge, wand, stoerer) in enumerate(je_ebene):
        if not wand: continue
        zmax = max(z for w in wand for _s, z in w)
        orte = [0.15, laenge - 0.15] + [rng.uniform(0.8, laenge - 0.8) for _ in range(max(0, int(laenge / 2.5) - 1))] if laenge > 1.8 else [laenge / 2]
        if nur_ecken and laenge > 1.8: orte = [0.15, laenge - 0.15]        # Patrizierhaus der Elben: Efeu nur an den Hausecken
        if orte_je_ebene and nr in orte_je_ebene: orte = list(orte_je_ebene[nr])           # eigene Wurzeln (Giebelrand der Elben)
        rng.shuffle(orte)
        for k, s in enumerate(orte):
            wurzeln.append((k, rng.random(), nr, s, rng.uniform(0.9, 1.9), rng.uniform(0.65, 1.0) * zmax))
    wurzeln.sort()
    print("EFEU Ebenen:", [(len(w), len(st)) for _o, _e, _n, _l, w, st in je_ebene], "Wurzeln", len(wurzeln))
    for _k, _r, nr, s_w, reich, hoch_w in wurzeln:
        if len(karten) >= budget: break
        s = s_w - reich
        while s < s_w + reich and len(karten) < budget:
            hoch = hoch_w * max(0.0, 1.0 - abs(s + 0.25 - s_w) / reich) ** 0.7
            z = z_fuss
            while len(karten) < budget:
                gr = rng.uniform(0.5, 0.75) * gross
                ss = s + rng.uniform(-0.07, 0.07)
                grenze = min(z_grenze(nr, ss), z_grenze(nr, ss + gr))
                if z + gr * 0.6 >= min(hoch, grenze): break
                z1 = min(z + gr, grenze)
                art = "dicht" if z < z_fuss + hoch * 0.55 else "locker"
                if frei(nr, ss, ss + gr, z, z1):
                    karten.append((nr, ss, ss + gr, z, z1, {"dicht": rng.randrange(0, 4), "locker": rng.choice((4, 5))}[art]))
                z += gr * rng.uniform(0.5, 0.7)
            if hoch > 0.5 and len(karten) < budget:                # Trieb ueber dem Rand der Spalte
                t0, t1 = s + 0.08, s + 0.08 + 0.42 * gross
                grenze = min(z_grenze(nr, t0), z_grenze(nr, t1))
                zt1 = min(z + 0.6 * gross, grenze)
                if frei(nr, t0, t1, z, zt1): karten.append((nr, t0, t1, z, zt1, rng.choice((6, 7))))
            s += 0.40 * gross
    v0, v1 = EFEU_V
    # Lagen 4 mm auseinander (deckungsgleich ab 2 mm, Pruefung 12)
    abstaende = [VOR + 0.004 * i for i in range(24)]
    lagen = []
    for i, (nr, s0, s1, z0, z1, b) in enumerate(karten):
        belegt = {lagen[j] for j in range(i) if karten[j][0] == nr and karten[j][1] < s1 and karten[j][2] > s0 and karten[j][3] < z1 and karten[j][4] > z0}
        lagen.append(next((l for l in range(24) if l not in belegt), i % 24))
    for k, ((nr, s0, s1, z0, z1, b), lage) in enumerate(zip(karten, lagen)):
        o, e, n = je_ebene[nr][:3]
        p = [o + e * s0 + Vector((0, 0, z0)), o + e * s1 + Vector((0, 0, z0)), o + e * s1 + Vector((0, 0, z1)), o + e * s0 + Vector((0, 0, z1))]
        p = [q + n * abstaende[lage] for q in p]
        u0, u1 = b / 8 + 0.002, (b + 1) / 8 - 0.002
        if k % 2: u0, u1 = u1, u0                                  # gespiegelt
        haus.vieleck("Efeublatt", p, [(u0, v1), (u1, v1), (u1, v0), (u0, v0)], weg_von=p[0] - n)
    return len(karten)


def stoff_anlegen(g1, blatt):
    """Der Stoff „Efeublatt“ fehlt in den Stofflisten der Menschenskripte: beim Bau des Objekts aus dem Blatt `blatt` anlegen (mit
    Alpha-Schnitt, wie wohnhaus_halbling_s23). Einmal je Lauf."""
    import netzbau
    if getattr(netzbau.Teil, "_efeu_stoff", False): return
    alt_objekt = netzbau.Teil.objekt

    def objekt(self, stoffe, *a, **kw):
        if "Efeublatt" not in stoffe and any(fl[2] == "Efeublatt" for fl in self.flaechen):
            stoffe = dict(stoffe); stoffe["Efeublatt"] = g1.stoff("Efeublatt", blatt, schere=True)
        return alt_objekt(self, stoffe, *a, **kw)
    netzbau.Teil.objekt = objekt
    netzbau.Teil._efeu_stoff = True
