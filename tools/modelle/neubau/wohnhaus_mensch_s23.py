"""Neubau der Gebaeude: Wohnhaus Mensch, Stufe 2 (Buergerhaus) und Stufe 3 (Steinhaus) - aussen und innen.

    blender -b --factory-startup --python tools/modelle/neubau/wohnhaus_mensch_s23.py -- --stufe=2 --form=1 [--bilder=<ordner>] [--ohne-ao] [--gaube-alt] [--dach-alt] [--schnitt-alt]

Schreibt
    game/assets/modelle/gebaeude/neubau/wohnhaus_mensch_s<stufe>_g<form>.glb        Huelle (Haus, Dach, Scheiben, Wappen, Fahne, Leerstand, Geruest, Anker)
    game/assets/modelle/gebaeude/neubau/wohnhaus_mensch_s<stufe>_g<form>_innen.glb  Wohnstube (im Spiel „InteriorRoot“)
    und die Zellen in den AO-Atlanten der Gruppe (tools/modelle/neubau/ao_atlas.json).
Die Raeume (Hinterzimmer, Bibliothek) baut raeume_s23.py fuer dasselbe Haus.

Masse: wohnhaus_mensch_s23_masse.py (Soll/Ist dort). Vorlagen des Nutzers (Artworks wohnhaus_lvl2_v01..v04, wohnhaus_in_lvl2_mensch)
nur als Stil: Steinerdgeschoss mit Eckquadern, vorkragendes Fachwerk-Obergeschoss, Fachwerkgiebel, Ziegeldach mit Gaube, Steinschornstein,
Vordach ueber der Tuer, Laeden; innen Dielen, Kalkputz mit Holztaefelung, Balkendecke, Herd mit Haube und Fliesen davor.
Regeln wie bei Stufe 1 (Style-Guide): Trim-Sheet (hier trim_s2 / innen_trim_s23, gleiche Baender wie trim), UV2 mit gebackener
Verdeckung im Atlas, Eckenfarbe Rot auf den Laeden, Fensterstoecke in 3D mit Butzenglas, echte Haustuer, Gegenstaende statt Reiter,
Einrichtung als Knoten „Einrichtung_<Kennung>“ (wirtschaft.json, wohnen.einrichtung), Plaetze der Familie als Anker.
Die Moebel der Stufe 1 (Herd, Tisch, Truhe, Gegenstaende, Hausapotheke, Hauswaechter) kommen aus wohnhaus_mensch_s1_g1.py und
werden als starre Gruppen an ihre Wand versetzt (versetze aus wohnhaus_mensch_s1.py).
"""
import math
import os
import sys

import bpy
from mathutils import Matrix, Vector

HIER = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HIER)
sys.path.insert(0, os.path.join(os.path.dirname(HIER), "raeume"))
import netzbau                                                   # noqa: E402,F401
from netzbau import ROT, Teil                                      # noqa: E402
from trimsheet import BAENDER_AUSSEN as BA, BAENDER_INNEN as BI   # noqa: E402
from trimsheet_stufen import FLIESEN_U, WERK_LAGE                             # noqa: E402
import wohnhaus_mensch_s1_g1 as g1                                 # noqa: E402  (Moebel, Gegenstaende, Blender-Helfer der Probe)
import wohnhaus_mensch_s1 as s1                                    # noqa: E402  (versetze, teile_auf, bretterwand)
import wohnhaus_mensch_s23_masse as M                              # noqa: E402
from zimmertuer import zimmertuer                                  # noqa: E402
import wohnhaus_mensch_s23_formen as FM                            # noqa: E402  (Grundformen 2 bis 4: Fluegel, Schuppen, Erker)
import fachwerk_echt                                               # noqa: E402  (echtes Fachwerk, Nutzer 21.11.2026)
import holzlage                                                    # noqa: E402  (eine Holzlage je Fachwerkwand)
import random                                                      # noqa: E402

# Echtes Fachwerk (Nutzer 21.11.2026, Probe Stufe 2 Grundform 1): Mann-Figuren, Kopfbaender, Knaggen unter den Balkenkoepfen. Nur fuer
# Menschen (und spaeter Halblinge): gesetzt im eigenen Einstieg (main dieses Skripts), die Schichten der Zwerge und Elben behalten ihr Fachwerk.
FACHWERK_ECHT = False
LAGEN = {}          # Holzlage je Wand (echtes Fachwerk): fuer die Laeden (nur auf freiem Gefach)
GIEBEL_AUSSPARUNG = {}   # Giebelwand -> (z0, z1): dort setzt das senkrechte Holz der Mitte aus (Ochsenauge der Halblinge)
TIEFE_HOLZ = 0.05   # alle Hoelzer einer Wand buendig in dieser Tiefe vor der Putzflaeche

EZ = Vector((0, 0, 1))
feld = g1.feld
versetze = s1.versetze
S = "Innen"

# ── Waende der Huelle: Name -> (Ursprung aussen, Richtung, Laenge, Normale nach aussen) ─────────────────────────────────
WAENDE = {}


def waende_setzen():
    """Waende der Huelle aus den Massen der Stufe (M.stufe_setzen vorher): Erdgeschoss, Obergeschoss(e), Giebel."""
    WAENDE.clear()
    WAENDE.update({
        "vorn_eg": ((-M.BX, M.VORN), (1, 0), 2 * M.BX, (0, -1)), "hinten_eg": ((-M.BX, M.HINTEN), (1, 0), 2 * M.BX, (0, 1)),
        "links_eg": ((-M.BX, M.VORN), (0, 1), M.HINTEN - M.VORN, (-1, 0)), "rechts_eg": ((M.BX, M.VORN), (0, 1), M.HINTEN - M.VORN, (1, 0)),
        "vorn_gb": ((-M.OX, M.OVORN), (1, 0), 2 * M.OX, (0, -1)), "hinten_gb": ((-M.OX, M.HINTEN), (1, 0), 2 * M.OX, (0, 1)),
    })
    WAENDE["vorn_tg"] = ((-M.OX, M.OVORN - 0.04), (1, 0), 2 * M.OX, (0, -1))      # Treppengiebel (Stufe 3): Aussenflaeche der Mauer
    for g in ("og", "o2"):
        WAENDE.update({
            f"vorn_{g}": ((-M.OX, M.OVORN), (1, 0), 2 * M.OX, (0, -1)), f"hinten_{g}": ((-M.OX, M.HINTEN), (1, 0), 2 * M.OX, (0, 1)),
            f"links_{g}": ((-M.OX, M.OVORN), (0, 1), M.HINTEN - M.OVORN, (-1, 0)), f"rechts_{g}": ((M.OX, M.OVORN), (0, 1), M.HINTEN - M.OVORN, (1, 0)),
        })


def geschosse_og():
    """Die Obergeschosse als (Kuerzel, Unterkante, Oberkante)."""
    return [("og", M.Z_EG_DECKE, M.Z_TRAUFE)]      # Stufe 2 und 3 (Steinhaus wie die Vorlagen: ein Obergeschoss und das Dach)


waende_setzen()
MITTE = Vector((0, 0.3, 3.0))
RAUM = Vector((0, -0.2, 1.4))          # Mitte der Wohnstube (Sichtseiten innen)


def ist_stein():
    """Steinhaus (Stufe 3): ganz aus Stein – kein Fachwerk, Fenster mit Hausteingewaenden und steinernem Kreuzstock (Nutzer, 14.11.2026,
    abweichend von den Vorlagen)."""
    return M.STUFE == 3


def _band_von(teil, uvs):
    v = sum(uv[1] for uv in uvs) / len(uvs)
    return next((b for b, (v0, v1, _) in teil.baender.items() if v0 - 1e-6 <= v <= v1 + 1e-6 and b != "stroh"), None)


def versteinern(teil, ab=0, bis=None, z_min=0.7, ganz=False):
    """Steinhaus: macht aus Holz und Putz Haustein (Flaechen ab Index `ab` bis `bis`, nur oberhalb z_min – Bank, Fass und Scheitholz
    bleiben Holz). Wandhoelzer (Staender, Riegel, Streben: Baender balken_a, balken_b) entfallen, Bretter und Laibungen (balken_c) und
    Putz werden Stein (UV auf das Band „bruchstein“ gelegt). `ganz`: alles Holz wird Stein (Pfeiler des Schuppens), nichts entfaellt."""
    s0, s1_, _ = teil.baender["bruchstein"]
    akzente = getattr(teil, "holz_akzente", [])
    neu = []
    for i, (pts, uvs, stoff, farbe, glatt) in enumerate(teil.flaechen):
        im_bereich = i >= ab and (bis is None or i < bis) and max(p.z for p in pts) > z_min and not any(a <= i < e for a, e in akzente)
        band = _band_von(teil, uvs) if im_bereich else None
        if im_bereich and stoff in ("Fachwerk", "Lehmputz") and band not in ("farbholz", "eisen"):
            if band in ("balken_a", "balken_b") and not ganz: continue
            if band == "planken" and not ganz:
                neu.append((pts, uvs, stoff, farbe, glatt)); continue
            v0, v1, _ = teil.baender[band] if band else (0.0, 1.0, 1)
            uvs = [(u, s0 + (v - v0) / max(v1 - v0, 1e-6) * (s1_ - s0)) for u, v in uvs]
            neu.append((pts, uvs, "Bruchstein", farbe, glatt)); continue
        neu.append((pts, uvs, stoff, farbe, glatt))
    teil.flaechen = neu


WERKSTEIN = "putz"     # Steinhaus: Band des hellen Sandsteins im Trim-Sheet trim_s3 (Eckquader, Gewaende, Abdeckplatten; trimsheet_stufen.py)


def holz_akzent(teil, bau):
    """Steinhaus: die Flaechen, die `bau()` an `teil` anhaengt, bleiben dunkles Holz (Balkenkoepfe, Laeden, Traufe) – versteinern()
    laesst sie aus. Erlaubt sind nur Holzteile, die die Vorlagen wohnhaus_lvl3_* zeigen (Nutzer 15.11.2026); kein Fachwerk."""
    a = len(teil.flaechen)
    bau()
    if not hasattr(teil, "holz_akzente"): teil.holz_akzente = []
    teil.holz_akzente.append((a, len(teil.flaechen)))


def gewaende(h, wand, spannen, z0, z1):
    """Werksteingewaende aus Sandstein um ein Fenster oder ein Fensterpaar (Steinhaus): Gewaendesteine aussen, bei einem Paar ein
    Mittelpfeiler ueber den Mauerpfeiler, eine durchgehende Sohlbank und ein Sturz – liegen an der Oeffnung an, stehen 5 cm vor.
    Ein Paar bekommt EIN Gewaende: zwei einzelne ueberlappten sich am 17-cm-Pfeiler (deckungsgleiche Flaechen, beim Backen der
    Verdeckung schwarz – Rueckmeldung des Nutzers 15.11.2026)."""
    st, band = "Bruchstein", WERKSTEIN
    links, rechts = spannen[0][0], spannen[-1][1]
    for a, b in ((links - 0.13, links), (rechts, rechts + 0.13)):
        wkasten(h, st, wand, a, b, 0.0, 0.05, z0 - 0.02, z1 + 0.02, band, ohne=("-z", "+z"), laengs="z")   # ab der Mauerflucht: nicht deckungsgleich mit der Laibung
    # Mittelpfosten eines Fensterpaars: 25 cm breit (deckt beide Laibungskanten mit je 4 cm), 10 cm vor der Mauer, von der Sohlbank
    # bis zum Sturz – der 17-cm-Pfeiler mit den tiefen Laibungen las sich als schwarzer Pfeiler (Rueckmeldung 15.11.2026)
    for i in range(len(spannen) - 1):
        a_, b_ = spannen[i][1] - 0.04, spannen[i + 1][0] + 0.04
        # A (Patrizierhaus): gekuppeltes Fenster mit Steinsaeule – flache Vorlage (deckt die Laibungskanten), davor Basis, achteckiger
        # Schaft und Kapitell (Nutzer 21.11.2026)
        wkasten(h, st, wand, a_, b_, 0.0, 0.03, z0 - 0.005, z1 + 0.005, band, laengs="z")
        sm = (a_ + b_) / 2
        for za, zb, br in ((z0 - 0.005, z0 + 0.10, 0.11), (z1 - 0.13, z1 + 0.005, 0.12)):          # Basis, Kapitell
            wkasten(h, st, wand, sm - br, sm + br, 0.03, 0.16, za, zb, band, laengs="z")
        fuss = punkt(wand, sm, z0 + 0.10, 0.095)
        h.dreh(st, fuss, [(0.055, 0.0), (0.05, (z1 - 0.13) - (z0 + 0.10))], band, seiten=8, deckel=False)
    wkasten(h, st, wand, links - 0.17, rechts + 0.17, 0.0, 0.08, z0 - 0.11, z0 - 0.005, band, laengs="x")   # 5 mm unter der Laibung
    wkasten(h, st, wand, links - 0.17, rechts + 0.17, 0.0, 0.06, z1 + 0.005, z1 + 0.17, band, laengs="x")


# ── Patrizierhaus (Stufe 3; Nutzer 21.11.2026 „Patrizier A–D“, patrizier_recherche.md): Saeulen, Rundbogenfries, Blendboegen ──
RELIEF = 0.09          # Tiefe der Blendboegen und des Frieses (8–10 cm, sonst verschwinden sie im Pixel-Look)


def bogen_relief(h, P, xm, z_fuss, r_aussen, breite_ring, tiefe, band=None, seiten=8, pfeiler=0.0):
    """Rundbogen als Relief vor der Wand: Ring (Halbkreis um (xm, z_fuss), aussen r_aussen, Ringbreite breite_ring), auf Wunsch
    darunter zwei Pfeiler (Lisenen) bis z_fuss - pfeiler. P(s, z, raus) -> Punkt, raus nach aussen. Vorderflaeche in `tiefe`,
    Seitenflaechen aussen und innen, unten geschlossen."""
    band = band or WERKSTEIN
    v0, v1, m = h.baender[band]
    ri = r_aussen - breite_ring
    winkel = [math.pi * k / seiten for k in range(seiten + 1)]
    aussen = [(xm + r_aussen * math.cos(w), z_fuss + r_aussen * math.sin(w)) for w in winkel]
    innen = [(xm + ri * math.cos(w), z_fuss + ri * math.sin(w)) for w in winkel]
    hinten = P(xm, z_fuss, -1.0)
    uv = lambda s_, z_: (s_ / m, v0 + (v1 - v0) * (0.2 + 0.6 * ((z_ * 1.7) % 1)))
    for k in range(seiten):
        q = [aussen[k], aussen[k + 1], innen[k + 1], innen[k]]
        h.vieleck("Bruchstein", [P(a, b, tiefe) for a, b in q], [uv(a, b) for a, b in q], weg_von=hinten)
        for (a0, b0), (a1, b1), nach in ((aussen[k], aussen[k + 1], 1), (innen[k + 1], innen[k], -1)):
            pts = [P(a0, b0, 0.0), P(a1, b1, 0.0), P(a1, b1, tiefe), P(a0, b0, tiefe)]
            mitte = P((a0 + a1) / 2 - nach * (a0 + a1 - 2 * xm) * 0.1, (b0 + b1) / 2 - nach * ((b0 + b1) / 2 - z_fuss) * 0.1, tiefe / 2)
            h.vieleck("Bruchstein", pts, [(0, v0), (0.1, v0), (0.1, v0 + 0.02), (0, v0 + 0.02)], weg_von=mitte)
    for x0, x1 in ((xm - r_aussen, xm - ri), (xm + ri, xm + r_aussen)):
        if pfeiler > 0:   # Lisene unter dem Bogenfuss
            z0 = z_fuss - pfeiler
            for pts, weg in (([P(x0, z0, tiefe), P(x1, z0, tiefe), P(x1, z_fuss, tiefe), P(x0, z_fuss, tiefe)], hinten),
                             ([P(x0, z0, 0.0), P(x0, z_fuss, 0.0), P(x0, z_fuss, tiefe), P(x0, z0, tiefe)], P(x0 + 1, z0 + 0.5, tiefe / 2)),
                             ([P(x1, z0, 0.0), P(x1, z_fuss, 0.0), P(x1, z_fuss, tiefe), P(x1, z0, tiefe)], P(x1 - 1, z0 + 0.5, tiefe / 2)),
                             ([P(x0, z0, 0.0), P(x1, z0, 0.0), P(x1, z0, tiefe), P(x0, z0, tiefe)], P((x0 + x1) / 2, z0 + 1, tiefe / 2))):
                h.vieleck("Bruchstein", pts, [uv(a, 0) for a in (0.0, 0.1, 0.1, 0.0)], weg_von=weg)
        else:             # Bogenfuss unten geschlossen
            pts = [P(x0, z_fuss, 0.0), P(x1, z_fuss, 0.0), P(x1, z_fuss, tiefe), P(x0, z_fuss, tiefe)]
            h.vieleck("Bruchstein", pts, [uv(a, 0) for a in (0.0, 0.1, 0.1, 0.0)], weg_von=P((x0 + x1) / 2, z_fuss + 1, tiefe / 2))


def rundbogenfries(h):
    """B: Rundbogenfries aus Haustein am Erdgeschoss der Gassenseite unter dem Gesims der Auskragung (ueber Tuer- und Fenstersturz),
    Deckband darueber; zwischen den Eckquadern."""
    P = lambda s_, z_, r_: Vector((s_, M.VORN - r_, z_))
    z_band0, z_band1 = M.Z_EG_DECKE - 0.12, M.Z_EG_DECKE - 0.06
    x0, x1 = -M.BX + 0.52, M.BX - 0.52
    v0, v1, m = h.baender[WERKSTEIN]
    h.kasten("Bruchstein", (x0, M.VORN - RELIEF, z_band0), (x1, M.VORN, z_band1), WERKSTEIN, ohne=("+y",), laengs="x", ohne_streckung=GURT_WERK)
    r = 0.13
    n = int((x1 - x0) / (2 * r))
    rand = (x1 - x0 - n * 2 * r) / 2
    for k in range(n):
        bogen_relief(h, P, x0 + rand + r + 2 * r * k, z_band0 - r, r, 0.045, RELIEF - 0.01, seiten=6)


BOGEN_RUND = False   # Halbling Stufe 3 (22.11.2026): Rundbogen statt Spitzbogen im Schaugiebel (wohnhaus_halbling_s23.py setzt es)
BOGEN_H = 0.866     # Scheitelhoehe eines Bogens je Spannweite: Spitzbogen 0,866, Rundbogen 0,5 (BOGEN_RUND)
FIALE_SKALA = 1.0    # Groesse der Fialen auf dem Schaugiebel (Halbling: niedriger)
GIEBEL_LAGEN = False  # Halbling Stufe 3: Zwickel und Nischengrund der Blendnischen in Backsteinlagen (quader_flaeche) statt eine Bandhoehe auf 0,77 m


def spitzbogen(x0, x1, zs, n=4):
    """Spitzbogen (gleichseitig) ueber der Spannweite x0..x1 mit Kaempfer zs: Punkte vom linken Kaempfer ueber den Scheitel zum rechten.
    Mit BOGEN_RUND ein Halbkreis (gleich viele Punkte, Scheitel in der Mitte)."""
    w = x1 - x0
    if BOGEN_RUND:
        r, xm = w / 2, (x0 + x1) / 2
        return [(xm + r * math.cos(math.radians(180 - 90 * k / n)), zs + r * math.sin(math.radians(180 - 90 * k / n))) for k in range(2 * n + 1)]
    links = [(x1 + w * math.cos(math.radians(180 - 60 * k / n)), zs + w * math.sin(math.radians(180 - 60 * k / n))) for k in range(n + 1)]
    rechts = [(x0 + w * math.cos(math.radians(60 - 60 * k / n)), zs + w * math.sin(math.radians(60 - 60 * k / n))) for k in range(n + 1)]
    return links + rechts[1:]


def _uv(h, band):
    v0, v1, m = h.baender[band]
    return lambda x, z: (x / m, v0 + (v1 - v0) * (0.15 + 0.7 * ((z * 1.3) % 1)))


def _beschneide(punkte, p, q, behalte=None):
    """Polygon (Liste von (x, z)) an der Geraden p-q beschneiden; behalten wird die Seite, auf der `behalte` liegt (Sutherland-Hodgman, eine Gerade)."""
    seite = lambda r: (q[0] - p[0]) * (r[1] - p[1]) - (q[1] - p[1]) * (r[0] - p[0])
    vz = 1.0 if seite(behalte) >= 0 else -1.0
    aus = []
    for r, s in zip(punkte, punkte[1:] + punkte[:1]):
        dr, ds = vz * seite(r), vz * seite(s)
        if dr >= -1e-9: aus.append(r)
        if (dr > 1e-9 and ds < -1e-9) or (dr < -1e-9 and ds > 1e-9):
            tt = dr / (dr - ds)
            aus.append((r[0] + (s[0] - r[0]) * tt, r[1] + (s[1] - r[1]) * tt))
    return aus


def spitz_rahmen(h, P, x0, x1, zb, zs, breite, tiefe, band=None, n=4, ohne_kreuzung=False):
    """Gestuftes Gewaende: Rahmen aus Werkstein um eine Spitzbogenoeffnung (x0..x1, Fuss zb, Kaempfer zs), `breite` breit, `tiefe` vor
    der Ebene von P (raus); Vorderflaechen und Seitenflaechen aussen und innen."""
    band = band or WERKSTEIN
    uv = _uv(h, band)
    innen = [(x0, zb)] + spitzbogen(x0, x1, zs, n) + [(x1, zb)]
    aussen = [(x0 - breite, zb)] + spitzbogen(x0 - breite, x1 + breite, zs, n) + [(x1 + breite, zb)]
    hinten = P((x0 + x1) / 2, zs, -1.0)
    for k in range(len(innen) - 1):
        q = [aussen[k], aussen[k + 1], innen[k + 1], innen[k]]
        if ohne_kreuzung and k in (1, 2):
            # Steht neben diesem Rahmen ein zweiter (Blendmasswerk), ueberdecken sich die ersten Bogenflaechen beider ueber dem Pfosten in einem Fuenfeck
            # (Fuss: Kaempfer, Spitze: wo sich die Aussenkanten kreuzen, x = Mitte zwischen den Rahmen). Zwei Flaechen in einer Ebene: schwarz
            # (Cycles und Verdeckung, Bildpruefung 23.11.2026). Dieser Rahmen wird an der Aussenkante des Nachbarn (Spiegelbild seiner eigenen) beschnitten.
            xm = x0 - breite / 2
            sp = lambda p: (2 * xm - p[0], p[1])
            kante = (sp(aussen[k]), sp(aussen[k + 1]))
            q = _beschneide(q, kante[0], kante[1], innen[k + 1])
            if k == 2:      # Knick der Aussenkante des Nachbarn (sp(aussen[2])) liegt hinter der gemeinsamen Kante der beiden Flaechen: Zwickel dazwischen schliessen
                kn = sp(aussen[2])
                bc0, bc1 = aussen[2], innen[2]
                def _auf_bc(g0, g1):
                    d1, d2 = (bc1[0] - bc0[0], bc1[1] - bc0[1]), (g1[0] - g0[0], g1[1] - g0[1])
                    nenner = d1[0] * d2[1] - d1[1] * d2[0]
                    s = ((g0[0] - bc0[0]) * d2[1] - (g0[1] - bc0[1]) * d2[0]) / nenner
                    return (bc0[0] + d1[0] * s, bc0[1] + d1[1] * s)
                zw = [_auf_bc(sp(aussen[1]), kn), _auf_bc(kn, sp(aussen[3])), kn]
                h.vieleck("Bruchstein", [P(a, c, tiefe) for a, c in zw], [uv(a, c) for a, c in zw], weg_von=hinten)
        for m in range(1, len(q) - 1) if len(q) > 4 else ([0] if len(q) >= 3 else []):    # mehr als 4 Ecken: als Faecher aus Dreiecken
            teil = q if len(q) <= 4 else [q[0], q[m], q[m + 1]]
            h.vieleck("Bruchstein", [P(a, c, tiefe) for a, c in teil], [uv(a, c) for a, c in teil], weg_von=hinten)
        for (a0, c0), (a1, c1), ref in ((aussen[k], aussen[k + 1], (x0 + x1) / 2), (innen[k], innen[k + 1], None)):
            pts = [P(a0, c0, 0.0), P(a1, c1, 0.0), P(a1, c1, tiefe), P(a0, c0, tiefe)]
            mx, mz = (a0 + a1) / 2, (c0 + c1) / 2
            if ref is None:   # innen: Sichtseite zur Oeffnung hin
                weg = P(2 * mx - (x0 + x1) / 2 if abs(mx - (x0 + x1) / 2) > 1e-3 else mx, mz + (0.3 if mz < zs else 0.0), tiefe / 2)
            else:
                weg = P((x0 + x1) / 2, zs, tiefe / 2)
            h.vieleck("Bruchstein", pts, [uv(a0, c0), uv(a1, c1), uv(a1, c1 + 0.02), uv(a0, c0 + 0.02)], weg_von=weg)
    for xa, xb in ((x0 - breite, x0), (x1, x1 + breite)):   # Fussflaechen
        pts = [P(xa, zb, 0.0), P(xb, zb, 0.0), P(xb, zb, tiefe), P(xa, zb, tiefe)]
        h.vieleck("Bruchstein", pts, [uv(xa, 0), uv(xb, 0), uv(xb, 0.02), uv(xa, 0.02)], weg_von=P((xa + xb) / 2, zb + 1, tiefe / 2))


def kreis_ring(h, P, xm, zm, r, breite, tiefe, n=8, band=None):
    """Okulus: Kreisring aus Werkstein vor der Ebene von P."""
    band = band or WERKSTEIN
    uv = _uv(h, band)
    ri = r - breite
    pa = [(xm + r * math.cos(2 * math.pi * k / n), zm + r * math.sin(2 * math.pi * k / n)) for k in range(n + 1)]
    pi_ = [(xm + ri * math.cos(2 * math.pi * k / n), zm + ri * math.sin(2 * math.pi * k / n)) for k in range(n + 1)]
    hinten = P(xm, zm, -1.0)
    for k in range(n):
        q = [pa[k], pa[k + 1], pi_[k + 1], pi_[k]]
        h.vieleck("Bruchstein", [P(a, c, tiefe) for a, c in q], [uv(a, c) for a, c in q], weg_von=hinten)
        pts = [P(*pa[k], 0.0), P(*pa[k + 1], 0.0), P(*pa[k + 1], tiefe), P(*pa[k], tiefe)]
        h.vieleck("Bruchstein", pts, [uv(*pa[k]), uv(*pa[k + 1]), uv(pa[k + 1][0], pa[k + 1][1] + 0.02), uv(pa[k][0], pa[k][1] + 0.02)], weg_von=P(xm, zm, tiefe / 2))
        pts = [P(*pi_[k], 0.0), P(*pi_[k + 1], 0.0), P(*pi_[k + 1], tiefe), P(*pi_[k], tiefe)]
        h.vieleck("Bruchstein", pts, [uv(*pi_[k]), uv(*pi_[k + 1]), uv(pi_[k + 1][0], pi_[k + 1][1] + 0.02), uv(pi_[k][0], pi_[k][1] + 0.02)],
                  weg_von=P(xm + (pi_[k][0] - xm) * 3, zm + (pi_[k][1] - zm) * 3, tiefe / 2))


def blendnische(h, P, x0, x1, zb, zs, tiefe):
    """Spitzbogige Blendnische `tiefe` in die Mauer (Ebene von P bei raus 0): Zwickel in der Mauerflaeche (die Oeffnung der Mauer ist
    rechteckig bis zum Scheitel), Laibung aus Werkstein, Nischengrund aus Bruchstein (dunkler), davor gestuftes Gewaende, im Grund
    Blendmasswerk: gekuppelte Spitzboegen mit Okulus (Nutzer 22.11.2026)."""
    bogen = spitzbogen(x0, x1, zs)
    zsch = bogen[len(bogen) // 2][1]
    uvb, uvw = _uv(h, "bruchstein"), _uv(h, WERKSTEIN)
    vorn = P((x0 + x1) / 2, zs, 1.0)
    # Zwickel in der Mauerflaeche (Scheitelecke zuerst: Faecher von dort)
    halb = len(bogen) // 2
    for ecke, kurve in (((x0, zsch), bogen[:halb + 1]), ((x1, zsch), bogen[halb:])):
        pts2 = [ecke] + (kurve if ecke[0] == x0 else kurve[::-1])
        if GIEBEL_LAGEN:                                   # Dreiecke aus der Ecke, je an den Lagenfugen geschnitten (keine Streckung)
            for (a, c), (a2, c2) in zip(pts2[1:-1], pts2[2:]):
                quader_flaeche(h, [P(*pts2[0], 0.0), P(a, c, 0.0), P(a2, c2, 0.0)], (0, 0, 0), (1, 0, 0), zb, weg_von=P((x0 + x1) / 2, zs, -1.0))
            continue
        h.vieleck("Bruchstein", [P(a, c, 0.0) for a, c in pts2], [uvb(a, c) for a, c in pts2], weg_von=P((x0 + x1) / 2, zs, -1.0))
    # Laibung: Seiten, Bogen, Sohle
    umriss = [(x0, zb)] + bogen + [(x1, zb)]
    mx = (x0 + x1) / 2
    for (a0, c0), (a1, c1) in zip(umriss[:-1], umriss[1:]):
        pts = [P(a0, c0, 0.0), P(a1, c1, 0.0), P(a1, c1, -tiefe), P(a0, c0, -tiefe)]
        h.vieleck("Bruchstein", pts, [uvw(a0, c0), uvw(a1, c1), uvw(a1, c1 + 0.03), uvw(a0, c0 + 0.03)], weg_von=P(2 * (a0 + a1) / 2 - mx, (c0 + c1) / 2 + (0.5 if (c0 + c1) / 2 > zs else 0), -tiefe / 2))
    pts = [P(x0, zb, 0.0), P(x1, zb, 0.0), P(x1, zb, -tiefe), P(x0, zb, -tiefe)]
    h.vieleck("Bruchstein", pts, [uvw(x0, 0), uvw(x1, 0), uvw(x1, 0.03), uvw(x0, 0.03)], weg_von=P(mx, zb - 1, -tiefe / 2))
    # Nischengrund
    if GIEBEL_LAGEN:
        quader_flaeche(h, [P(a, c, -tiefe) for a, c in umriss], (0, 0, 0), (1, 0, 0), zb, weg_von=P(mx, zs, -tiefe - 1))
    else:
        h.vieleck("Bruchstein", [P(a, c, -tiefe) for a, c in umriss], [uvb(a, c) for a, c in umriss], weg_von=P(mx, zs, -tiefe - 1))
    # Gewaende davor
    spitz_rahmen(h, P, x0, x1, zb, zs, 0.05, 0.05)
    # Blendmasswerk im Grund: Mittelpfosten, zwei Spitzboegen, Okulus
    G = lambda a, c, r: P(a, c, r - tiefe)
    w = x1 - x0
    st = 0.035
    z_kb = max(zb + 0.55 * (zs - zb), zs - 0.45 * w)       # Masswerk bis unter den Scheitel (Bildpruefung 22.11.: oben sonst leer)
    for a, b_, rechts in ((x0 + st, mx - st / 2, False), (mx + st / 2, x1 - st, True)):
        spitz_rahmen(h, G, a, b_, zb, z_kb, st, 0.04, n=3, ohne_kreuzung=rechts and MASSWERK_SCHNITT)
    if BOGEN_RUND:                                          # Rundboegen: zwischen den Rahmen oberhalb ihrer Beruehrung blieb ein schwarzer Keil
        R = (mx + st / 2 - x0) / 2                          # Aussenradius jedes Rahmens (Rahmen reichen um st ueber die Oeffnung)
        xl, xr = x0 + R, x1 - R
        z_i = z_kb + math.sqrt(max(R * R - (mx - xl) ** 2, 0.0))
        z2 = z_i
        while z2 < z_kb + R * 0.95:
            z2 += 0.01
            xa, xb = xl + math.sqrt(max(R * R - (z2 - z_kb) ** 2, 0.0)), xr - math.sqrt(max(R * R - (z2 - z_kb) ** 2, 0.0))
            if xb - xa >= 0.07: break
        uvw2 = _uv(h, WERKSTEIN)
        tri = [(mx, z_i - 0.02), (xb + 0.01, z2), (xa - 0.01, z2)]
        h.vieleck("Bruchstein", [G(a, c, 0.036) for a, c in tri], [uvw2(a, c) for a, c in tri], weg_von=G(mx, z_i, -1.0))
        # Mittelpfosten bis fast zum Scheitel der Boegen: schliesst den schwarzen Keil im Kaempfer auch in den schmalen Seitenblenden
        p1, p2 = G(mx - 0.0225, z_kb - 0.01, 0.0), G(mx + 0.0225, z_kb + R * 0.9, 0.045)
        h.kasten("Bruchstein", (min(p1.x, p2.x), min(p1.y, p2.y), min(p1.z, p2.z)), (max(p1.x, p2.x), max(p1.y, p2.y), max(p1.z, p2.z)), WERKSTEIN,
                 ohne=("-z", "+y"), laengs="z")
    kreis_ring(h, G, mx, z_kb + BOGEN_H * (w / 2) + (w / 2) * 0.55, (w / 2) * 0.42, 0.03, 0.04, n=12)


def fiale(h, x, y_vorn, z, gross=False, tief=None):
    """Fiale auf der Deckplatte: Schaft und Pyramidenhelm aus Werkstein (Nutzer 22.11.2026); `tief`: Tiefe des Schafts (so tief wie
    die Mauer, sonst blieb zwischen Fiale und Dach ein schmaler Spalt, Pruefung 16)."""
    b = 0.18 if gross else 0.13
    hs, hh = (0.45, 0.55) if gross else (0.32, 0.36)
    hs, hh = hs * FIALE_SKALA, hh * FIALE_SKALA
    y0, y1 = y_vorn, y_vorn + (tief or b)
    h.kasten("Bruchstein", (x - b / 2, y0, z), (x + b / 2, y1, z + hs), WERKSTEIN, laengs="z")
    spitze = Vector((x, (y0 + y1) / 2, z + hs + hh))
    ecken = [Vector((x - b / 2 - 0.02, y0 - 0.02, z + hs)), Vector((x + b / 2 + 0.02, y0 - 0.02, z + hs)),
             Vector((x + b / 2 + 0.02, y1 + 0.02, z + hs)), Vector((x - b / 2 - 0.02, y1 + 0.02, z + hs))]
    v0, v1, m = h.baender[WERKSTEIN]
    mitte = Vector((x, (y0 + y1) / 2, z + hs + 0.05))
    for i in range(4):
        h.vieleck("Bruchstein", [ecken[i], ecken[(i + 1) % 4], spitze], [(0, v1), (0.2, v1), (0.1, v0)], weg_von=mitte)
    h.vieleck("Bruchstein", ecken[::-1], [(0, v0), (0.2, v0), (0.2, v1), (0, v1)], weg_von=mitte + Vector((0, 0, 1)))


LISENE = 0.10    # Breite der Lisenen am Schaugiebel


def gotik_plan(breiten, hoehe, zt, fenster):
    """Lisenen (x, Oberkante) ueber jeder Stufenschulter und Blendnischen (x0, x1, zb, zs) zwischen ihnen; das Fenster (a0, a1, z0, z1)
    bleibt frei, darueber eine Mittelnische."""
    lisenen = []
    for k in range(len(breiten) - 1):
        xm = breiten[k + 1]            # auf der Ecke der hoeheren Stufe (frei auf der Schulter liess die Fiale einen Lichtspalt, Pruefung 16)
        lisenen.append((xm, zt + (k + 1) * hoehe))
    nischen = []
    for k in range(1, len(lisenen) - 1):
        xa, xb = lisenen[k][0] + LISENE / 2 + 0.07, lisenen[k - 1][0] - LISENE / 2 - 0.07   # zwischen Lisene k (innen) und k-1 (aussen), Platz fuers Gewaende
        w = xb - xa
        if w < 0.24: continue
        oben = zt + (k + 1) * hoehe - 0.22                                          # unter der Deckplatte dieses Feldes
        zs = oben - BOGEN_H * w
        zb = zt + 0.22
        if zs - zb < 0.25: continue
        zsch = zs + BOGEN_H * w
        if any(xa < a1 + 0.3 and xb > a0 - 0.3 and zb < z1 + 0.3 and zsch > z0 - 0.2 for a0, a1, z0, z1 in fenster): continue   # Fenster frei
        for sx in (-1, 1):
            nischen.append((min(sx * xa, sx * xb), max(sx * xa, sx * xb), zb, zs))
    # Mittelnische ueber dem Fenster
    if fenster:
        a0, a1, z0, z1 = fenster[0]
        xi = lisenen[-1][0] - LISENE / 2 - 0.07
        w = min(2 * xi, 0.6)
        oben = zt + (len(breiten) - 1) * hoehe - 0.18
        zs = oben - BOGEN_H * w
        zb = z1 + 0.16 + BOGEN_H * (a1 - a0 + 0.12) + 0.16
        if zs - zb >= 0.2:
            nischen.append((-w / 2, w / 2, zb, zs))
    return lisenen, nischen


def fenster_gruppen(wand, luecke=0.30):
    """Fenster einer Wand, deren Abstand kleiner als `luecke` ist, als Gruppe (Spannen entlang der Wand, gleiche Hoehe)."""
    (ox, oy), (ex, ey), _, _ = WAENDE[wand]
    start = ox if ex else oy
    fen = sorted((a0 - start, a1 - start, z0, z1) for w, (a0, a1), (z0, z1), art in M.FENSTER if w == wand and art == "fenster")
    gruppen = []
    for s0, s1_, z0, z1 in fen:
        if gruppen and s0 - gruppen[-1][0][-1][1] < luecke and abs(gruppen[-1][1] - z0) < 1e-3:
            gruppen[-1][0].append((s0, s1_))
        else:
            gruppen.append(([(s0, s1_)], z0, z1))
    return gruppen


def loecher(wand):
    (ox, oy), (ex, ey), _, _ = WAENDE[wand]
    start = ox if ex else oy
    return [(a0 - start, a1 - start, z0, z1, art) for w, (a0, a1), (z0, z1), art in M.FENSTER if w == wand]


def punkt(wand, s, z, raus=0.0):
    (ox, oy), (ex, ey), _, (nx, ny) = WAENDE[wand]
    return Vector((ox + ex * s + nx * raus, oy + ey * s + ny * raus, z))


def nvec(wand):
    n = WAENDE[wand][3]
    return Vector((n[0], n[1], 0))


# ═══════════════════════════════════════════════════════════════════════════════════════════════════════════════════════
#  Aussen
# ═══════════════════════════════════════════════════════════════════════════════════════════════════════════════════════

def steinwand(h, stoff, wand, z0, z1, band="bruchstein", laibung=True, lage=None):
    """Mauer in Lagen von der Hoehe des Bandes (keine Streckung), mit den Oeffnungen der Wand; Laibung aus Stein.
    `lage`: Hoehe einer Lage in Metern, falls das Band eine Quaderreihe trennt, die kleiner ist als das Band hoch
    (Werksteinband: eine Reihe von WERK_LAGE ueber die ganze Bandhoehe)."""
    (ox, oy), (ex, ey), breite, _ = WAENDE[wand]
    v0, v1, m = h.baender[band]
    kurs = lage or (v1 - v0) * m
    l = [x[:4] for x in loecher(wand)]
    z = z0
    while z < z1 - 1e-4:
        zn = min(z + kurs, z1)
        h.wand(stoff, (ox, oy, 0), (ex, ey, 0), breite, z, zn, band, loecher=l, zbasis=z, hoehe=kurs, weg_von=MITTE, u_start=(z * 3.7) % 1)
        z = zn
    if laibung:
        for s0, s1_, za, zb in l:
            n = nvec(wand)
            mitte = punkt(wand, (s0 + s1_) / 2, (max(za, z0) + min(zb, z1)) / 2, -M.T / 2)
            za, zb = max(za, z0), min(zb, z1)
            for p, richtung, laenge in ((punkt(wand, s0, za), Vector((ex, ey, 0)), s1_ - s0), (punkt(wand, s0, zb), Vector((ex, ey, 0)), s1_ - s0),
                                        (punkt(wand, s0, za), EZ, zb - za), (punkt(wand, s1_, za), EZ, zb - za)):
                h.rechteck(stoff, p, richtung, -n, laenge, M.T, band, zu=mitte, anteil=(0.0, 0.6))


def fachwerk_lage(h, wand, z0, z1, raehm=True):
    """Echtes Fachwerk (Nutzer 21.11.2026 „sauberer bauen“): alle Hoelzer der Wand als eine Holzlage (holzlage.py) – buendig in einer
    Ebene, zugeschnitten, geschlossen. Giebelseiten: Schwelle und Raehm laufen ueber die Ecke, die Eckstaender um die Holztiefe nach
    aussen (die Traufseite stoesst dagegen, keine Luecke an der Ecke)."""
    _, _, breite, _ = WAENDE[wand]
    n3 = nvec(wand)
    b, r = M.BW, M.RIEGEL
    achsen, _ = M.raster(breite)
    oeff = [(o[0], o[1], o[2], o[3], o[4]) for o in loecher(wand)]
    fen = [(s0, s1_, za, zb) for s0, s1_, za, zb, art in oeff if art == "fenster"]
    zbr, zst = (min(f[2] for f in fen), max(f[3] for f in fen)) if fen else (z0 + M.OG_FENSTER_Z[0] - M.Z_EG_DECKE, z0 + M.OG_FENSTER_Z[1] - M.Z_EG_DECKE)
    giebelseite = wand.startswith(("vorn", "hinten"))
    e = TIEFE_HOLZ + 0.02 if giebelseite else 0.0
    lage = holzlage.Holzlage(achse=breite / 2 if giebelseite else None)
    lage.holz((-e, z0 + 0.10), (breite + e, z0 + 0.10), 0.20, "balken_c", 0, gerade=True)                   # Schwelle
    if raehm:
        lage.holz((-e, z1 - 0.09), (breite + e, z1 - 0.09), 0.18, "balken_b", 0, gerade=True)               # Raehm
    for i, s_ in enumerate(achsen):
        a, c = s_ - b / 2, s_ + b / 2
        if giebelseite and i == 0: a -= e
        if giebelseite and i == len(achsen) - 1: c += e
        lage.holz(((a + c) / 2, z0 + 0.20), ((a + c) / 2, z1 - (0.18 if raehm else 0.0)), c - a, "balken_a", 0, gerade=True)
    for k in range(len(achsen) - 1):
        g0, g1 = achsen[k] + b / 2, achsen[k + 1] - b / 2
        if not (0.30 - 1e-6 <= g1 - g0 <= 1.30 + 1e-6):                                          # lichte Gefachbreite (Nutzer 21.11.2026)
            print("HOLZLAGE Gefach", wand, f"{g1 - g0:.2f} m")
        lage.holz((g0, zbr - r / 2), (g1, zbr - r / 2), r, "balken_b", 2, gerade=True)                     # Brustriegel
        lage.holz((g0, zst + r / 2), (g1, zst + r / 2), r, "balken_b", 2, gerade=True)                     # Sturzriegel
    wuerfel = random.Random(sum(ord(c) * (i + 1) for i, c in enumerate(f"{wand}|{M.STUFE}|{getattr(M, 'FORM', 0)}")))
    fachwerk_echt.figuren(lage, list(achsen), z0, z1, oeff, M.STUFE, wuerfel, bw=b, schwelle=0.20, raehm=0.18 if raehm else 0.0,
                          z_brust=zbr - r, spiegel=giebelseite)
    befunde = lage.pruefen()
    if befunde: print("HOLZLAGE", wand, befunde[:3])
    lage.bauen(h, lambda s_, z_, r_: punkt(wand, s_, z_, r_), Vector(n3), tiefe=TIEFE_HOLZ, wand=(0.0, breite))
    LAGEN[wand] = lage


def fachwerk(h, wand, z0, z1, raehm=True):
    if FACHWERK_ECHT:
        return fachwerk_lage(h, wand, z0, z1, raehm)
    """Fachwerk einer Geschosswand nach der Konstruktionsregel (wohnhaus_mensch_s23_masse.py): Schwelle, Rahm, Staender im
    regelmaessigen Raster, Fenster genau in Gefachen zwischen Brust- und Sturzriegel, Riegel in allen anderen Gefachen auf derselben
    Hoehe, in den beiden Eckgefachen spiegelgleiche Streben (vom Eckstaender unten nach innen oben)."""
    holz = "Fachwerk"
    _, _, breite, _ = WAENDE[wand]
    n3 = nvec(wand)
    P = lambda s, z: punkt(wand, s, z)
    b, r = M.BW, M.RIEGEL
    achsen, _ = M.raster(breite)
    fen = [(s0, s1_, za, zb) for s0, s1_, za, zb, art in loecher(wand) if art == "fenster"]
    zbr, zst = (min(f[2] for f in fen), max(f[3] for f in fen)) if fen else (z0 + M.OG_FENSTER_Z[0] - M.Z_EG_DECKE, z0 + M.OG_FENSTER_Z[1] - M.Z_EG_DECKE)
    # Schwelle und Rahm an den Ecken (wie Stufe 1, UV2-Korrektur): an der Giebelseite (vorn, hinten) laufen sie um die eigene Tiefe
    # ueber die Ecke und bekommen eine Stirn, an der Traufseite enden sie buendig – sonst decken sich die Balken beider Waende an
    # der Ecke (deckungsgleiche Flaechen backen schwarz)
    kappe = wand.startswith(("vorn", "hinten"))

    def stirn(s_, zm, hoehe, tiefe, aussen):
        h.rechteck(holz, Vector(P(s_, zm - hoehe / 2)), n3, (0, 0, 1), tiefe, hoehe, "balken_c", weg_von=Vector(P(s_ - aussen, zm)))
    e = 0.07 if kappe else 0.0
    h.brett(holz, P(-e, z0 + 0.10), P(breite + e, z0 + 0.10), 0.20, n3, "balken_c", tiefe=0.07)                 # Schwelle
    if kappe: stirn(-e, z0 + 0.10, 0.20, 0.07, -1); stirn(breite + e, z0 + 0.10, 0.20, 0.07, 1)
    e = 0.06 if kappe else 0.0
    if raehm:
        h.brett(holz, P(-e, z1 - 0.09), P(breite + e, z1 - 0.09), 0.18, n3, "balken_b", tiefe=0.06)            # Rahm
        if kappe: stirn(-e, z1 - 0.09, 0.18, 0.06, -1); stirn(breite + e, z1 - 0.09, 0.18, 0.06, 1)
    for s in achsen:
        h.brett(holz, P(s, z0 + 0.20), P(s, z1 - (0.18 if raehm else 0.0)), b, n3, "balken_a", laengs="v")
    for k in range(len(achsen) - 1):
        g0, g1 = achsen[k] + b / 2, achsen[k + 1] - b / 2
        hat_fenster = any(abs(f[0] - g0) < 0.01 and abs(f[1] - g1) < 0.01 for f in fen)
        ecke = k in (0, len(achsen) - 2) and not hat_fenster and len(achsen) > 3 and not FACHWERK_ECHT   # echtes Fachwerk: Mann-Figuren
        if ecke:
            aussen, innen = (g0, g1) if k == 0 else (g1, g0)
            h.brett(holz, P(aussen, z0 + 0.22), P(innen, z1 - 0.20), 0.13, n3, "balken_a", tiefe=0.045)
            continue
        h.brett(holz, P(g0, zbr - r / 2), P(g1, zbr - r / 2), r, n3, "balken_b", tiefe=0.045)                  # Brustriegel
        h.brett(holz, P(g0, zst + r / 2), P(g1, zst + r / 2), r, n3, "balken_b", tiefe=0.045)                  # Sturzriegel


def laeden_frei(lage, s0, s1, t0, t1, breite):
    """Offene Laeden nur auf freiem Gefach (Nutzer 21.11.2026): je Seite das freie Stueck Wand neben dem Fenster in Fensterhoehe; der Laden
    ist so breit wie das halbe Fenster, hoechstens so breit wie das freie Stueck (mindestens 0,22 m), sonst entfaellt er."""
    sperren = []
    for poly, *_ in lage.hoelzer:
        zs = [q[1] for q in poly]
        if max(zs) > t0 - 0.02 and min(zs) < t1 + 0.02:
            xs = [q[0] for q in poly]
            sperren.append((min(xs), max(xs)))
    sperren.append((s0 - 0.04, s1 + 0.04))
    sperren.sort()
    frei, x = [], 0.0
    for a, e in sperren:
        if a > x: frei.append((x, a))
        x = max(x, e)
    if x < breite: frei.append((x, breite))
    halb = (s1 - s0) / 2
    aus = []
    links = [f for f in frei if f[1] <= s0 + 1e-6]
    rechts = [f for f in frei if f[0] >= s1 - 1e-6]
    if links:
        a, e = links[-1]; w = min(halb, e - a - 0.04)
        if w >= 0.22: aus.append((e - 0.02 - w, e - 0.02))
    if rechts:
        a, e = rechts[0]; w = min(halb, e - a - 0.04)
        if w >= 0.22: aus.append((a + 0.02, a + 0.02 + w))
    return aus


def oeffnungen(h):
    """Haustuer mit Vordach, Fensterrahmen, Fensterstoecke, Laeden (Eckenfarbe Rot = Akzentfarbe)."""
    holz, stein = "Fachwerk", "Bruchstein"
    for wand, (a0, a1), (z0, z1), art in M.FENSTER:
        (ox, oy), (ex, ey), breite, n = WAENDE[wand]
        n3 = nvec(wand)
        start = ox if ex else oy
        s0, s1_ = a0 - start, a1 - start
        P = lambda s, z, raus=0.0, w=wand: punkt(w, s, z, raus)
        eg = wand.endswith("_eg")
        if art == "tuer":
            for i in range(3):
                h.rechteck(holz, P(s0 + i * (s1_ - s0) / 3, z0, -0.10), (ex, ey, 0), EZ, (s1_ - s0) / 3, z1 - z0, "planken", laengs="v", weg_von=MITTE)
            # Tuergewaende und Sturz aus Stein (stehen 4 cm vor; im Steinhaus heller Sandstein wie in den Vorlagen)
            gb = WERKSTEIN if ist_stein() else "bruchstein"
            for sa, sb in ((s0 - 0.22, s0), (s1_, s1_ + 0.22)):
                wkasten(h, stein, wand, sa, sb, 0.0, 0.04, -0.12, z1, gb, ohne=("-z",), laengs="z")   # ab der Mauerflucht: nicht deckungsgleich mit der Laibung
            wkasten(h, stein, wand, s0 - 0.22, s1_ + 0.22, 0.0, 0.04, z1 + 0.005, z1 + 0.26, gb, laengs="x")
            for z in (z0 + 0.40, z1 - 0.40):
                h.rechteck(stein, P(s0 + 0.03, z - 0.035, -0.09), (ex, ey, 0), EZ, 0.66, 0.07, "eisen", weg_von=MITTE)
            h.rechteck(stein, P(s1_ - 0.22, z0 + 0.90, -0.085), (ex, ey, 0), EZ, 0.1, 0.14, "eisen", weg_von=MITTE)   # Tuerring
            if GURT_WERK:                                    # Halbling Stufe 3: die erste Stufe vor der Tuer aus hellem Werkstein, Lage ohne Streckung
                h.kasten(stein, (a0 - 0.18, M.VORN - 0.38, -0.12), (a1 + 0.18, M.VORN, M.Z_BODEN), WERKSTEIN, ohne=("-z", "+y"), ohne_streckung=True)
            else:
                h.kasten(stein, (a0 - 0.18, M.VORN - 0.38, -0.12), (a1 + 0.18, M.VORN, M.Z_BODEN), "bruchstein", ohne=("-z", "+y"))   # Stufe
            vordach(h, (a0 + a1) / 2, z1 + 0.30)
            continue
        if ist_stein() and art == "fenster":                 # Steinhaus: Kreuzstock aus Sandstein; Gewaende je Gruppe (unten)
            fensterstock(h, wand, s0, s1_, z0, z1)
            continue
        if wand.endswith(("_gb", "_tg")):
            fensterstock(h, wand, s0, s1_, z0, z1)
            ul_ = ur_ = 0.08
            if FACHWERK_ECHT and wand in LAGEN:   # echtes Fachwerk: der Brustriegel ist die Bank (buendig in der Holzlage)
                fensterstock(h, wand, s0, s1_, z0, z1)
                continue
            if FACHWERK_ECHT:   # die Bank endet buendig mit der Aussenkante des Staenders (am Nachbarfenster in der Mitte des Staenders)
                nb = [(b0 - start, b1 - start) for w2, (b0, b1), _z, art2 in M.FENSTER if w2 == wand and (b0, b1) != (a0, a1)]
                ul_ = min([M.BW] + [(s0 - n1) / 2 for n0, n1 in nb if n1 <= s0 + 1e-6])
                ur_ = min([M.BW] + [(n0 - s1_) / 2 for n0, n1 in nb if n0 >= s1_ - 1e-6])
            h.brett(holz, P(s0 - ul_, z0 - 0.055), P(s1_ + ur_, z0 - 0.055), 0.10, n3, "balken_c", tiefe=0.07)   # 5 mm unter der Oeffnung
            continue
        if eg:   # Stein: Sohlbank und Sturz aus Stein, Rahmen aus Holz
            h.kasten(stein, tuple(min(a, b) for a, b in zip(P(s0 - 0.08, z0 - 0.09, 0.07), P(s1_ + 0.08, z0 - 0.005, 0.0))),
                     tuple(max(a, b) for a, b in zip(P(s0 - 0.08, z0 - 0.09, 0.07), P(s1_ + 0.08, z0 - 0.005, 0.0))), "bruchstein", laengs="x" if ex else "y")
            h.kasten(stein, tuple(min(a, b) for a, b in zip(P(s0 - 0.12, z1 + 0.005, 0.04), P(s1_ + 0.12, z1 + 0.18, 0.0))),
                     tuple(max(a, b) for a, b in zip(P(s0 - 0.12, z1 + 0.005, 0.04), P(s1_ + 0.12, z1 + 0.18, 0.0))), "bruchstein", laengs="x" if ex else "y")
        else:
            # Fensterbank 5 mm unter dem Brustriegel; bei einem Fensterpaar (Pfeiler 17 cm) eine durchgehende Bank fuer beide Fenster –
            # zwei Baenke ueberlappten sich am Pfeiler (deckungsgleich)
            gruppe = next(g for g in fenster_gruppen(wand) if any(abs(a_ - s0) < 1e-6 for a_, _b in g[0]))
            if abs(gruppe[0][0][0] - s0) < 1e-6 and not (FACHWERK_ECHT and wand in LAGEN):   # echtes Fachwerk: Brustriegel statt Bank
                ue = M.BW if FACHWERK_ECHT else 0.12      # echtes Fachwerk: buendig mit den Aussenkanten der Staender (Bildpruefung)
                h.brett(holz, P(s0 - ue, z0 - 0.055), P(gruppe[0][-1][1] + ue, z0 - 0.055), 0.10, n3, "balken_c", tiefe=0.08)
        fensterstock(h, wand, s0, s1_, z0, z1)
        lb = (s1_ - s0) / 2
        fluegel = [(s0 - lb - 0.03, s0 - 0.03), (s1_ + 0.03, s1_ + lb + 0.03)]
        nachbarn = [(b0 - start, b1 - start) for w2, (b0, b1), _z, art2 in M.FENSTER if w2 == wand and art2 == "fenster" and (b0, b1) != (a0, a1)]
        fluegel = [(f0, f1) for f0, f1 in fluegel if not any(min(f1, n1) - max(f0, n0) > -0.25 for n0, n1 in nachbarn)]   # kein Laden vor dem Nachbarfenster
        if FACHWERK_ECHT and wand in LAGEN:   # nur auf freiem Gefach, so breit wie Platz ist (Nutzer 21.11.2026)
            fluegel = laeden_frei(LAGEN[wand], s0, s1_, z0, z1, breite)
        for f0, f1 in fluegel:
            if f0 < 0.05 or f1 > breite - 0.05: continue
            vor = 0.04 if eg else (TIEFE_HOLZ * 0.3 if FACHWERK_ECHT and wand in LAGEN else 0.085)
            h.rechteck(holz, P(f0, z0 - 0.02, vor), (ex, ey, 0), EZ, f1 - f0, z1 - z0 + 0.04, "farbholz", laengs="v", weg_von=MITTE, farbe=ROT)
            for z in (z0 + 0.12, z1 - 0.12):
                h.rechteck(stein, P(f0, z - 0.03, vor + 0.006), (ex, ey, 0), EZ, f1 - f0, 0.06, "eisen", weg_von=MITTE)
    if ist_stein():
        steinhaus_fenster(h)


GURT_WERK = False      # Halbling Stufe 3: Gurtgesims ueber dem Erdgeschoss, Gesims am Giebelfuss (vorn und hinten) und erste Stufe vor der Tuer aus hellem Werkstein
LADEN_ZUFALL = False   # Halbling Stufe 3: Laeden je Fenstergruppe nach festem Zufall (keine, links, rechts, beide), auch im Erdgeschoss
LADEN_SEED = 1         # Startwert je Variante (Grundform); aendert die Verteilung


def _laden_wahl(wand, i):
    """Feste Zufallszahl 0..5 je Fenstergruppe aus Startwert, Wandname und Nummer (keine Uhrzeit, kein hash(): bei jedem Bau gleich;
    ein Mischer wie bei Zufallsquellen, sonst gaben Nachbarwerte fast gleiche Zahlen)."""
    x = (LADEN_SEED * 2654435761 + i * 40503) & 0xFFFFFFFF
    for c in wand:
        x = ((x ^ ord(c)) * 16777619) & 0xFFFFFFFF
    x ^= x >> 15
    x = (x * 2246822519) & 0xFFFFFFFF
    x ^= x >> 13
    return x % 6


def steinhaus_fenster(h):
    """Steinhaus (Rueckmeldung des Nutzers 15.11.2026): Sandsteingewaende je Fenster bzw. Fensterpaar (nicht im Treppengiebel, der
    eigene Sohlbank und Sturz hat) und dunkle Holzlaeden an den Fenstern des Obergeschosses wie in der Vorlage wohnhaus_lvl3_v01
    (Holzakzent, kein Fachwerk; bei einem Paar nur aussen)."""
    for wand in WAENDE:
        if wand.endswith("_tg"): continue
        (ox, oy), (ex, ey), breite, n = WAENDE[wand]
        for gi, (spannen, z0, z1) in enumerate(fenster_gruppen(wand)):
            gewaende(h, wand, spannen, z0, z1)
            if not wand.endswith("_og") and not LADEN_ZUFALL: continue
            lb = min((b - a) / 2 for a, b in spannen)
            links, rechts = spannen[0][0], spannen[-1][1]
            seiten = ((links - 0.15 - lb, links - 0.15), (rechts + 0.15, rechts + 0.15 + lb))   # neben dem Gewaende
            if LADEN_ZUFALL:                                  # 0 keine, 1 nur links, 2 nur rechts, 3 bis 5 beide
                w_ = _laden_wahl(wand, gi)
                seiten = {0: (), 5: (), 1: seiten[:1], 2: seiten[1:]}.get(w_, seiten)
                if len(spannen) > 1 and len(seiten) == 1: seiten = ()      # an einem Doppelfenster nie ein einzelner Laden (Bildpruefung Runde 2)
            for f0, f1 in seiten:
                if f0 < 0.05 or f1 > breite - 0.05: continue
                if LADEN_ZUFALL and any(w2 == wand and art2 == "tuer" and min(f1, b1 - (ox if ex else oy) + 0.3) - max(f0, b0 - (ox if ex else oy) - 0.3) > 0
                                        for w2, (b0, b1), _z, art2 in M.FENSTER): continue   # nicht vor das Gewaende der Haustuer
                sx0, sx1, sy0, sy1 = M.SCHORNSTEIN_RECHTS               # kein Laden hinter dem Schornstein
                a_, b_ = punkt(wand, f0, z0), punkt(wand, f1, z0)
                if min(a_.x, b_.x) > sx0 - 0.3 and min(max(a_.y, b_.y), sy1) - max(min(a_.y, b_.y), sy0) > -0.05: continue
                if any(w2 == wand and art2 == "fenster" and min(f1, b1 - (ox if ex else oy)) - max(f0, b0 - (ox if ex else oy)) > -0.10
                       for w2, (b0, b1), _z, art2 in M.FENSTER): continue
                P = lambda s, z, raus=0.0, w=wand: punkt(w, s, z, raus)

                def laden(f0=f0, f1=f1, P=P):
                    h.rechteck("Fachwerk", P(f0, z0 - 0.02, 0.05), (ex, ey, 0), EZ, f1 - f0, z1 - z0 + 0.04, "balken_c", laengs="v", weg_von=MITTE)
                    for z in (z0 + 0.12, z1 - 0.12):
                        h.rechteck("Bruchstein", P(f0, z - 0.03, 0.056), (ex, ey, 0), EZ, f1 - f0, 0.06, "eisen", weg_von=MITTE)
                holz_akzent(h, laden)


def wkasten(teil, stoff, wand, s0, s1_, r0, r1, z0, z1, band, **kw):
    """Kasten an einer Wand: von s0 bis s1 entlang der Wand, von r0 bis r1 nach aussen (negativ: nach innen)."""
    a, b = punkt(wand, s0, z0, r0), punkt(wand, s1_, z1, r1)
    teil.kasten(stoff, (min(a.x, b.x), min(a.y, b.y), min(a.z, b.z)), (max(a.x, b.x), max(a.y, b.y), max(a.z, b.z)), band, **kw)


def fensterstock(h, wand, s0, s1_, z0, z1):
    """Fensterstock wie in Stufe 1: Rahmen 5 x 7 cm und Holzkreuz, mittig in der Wand (innen und aussen dasselbe Holz); im Steinhaus
    ein steinerner Kreuzstock (Haustein)."""
    st, band = ("Bruchstein", WERKSTEIN) if ist_stein() else ("Fachwerk", "balken_c")
    n3 = nvec(wand)
    P = lambda s, z: punkt(wand, s, z, -M.T / 2)
    sm, zm = (s0 + s1_) / 2, (z0 + z1) / 2
    for a, b in ((P(s0, z0 + 0.025), P(s1_, z0 + 0.025)), (P(s0, z1 - 0.025), P(s1_, z1 - 0.025)),
                 (P(s0 + 0.025, z0 + 0.05), P(s0 + 0.025, z1 - 0.05)), (P(s1_ - 0.025, z0 + 0.05), P(s1_ - 0.025, z1 - 0.05))):
        h.stab(st, a, b, 0.07, band, breite=0.05, oben=n3)
    h.stab(st, P(sm, z0 + 0.05), P(sm, z1 - 0.05), 0.055, band, breite=0.045, oben=n3)
    h.stab(st, P(s0 + 0.05, zm), P(s1_ - 0.05, zm), 0.06, band, breite=0.045, oben=n3)   # 5 mm dicker als der Pfosten: am Kreuz nicht deckungsgleich


def ziegelflaeche(d, stoff, unten, oben, ys, saat, lage=M.LAGE, band="stroh_franse", stufe=0.03, weg_von=None):
    """Dachflaeche aus Lagen von `lage` Metern (jede fuellt das Band; kleine Stufe am Fuss jeder Lage): `unten`, `oben` sind
    Funktionen y -> Punkt an der Traufe bzw. am First; ys die Grenzen entlang des Firsts (Anfang, Ende). Die Lagen zeigen von
    `weg_von` weg (Standard die Hausmitte MITTE; ein Dach unterhalb von MITTE braucht einen Punkt unter sich, siehe schuppen)."""
    weg_von = MITTE if weg_von is None else Vector(weg_von)
    v0, v1, m = d.baender[band]
    y0, y1 = ys
    a, b = unten(y0), oben(y0)
    laenge = (b - a).length
    n = max(1, round(laenge / lage))
    hang = (b - a).normalized()
    normale = (unten(y1) - a).normalized().cross(hang)
    if normale.z < 0: normale = -normale
    u0 = d.zufall.random()
    for k in range(n):
        t0, t1 = k / n, (k + 1) / n
        p0, p1 = a.lerp(b, t0), a.lerp(b, t1)
        q0, q1 = unten(y1).lerp(oben(y1), t0), unten(y1).lerp(oben(y1), t1)
        hub = normale * stufe
        uu = u0 + 0.37 * k
        lu = (q0 - p0).length / m
        d.vieleck(stoff, [p0 + hub, q0 + hub, q1, p1], [(uu, v1), (uu + lu, v1), (uu + lu, v0), (uu, v0)], weg_von=weg_von)
        d.vieleck(stoff, [p0, q0, q0 + hub, p0 + hub], [(uu, v1 - 0.002), (uu + lu, v1 - 0.002), (uu + lu, v1 - 0.01), (uu, v1 - 0.01)], weg_von=MITTE + EZ * 30)


def baue_dach(stoff="Dachziegel", first="Firstziegel"):
    d = Teil("Dach", BA, saat=23)
    xe = M.OX + M.UEBER
    yv, yh = M.OVORN - M.UEBER_ORTGANG_VORN, M.HINTEN + M.UEBER_ORTGANG
    dick = 0.10
    zf = M.Z_FIRST + dick
    for seite in (-1, 1):
        unten = lambda y, s=seite: Vector((s * xe, y, M.z_dach(xe) + dick))
        oben = lambda y: Vector((0.0, y, zf))
        ziegelflaeche(d, stoff, unten, oben, (yv, yh), saat=seite)
        # Untersicht (Bretter) und Stirn der Deckung an Traufe und Ortgang
        d.vieleck("Fachwerk", [Vector((seite * xe, yv, M.z_dach(xe))), Vector((seite * xe, yh, M.z_dach(xe))), Vector((0, yh, M.Z_FIRST)), Vector((0, yv, M.Z_FIRST))],
                  [(0, BA["planken"][1]), ((yh - yv) / 4, BA["planken"][1]), ((yh - yv) / 4, BA["planken"][0]), (0, BA["planken"][0])], zu=(0, 0, 0))
        d.vieleck(stoff, [Vector((seite * xe, yv, M.z_dach(xe))), Vector((seite * xe, yh, M.z_dach(xe))), unten(yh), unten(yv)],
                  [(0, 0.24), (2, 0.24), (2, 0.25), (0, 0.25)], weg_von=MITTE)
        for y, aussen in ((yv, -1), (yh, 1)):
            if M.STUFE == 3 and aussen < 0: continue           # vorn steckt der Ortgang im Treppengiebel (verdeckt, unbelichtet)
            d.vieleck(stoff, [Vector((seite * xe, y, M.z_dach(xe))), unten(y), oben(y), Vector((0, y, M.Z_FIRST))],
                      [(0, 0.24), (0.2, 0.24), (0.2, 0.25), (0, 0.25)], weg_von=(0, y - aussen, 4))
            # Ortgangbrett (im Steinhaus Holzakzent der Traufe, bleibt Holz)
            holz_akzent(d, lambda y=y, aussen=aussen, seite=seite: d.brett(
                "Fachwerk", Vector((seite * (xe + 0.02), y + aussen * (0.01 + 0.005 * (seite > 0)), M.z_dach(xe) - 0.06)),
                Vector((0, y + aussen * (0.01 + 0.005 * (seite > 0)), M.Z_FIRST - 0.06)), 0.20,   # rechts 5 mm weiter vorn: am First nicht deckungsgleich
                (0, aussen, 0), "balken_c", tiefe=0.04))
        # Sparrenkoepfe unter der Traufe (Holzakzent)
        for j in range(7):
            y = yv + 0.4 + j * (yh - yv - 0.8) / 6
            xi, xa = seite * (M.OX + 0.08), seite * (xe - 0.06)   # 8 cm vor der Wand (nicht in ihr: sonst backt die Unterseite schwarz)
            holz_akzent(d, lambda xi=xi, xa=xa, y=y: d.stab("Fachwerk", (xi, y, M.z_dach(xi) - 0.07), (xa, y, M.z_dach(xa) - 0.07), 0.12, "balken_c", breite=0.10))
    # First: Firstziegel als flaches Dreiecksprisma
    for seite in (-1, 1):
        a, b = Vector((0, yv - 0.02, zf + 0.11)), Vector((0, yh + 0.02, zf + 0.11))
        c, e = Vector((seite * 0.17, yh + 0.02, zf - 0.17 * M.NEIGUNG + 0.06)), Vector((seite * 0.17, yv - 0.02, zf - 0.17 * M.NEIGUNG + 0.06))
        v0, v1, mm = BA["stroh"]
        d.vieleck(first, [e, c, b, a], [(0, v1), ((yh - yv) / mm, v1), ((yh - yv) / mm, v1 - 0.03), (0, v1 - 0.03)], weg_von=MITTE)
    for y, aussen in ((yv - 0.02, -1), (yh + 0.02, 1)):
        d.vieleck(first, [Vector((-0.17, y, zf - 0.17 * M.NEIGUNG + 0.06)), Vector((0.17, y, zf - 0.17 * M.NEIGUNG + 0.06)), Vector((0, y, zf + 0.11))],
                  [(0.1, 0.18), (0.2, 0.18), (0.15, 0.16)], weg_von=(0, y - aussen, zf))
    gaube(d, stoff)
    if M.STUFE == 3 and DACH_HINTER:
        print(f"DACH Vorderkante hinter den Nischengrund: {dach_hinter_nischen(d)} Flaechen")
    return d


DACH_HINTER = False       # S23.main schaltet es fuer Stufe 3 ein, fuer jeden Aufrufer (Mensch, Zwerg, Halbling, Elb); --dach-alt: wie vorher (die Elben setzen es, ihr Schildgiebel hat keine Nischen)


def dach_hinter_nischen(d):
    """Steinhaus: Die Vorderkante des Dachs (Deckung, Untersicht, Firstblech; yv = OVORN + 0,12) stand 2 cm vor dem Grund der
    Blendnischen im Schaugiebel (Grund bei OVORN - 0,04 + 0,18 = OVORN + 0,14) und zeichnete als duenne schraege dunkle Linien durch
    die Seitenblenden (Schiefer blau, Holz braun; wie beim Halbling, dort Bildpruefung Runde 2). Jetzt liegt sie 2 cm dahinter, in
    der Mauer des Giebels (36 cm dick). Gaube und Fluegel (y weiter hinten) bleiben unberuehrt."""
    grenze = M.OVORN + 0.16
    n = 0
    for i, (pts, uv, st, fa, gl) in enumerate(d.flaechen):
        if min(p.y for p in pts) < grenze - 1e-6:
            d.flaechen[i] = ([Vector((p.x, max(p.y, grenze), p.z)) for p in pts], uv, st, fa, gl)
            n += 1
    return n


MASSWERK_SCHNITT = False  # Stufe 3 (S23.main, fuer jeden Aufrufer): die Rahmen des Blendmasswerks ueberdecken sich nicht mehr; --schnitt-alt: wie vorher


GAUBE = dict(y=(-0.75, 0.45), x_front=1.75, z_fuss=None, fenster=(0.55, 0.62))
# Patrizierhaus (Mensch S3, Nutzer 22.11.2026): Gaube hinter dem Schornstein (er steht aussen an der rechten Wand bei y -0,70..0,20 und
# verstellte der Gaube den Blick) und mit Stirn, Giebel und Wangen aus der Quaderreihe des Werksteinbands (wie das Obergeschoss).
# Gilt nur fuer den Einstieg der Menschen (main); Zwerge und Halblinge bauen die Gaube unveraendert.
GAUBE_PATRIZIER = dict(y=(0.45, 1.65), quader=True)
# Stein des Obergeschosses und der Gaube: Mensch heller Haustein in Quaderlagen (WERKSTEIN, WERK_LAGE); Halbling Stufe 3 roter Backstein
# im Band "bruchstein" (lage None: eine Lage so hoch wie das Band, keine Streckung; wohnhaus_halbling_s23.py setzt es).
OBEN = dict(band=WERKSTEIN, lage=WERK_LAGE)


def _oben(h):
    """Band und Lagenhoehe fuer Obergeschoss und Gaube im Steinhaus."""
    band, lage = OBEN["band"], OBEN["lage"]
    if lage is None:
        v0, v1, m = h.baender[band]
        lage = (v1 - v0) * m
    return band, lage


def _quader_schnitt(pts, za, zb):
    """Schneidet das Vieleck `pts` auf den Streifen za <= z <= zb (Sutherland-Hodgman, zwei Halbebenen)."""
    for grenze, oben in ((za, False), (zb, True)):
        aus, n = [], len(pts)
        for i in range(n):
            a, b = pts[i], pts[(i + 1) % n]
            ina = (a.z <= grenze + 1e-9) if oben else (a.z >= grenze - 1e-9)
            inb = (b.z <= grenze + 1e-9) if oben else (b.z >= grenze - 1e-9)
            if ina: aus.append(a)
            if ina != inb:
                t = (grenze - a.z) / (b.z - a.z)
                aus.append(a + (b - a) * t)
        pts = aus
        if len(pts) < 3: return []
    return pts


def quader_flaeche(d, pts, o, eu, z_unten, **kw):
    """Steinflaeche aus Quaderlagen (WERK_LAGE) im Band WERKSTEIN: das Vieleck wird an den Lagenfugen (ab `z_unten`) geteilt und jede Lage
    zeigt die ganze Bandhoehe, keine Streckung (wie steinwand). `o`, `eu`: Ursprung und Richtung der Breite (UV.x)."""
    pts = [Vector(p) for p in pts]
    o, eu = Vector(o), Vector(eu).normalized()
    lo, hi = min(p.z for p in pts), max(p.z for p in pts)
    band, lage = _oben(d)
    k = int(math.floor((lo - z_unten) / lage + 1e-9))
    z = z_unten + k * lage
    while z < hi - 1e-6:
        teil = _quader_schnitt(pts, z, z + lage)
        if len(teil) >= 3 and max(p.z for p in teil) - min(p.z for p in teil) > 1e-4:
            d.welt("Bruchstein", teil, band, o - eu * ((z * 3.7) % 1), eu, z, lage, **kw)
        z += lage


def gaube(d, stoff):
    """Schleppgaube? Nein: kleine Giebelgaube auf der rechten Dachseite (Vorlage lvl2_v01): Stirn mit Fenster, Wangen, Satteldach."""
    y0, y1 = GAUBE["y"]
    xf = GAUBE["x_front"]
    zb = M.z_dach(xf) + 0.10                    # Fuss der Stirn auf der Dachflaeche
    zt = zb + 1.05                              # Traufe der Gaube
    ym = (y0 + y1) / 2
    hw = (y1 - y0) / 2
    zfirst = zt + hw * 1.0
    x_hinten = M.OX - (zt - M.Z_TRAUFE) / M.NEIGUNG    # wo die Traufhoehe der Gaube die Dachflaeche trifft
    # Stirnwand (Putz) mit Fenster und Rahmen
    fb, fh = GAUBE["fenster"]
    fz0 = zb + 0.22
    o = Vector((xf, y0, 0))
    stein = ist_stein() and GAUBE.get("quader")
    band, lage = _oben(d)
    if stein:       # Patrizierhaus: Stirn, Giebelfeld und Wangen in Quaderlagen des Werksteinbands (wie steinwand), Laibung aus Stein
        loch = (hw - fb / 2, hw + fb / 2, fz0, fz0 + fh)
        z = zb
        while z < zt - 1e-4:
            zn = min(z + lage, zt)
            d.wand("Bruchstein", o, (0, 1, 0), y1 - y0, z, zn, band, loecher=[loch], zbasis=z, hoehe=lage, weg_von=MITTE, u_start=(z * 3.7) % 1)
            z += lage
        mitte = Vector((xf - 0.06, ym, (fz0 + fz0 + fh) / 2))
        n = Vector((1, 0, 0))
        for p, richtung, laenge in ((Vector((xf, y0 + loch[0], fz0)), Vector((0, 1, 0)), fb), (Vector((xf, y0 + loch[0], fz0 + fh)), Vector((0, 1, 0)), fb),
                                    (Vector((xf, y0 + loch[0], fz0)), EZ, fh), (Vector((xf, y0 + loch[1], fz0)), EZ, fh)):
            d.rechteck("Bruchstein", p, richtung, -n, laenge, 0.12, WERKSTEIN, zu=mitte, anteil=(0.0, 0.6))
        quader_flaeche(d, [(xf, y0, zt), (xf, y1, zt), (xf, ym, zfirst)], o, (0, 1, 0), zb, weg_von=MITTE)
        if GAUBE.get("rahmen"):                              # Halbling: Werkstein-Gewaende und Sohlbank (ein Holzrahmen in der Laibung gab bei Pruefung 12 deckungsgleiche Flaechen und Schwarz)
            yl, yr = y0 + loch[0], y0 + loch[1]
            ob = fz0 + fh
            for ya, yb, za, zb2 in ((yl - 0.09, yl, fz0 - 0.005, ob), (yr, yr + 0.09, fz0 - 0.005, ob), (yl - 0.09, yr + 0.09, ob, ob + 0.12)):   # Pfosten enden an Bank und Sturz (keine deckungsgleichen Flaechen)
                d.kasten("Bruchstein", (xf, ya, za), (xf + 0.04, yb, zb2), WERKSTEIN, laengs="z" if zb2 - za > yb - ya else "y")
            d.kasten("Bruchstein", (xf, yl - 0.11, fz0 - 0.08), (xf + 0.07, yr + 0.11, fz0 - 0.005), WERKSTEIN, laengs="y")
    else:
        d.wand("Lehmputz", o, (0, 1, 0), y1 - y0, zb, zt, "putz", loecher=[(hw - fb / 2, hw + fb / 2, fz0, fz0 + fh)], zbasis=zb, hoehe=1.2,
               laibung=("Fachwerk", "balken_c"), laibung_tiefe=0.12, weg_von=MITTE)
        d.welt("Lehmputz", [(xf, y0, zt), (xf, y1, zt), (xf, ym, zfirst)], "putz", (xf, y0, 0), (0, 1, 0), zt, zfirst - zt, weg_von=MITTE)
    n3 = (1, 0, 0)
    if stein:
        pass            # Steinhaus: kein Holz an der Stirn (Staender und Riegel entfielen schon bisher beim Versteinern)
    elif FACHWERK_ECHT:   # Stirn der Gaube als eine Holzlage (buendig, geschlossen; Nutzer 21.11.2026)
        lage = holzlage.Holzlage()
        lage.holz((y0, zb + 0.06), (y1, zb + 0.06), 0.12, "balken_c", 0, gerade=True)
        lage.holz((y0, zt - 0.05), (y1, zt - 0.05), 0.12, "balken_b", 0, gerade=True)
        for y in (y0 + 0.06, y1 - 0.06):
            lage.holz((y, zb + 0.12), (y, zt - 0.11), 0.12, "balken_a", 0, gerade=True)
        befunde = lage.pruefen()
        if befunde: print("HOLZLAGE gaube", befunde[:3])
        lage.bauen(d, lambda s_, z_, r_: Vector((xf + r_, s_, z_)), Vector(n3), tiefe=TIEFE_HOLZ, wand=(y0, y1))
    else:
        for y in (y0 + 0.06, y1 - 0.06):
            d.brett("Fachwerk", (xf, y, zb), (xf, y, zt), 0.12, n3, "balken_a", laengs="v", tiefe=0.04)
        d.brett("Fachwerk", (xf, y0, zt - 0.05), (xf, y1, zt - 0.05), 0.12, n3, "balken_b", tiefe=0.05)
        d.brett("Fachwerk", (xf, y0, zb + 0.06), (xf, y1, zb + 0.06), 0.12, n3, "balken_c", tiefe=0.05)
    for s0, s1_ in (((ym - fb / 2), (ym + fb / 2)),):
        P = lambda y, z: Vector((xf - 0.06, y, z))
        for a, b in ((P(s0, fz0 + 0.025), P(s1_, fz0 + 0.025)), (P(s0, fz0 + fh - 0.025), P(s1_, fz0 + fh - 0.025)),
                     (P(s0 + 0.025, fz0), P(s0 + 0.025, fz0 + fh)), (P(s1_ - 0.025, fz0), P(s1_ - 0.025, fz0 + fh)), (P(ym, fz0), P(ym, fz0 + fh))):
            d.stab("Fachwerk", a, b, 0.06 if abs(a.z - b.z) > 0.1 else 0.065, "balken_c", breite=0.05, oben=n3)   # Querhoelzer 5 mm dicker: an den Ecken nicht deckungsgleich
    # Wangen (Dreiecke bis zur Dachflaeche)
    for y in (y0, y1):
        wange = [(xf, y, zb), (xf, y, zt), (x_hinten, y, zt)]
        if stein: quader_flaeche(d, wange, (xf, y, 0), (-1, 0, 0), zb, weg_von=Vector((xf - 0.5, ym, zt)))
        else: d.welt("Lehmputz", wange, "putz", (xf, y, 0), (-1, 0, 0), zb, zt - zb, weg_von=Vector((xf - 0.5, ym, zt)))
    # Satteldach der Gaube (Ziegel), First nach hinten bis zur Dachflaeche
    x_first_hinten = M.OX - (zfirst - M.Z_TRAUFE) / M.NEIGUNG
    for seite in (-1, 1):
        ya = ym + seite * (hw + 0.14)
        unten = lambda x, ya=ya: Vector((x, ya, zt - 0.12))
        a, b = Vector((xf + 0.18, ya, zt - 0.10)), Vector((xf + 0.18, ym, zfirst + 0.08))
        c, e = Vector((x_first_hinten, ym, zfirst + 0.08)), Vector((x_hinten - 0.1, ya, zt - 0.10))
        v0, v1, mm = BA["stroh"]
        d.vieleck(stoff, [a, e, c, b], [(0, v1), ((a - e).length / mm, v1), ((a - e).length / mm, v1 - 0.12), (0, v1 - 0.12)], weg_von=Vector((xf - 0.3, ym, zt - 0.4)))
    return d


def vordach(h, x, z):
    """Vordach ueber der Haustuer: zwei Knaggen, Satteldach aus Ziegeln (Vorlage lvl2_v01)."""
    y = M.VORN
    tiefe, b = 0.62, 0.72
    for dx in (-b + 0.08, b - 0.08):
        a, bb, c = Vector((x + dx, y, z - 0.55)), Vector((x + dx, y, z)), Vector((x + dx, y - tiefe, z))
        h.stab("Fachwerk", a + Vector((0, -0.02, 0)), c + Vector((0, 0.05, 0.02)), 0.08, "balken_c")
        h.stab("Fachwerk", bb + Vector((0, -0.02, 0)), c, 0.09, "balken_c")
    zf = z + 0.42
    for seite in (-1, 1):
        pts = [Vector((x + seite * (b + 0.06), y - tiefe - 0.08, z - 0.02)), Vector((x + seite * (b + 0.06), y, z - 0.02)), Vector((x, y, zf)), Vector((x, y - tiefe - 0.08, zf))]
        v0, v1, mm = BA["stroh"]
        h.vieleck("Dachziegel", pts, [(0, v1), (tiefe / mm, v1), (tiefe / mm, v1 - 0.05), (0, v1 - 0.05)], weg_von=Vector((x, y - 0.3, z - 1)))
        h.vieleck("Fachwerk", [pts[1], pts[0], pts[3], pts[2]], [(0.1, 0.78), (0.3, 0.78), (0.3, 0.70), (0.1, 0.70)], zu=Vector((x, y - 0.3, z - 1)))
    h.vieleck("Lehmputz", [Vector((x - b, y - tiefe, z - 0.01)), Vector((x + b, y - tiefe, z - 0.01)), Vector((x, y - tiefe, zf - 0.03))], [(0.1, 0.69), (0.5, 0.69), (0.3, 0.55)], weg_von=Vector((x, y, z)))


def schornstein(h, x0, x1, y0, y1, z_unten, z_oben, band="bruchstein", stoff="Bruchstein"):
    v0, v1, m = BA[band]
    kurs = (v1 - v0) * m
    z = z_unten
    while z < z_oben - 1e-3:
        zn = min(z + kurs, z_oben)
        # unter der Auskragung reicht der Schornstein bis an die Mauer des Erdgeschosses (sonst bleibt ein schmaler, dunkler Spalt
        # mit einer kaum belichteten Innenseite); die Lage an der Decke des Erdgeschosses wird dort geteilt
        if x0 > M.BX + 1e-3 and z < M.Z_EG_DECKE - 1e-3 < zn:
            zn = M.Z_EG_DECKE
        xi = M.BX if x0 > M.BX + 1e-3 and zn <= M.Z_EG_DECKE + 1e-6 else x0
        h.kasten(stoff, (xi, y0, z), (x1, y1, zn), band, ohne=("-z", "+z"), laengs="x")
        z = zn
    h.kasten(stoff, (x0 - 0.05, y0 - 0.05, z_oben), (x1 + 0.05, y1 + 0.05, z_oben + 0.12), band, laengs="x")
    h.rechteck(stoff, (x0 + 0.08, y0 + 0.08, z_oben + 0.125), (1, 0, 0), (0, 1, 0), x1 - x0 - 0.16, y1 - y0 - 0.16, "eisen", zu=(0, 0, 99), anteil=(0.0, 0.2))


def giebel(h, wand, y, aussen):
    """Fachwerkgiebel ueber der Traufe bis zum First, mit dem Giebelfenster: Putzflaechen und Holz."""
    lehm, holz = "Lehmputz", "Fachwerk"
    fen = [(a0, a1, z0, z1) for w, (a0, a1), (z0, z1), art in M.FENSTER if w == wand]
    zt, zf = M.Z_TRAUFE, M.Z_FIRST
    xk = lambda z: M.OX - (z - zt) / M.NEIGUNG
    o, eu = (-M.OX, y, 0), (1, 0, 0)
    stufen = [zt] + [z for f in fen for z in (f[2], f[3])] + [zf]
    for za, zb in zip(stufen[:-1], stufen[1:]):
        if zb - za < 1e-4: continue
        xa, xb = xk(za), xk(zb)
        loch = [f for f in fen if f[2] <= za + 1e-4 and f[3] >= zb - 1e-4]
        if not loch:
            pts = [(-xa, y, za), (xa, y, za), (xb, y, zb), (-xb, y, zb)] if xb > 1e-3 else [(-xa, y, za), (xa, y, za), (0, y, zb)]
            h.welt(lehm, pts, "putz", o, eu, za, zf - za, weg_von=MITTE)
            continue
        loch.sort()
        h.welt(lehm, [(-xa, y, za), (loch[0][0], y, za), (loch[0][0], y, zb), (-xb, y, zb)], "putz", o, eu, za, zf - za, weg_von=MITTE)
        for l1, l2 in zip(loch[:-1], loch[1:]):                 # Putz zwischen zwei Fenstern derselben Hoehe
            h.welt(lehm, [(l1[1], y, za), (l2[0], y, za), (l2[0], y, zb), (l1[1], y, zb)], "putz", o, eu, za, zf - za, weg_von=MITTE)
        h.welt(lehm, [(loch[-1][1], y, za), (xa, y, za), (xb, y, zb), (loch[-1][1], y, zb)], "putz", o, eu, za, zf - za, weg_von=MITTE)
        n = Vector((0, aussen, 0))
        for a0, a1, *_ in loch:
            for p, richtung, laenge in (((a0, y, za), Vector((1, 0, 0)), a1 - a0), ((a0, y, zb), Vector((1, 0, 0)), a1 - a0), ((a0, y, za), EZ, zb - za), ((a1, y, za), EZ, zb - za)):
                h.rechteck(holz, p, richtung, -n, laenge, 0.14, "balken_c", zu=Vector(((a0 + a1) / 2, y - aussen * 0.07, (za + zb) / 2)), anteil=(0.0, 0.6))
    n3 = (0, aussen, 0)
    if FACHWERK_ECHT:
        return giebel_lage(h, wand, y, aussen, fen, xk)
    # Giebelfachwerk nach der Konstruktionsregel: dasselbe Staenderraster wie das Geschoss darunter, Giebelschwelle, Fenster in den
    # beiden Gefachen an der Mittelachse zwischen Brustriegel und Kehlbalken (der Kehlbalken ist ihr Sturz), Staender bis unter die
    # Giebelsparren, darueber Hahnenbalken und Spitzsaeule bis zum First; Streben in den aeusseren Gefachen; spiegelgleich.
    b, r = M.BW, M.RIEGEL
    achsen = [s - M.OX for s in M.raster(2 * M.OX)[0]]
    z_br = min([f[2] for f in fen] or [zt + 0.75])
    z_kb = max([f[3] for f in fen] or [zt + 1.50])
    zk = z_kb + 0.08                                                # Kehlbalken (0,16 hoch), Unterkante = Sturz der Fenster
    zh = zk + 0.55 * (zf - zk)                                      # Hahnenbalken
    dach = lambda x: M.z_dach(x) - 0.10                             # Unterkante der Giebelsparren an der Stelle x
    h.brett(holz, (-M.OX - 0.02, y, zt + 0.08), (M.OX + 0.02, y, zt + 0.08), 0.18, n3, "balken_c", tiefe=0.07)   # Giebelschwelle
    for s in (-1, 1):
        h.brett(holz, (s * (M.OX + 0.02), y, zt + 0.02), (0.0, y, zf - 0.02), 0.16, n3, "balken_c", tiefe=0.075 if s < 0 else 0.08)   # Giebelsparren (vor der Schwelle, rechts vor links)
    h.brett(holz, (-xk(zk), y, zk), (xk(zk), y, zk), 0.16, n3, "balken_b", tiefe=0.055)                          # Kehlbalken (vor der Spitzsaeule)
    h.brett(holz, (-xk(zh), y, zh), (xk(zh), y, zh), 0.15, n3, "balken_b", tiefe=0.06)                           # Hahnenbalken
    h.brett(holz, (0.0, y, zt + 0.16), (0.0, y, zf - 0.05), b, n3, "balken_a", laengs="v")                      # Mittelstaender und Spitzsaeule
    for x in achsen:
        if abs(x) < 1e-6 or dach(abs(x)) - (zt + 0.16) < 0.35: continue
        oben = min(dach(abs(x)), zk - 0.08) if abs(x) < xk(zk) else dach(abs(x))
        h.brett(holz, (x, y, zt + 0.16), (x, y, oben), b, n3, "balken_a", laengs="v")                           # Staender bis unter den Sparren
    innen = [x for x in achsen if 0 < x < xk(zk)]
    aussen_x = [x for x in achsen if x > 0 and dach(x) - (zt + 0.16) >= 0.35]
    for s in (-1, 1):
        # Brustriegel unter den Fenstern bis zum letzten Staender, der unter den Kehlbalken reicht
        x_bis = max(innen) if innen else 0.0
        h.brett(holz, (s * b / 2, y, z_br - r / 2), (s * (x_bis - b / 2), y, z_br - r / 2), r, n3, "balken_b", tiefe=0.045)
        # aeussere Gefache: Strebe vom Staender unten nach aussen oben bis unter den Sparren (spiegelgleich)
        rand = [x for x in aussen_x if x > x_bis + 1e-6]
        for x in rand:
            x2 = x + 0.55
            h.brett(holz, (s * (x - b / 2), y, zt + 0.22), (s * x2, y, dach(x2) - 0.08), 0.12, n3, "balken_a", tiefe=0.04)
        # ueber dem Kehlbalken: Strebe von dessen Ende zur Spitzsaeule unter dem Hahnenbalken
        h.brett(holz, (s * (xk(zk) - 0.15), y, zk + 0.10), (s * (b / 2), y, zh - 0.10), 0.12, n3, "balken_a", tiefe=0.04)


def giebel_lage(h, wand, y, aussen, fen, xk):
    """Giebelfachwerk als Holzlage (echtes Fachwerk): Giebelschwelle, Giebelsparren, Mittelstaender, Kehl- und Hahnenbalken, Staender,
    Brustriegel, Streben – alle buendig, Staender und Streben genau unter den Sparren zugeschnitten; spiegelgleich (Pruefung 2)."""
    from holzlage import kasten, schnitt, schneide
    zt, zf = M.Z_TRAUFE, M.Z_FIRST
    b, r = M.BW, M.RIEGEL
    achsen = [s_ - M.OX for s_ in M.raster(2 * M.OX)[0]]
    z_br = min([f[2] for f in fen] or [zt + 0.75])
    z_kb = max([f[3] for f in fen] or [zt + 1.50])
    zk = z_kb + 0.08
    zh = zk + 0.55 * (zf - zk)
    x0 = M.OX + 0.02
    # Giebelsparren: Mittellinie von (+-x0, zt + 0,02) zum First, 0,16 breit; innen darunter liegt das Feld der uebrigen Hoelzer
    innen = kasten(-x0, x0, zt, zf + 1.0)
    for sg in (-1, 1):
        ax, az, bx, bz = sg * x0, zt + 0.02, 0.0, zf - 0.02
        dx, dz = bx - ax, bz - az
        l = math.hypot(dx, dz); dx, dz = dx / l, dz / l
        nx, nz = (dz, -dx) if sg > 0 else (-dz, dx)         # nach innen unten
        if nz > 0: nx, nz = -nx, -nz
        p0 = (ax + nx * 0.08, az + nz * 0.08)
        innen = schneide(innen, p0, (nx, nz))
    # Umriss: das Giebeldreieck ab der Unterkante der Giebelschwelle (darunter liegt das Raehm des Obergeschosses, eine eigene Lage)
    lage = holzlage.Holzlage(achse=0.0, umriss=[(-x0 - 0.3, zt), (x0 + 0.3, zt), (0.0, zf + 0.3)])
    lage.holz((-x0, zt + 0.09), (x0, zt + 0.09), 0.18, "balken_c", 0, gerade=True)                            # Giebelschwelle (auf dem Raehm)
    for sg in (-1, 1):
        lage.holz((sg * x0, zt + 0.02), (0.0, zf - 0.02), 0.16, "balken_c", 0, gerade=True, zuschnitt=kasten(-x0 - 0.5, x0 + 0.5, zt, zf + 1.0))   # Giebelsparren: ab der Traufe abgeschnitten (die Fussspitze ragte 3 cm unter zt ins Raehm der anderen Holzlage: deckungsgleich, Pruefer 23.11.2026)
    aus = GIEBEL_AUSSPARUNG.get(wand)
    for za_, zb_ in (((zt, aus[0]), (aus[1], zf + 1.0)) if aus else ((zt, zf + 1.0),)):
        if zb_ - za_ > 0.05:
            lage.holz((0.0, zt + 0.16), (0.0, zf), b, "balken_a", 0, zuschnitt=schnitt(innen, kasten(-x0, x0, za_, zb_)))   # Mittelstaender, Spitzsaeule
    lage.holz((-x0, zk), (x0, zk), 0.16, "balken_b", 1, zuschnitt=innen)                                     # Kehlbalken
    lage.holz((-x0, zh), (x0, zh), 0.15, "balken_b", 1, zuschnitt=innen)                                     # Hahnenbalken
    unter_kehl = schnitt(innen, kasten(-x0, x0, zt, zk))
    for x in achsen:
        if abs(x) < 1e-6: continue
        feld = unter_kehl if abs(x) < xk(zk) else innen
        if not schnitt(kasten(x - b / 2, x + b / 2, zt + 0.16, zt + 0.5), feld): continue
        lage.holz((x, zt + 0.16), (x, zf), b, "balken_a", 0, zuschnitt=feld)                                  # Staender bis unter den Sparren
    innen_x = [x for x in achsen if 0 < x < xk(zk)]
    x_bis = max(innen_x) if innen_x else 0.0
    aussen_x = [x for x in achsen if x > x_bis + 1e-6]
    for sg in (-1, 1):
        lage.holz((sg * b / 2, z_br - r / 2), (sg * (x_bis - b / 2), z_br - r / 2), r, "balken_b", 2, gerade=True)   # Brustriegel
        # (keine Strebe zum Sparren mehr in den aeusseren Feldern: im schmalen Restfeld lag sie fast am Staender, Bildpruefung 21.11.2026)
        lage.holz((sg * (xk(zk) - 0.15), zk + 0.08), (sg * (b / 2), zh - 0.075), 0.12, "balken_a", 1,
                  zuschnitt=schnitt(innen, kasten(-x0, x0, zk + 0.08, zh - 0.075)))                          # Strebe ueber dem Kehlbalken
    befunde = lage.pruefen()
    if befunde: print("HOLZLAGE", wand, befunde[:3])
    lage.bauen(h, lambda s_, z_, r_: Vector((s_, y + aussen * r_, z_)), Vector((0, aussen, 0)), tiefe=TIEFE_HOLZ, wand=(-M.OX, M.OX))
    LAGEN[wand] = lage


def steingiebel(h, wand, y, aussen):
    """Steinhaus: Giebel aus Haustein bis unter die Dachdeckung, in Lagen von Bandhoehe, mit den Giebelfenstern (Laibung aus Stein)."""
    fen = [(a0, a1, z0, z1) for w, (a0, a1), (z0, z1), art in M.FENSTER if w == wand]
    zt, zf = M.Z_TRAUFE, M.Z_FIRST
    xk = lambda z: max(M.OX - (z - zt) / M.NEIGUNG, 0.0)
    v0, v1, m = BA["bruchstein"]
    kurs = (v1 - v0) * m
    mitte = Vector((0, y - aussen * 3, zt + 1))
    z = zt
    while z < zf - 1e-3:
        zn = min(z + kurs, zf)
        # Eine Lage, die ein Fenster nur teilweise schneidet, wird an Fensterunter- und -oberkante geteilt: sonst fehlte die Mauer
        # zwischen Lagenkante und Fenster (Loch unter der Sohlbank, Rueckmeldung des Nutzers 15.11.2026). Die Textur laeuft in der
        # ganzen Lage durch (Bezug z, kurs).
        teile = sorted({z, zn} | {k for a0, a1, z0, z1 in fen for k in (z0, z1) if z + 1e-4 < k < zn - 1e-4})
        for s, t in zip(teile, teile[1:]):
            xa, xb = xk(s), xk(t)
            schnitte = sorted([(a0, a1) for a0, a1, z0, z1 in fen if z0 < t - 1e-4 and z1 > s + 1e-4])
            kanten = [-xa] + [x for a0, a1 in schnitte for x in (a0, a1)] + [xa]
            for i in range(0, len(kanten), 2):
                l, r = kanten[i], kanten[i + 1]
                lo, ro = (-xb if i == 0 else l), (xb if i + 2 == len(kanten) else r)
                if xb < 1e-3 and i == 0 and len(kanten) == 2:
                    pts = [(l, y, s), (r, y, s), (0.0, y, t)]
                else:
                    pts = [(l, y, s), (r, y, s), (ro, y, t), (lo, y, t)]
                h.welt("Bruchstein", pts, "bruchstein", (-M.OX - (z * 3.7) % 1 * m, y, 0), (1, 0, 0), z, kurs, weg_von=Vector((0, y - aussen, s)), m=None)   # Lagen waagerecht versetzt (Quaderverband)
        z = zn
    for a0, a1, z0, z1 in fen:                                     # Laibung aus Stein
        n = Vector((0, aussen, 0))
        for p_, richtung, laenge in (((a0, y, z0), Vector((1, 0, 0)), a1 - a0), ((a0, y, z1), Vector((1, 0, 0)), a1 - a0), ((a0, y, z0), EZ, z1 - z0), ((a1, y, z0), EZ, z1 - z0)):
            h.rechteck("Bruchstein", p_, richtung, -n, laenge, M.T, "bruchstein", zu=Vector(((a0 + a1) / 2, y - aussen * M.T / 2, (z0 + z1) / 2)), anteil=(0.0, 0.6))
    h.kasten("Bruchstein", (-M.OX - 0.06, min(y, y + aussen * 0.08), zt - 0.12), (M.OX + 0.06, max(y, y + aussen * 0.08), zt),
             WERKSTEIN if GURT_WERK else "bruchstein", laengs="x", ohne=("-y" if aussen > 0 else "+y",), ohne_streckung=GURT_WERK)   # Gesims (ohne die Rueckseite in der Mauer)


def treppengiebel_rueckseite(h, w, y1, z, zn, kurs):
    """Rueckseite des Treppengiebels, soweit sie ueber das Dach ragt (von hinten sah man sonst die Rueckseite der Vorderflaeche –
    in Godot unsichtbar; luecken_pruefung „Rueckseite“, Rueckmeldung 15.11.2026). Unter dem Dach bleibt sie offen (Dachboden).
    Unterkante 3 cm unter der Dachoberflaeche (z_dach + Deckung 0,10)."""
    dach = lambda x: M.z_dach(x) + 0.10 - 0.03
    t = lambda zz: min(max(M.OX - (zz - (M.Z_TRAUFE + 0.07)) / M.NEIGUNG, 0.0), w)   # |x|, an dem das Dach die Hoehe zz hat
    if t(zn) >= w - 1e-3: return
    for sx in (-1, 1):
        if t(z) < w - 1e-3:
            pts = [(t(zn), zn), (w, zn), (w, z), (t(z), z)]
        else:
            pts = [(t(zn), zn), (w, zn), (w, dach(w))]
        pts = [(sx * x, y1, zz) for x, zz in pts]
        if sx < 0: pts = pts[::-1]
        h.welt("Bruchstein", pts, "bruchstein", (-w - (z * 3.7) % 1 * BA["bruchstein"][2], y1, 0), (1, 0, 0), z, kurs,
               weg_von=Vector((0, y1 - 1, (z + zn) / 2)), m=None)


def treppengiebel(h, stufen=6, dicke=0.36):
    """Stufe 3 (Vorlage lvl3_v01): Treppengiebel aus Haustein vor dem Dach zur Gasse, sechs Stufen mit Abdeckplatten; das Dach endet
    dahinter. In Lagen von Bandhoehe gemauert (keine Streckung der Textur)."""
    y0, y1 = M.OVORN - 0.04, M.OVORN - 0.04 + dicke
    zt, zf = M.Z_TRAUFE, M.Z_FIRST + 0.75
    hoehe = (zf - zt) / stufen
    v0, v1, m = BA["bruchstein"]
    kurs = (v1 - v0) * m
    mitte = Vector((0, y0 + 2, zt + 1))
    breiten = [min(M.OX + 0.06 if k == 0 else max((M.Z_FIRST + 0.15 - (zt + k * hoehe)) / M.NEIGUNG, 0.0) + 0.32, M.OX + 0.06) for k in range(stufen)]
    fen = [(a0, a1, z0_, z1_) for wd, (a0, a1), (z0_, z1_), art in M.FENSTER if wd == "vorn_tg"]
    lisenen, nischen = gotik_plan(breiten, hoehe, zt, fen)
    NISCHE_TIEF = 0.18                                     # Nutzer 22.11.2026: Nischen 15–30 cm tief (Mauer 36 cm)
    for k in range(stufen):
        za, zb = zt + k * hoehe, zt + (k + 1) * hoehe
        w = breiten[k]
        z = za
        loch = [(a0, a1, z0_, z1_) for wd, (a0, a1), (z0_, z1_), art in M.FENSTER if wd == "vorn_tg"]
        while z < zb - 1e-4:
            zn = min(z + kurs, zb)
            h.kasten("Bruchstein", (-w, y0, z), (w, y1, zn), "bruchstein", ohne=("-z", "+z", "+y", "-y"), laengs="x")
            ausschnitt = loch + [(x0, x1, zb_, spitzbogen(x0, x1, zs_)[4][1]) for x0, x1, zb_, zs_ in nischen]
            h.wand("Bruchstein", (-w, y0, 0), (1, 0, 0), 2 * w, z, zn, "bruchstein", loecher=[(a0 + w, a1 + w, z0_, z1_) for a0, a1, z0_, z1_ in ausschnitt],
                   zbasis=z, hoehe=kurs, weg_von=mitte, u_start=(z * 3.7) % 1)
            treppengiebel_rueckseite(h, w, y1, z, zn, kurs)
            z = zn
        for a0, a1, z0_, z1_ in loch:                         # Laibung aus Haustein durch die ganze Giebelmauer, Sohlbank und Sturz
            if not (za <= z0_ < zb): continue
            for p_, richtung, laenge in (((a0, y0, z0_), Vector((1, 0, 0)), a1 - a0), ((a0, y0, z1_), Vector((1, 0, 0)), a1 - a0),
                                         ((a0, y0, z0_), EZ, z1_ - z0_), ((a1, y0, z0_), EZ, z1_ - z0_)):
                h.rechteck("Bruchstein", p_, richtung, (0, 1, 0), laenge, dicke, "bruchstein", zu=Vector(((a0 + a1) / 2, y0 + dicke / 2, (z0_ + z1_) / 2)), anteil=(0.0, 0.6))
            h.kasten("Bruchstein", (a0 - 0.08, y0 - 0.06, z0_ - 0.08), (a1 + 0.08, y0 + 0.02, z0_ - 0.005), WERKSTEIN, ohne=("+y",), laengs="x")
            h.kasten("Bruchstein", (a0 - 0.12, y0 - 0.04, z1_ + 0.005), (a1 + 0.12, y0 + 0.02, z1_ + 0.16), WERKSTEIN, ohne=("+y",), laengs="x")
        if k == stufen - 1:
            h.kasten("Bruchstein", (-w - 0.05, y0 - 0.05, zb), (w + 0.05, y1 + 0.03, zb + 0.08), WERKSTEIN, laengs="x")   # Abdeckplatte oben
        else:
            for sx in (-1, 1):                                # Abdeckplatten nur auf den Schultern der Stufe (nicht durch die Mauer)
                xa, xb = sorted((sx * (breiten[k + 1] - 0.02), sx * (w + 0.05)))
                h.kasten("Bruchstein", (xa, y0 - 0.05, zb), (xb, y1 + 0.03, zb + 0.08), WERKSTEIN, laengs="x")
    # Gesims an der Traufe
    h.kasten("Bruchstein", (-M.OX - 0.08, y0 - 0.08, zt - 0.10), (M.OX + 0.08, y1, zt), WERKSTEIN if GURT_WERK else "bruchstein", ohne=("+y",), laengs="x",
             ohne_streckung=GURT_WERK)
    # Schaugiebel (Nutzer 22.11.2026, patrizier_giebel_recherche.md): Lisenen ueber jeder Stufe mit Fiale, Blendnischen, Fenstergewaende
    P = lambda x, z, r: Vector((x, y0 - r, z))
    for x0, x1, zb_, zs_ in nischen:
        blendnische(h, P, x0, x1, zb_, zs_, NISCHE_TIEF)
    for xm, z_ob in lisenen[1:]:      # nicht an der untersten Schulter: dort laege die Fiale in der Flucht der Traufwand (Pruefung 9)
        for sx in (-1, 1):
            h.kasten("Bruchstein", (sx * xm - LISENE / 2, y0 - 0.08, zt), (sx * xm + LISENE / 2, y0, z_ob), WERKSTEIN, ohne=("+y", "-z", "+z"), laengs="z")
            fiale(h, sx * xm, y0 - 0.04, z_ob + 0.08, tief=dicke + 0.06)   # auf der Deckplatte, so tief wie sie
    fiale(h, 0.0, y0 - 0.04, zt + stufen * hoehe + 0.08, gross=True, tief=dicke + 0.06)
    for a0, a1, z0_, z1_ in fen:                              # Fenster der Bibliothek: spitzbogiges Gewaende um Sohlbank und Sturz
        w_ = a1 - a0 + 0.12
        spitz_rahmen(h, P, a0 - 0.06, a1 + 0.06, z0_ - 0.08, z1_ + 0.16, 0.07, 0.07)
        kreis_ring(h, P, (a0 + a1) / 2, z1_ + 0.16 + 0.42 * BOGEN_H * w_, min(0.17, 0.25 * w_), 0.04, 0.05, n=12)   # Okulus im Bogenfeld
    # (keine Ankereisen mehr: flach auf dem Giebel lasen sie sich als schwarze Spalten neben dem Giebelfenster – Rueckmeldung 15.11.2026)


def baue_haus():
    h = Teil("Haus", BA, saat=11)
    stein, lehm, holz = "Bruchstein", "Lehmputz", "Fachwerk"
    # Erdgeschoss: Bruchsteinmauer (12 cm eingelassen) bis unter die Schwelle des Obergeschosses
    for wand in ("vorn_eg", "rechts_eg", "hinten_eg", "links_eg"):
        steinwand(h, stein, wand, -0.12, M.Z_EG_DECKE)
    # Eckquader (heller, versetzt)
    for sx in (-1, 1):
        for y, sy in ((M.VORN, -1), (M.HINTEN, 1)):
            z = 0.0
            i = 0
            while z < M.Z_EG_DECKE - 0.3:
                hq = 0.34 if i % 2 == 0 else 0.26
                lx, ly = (0.48, 0.22) if i % 2 == 0 else (0.22, 0.48)
                zb = min(z + hq, M.Z_EG_DECKE - 0.02)
                if ist_stein():
                    # Steinhaus: Eckquader als Werkstuecke aus hellem Sandstein (Vorlagen wohnhaus_lvl3_*), 3 cm vor der Mauer, 1,5 cm
                    # Fuge darueber; Laeufer und Binder wechseln. Zwei Kaesten, die sich nicht ueberdecken (A mit der Ecke, B daneben)
                    za, ze = z, zb - 0.015
                    xa, xi, ya, yi = sx * (M.BX + 0.03), sx * (M.BX - lx), y + sy * 0.03, y - sy * 0.01
                    h.kasten(stein, (min(xa, xi), min(ya, yi), za), (max(xa, xi), max(ya, yi), ze), WERKSTEIN, ohne=("+y" if sy < 0 else "-y",), laengs="x")
                    xb, xbi, yb0, yb1 = sx * (M.BX + 0.03), sx * (M.BX - 0.01), y - sy * 0.01, y - sy * ly
                    h.kasten(stein, (min(xb, xbi), min(yb0, yb1), za), (max(xb, xbi), max(yb0, yb1), ze), WERKSTEIN,
                             ohne=("+x" if sx < 0 else "-x", "-y" if sy < 0 else "+y"), laengs="y")
                else:
                    x0 = sx * M.BX - (lx if sx > 0 else 0)
                    h.rechteck(stein, (x0, y + sy * 0.03, z), (1, 0, 0), EZ, lx, zb - z, "bruchstein", weg_von=MITTE, anteil=(0.62, 0.98))
                    y0 = y - (ly if sy > 0 else 0)
                    h.rechteck(stein, (sx * (M.BX + 0.03), y0, z), (0, 1, 0), EZ, ly, zb - z, "bruchstein", weg_von=MITTE, anteil=(0.62, 0.98))
                z = zb + 0.0
                i += 1
    # Obergeschoss: Fachwerk mit Lehmputz
    for g, za, zb in geschosse_og():
        umfang = 0.0
        for wand in (f"vorn_{g}", f"rechts_{g}", f"hinten_{g}", f"links_{g}"):
            (ox, oy), (ex, ey), breite, _ = WAENDE[wand]
            l = [x[:4] for x in loecher(wand)]
            if ist_stein():                                # Steinhaus: Obergeschoss aus Haustein wie das Erdgeschoss
                steinwand(h, stein, wand, za, zb, band=OBEN["band"], lage=OBEN["lage"])   # Patrizierhaus D (Nutzer 22.11.2026): heller Haustein
                continue
            h.wand(lehm, (ox, oy, 0), (ex, ey, 0), breite, za, zb, "putz", loecher=l, zbasis=za, hoehe=zb - za,
                   laibung=(holz, "balken_c"), laibung_tiefe=M.T, weg_von=MITTE, u_start=umfang)
            umfang += breite
            fachwerk(h, wand, za, zb)
    if M.STUFE == 3:
        treppengiebel(h)
        steingiebel(h, "hinten_gb", M.HINTEN, 1)
        rundbogenfries(h)
    else:
        for wand, y, aussen in (("vorn_gb", M.OVORN, -1), ("hinten_gb", M.HINTEN, 1)):
            giebel(h, wand, y, aussen)
    if ist_stein():
        # Steinhaus: die Auskragung des Obergeschosses ruht auf einem Gesims aus Haustein ueber dunklen Balkenkoepfen (Holzakzent wie in
        # der Vorlage wohnhaus_lvl3_v01, Rueckmeldung des Nutzers 15.11.2026)
        for (a, b) in (((-M.OX, M.OVORN), (M.OX, M.VORN)), ((-M.OX, M.VORN), (-M.BX, M.HINTEN)), ((M.BX, M.VORN), (M.OX, M.HINTEN))):
            lx, ly = b[0] - a[0], b[1] - a[1]
            h.rechteck(stein, (a[0], a[1], M.Z_EG_DECKE), (1, 0, 0), (0, 1, 0), lx, ly, "bruchstein", laengs="u" if lx > ly else "v", zu=(0, 0, 0))
        for (x0_, y0_, x1_, y1_) in ((-M.OX - 0.03, M.OVORN - 0.03, M.OX + 0.03, M.OVORN + 0.05), (-M.OX - 0.03, M.OVORN + 0.05, -M.OX + 0.05, M.HINTEN + 0.03), (M.OX - 0.05, M.OVORN + 0.05, M.OX + 0.03, M.HINTEN + 0.03)):
            if GURT_WERK:                                    # Halbling Stufe 3: Gurtgesims aus hellem Werkstein: Lage und Deckleiste (2 cm weiter vor), Lagen ohne Streckung
                lg = "x" if x1_ - x0_ > y1_ - y0_ else "y"
                h.kasten(stein, (x0_, y0_, M.Z_EG_DECKE - 0.04), (x1_, y1_, M.Z_EG_DECKE + 0.08), WERKSTEIN, laengs=lg, ohne=("+z",), ohne_streckung=True)
                if lg == "x": h.kasten(stein, (x0_ - 0.02, y0_ - 0.02, M.Z_EG_DECKE + 0.08), (x1_ + 0.02, y1_, M.Z_EG_DECKE + 0.12), WERKSTEIN, laengs="x", ohne_streckung=True)
                else: h.kasten(stein, (x0_ - (0.02 if x0_ < 0 else 0.0), y0_, M.Z_EG_DECKE + 0.08), (x1_ + (0.02 if x1_ > 0 else 0.0), y1_, M.Z_EG_DECKE + 0.12), WERKSTEIN, laengs="y", ohne_streckung=True)
            else:
                h.kasten(stein, (x0_, y0_, M.Z_EG_DECKE - 0.04), (x1_, y1_, M.Z_EG_DECKE + 0.12), "bruchstein", laengs="x" if x1_ - x0_ > y1_ - y0_ else "y")
    # Untersicht der Auskragung und Balkenkoepfe
    for (a, b) in ((((-M.OX, M.OVORN), (M.OX, M.VORN)), ((-M.OX, M.VORN), (-M.BX, M.HINTEN)), ((M.BX, M.VORN), (M.OX, M.HINTEN))) if not ist_stein() else ()):
        lx, ly = b[0] - a[0], b[1] - a[1]
        h.rechteck(holz, (a[0], a[1], M.Z_EG_DECKE), (1, 0, 0), (0, 1, 0), lx, ly, "balken_c", laengs="u" if lx > ly else "v", zu=(0, 0, 0))
    def balkenkoepfe():
        for i in range(7):
            x = -M.OX + 0.2 + i * (2 * M.OX - 0.4) / 6
            # endet an der Mauerflucht mit Stirn (nicht in der Mauer: das verdeckte Mauerstueck hinter dem Balken backte schwarz)
            h.kasten(holz, (x - 0.08, M.OVORN - 0.02, M.Z_EG_DECKE - 0.17), (x + 0.08, M.VORN, M.Z_EG_DECKE), "balken_c",
                     ohne=("+z", "+y") if ist_stein() else ("+z",), laengs="y")   # Steinhaus: ohne die Flaeche an der Mauer (backte schwarz)
            if FACHWERK_ECHT:   # Knagge unter dem Balkenkopf (Vorbild Rathaus Alsfeld)
                fachwerk_echt.knaggen(h, [(x, M.VORN, M.Z_EG_DECKE - 0.17 - 0.25)], (0, -1, 0), hoehe=0.25, krag=M.VORN - M.OVORN)
        for seite in (-1, 1):
            for i in range(5):
                y = M.VORN + 0.4 + i * (M.HINTEN - M.VORN - 0.8) / 4
                h.kasten(holz, (min(seite * M.BX, seite * (M.OX + 0.02)), y - 0.08, M.Z_EG_DECKE - 0.17), (max(seite * M.BX, seite * (M.OX + 0.02)), y + 0.08, M.Z_EG_DECKE), "balken_c",
                         ohne=("+z", "+x" if seite < 0 else "-x") if ist_stein() else ("+z",), laengs="x")
                if FACHWERK_ECHT:
                    fachwerk_echt.knaggen(h, [(seite * M.BX, y, M.Z_EG_DECKE - 0.17 - 0.25)], (seite, 0, 0), hoehe=0.25, krag=M.OX - M.BX)
    holz_akzent(h, balkenkoepfe)
    oeffnungen(h)
    # Steinschornstein aussen an der rechten Wand (Herd der Wohnstube, Kamin bzw. Ofen des Raums)
    x0, x1, y0, y1 = M.SCHORNSTEIN_RECHTS
    schornstein(h, x0, x1, y0, y1, -0.12, M.Z_FIRST - 0.6)
    # Bank unter dem Fenster an der Gasse, Regenfass, Scheitholz an der linken Seite
    h.kasten(holz, (-1.55, M.VORN - 0.40, 0.42), (-0.15, M.VORN - 0.08, 0.48), "balken_b", laengs="x")
    for x in (-1.40, -0.30):
        h.kasten(holz, (x - 0.05, M.VORN - 0.36, 0.0), (x + 0.05, M.VORN - 0.12, 0.42), "balken_c", ohne=("-z", "+z"), laengs="z")
    h.dreh(holz, (-M.BX - 0.40, M.VORN + 0.45, 0.0), [(0.25, 0.0), (0.31, 0.3), (0.31, 0.5), (0.26, 0.8)], "planken", seiten=10, deckel=False, gedreht=True)
    h.dreh(stein, (-M.BX - 0.40, M.VORN + 0.45, 0.72), [(0.255, 0.0), (0.0, 0.0)], "eisen", seiten=8, deckel=False, anteil=(0.0, 0.3))
    # Scheite 1 cm auseinander und gerade (vorher 2 cm Fuge: Durchblick bis an die Hauswand, luecken_pruefung)
    for i, (y, z) in enumerate([(1.0 + 0.18 * k, 0.09) for k in range(6)] + [(1.09 + 0.18 * k, 0.265) for k in range(5)] + [(1.18 + 0.18 * k, 0.44) for k in range(4)]):
        h.stab(holz, (-M.BX - 0.03, y, z), (-M.BX - 0.62, y, z), 0.17, "balken_a", kappen=False)
        h.rechteck(holz, (-M.BX - 0.62, y - 0.085, z - 0.085), (0, 1, 0), EZ, 0.17, 0.17, "farbholz", zu=Vector((-9, y, z)), anteil=(0.1, 0.9), u_bereich=(0.1 + 0.07 * i, 0.14 + 0.07 * i))
    return h


def scheibe(teil, stoff, wand, a0, a1, z0, z1, raus, **kw):
    (ox, oy), (ex, ey), _, _ = WAENDE[wand]
    start = ox if ex else oy
    b, hoch = a1 - a0, z1 - z0
    pts = [punkt(wand, a0 - start, z0, raus), punkt(wand, a1 - start, z0, raus), punkt(wand, a1 - start, z1, raus), punkt(wand, a0 - start, z1, raus)]
    teil.vieleck(stoff, pts, [(0, 1), (b, 1), (b, 1 - hoch), (0, 1 - hoch)], **kw)


def baue_scheiben():
    s = Teil("Scheiben", BA, saat=5)
    for wand, (a0, a1), (z0, z1), art in M.FENSTER:
        if art != "fenster": continue
        tief = 0.10 if wand.endswith("_gb") else 0.18 if wand.endswith("_tg") else M.T / 2
        scheibe(s, "Fenster", wand, a0, a1, z0, z1, -tief, weg_von=MITTE)
        scheibe(s, "Bleinetz", wand, a0, a1, z0, z1, -tief + 0.006, weg_von=MITTE)
    # Gaubenfenster
    y0, y1 = GAUBE["y"]; ym = (y0 + y1) / 2
    fb, fh = GAUBE["fenster"]
    zb = M.z_dach(GAUBE["x_front"]) + 0.10 + 0.22
    for stoff, dx in (("Fenster", -0.08), ("Bleinetz", -0.074)):
        x = GAUBE["x_front"] + dx
        s.vieleck(stoff, [(x, ym - fb / 2, zb), (x, ym + fb / 2, zb), (x, ym + fb / 2, zb + fh), (x, ym - fb / 2, zb + fh)], [(0, 1), (fb, 1), (fb, 1 - fh), (0, 1 - fh)], zu=(9, ym, zb))
    return s


WAPPEN_ABSTAND = 0.05     # Mindestabstand des Wappens zu Laden, Fenster, Gewaende, Holz und Vordach
WAPPEN_G = 0.62           # Groesse wie bisher, kleiner nur, wenn der Platz unter dem Fenster nicht reicht
# Masse des Schilds (g1.SCHILD, aussen 1,16 x 1,12 g, Aufhaengung bis 0,58 g): Breite und Hoehe ueber bzw. unter dem Ursprung
SCHILD_B, SCHILD_UNTEN, SCHILD_OBEN = 0.394, 0.493, 0.58


def wappen_ort():
    """Regel fuer das Wappen (Rueckmeldung des Nutzers 15.11.2026, alle Grundformen beider Stufen): an der Gassenwand des
    Obergeschosses ueber und rechts der Haustuer, unter den Fenstern – im Gefach, dessen Mitte der Tuermitte am naechsten liegt
    (ohne die Eckgefache mit Streben). Unten ueber dem Vordach bzw. der Schwelle (Buergerhaus) oder dem Gesims (Steinhaus), oben
    unter Laden, Fenster, Sohlbank bzw. Brustriegel, je mit WAPPEN_ABSTAND; reicht die Hoehe nicht, wird das Schild kleiner.
    Rueckgabe (x, z des Ursprungs, g)."""
    tuer = next(f for f in M.FENSTER if f[3] == "tuer")
    xt, zt = (tuer[1][0] + tuer[1][1]) / 2, tuer[2][1]
    achsen, _ = M.raster(2 * M.OX)
    gefache = [(-M.OX + achsen[k] + M.BW / 2, -M.OX + achsen[k + 1] - M.BW / 2) for k in range(1, len(achsen) - 2)]
    a0, a1 = min(gefache, key=lambda g_: abs((g_[0] + g_[1]) / 2 - xt))
    x = (a0 + a1) / 2
    halb = SCHILD_B * WAPPEN_G
    xa, xb = x - halb - WAPPEN_ABSTAND, x + halb + WAPPEN_ABSTAND
    # unten: Vordach (Satteldach ueber der Tuer, First in der Tuermitte, vordach()) und Schwelle bzw. Gesims
    vz, vb = zt + 0.30, 0.72 + 0.06
    zf = vz + 0.42
    naechst = min(abs(xt - xa), abs(xt - xb)) if not (xa <= xt <= xb) else 0.0
    vordach_oben = zf - min(naechst / vb, 1.0) * (zf - (vz - 0.02)) if naechst < vb else -9.0
    boden = M.Z_EG_DECKE + (0.12 if ist_stein() else 0.20)
    unten = max(vordach_oben, boden) + WAPPEN_ABSTAND
    # oben: Fenster des Obergeschosses mit Gewaende bzw. Laden (gleiche Hoehe) oder der Brustriegel des Gefachs
    z0_og = min(z0 for w, _a, (z0, _z1), art in M.FENSTER if w == "vorn_og")
    oben = (z0_og - 0.11 if ist_stein() else z0_og - M.RIEGEL - 0.03) - WAPPEN_ABSTAND   # Sohlbank bzw. Brustriegel (mit Fase)
    g = min(WAPPEN_G, (oben - unten) / (SCHILD_UNTEN + SCHILD_OBEN))
    return x, unten + SCHILD_UNTEN * g, g


def baue_wappen():
    w = Teil("Wappen", BA, saat=61)
    x, z, g = wappen_ort()
    o = (x, M.OVORN, z)
    eu, ev, n = (1, 0, 0), (0, 0, 1), (0, -1, 0)
    g1.prisma(w, "Fachwerk", [(x_ * 1.16 * g, y_ * 1.12 * g) for x_, y_ in g1.SCHILD], o, eu, ev, n, 0.0, 0.045, "balken_c")
    g1.prisma(w, "Hausfarbe", [(x_ * g, y_ * g) for x_, y_ in g1.SCHILD], o, eu, ev, n, 0.045, 0.075, "farbholz", farbe=(1.0, 1.0, 1.0, 1.0))
    w.kasten("Bruchstein", (x - 0.02, M.OVORN - 0.05, z + 0.47 * g), (x + 0.02, M.OVORN, z + 0.58 * g), "eisen", ohne=("+y",))
    return w


def baue_fahne():
    f = Teil("Fahne", BA, saat=67)
    x, y, z = 0.0, M.OVORN - M.UEBER_ORTGANG_VORN + 0.55, M.Z_FIRST + 0.25
    hoehe = 2.3
    f.stab("Holz", (x, y, z), (x, y, z + hoehe), 0.08, "balken_a")
    f.kasten("Eisen", (x - 0.07, y - 0.07, z + hoehe - 0.02), (x + 0.07, y + 0.07, z + hoehe + 0.12), "eisen")
    tuch = [(0, 0), (1.25, 0.05), (0.98, 0.38), (1.25, 0.72), (0, 0.72)]
    o = Vector((x + 0.04, y, z + hoehe - 0.85))
    for t in (-0.015, 0.015):
        f.vieleck("Hausfarbe", [o + Vector((u, t, v)) for u, v in tuch], [(0.1, 0.8)] * 5, weg_von=o + Vector((0.6, -t, 0.36)))
    for (au, av), (bu, bv) in zip(tuch, tuch[1:] + tuch[:1]):
        f.vieleck("Hausfarbe", [o + Vector((au, -0.015, av)), o + Vector((bu, -0.015, bv)), o + Vector((bu, 0.015, bv)), o + Vector((au, 0.015, av))],
                  [(0.1, 0.8)] * 4, weg_von=o + Vector((0.6, 0, 0.36)))
    return f


def baue_leerstand():
    l = Teil("Leerstand", BA, saat=71)
    for wand, (a0, a1), (z0, z1), art in M.FENSTER:
        if wand.endswith("_gb"): continue
        (ox, oy), (ex, ey), _, _ = WAENDE[wand]
        start = ox if ex else oy
        s0, s1_ = a0 - start, a1 - start
        n3 = nvec(wand)
        if art == "tuer":
            for z in (z0 + 0.55, z0 + 1.05, z0 + 1.5):
                l.brett("Fachwerk", punkt(wand, s0 - 0.16, z - 0.06), punkt(wand, s1_ + 0.16, z + 0.07), 0.17, n3, "planken", tiefe=0.12)
        else:
            l.brett("Fachwerk", punkt(wand, s0 - 0.1, z0 + 0.05), punkt(wand, s1_ + 0.1, z1 - 0.08), 0.15, n3, "planken", tiefe=0.13)
            l.brett("Fachwerk", punkt(wand, s0 - 0.1, z1 - 0.02), punkt(wand, s1_ + 0.1, z0 + 0.12), 0.15, n3, "planken", tiefe=0.155)
    return l


def baue_geruest():
    g = Teil("Geruest", BA, saat=73)
    yi, ya = M.OVORN - 0.30, M.OVORN - 1.10
    xs = (-M.OX - 0.1, 0.0, M.OX + 0.1)
    for x in xs:
        g.stab("Fachwerk", (x, yi, 0), (x, yi, 5.6), 0.09, "balken_a")
        g.stab("Fachwerk", (x, ya, 0), (x, ya, 6.4), 0.09, "balken_a")
    for z in (1.9, 3.9, 5.4):
        for x in xs:
            g.stab("Fachwerk", (x, yi - 0.05, z - 0.07), (x, ya + 0.05, z - 0.07), 0.08, "balken_b")
        g.kasten("Fachwerk", (xs[0] - 0.15, ya + 0.08, z), (xs[-1] + 0.15, yi - 0.08, z + 0.05), "planken", laengs="x")
        g.stab("Fachwerk", (xs[0], ya, z + 0.95), (xs[-1], ya, z + 0.95), 0.06, "balken_a")
    g.stab("Fachwerk", (xs[0], ya, 0.1), (xs[1], ya, 1.85), 0.07, "balken_c")
    g.stab("Fachwerk", (xs[1], ya, 2.0), (xs[2], ya, 3.85), 0.07, "balken_c")
    fuss, kopf = Vector((1.75, ya - 0.55, 0)), Vector((1.75, ya + 0.02, 2.5))
    for dx in (-0.2, 0.2):
        g.stab("Fachwerk", fuss + Vector((dx, 0, 0)), kopf + Vector((dx, 0, 0)), 0.05, "balken_a")
    for i in range(7):
        p = fuss.lerp(kopf, (i + 0.7) / 7.6)
        g.stab("Fachwerk", p + Vector((-0.2, 0, 0)), p + Vector((0.2, 0, 0)), 0.04, "balken_b")
    return g


# ═══════════════════════════════════════════════════════════════════════════════════════════════════════════════════════
#  Innen: Wohnstube
# ═══════════════════════════════════════════════════════════════════════════════════════════════════════════════════════

IX, IV, IH = M.IX, M.IV, M.STUBE_HINTEN
ZB = M.Z_BODEN
DX_RECHTS, DY_RECHTS = IX - g1.IX, IH - g1.IH        # Moebel der rechten Wand der Probe -> rechte Wand hier
DX_LINKS = -(IX - g1.IX)
TISCH_LAGE = (((0, 1), (-1, 0)), (0.0, IH - g1.IX))   # (x, y) -> (y, IH - (x + IX_probe)): linke Wand der Probe -> Rueckwand


def an_rueckwand(x, y):
    (a, b), (c, d) = TISCH_LAGE[0]
    return (a * x + b * y + TISCH_LAGE[1][0], c * x + d * y + TISCH_LAGE[1][1])


def streifen_boden(r, band, x0, x1, y0, y1, z, breite, saat=0):
    """Dielen in Streifen von `breite` (Bandhoehe in Metern), Bretter entlang y."""
    x = x0
    k = 0
    while x < x1 - 1e-4:
        xn = min(x + breite, x1)
        r.rechteck(S, (x, y0, z), (0, 1, 0), (1, 0, 0), y1 - y0, xn - x, band, zu=(x, y0, z + 9), u0=(0.31 * k + saat) % 1, anteil=(0.0, (xn - x) / breite))
        x = xn; k += 1


def fliesen_boden(r, x0, x1, y0, y1, z, feld_nr=0):
    """Fliesen im Band „lehmboden“ (innen_trim_s23): Felder von 0,6 m (quer, x) x 1,8 m (laengs, y); feld_nr 0 Tonfliesen, 1 Schachbrett."""
    ua = 0.0 if feld_nr == 0 else 0.5
    v0, v1, _ = BI["lehmboden"]
    x = x0
    while x < x1 - 1e-4:
        xn = min(x + 0.6, x1)
        y = y0
        while y < y1 - 1e-4:
            yn = min(y + 1.8, y1)
            ub = ua + FLIESEN_U * (yn - y) / 1.8
            vb = v1 - (v1 - v0) * (xn - x) / 0.6
            r.vieleck(S, [(x, y, z), (x, yn, z), (xn, yn, z), (xn, y, z)], [(ua, v1), (ub, v1), (ub, vb), (ua, vb)], zu=(x, y, z + 9))
            y = yn
        x = xn


def bretterwand_innen(r, x0, x1, y, z0, z1, richtung=-1):
    """Bretterwand quer zur Y-Achse (Sichtseite nach `richtung`): stehende Bohlen, oben ein Riegel."""
    v0, v1, m = BI["bohlen"]
    x = x0
    k = 0
    while x < x1 - 1e-4:
        xn = min(x + 0.26, x1)
        u = (0.29 * k) % 1
        r.vieleck(S, [(x, y, z0), (xn, y, z0), (xn, y, z1), (x, y, z1)], [(u, v1), (u, v1 - (v1 - v0) * (xn - x) / 0.8), (u + (z1 - z0) / m, v1 - (v1 - v0) * (xn - x) / 0.8), (u + (z1 - z0) / m, v1)],
                  zu=(x, y + richtung, z0 + 1))
        x = xn; k += 1
    for z in (z0 + 0.06, z1 - 0.12):
        r.brett(S, (x0, y, z), (x1, y, z), 0.12, (0, richtung, 0), "balken", tiefe=0.04)


def tuer_quer(teil, x0, x1, y_wand, z0, hoehe, richtung=-1, **kw):
    """Zimmertuer (raeume/zimmertuer.py) in einer Wand quer zur Y-Achse: dort gebaut (Wand quer zu X) und um 90 Grad gedreht."""
    tmp = Teil("_", teil.baender, saat=3)
    mitte = zimmertuer(tmp, S, "bohlen", "balken", "kleinteile", feld("eisen"), 0.0, x0, x1, z0, hoehe, 1, **kw)
    # (x, y) -> (y, y_wand - x): Wand x = 0 -> y = y_wand, Sichtseite +X -> -Y
    if richtung < 0:
        versetze(tmp, ((0, 1), (-1, 0)), (0.0, y_wand))
        mitte = Vector((mitte.y, y_wand - mitte.x, mitte.z))
    teil.dazu(tmp)
    return mitte


def baue_raum():
    r = Teil("Raum", BI, saat=31)
    # Boden: Dielen, vor dem Herd Tonfliesen
    fx0, fy0, fy1 = IX - 1.20, -1.07, 0.73
    if M.STUFE == 3:                                   # Steinhaus: Fliesen im Schachbrett (Vorlage wohnhaus_in_lvl3_mensch)
        fliesen_boden(r, -IX, IX, IV, IH, ZB, 1)
    else:
        streifen_boden(r, "bohlen", -IX, IX, IV, fy0, ZB, 0.8, saat=0.1)
        streifen_boden(r, "bohlen", -IX, IX, fy1, IH, ZB, 0.8, saat=0.4)
        streifen_boden(r, "bohlen", -IX, fx0, fy0, fy1, ZB, 0.8, saat=0.7)
        fliesen_boden(r, fx0, IX, fy0, fy1, ZB + 0.004, 0)
    # Waende: unten Taefelung (Bohlen bis 0,95), darueber Kalkputz; mit den Fensteroeffnungen der Huelle
    innen = {"vorn_eg": ((-IX, IV), (1, 0), 2 * IX), "links_eg": ((-IX, IV), (0, 1), IH - IV), "rechts_eg": ((IX, IV), (0, 1), IH - IV)}
    for wand, ((ox, oy), (ex, ey), breite) in innen.items():
        (ax, ay), _, _, _ = WAENDE[wand]
        versatz = (ox - ax) if ex else (oy - ay)
        l = [(a - versatz, b - versatz, c, d) for a, b, c, d, _ in loecher(wand)]
        r.wand(S, (ox, oy, 0), (ex, ey, 0), breite, ZB, ZB + 0.95, "bohlen", loecher=l, zbasis=ZB, hoehe=0.8, zu=RAUM)
        r.wand(S, (ox, oy, 0), (ex, ey, 0), breite, ZB + 0.95, M.Z_EG_DECKE, "lehmwand", loecher=l, zbasis=ZB + 0.95, hoehe=1.6, zu=RAUM)
        n_innen = Vector((-WAENDE[wand][3][0], -WAENDE[wand][3][1], 0))
        segs = [(0.0, breite)]
        for a, b, c, d in l:
            if c < ZB + 0.97: segs = [(s0, min(s1_, a)) for s0, s1_ in segs] + [(b, breite)]
        for s0, s1_ in segs:
            if s1_ - s0 > 0.05:
                r.brett(S, Vector((ox, oy, 0)) + Vector((ex, ey, 0)) * s0 + EZ * (ZB + 0.95), Vector((ox, oy, 0)) + Vector((ex, ey, 0)) * s1_ + EZ * (ZB + 0.95), 0.06, n_innen, "balken", tiefe=0.03)
    # Rueckwand: Bretterwand zum Treppengang, darin die Zimmertuer (Knoten Zimmertuer_<Raum>, siehe baue_zimmertueren)
    bretterwand_innen(r, -IX, IX, IH, ZB, M.Z_EG_DECKE)
    return r


LICHT_UNTER_DECKE = 0.85   # Grundlicht der Wohnstube so weit unter der Decke (1,70 m ueber dem Boden; vorher 0,35 = 2,20 m). Nutzer 21.11.2026:
#                            Unterzug und Balkenunterseiten waren dunkel – das Licht stand nur 11 cm unter dem Unterzug (2,31 m) und streifte
#                            die Unterseiten. In 1,70 m trifft es sie mit 0,6 bis 0,7 m Abstand; Waende und Boden bleiben hell (nachgerechnet)


LICHT_DECKE = True         # Deckenlicht der Wohnstube (Anker Licht_Decke und Licht_Decke_Ziel; im Spiel ein Strahler ohne Schatten, der vom
#                            ersten zum zweiten zeigt, Stadtansicht.Neubau.cs; Nutzer 21.11.2026): Unterzug und Deckenbalken blieben auch mit
#                            tieferem Grundlicht dunkel. Das Holzband ist sehr dunkel (Helligkeit 0,10), und das Grundlicht ueber dem Auge
#                            streift die langen Balkenseiten nur. Der Strahler steht an der linken Wand (der Seite des Blickpunkts) 0,6 m
#                            ueber dem Boden und leuchtet schraeg hinauf zur Deckenmitte: Er trifft die Unterseiten und die Balkenseiten, die
#                            zum Auge zeigen, nicht aber Boden und Waende darunter. Menschen und Zwerge; die Schichten der Elben und Halblinge
#                            schalten ihn ab (helles Holz, nicht beauftragt)


def baue_decke():
    """Decke der Wohnstube: Dielen auf sechs Balken und ein Unterzug (eigenes Teil, damit das Kontrollbild von oben hineinsieht)."""
    r = Teil("Decke", BI, saat=33)
    for x0, x1 in ((-IX, IX),):
        r.rechteck(S, (x0, IV, M.Z_EG_DECKE), (0, 1, 0), (1, 0, 0), IH - IV, x1 - x0, "bohlen", zu=(0, 0, 0), anteil=(0.0, 1.0))
    for i in range(6):
        x = -IX + 0.40 + i * (2 * IX - 0.80) / 5
        r.kasten(S, (x - 0.08, IV, M.Z_EG_DECKE - 0.16), (x + 0.08, IH, M.Z_EG_DECKE), "balken", ohne=("+z", "-y", "+y"), laengs="y")
    r.kasten(S, (-IX, -0.10, M.Z_EG_DECKE - 0.24), (IX, 0.10, M.Z_EG_DECKE - 0.16), "balken", ohne=("-x", "+x"), laengs="x")   # Unterzug
    return r


def baue_fensterseiten():
    r = Teil("Fensterseiten", BI, saat=35)
    # Fenster von innen: Rahmen und Bank
    for wand, (a0, a1), (z0, z1), art in M.FENSTER:
        if art != "fenster" or not wand.endswith("_eg") or wand == "hinten_eg": continue
        (ox, oy), (ex, ey), _, (nx, ny) = WAENDE[wand]
        start = ox if ex else oy
        s0, s1_ = a0 - start, a1 - start
        n_innen = (-nx, -ny, 0)
        for a, b, breite in ((punkt(wand, s0 - 0.10, z0 - 0.04, -M.T), punkt(wand, s1_ + 0.10, z0 - 0.04, -M.T), 0.08),
                             (punkt(wand, s0 - 0.10, z1 + 0.04, -M.T), punkt(wand, s1_ + 0.10, z1 + 0.04, -M.T), 0.08),
                             (punkt(wand, s0 - 0.05, z0, -M.T), punkt(wand, s0 - 0.05, z1, -M.T), 0.10),
                             (punkt(wand, s1_ + 0.05, z0, -M.T), punkt(wand, s1_ + 0.05, z1, -M.T), 0.10)):
            # die Pfosten 5 mm vor den Querhoelzern: an den Ecken nicht deckungsgleich
            r.brett(S, a, b, breite, n_innen, "balken", tiefe=0.035 if abs(a.z - b.z) < 0.01 else 0.04)
        p0, p1 = punkt(wand, s0, z0 - 0.015, -M.T - 0.02), punkt(wand, s1_, z0 - 0.005, -M.T / 2)   # Bank 5 mm unter dem Rahmen
        r.kasten(S, tuple(min(u, v) for u, v in zip(p0, p1)), tuple(max(u, v) for u, v in zip(p0, p1)), "bruchstein", laengs="x" if ex else "y")
    # Haustuer von innen (wie Stufe 1): sechs Bretter, Riegel, Strebe, Langbaender, Griff, Rahmen
    for wand, (a0, a1), (z0, z1), art in M.FENSTER:
        if art != "tuer": continue
        yd = M.VORN + 0.10
        for i in range(3):
            r.rechteck(S, (a0 + i * (a1 - a0) / 3, yd, z0), (1, 0, 0), EZ, (a1 - a0) / 3, z1 - z0, "bohlen", laengs="v", zu=RAUM)
        for z in (z0 + 0.35, z1 - 0.35):
            r.brett(S, (a0 + 0.04, yd, z), (a1 - 0.04, yd, z), 0.14, (0, 1, 0), "balken", tiefe=0.035)
        r.brett(S, (a0 + 0.12, yd, z0 + 0.44), (a1 - 0.12, yd, z1 - 0.44), 0.12, (0, 1, 0), "balken", tiefe=0.03)
        ue = feld("eisen")
        k0, k1, _ = BI["kleinteile"]
        for z in (z0 + 0.35, z1 - 0.35):
            r.rechteck(S, (a0 + 0.01, yd + 0.04, z - 0.03), (1, 0, 0), EZ, 0.5, 0.06, "kleinteile", u_bereich=ue, zu=RAUM)
        r.kasten(S, (a1 - 0.3, yd, z0 + 1.02), (a1 - 0.02, yd + 0.05, z0 + 1.08), "kleinteile", ohne=("-y",), laengs="x")
        for fl in r.flaechen[-5:]:
            fl[1][:] = [(ue[0], k1), (ue[1], k1), (ue[1], k0), (ue[0], k0)]
        r.stab(S, (a1 - 0.2, yd + 0.05, z0 + 1.05), (a1 - 0.2, yd + 0.12, z0 + 1.05), 0.03, "kleinteile", u_bereich=ue, kappen=True)
        for sx_ in (a0 - 0.06, a1 + 0.06):
            r.brett(S, (sx_, IV, z0), (sx_, IV, z1 + 0.12), 0.12, (0, 1, 0), "balken", tiefe=0.04)
        r.brett(S, (a0 - 0.12, IV, z1 + 0.06), (a1 + 0.12, IV, z1 + 0.06), 0.12, (0, 1, 0), "balken", tiefe=0.045)   # vor den Pfosten
    # Schlot ueber der Herdhaube bis zur Decke (fuehrt in den Schornstein aussen an der rechten Wand)
    kx0, kx1, ky0, ky1 = g1.KAMIN
    hy0, hy1 = ky0 - 0.3 + DY_RECHTS, ky1 + 0.3 + DY_RECHTS
    r.kasten(S, (kx0 - 0.16 + DX_RECHTS, hy0 + 0.06, g1.Z_HAUBE + 0.2), (IX, hy1 - 0.06, M.Z_EG_DECKE), "lehmwand", ohne=("+x", "+z", "-z"), laengs="y")
    return r


def baue_glas():
    g = Teil("Glas", BI, saat=7)
    for wand, (a0, a1), (z0, z1), art in M.FENSTER:
        if art == "fenster" and wand.endswith("_eg") and wand != "hinten_eg":
            scheibe(g, "Innenglas", wand, a0, a1, z0, z1, -M.T / 2 - 0.004, zu=RAUM)
    return g


def baue_zimmertueren():
    """Zimmertuer der Wohnstube in der Bretterwand (zum Treppengang und hinauf zu den Raeumen): EIN Tuerblatt „Zimmertuer_Gang“ fuer
    Hinterzimmer und Bibliothek – beide Anker „Tuer_Wohnstube_<Raum>“ verweisen mit blatt=Gang darauf (das Spiel zeigt die Tuer,
    sobald das Haus einen der Raeume hat). Frueher zwei deckungsgleiche Tuerblaetter: beim Backen gegenseitig verdeckt (schwarz),
    im Steinhaus mit beiden Raeumen doppelt gezeichnet."""
    x0, x1 = M.STUBE_TUER
    c = Teil("Zimmertuer_Gang", BI, saat=101)
    m = tuer_quer(c, x0, x1, IH - 0.001, ZB, M.ZIMMERTUER[1], richtung=-1, klinke_rechts=True)
    return [c], {"Hinterzimmer": m, "Bibliothek": m}


# ── Einrichtung der Stufe 2 (wirtschaft.json, wohnen.einrichtung, abStufe 2): Vorratskammer, Gute Stube, Eiserne Riegel ───

def baue_vorratskammer():
    """Vorratskammer: Vorratsgestell an der rechten Wand hinten mit Toepfen, Saecken, Kaeselaib und Faesschen."""
    c = Teil("Einrichtung_Vorratskammer", BI, saat=111)
    x0, x1, y0, y1 = IX - 0.42, IX - 0.02, 0.80, 1.70
    for y in (y0 + 0.03, y1 - 0.03):
        for x in (x0 + 0.03, x1 - 0.03):
            c.stab(S, (x, y, ZB), (x, y, ZB + 1.85), 0.05, "balken")
    boeden = (0.12, 0.62, 1.12, 1.62)
    for z in boeden:
        c.kasten(S, (x0, y0, ZB + z), (x1, y1, ZB + z + 0.03), "bohlen", laengs="y")
    ton, gelb = feld("ton"), feld("zwiebel_gelb")
    for i, y in enumerate((0.95, 1.20, 1.47)):
        c.dreh(S, (x0 + 0.20, y, ZB + 0.65), [(0.07, 0.0), (0.10, 0.06), (0.11, 0.16), (0.08, 0.25), (0.085, 0.28)], "kleinteile", seiten=8, deckel=True, u_bereich=ton)
    for y in (1.05, 1.45):
        c.dreh(S, (x0 + 0.20, y, ZB + 0.15), [(0.15, 0.0), (0.18, 0.15), (0.16, 0.36), (0.07, 0.46)], "sackleinen", seiten=7, deckel=True)
    c.dreh(S, (x0 + 0.20, 1.10, ZB + 1.15), [(0.16, 0.0), (0.17, 0.04), (0.17, 0.09), (0.15, 0.11)], "kleinteile", seiten=10, deckel=True, u_bereich=gelb)
    # Faesschen: Band "balken" (der Deckel liest die Mitte des Bandes; in "bohlen" liegt dort eine Brettfuge - Normale schief)
    c.dreh(S, (x0 + 0.21, 1.48, ZB + 1.15), [(0.12, 0.0), (0.145, 0.15), (0.12, 0.30)], "balken", seiten=8, deckel=True, gedreht=True)
    for y in (0.95, 1.30, 1.58):
        c.dreh(S, (x0 + 0.20, y, ZB + 1.65), [(0.06, 0.0), (0.085, 0.07), (0.07, 0.17), (0.045, 0.21)], "kleinteile", seiten=8, deckel=False, u_bereich=ton)
    return c


def baue_stube():
    """Gute Stube: Stollenschrank an der linken Wand hinten, Lehnstuhl mit Kissen am Fenster."""
    c = Teil("Einrichtung_Stube", BI, saat=113)
    x0, x1, y0, y1 = -IX + 0.02, -IX + 0.50, -1.25, -0.25
    zs = ZB + 0.30
    for y in (y0 + 0.04, y1 - 0.04):
        for x in (x0 + 0.04, x1 - 0.04):
            c.kasten(S, (x - 0.04, y - 0.04, ZB), (x + 0.04, y + 0.04, zs), "balken", ohne=("-z",), laengs="z")
    c.kasten(S, (x0, y0, zs), (x1, y1, ZB + 1.80), "balken", laengs="z")
    c.kasten(S, (x0 - 0.03, y0 - 0.03, ZB + 1.80), (x1 + 0.04, y1 + 0.03, ZB + 1.88), "balken", laengs="y")
    for ya, yb in ((y0 + 0.08, (y0 + y1) / 2 - 0.02), ((y0 + y1) / 2 + 0.02, y1 - 0.08)):     # zwei Tueren mit Fuellung
        c.kasten(S, (x1, ya, zs + 0.15), (x1 + 0.025, yb, ZB + 1.65), "bohlen", ohne=("-x",), laengs="z")
        c.kasten(S, (x1 + 0.025, ya + 0.06, zs + 0.25), (x1 + 0.04, yb - 0.06, ZB + 1.55), "balken", ohne=("-x",), laengs="z")
    ue = feld("eisen")
    for y in ((y0 + y1) / 2 - 0.08, (y0 + y1) / 2 + 0.08):
        c.stab(S, (x1 + 0.04, y, ZB + 1.10), (x1 + 0.08, y, ZB + 1.10), 0.025, "kleinteile", u_bereich=ue, kappen=True)
    # Lehnstuhl am Fenster (Blick in den Raum)
    sx, sy = -IX + 0.45, 0.02
    sitz = ZB + 0.44
    for dx in (-0.22, 0.22):
        for dy in (-0.22, 0.22):
            c.kasten(S, (sx + dx - 0.03, sy + dy - 0.03, ZB), (sx + dx + 0.03, sy + dy + 0.03, sitz), "balken", ohne=("-z",), laengs="z")
    c.kasten(S, (sx - 0.25, sy - 0.25, sitz), (sx + 0.25, sy + 0.25, sitz + 0.05), "bohlen", laengs="x")
    c.kasten(S, (sx - 0.215, sy - 0.205, sitz + 0.05), (sx + 0.20, sy + 0.215, sitz + 0.12), "wolle", laengs="x")        # Kissen
    c.kasten(S, (sx - 0.27, sy - 0.25, sitz - 0.005), (sx - 0.22, sy + 0.25, ZB + 1.15), "balken", laengs="z")           # Lehne
    for dy in (-0.25, 0.22):
        c.kasten(S, (sx - 0.22, sy + dy, sitz + 0.055), (sx + 0.25, sy + dy + 0.04, sitz + 0.30), "balken", laengs="x")   # Armlehnen
    # (je 5 mm versetzt: Kissen, Lehne, Armlehnen und Sitz decken sich nirgends in einer Ebene)
    return c


def baue_riegel():
    """Eiserne Riegel: Querbalken mit Eisenbeschlag vor der Haustuer und eine Eisenstange vor dem Fenster an der Gasse."""
    c = Teil("Einrichtung_Riegel", BI, saat=115)
    ue = feld("eisen")
    a0, a1 = M.FENSTER[0][1]
    yd = M.VORN + 0.16
    for x in (a0 - 0.14, a1 + 0.14):
        c.kasten(S, (x - 0.05, IV, ZB + 1.10), (x + 0.05, yd + 0.105, ZB + 1.24), "kleinteile", ohne=("-y",), laengs="x")   # 5 mm vor dem Tuerrahmen
    c.kasten(S, (a0 - 0.22, yd + 0.01, ZB + 1.13), (a1 + 0.22, yd + 0.09, ZB + 1.21), "balken", laengs="x")
    for x in (a0 + 0.1, a1 - 0.1):
        c.kasten(S, (x - 0.02, yd + 0.005, ZB + 1.12), (x + 0.02, yd + 0.095, ZB + 1.22), "kleinteile", laengs="z")
    k0, k1, _ = BI["kleinteile"]
    for fl in c.flaechen:
        if fl[1][0][1] >= k0 - 1e-6: fl[1][:] = [(ue[0], k1), (ue[1], k1), (ue[1], k0), (ue[0], k0)][:len(fl[1])]
    w0, w1 = M.FENSTER[1][1]
    zf = (M.FENSTER[1][2][0] + M.FENSTER[1][2][1]) / 2
    c.stab(S, (w0 - 0.12, IV + 0.03, zf), (w1 + 0.12, IV + 0.03, zf), 0.035, "kleinteile", u_bereich=ue, kappen=True)
    return c


def baue_schmuck():
    """Schmuck der Stufe (alte Wohnstube: ab Buergerhaus Teppich und Tellerbord): Teppich in der Mitte, Tellerbord mit Zinntellern
    an der Rueckwand. Fest im Modell der Stufe."""
    c = Teil("Schmuck_Set", BI, saat=117)
    x0, x1, y0, y1 = -0.80, 1.00, -1.25, 0.65
    v0, v1, m = BI["wolle"]
    c.vieleck(S, [(x0, y0, ZB + 0.008), (x1, y0, ZB + 0.008), (x1, y1, ZB + 0.008), (x0, y1, ZB + 0.008)], [(0, v1), ((x1 - x0) / m, v1), ((x1 - x0) / m, v0), (0, v0)], zu=(0, 0, 9))
    # Feld des Teppichs rot (Kleinteile), die Wolle bleibt als Rand
    rot = feld("zwiebel_rot")
    c.rechteck(S, (x0 + 0.14, y0 + 0.14, ZB + 0.011), (1, 0, 0), (0, 1, 0), x1 - x0 - 0.28, y1 - y0 - 0.28, "kleinteile", u_bereich=rot, zu=(0, 0, 9))
    bx0, bx1, zb = -0.40, 0.90, ZB + 1.80
    c.kasten(S, (bx0, IH - 0.22, zb), (bx1, IH - 0.005, zb + 0.03), "bohlen", ohne=("+y",), laengs="x")
    c.kasten(S, (bx0, IH - 0.22, zb + 0.03), (bx1, IH - 0.20, zb + 0.07), "balken", ohne=("+y",), laengs="x")
    for x in (bx0 + 0.05, bx1 - 0.05):
        c.vieleck(S, [(x - 0.015, IH, zb - 0.18), (x + 0.015, IH, zb - 0.18), (x + 0.015, IH - 0.18, zb), (x - 0.015, IH - 0.18, zb)], [(0.1, 0.30), (0.12, 0.30), (0.12, 0.22), (0.1, 0.22)], zu=(x, IH - 1, zb))
    ue = feld("eisen")
    for i, x in enumerate((-0.18, 0.25, 0.68)):
        o = Vector((x, IH - 0.10, zb + 0.20))
        ring = [o + Vector((0.17 * math.cos(2 * math.pi * k / 10), 0.035 * (k % 2) * 0, 0.17 * math.sin(2 * math.pi * k / 10))) for k in range(10)]
        for k in range(1, 9):
            c.vieleck(S, [ring[0], ring[k], ring[k + 1]], [(ue[0], BI["kleinteile"][1]), (ue[1], BI["kleinteile"][1]), (ue[1], BI["kleinteile"][0])], zu=o + Vector((0, -1, 0)))
    return c


# ── Einrichtung der Stufe 3 (wirtschaft.json, abStufe 3): Kueche, Hofhund; Schmuck ab Steinhaus: Wandbehang ──────────────────

def baue_kueche():
    """Kueche: Anrichte rechts neben der Haustuer mit Schuesseln und Brotlaib, darueber ein Bord mit Kupfertoepfen und Pfannen."""
    c = Teil("Einrichtung_Kueche", BI, saat=121)
    x0, x1, y0, y1 = 1.74, IX - 0.02, IV, IV + 0.42
    zp = ZB + 0.86
    for x in (x0 + 0.04, x1 - 0.04):
        c.kasten(S, (x - 0.04, y0 + 0.02, ZB), (x + 0.04, y1 - 0.04, zp - 0.05), "balken", ohne=("-z",), laengs="z")
    c.kasten(S, (x0, y0, zp - 0.05), (x1, y1, zp), "bohlen", laengs="x")
    c.kasten(S, (x0 + 0.04, y0 + 0.02, ZB + 0.12), (x1 - 0.04, y1 - 0.06, ZB + 0.15), "bohlen", laengs="x")
    ton, gelb, eisen = feld("ton"), feld("zwiebel_gelb"), feld("eisen")
    for x, r in ((x0 + 0.16, 0.11), (x0 + 0.40, 0.09)):
        c.dreh(S, (x, y0 + 0.22, zp), [(0.05, 0.0), (r, 0.05), (r + 0.02, 0.08)], "kleinteile", seiten=9, deckel=False, u_bereich=ton)
    c.dreh(S, (x1 - 0.12, y0 + 0.20, zp), [(0.08, 0.0), (0.10, 0.04), (0.08, 0.09), (0.0, 0.10)], "kleinteile", seiten=8, deckel=False, u_bereich=gelb)
    zb = ZB + 1.62
    c.kasten(S, (x0, y0, zb), (x1, y0 + 0.24, zb + 0.03), "bohlen", ohne=("-y",), laengs="x")
    for x in (x0 + 0.12, x0 + 0.38):
        c.dreh(S, (x, y0 + 0.13, zb + 0.03), [(0.09, 0.0), (0.10, 0.14), (0.11, 0.15)], "kleinteile", seiten=9, deckel=False, u_bereich=ton)
    for i, x in enumerate((x0 + 0.10, x0 + 0.30, x0 + 0.50)):           # Pfannen an Haken unter dem Bord
        c.stab(S, (x, y0 + 0.04, zb - 0.02), (x, y0 + 0.04, zb - 0.28), 0.02, "kleinteile", u_bereich=eisen)
        c.dreh(S, (x, y0 + 0.06, zb - 0.45 - 0.03 * i), [(0.0, 0.0), (0.10, 0.0), (0.11, 0.04)], "kleinteile", seiten=8, deckel=False, u_bereich=eisen)
    return c


def baue_hund():
    """Hofhund: liegt vor der Rueckwand unter dem Wandbehang auf einer Decke (grob: Rumpf, Kopf, Ohren, Schwanz, Laeufe)."""
    c = Teil("Einrichtung_Hund", BI, saat=123)
    hx, hy = 0.30, 1.30
    rinde, russ = feld("rinde"), feld("russ")
    c.rechteck(S, (hx - 0.50, hy - 0.32, ZB + 0.006), (1, 0, 0), (0, 1, 0), 1.0, 0.64, "sackleinen", zu=(0, 0, 9))
    c.kasten(S, (hx - 0.36, hy - 0.15, ZB + 0.01), (hx + 0.24, hy + 0.15, ZB + 0.30), "kleinteile", laengs="x")
    for fl in c.flaechen[-6:]:
        k0, k1, _ = BI["kleinteile"]; fl[1][:] = [(rinde[0], k1), (rinde[1], k1), (rinde[1], k0), (rinde[0], k0)]
    c.dreh(S, (hx + 0.38, hy, ZB + 0.12), [(0.0, 0.0), (0.10, 0.02), (0.12, 0.12), (0.09, 0.22), (0.0, 0.24)], "kleinteile", seiten=7, deckel=False, u_bereich=rinde)
    c.kasten(S, (hx + 0.46, hy - 0.05, ZB + 0.10), (hx + 0.62, hy + 0.05, ZB + 0.18), "kleinteile", laengs="x")          # Schnauze
    for fl in c.flaechen[-6:]:
        k0, k1, _ = BI["kleinteile"]; fl[1][:] = [(russ[0], k1), (russ[1], k1), (russ[1], k0), (russ[0], k0)]
    for dy in (-0.09, 0.09):
        c.vieleck(S, [(hx + 0.34, hy + dy, ZB + 0.30), (hx + 0.44, hy + dy, ZB + 0.30), (hx + 0.36, hy + dy * 1.6, ZB + 0.40)], [(russ[0], BI["kleinteile"][1]), (russ[1], BI["kleinteile"][1]), (russ[1], BI["kleinteile"][0])], zu=(hx + 0.38, hy + dy * 3, ZB + 0.3))
        c.stab(S, (hx + 0.20, hy + dy, ZB + 0.04), (hx + 0.58, hy + dy * 1.3, ZB + 0.04), 0.06, "kleinteile", u_bereich=rinde)      # Vorderlaeufe
    c.stab(S, (hx - 0.36, hy, ZB + 0.22), (hx - 0.62, hy + 0.18, ZB + 0.06), 0.05, "kleinteile", u_bereich=rinde)                 # Schwanz
    return c


BEHANG = dict(x0=-0.30, x1=0.85, unten=0.62, oben=1.68, wand=0.02)   # Lage an der Rueckwand (x, Hoehe ueber ZB, Abstand zur Wand)
BEHANG_SPALTEN, BEHANG_BAHNEN, BEHANG_GLIEDER = 10, 5, 3            # Felder quer; Ketten der Feder (Bahnen) und Glieder je Bahn
BEHANG_SAUM, BEHANG_SAUM_STREIFEN, BEHANG_FRANSEN = 0.07, 4, 0.05   # Saum 7 cm aus 4 Streifen, Fransen 5 cm lang


def behang_masse():
    """(x0, x1, z0, z1, y) des Behangs in der Wohnstube (vor dem Versatz dx der Grundform)."""
    b = BEHANG
    return b["x0"], b["x1"], ZB + b["unten"], ZB + b["oben"], IH - b["wand"]


def baue_wandbehang():
    """Schmuck ab Steinhaus (alte Wohnstube: Wandbehang in der Hausfarbe): Behang an einer Stange an der Rueckwand unter dem Tellerbord.
    Feinschliff (Nutzer 17.11.2026): Stoff „Behang“ (das Spiel faerbt ihn im gedaempften Ton der Hausfarbe, Behangfarbe.cs), unten ein
    dunklerer Saum „Behangsaum“ aus 4 Streifen (7 cm, leicht gefaltet: abwechselnd 4 mm vor der Stoffebene) und Fransen (5 cm); Stange und
    Quasten wie bisher. Das Netz ist in Felder geteilt, deren Zeilen auf den Gelenken der Feder liegen (BEHANG_GLIEDER, behang_skelett)."""
    c = Teil("Schmuck_Wandbehang", BI, saat=125)
    x0, x1, z0, z1, y = behang_masse()
    uv = [(0.1, 0.8)] * 4
    xs = [x0 + (x1 - x0) * i / BEHANG_SPALTEN for i in range(BEHANG_SPALTEN + 1)]
    zs = sorted({z1 - (z1 - z0) * k / BEHANG_GLIEDER for k in range(BEHANG_GLIEDER)} | {z0 + BEHANG_SAUM}, reverse=True)
    zu = (0, 0, 1.2)
    for za, zb in zip(zs, zs[1:]):                    # Stoff: Zeilen an den Gelenken, bis zum Saum
        for xa, xb in zip(xs, xs[1:]):
            c.vieleck("Behang", [(xa, y, zb), (xb, y, zb), (xb, y, za), (xa, y, za)], uv, zu=zu)
    h = BEHANG_SAUM / BEHANG_SAUM_STREIFEN            # Saum: Streifen abwechselnd schraeg nach vorn und zurueck (Licht und Schatten)
    tiefe = lambda k: y - (0.004 if k % 2 else 0.0)
    for k in range(BEHANG_SAUM_STREIFEN):
        za, zb = z0 + BEHANG_SAUM - k * h, z0 + BEHANG_SAUM - (k + 1) * h
        for xa, xb in zip(xs, xs[1:]):
            c.vieleck("Behangsaum", [(xa, tiefe(k + 1), zb), (xb, tiefe(k + 1), zb), (xb, tiefe(k), za), (xa, tiefe(k), za)], uv, zu=zu)
    n = 23                                            # Fransen: schmale Streifen, oben an der Unterkante des Saums
    for i in range(n):
        xm = x0 + 0.02 + (x1 - x0 - 0.04) * i / (n - 1)
        c.vieleck("Behangsaum", [(xm - 0.005, y, z0 - BEHANG_FRANSEN), (xm + 0.005, y, z0 - BEHANG_FRANSEN),
                                 (xm + 0.006, y, z0), (xm - 0.006, y, z0)], uv, zu=zu)
    c.stab(S, (x0 - 0.08, y - 0.03, z1 + 0.03), (x1 + 0.08, y - 0.03, z1 + 0.03), 0.035, "balken", kappen=True)
    for x in (x0 + 0.10, (x0 + x1) / 2, x1 - 0.10):
        c.vieleck(S, [(x - 0.04, y - 0.005, z0 - 0.08), (x + 0.04, y - 0.005, z0 - 0.08), (x, y - 0.005, z0)], [(feld("zwiebel_gelb")[0], BI["kleinteile"][1]), (feld("zwiebel_gelb")[1], BI["kleinteile"][1]), (feld("zwiebel_gelb")[1], BI["kleinteile"][0])], zu=(x, y - 1, z0))
    return c


def behang_skelett(objekte, dx):
    """Knochen fuer die Stoff-Feder des Wandbehangs (wie die Roecke, Stoff-Feder #486; Nutzer 17.11.2026): Armatur „Behang_Skelett“ mit
    dem Wurzelknochen „Behang“ an der Stange und je Bahn b (0 links bis BEHANG_BAHNEN-1 rechts) der Kette „Behang_<b>_0“ bis
    „Behang_<b>_3“: Glied 0 setzt an der Stange an, Glied 3 ist der Endknochen am Saum (Laenge = Fransen). Gewichte: Stange und oberste
    Zeile ganz an „Behang“ (haengt fest), darunter je Zeile das Glied, in dessen Abschnitt sie liegt (Saum, Fransen und Quasten am
    Glied 2), quer linear zwischen den beiden naechsten Bahnen. Der Behang haengt in allen Steinhaeusern frei an der Stange (unten nicht
    befestigt), also schwingt er (die Feder schaltet das Spiel ein). Rueckgabe: [Armatur] oder [] ohne Behang (Stufe 2)."""
    netz = next((o for o in objekte if o.name.split(".")[0] == "Schmuck_Wandbehang"), None)
    if netz is None:
        return []
    x0, x1, z0, z1, y = behang_masse()
    x0, x1 = x0 + dx, x1 + dx
    daten = bpy.data.armatures.new("Behang_Skelett")
    arm = bpy.data.objects.new("Behang_Skelett", daten)
    bpy.context.scene.collection.objects.link(arm)
    bpy.ops.object.select_all(action="DESELECT")
    arm.select_set(True); bpy.context.view_layer.objects.active = arm
    bpy.ops.object.mode_set(mode="EDIT")
    wurzel = daten.edit_bones.new("Behang")
    wurzel.head, wurzel.tail = Vector(((x0 + x1) / 2, y, z1)), Vector(((x0 + x1) / 2, y, z1 + 0.06))
    hoehen = [z1 - (z1 - z0) * k / BEHANG_GLIEDER for k in range(BEHANG_GLIEDER + 1)] + [z0 - BEHANG_FRANSEN]
    for b in range(BEHANG_BAHNEN):
        x = x0 + (x1 - x0) * (b + 0.5) / BEHANG_BAHNEN
        eltern = wurzel
        for g in range(BEHANG_GLIEDER + 1):
            k = daten.edit_bones.new(f"Behang_{b}_{g}")
            k.head, k.tail = Vector((x, y, hoehen[g])), Vector((x, y, hoehen[g + 1]))
            k.parent = eltern; k.use_connect = g > 0
            eltern = k
    bpy.ops.object.mode_set(mode="OBJECT")
    gruppen = {"Behang": netz.vertex_groups.new(name="Behang")}
    for b in range(BEHANG_BAHNEN):
        for g in range(BEHANG_GLIEDER + 1):
            gruppen[f"Behang_{b}_{g}"] = netz.vertex_groups.new(name=f"Behang_{b}_{g}")
    for v in netz.data.vertices:
        p = netz.matrix_world @ v.co
        if p.z >= z1 - 1e-3:
            gruppen["Behang"].add([v.index], 1.0, "REPLACE"); continue
        t = (z1 - p.z) / (z1 - z0)
        g = min(int(t * BEHANG_GLIEDER - 1e-6), BEHANG_GLIEDER - 1)
        s = min(max((p.x - x0) / (x1 - x0) * BEHANG_BAHNEN - 0.5, 0.0), BEHANG_BAHNEN - 1.0)
        b0 = min(int(s), BEHANG_BAHNEN - 1); f = s - b0
        gruppen[f"Behang_{b0}_{g}"].add([v.index], 1.0 - f, "REPLACE")
        if f > 1e-6:
            gruppen[f"Behang_{b0 + 1}_{g}"].add([v.index], f, "REPLACE")
    netz.parent = arm
    mod = netz.modifiers.new("Skelett", "ARMATURE"); mod.object = arm
    print("BEHANG Knochen:", len(daten.bones), "Bahnen", BEHANG_BAHNEN, "Glieder je Bahn", BEHANG_GLIEDER, "+ Endknochen; Dreiecke",
          sum(len(p.vertices) - 2 for p in netz.data.polygons))
    return [arm]


def baue_hauswappen_klein():
    """Hauswappen („Wappen ueber der Tuer“, Stufe 1): ueber der Haustuer innen (die Wohnstube hat eine Decke statt des offenen Giebels)."""
    c = Teil("Einrichtung_Hauswappen", BI, saat=87)
    g = 0.34
    a0, a1 = M.FENSTER[0][1]
    o = ((a0 + a1) / 2, IV, M.Z_EG_DECKE - 0.21)
    eu, ev, n = (-1, 0, 0), (0, 0, 1), (0, 1, 0)
    g1.prisma(c, "Innen", [(x * 1.2 * g, y * 1.14 * g) for x, y in g1.SCHILD], o, eu, ev, n, 0.0, 0.05, "balken")
    g1.prisma(c, "Hausfarbe", [(x * g, y * g) for x, y in g1.SCHILD], o, eu, ev, n, 0.05, 0.08, "balken", farbe=(1.0, 1.0, 1.0, 1.0))
    return c


def moebel():
    """Die Moebel der Stufe 1 an ihren Platz in der Wohnstube (rechte Wand: Herd, Vorrat; linke Wand: Tisch, Bauplan, Werkzeug;
    Gassenwand: Truhe, Hausapotheke, Hauswaechter)."""
    rechts = (DX_RECHTS, DY_RECHTS)
    herd = versetze(g1.baue_herd(), t=rechts)
    vorrat = g1.baue_vorrat()
    vorrat.flaechen = [fl for fl in vorrat.flaechen if sum(p.y for p in fl[0]) / len(fl[0]) < 0.80]    # ohne Hackklotz (dort steht die Vorratskammer)
    versetze(vorrat, t=rechts)
    # Tisch mit Bank, Familienbuch, Geldkassette, Bauplan und Werkzeug: von der linken Wand der Probe an die Rueckwand (links der Zimmertuer)
    tisch = versetze(g1.baue_tisch(), *TISCH_LAGE)
    gegen = [g1.baue_truhe()] + [versetze(f(), *TISCH_LAGE) for f in (g1.baue_familienbuch, g1.baue_bauplan, g1.baue_werkzeug, g1.baue_geldkassette)]
    links = (DX_LINKS, 0.0)
    apotheke = g1.baue_hausapotheke()
    waechter = s1.teile_auf(g1.baue_hauswaechter(), lambda m: "schemel" if m.x > -0.6 else "rest")
    versetze(waechter["rest"], t=links)
    w = s1.zusammen("Einrichtung_Hauswaechter", waechter.values())
    return [herd, vorrat, tisch], gegen, [apotheke, w]


def anker_innen(tuer_mitten):
    A = s1.Anker
    kx0, kx1, ky0, ky1 = g1.KAMIN
    tuer = next(f for f in M.FENSTER if f[3] == "tuer")
    tm = (tuer[1][0] + tuer[1][1]) / 2
    liste = [
        A("Blick_Auge", M.STUBE_AUGE), A("Blick_Ziel", M.STUBE_ZIEL),
        # Grundlicht (OmniLight ohne Schatten) ueber dem Blickpunkt wie bei den Elben (Feinschliff 20.11.2026): Jede senkrechte Flaeche,
        # die das Auge sieht, zeigt dann auch zum Licht. In der Raummitte (x 0; davor auch y -0,3) lag es hinter den Seiten der zwei
        # Deckenbalken vor der Fensterwand und hinter der Schlotseite ueber dem Herd – nur Umgebungslicht, im dunklen Holz fast schwarz
        A("Licht_Herd", ((kx0 + kx1) / 2 - 0.35 + DX_RECHTS, (ky0 + ky1) / 2 + DY_RECHTS, 0.95)), A("Licht_Raum", (M.STUBE_AUGE[0], M.STUBE_AUGE[1], M.Z_EG_DECKE - LICHT_UNTER_DECKE)),
    ] + ([A("Licht_Decke", (-IX + 0.30, -0.3, ZB + 0.6)), A("Licht_Decke_Ziel", (0.6, -0.3, M.Z_EG_DECKE))] if LICHT_DECKE else []) + [
        A("Feuer_Herd", ((kx0 + kx1) / 2 - 0.18 + DX_RECHTS, (ky0 + ky1) / 2 + DY_RECHTS, 0.53)),
        A("Tuer_Innen", (tm, IV, ZB + M.TUER_LICHT[1] / 2)),
        A("Platz_Oberhaupt", (*an_rueckwand(g1.BANK_X, g1.TISCH[2] + 0.52), ZB), "nord", haltung="sitzt", rolle="oberhaupt", sitzhoehe=g1.SITZ_HOEHE),
        A("Platz_Partner", (1.3 + DX_RECHTS, ky0 - 0.3 - 0.42 + DY_RECHTS, ZB), "ost", haltung="ruehrt", rolle="partner"),
        A("Platz_Kind_1", (*an_rueckwand(g1.BANK_X, g1.TISCH[2] + 0.90), ZB), "nord", haltung="sitzt", rolle="kind", sitzhoehe=g1.SITZ_HOEHE),
        A("Platz_Kind_2", (*an_rueckwand(*g1.SCHEMEL), ZB), "west", haltung="sitzt", rolle="kind", sitzhoehe=g1.SITZ_HOEHE),
        A("Platz_Kind_3", (g1.HERDSCHEMEL[0] + DX_RECHTS, g1.HERDSCHEMEL[1] + DY_RECHTS, ZB), "ost", haltung="sitzt", rolle="kind", sitzhoehe=g1.SITZ_HOEHE),
        A("Platz_Kind_4", (0.2, 0.2, ZB), "nord", haltung="spielt", rolle="kind"),
    ]
    for raum, m in tuer_mitten.items():
        liste.append(A(f"Tuer_Wohnstube_{raum}", tuple(m), ziel=raum, breite=M.ZIMMERTUER[0], hoehe=M.ZIMMERTUER[1], achse="z", blatt="Gang"))
    return liste


# ═══════════════════════════════════════════════════════════════════════════════════════════════════════════════════════

def main():
    args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    opt = dict((a[2:].split("=", 1) + ["1"])[:2] for a in args if a.startswith("--"))
    stufe, form = int(opt.get("stufe", "2")), int(opt.get("form", "1"))
    if stufe not in (2, 3) or form not in M.FORMEN:
        raise SystemExit("--stufe=<2|3> --form=<1..4>")
    M.stufe_setzen(stufe)                          # Stufe 3: tieferer Gang, drittes Geschoss, Treppengiebel
    globals()["MASSWERK_SCHNITT"] = stufe == 3 and "schnitt-alt" not in opt   # kein deckungsgleiches Dreieck im Masswerk (sonst schwarze Spitze)
    globals()["DACH_HINTER"] = stufe == 3 and "dach-alt" not in opt   # Dachvorderkante hinter den Nischengrund (fuer jeden Aufrufer von main; --dach-alt: alter Stand)
    if stufe == 3 and "gaube-alt" not in opt: GAUBE.update(GAUBE_PATRIZIER)   # Gaube hinter dem Schornstein, aus Quaderlagen (fuer jeden Aufrufer von main); --gaube-alt: alter Stand fuer Vergleichsbilder
    waende_setzen()
    M.FENSTER = M.fenster(form, stufe)             # Oeffnungen der Grundform (alle Bauteile lesen M.FENSTER)
    F = M.FORMEN[form]
    name = f"wohnhaus_mensch_s{stufe}_g{form}"
    blatt, blatt_innen = f"trim_s{stufe}", "innen_trim_s23"
    bpy.ops.wm.read_factory_settings(use_empty=True)
    if bpy.context.scene.world is None: bpy.context.scene.world = bpy.data.worlds.new("Welt")
    stoff = g1.stoff
    aussen_stoffe = {
        "Dachziegel": stoff("Dachziegel", blatt), "Firstziegel": stoff("Firstziegel", blatt), "Fachwerk": stoff("Fachwerk", blatt),
        "Schiefer": stoff("Schiefer", blatt), "Firstblech": stoff("Firstblech", blatt), "Haustein": stoff("Haustein", blatt),
        "Lehmputz": stoff("Lehmputz", blatt), "Bruchstein": stoff("Bruchstein", blatt),
        "Fenster": stoff("Fenster", farbe=(0.09, 0.11, 0.14), rauheit=0.2), "Bleinetz": stoff("Bleinetz", farbe=(0.1, 0.1, 0.11), alpha=0.5),
        "Hausfarbe": stoff("Hausfarbe", farbe=(0.55, 0.12, 0.10)), "Holz": stoff("Holz", farbe=(0.30, 0.22, 0.15)), "Eisen": stoff("Eisen", farbe=(0.12, 0.12, 0.13), rauheit=0.5),
    }
    innen_stoffe = {"Innen": stoff("Innen", blatt_innen), "Innenglas": stoff("Innenglas", farbe=(0.55, 0.62, 0.6), alpha=0.16, rauheit=0.15),
                    "Hausfarbe": aussen_stoffe["Hausfarbe"],
                    # Wandbehang: Vorgabe Krapprot (Behang zu Rot, Behangfarbe.cs), Saum dunkler; das Spiel setzt den Ton der Hausfarbe
                    "Behang": stoff("Behang", farbe=(0.557, 0.165, 0.141)), "Behangsaum": stoff("Behangsaum", farbe=(0.334, 0.099, 0.085))}

    huelle = [baue_haus(), baue_dach()]
    scheiben = baue_scheiben()
    sh0, sd0 = len(huelle[0].flaechen), len(huelle[1].flaechen)
    if "fluegel" in F:
        FM.fluegel_dach(huelle[1], FM.fluegel(huelle[0], F["fluegel"]), ziegelflaeche)
    if "schuppen" in F:
        FM.schuppen(huelle[0], huelle[1], F["schuppen"], ziegelflaeche)
    if "erker" in F:
        FM.erker(huelle[0], huelle[1], F["erker"], scheiben, ziegelflaeche)
    if stufe == 3:                                 # Steinhaus ganz aus Stein (Nutzer 14.11.2026, abweichend von den Vorlagen)
        if "schuppen" in F:                        # Schuppen: Pfeiler und Rueckwand aus Haustein, nichts entfaellt
            versteinern(huelle[0], ab=sh0, z_min=0.65, ganz=True)
            versteinern(huelle[1], ab=sd0, z_min=-1.0, ganz=True)
        versteinern(huelle[0], bis=None if "schuppen" not in F else sh0)
        versteinern(huelle[1], bis=None if "schuppen" not in F else sd0)
    zusatz = [baue_wappen(), baue_leerstand(), baue_geruest()]
    fahne = g1.fahne_verankern(baue_fahne(), huelle[1])      # Mast im Dach verankert, Schelle am Fuss (Mensch-Nachtrag 16.11.2026)
    m_raum, gegenstaende, einr = moebel()
    raum = [baue_raum(), baue_fensterseiten(), baue_decke(), baue_schmuck()] + m_raum
    zimmertueren, tuer_mitten = baue_zimmertueren()
    einrichtung = einr + [baue_hauswappen_klein(), baue_vorratskammer(), baue_stube(), baue_riegel()] + zimmertueren
    if stufe == 3:
        einrichtung += [baue_kueche(), baue_hund()]
        raum.append(baue_wandbehang())
    glas = baue_glas()
    dx = F["dx"]                                   # der Kern rueckt auf dem Platz (Platz fuer Fluegel bzw. Schuppen)
    if dx:
        versetze(huelle + [scheiben] + zusatz + [fahne] + raum + einrichtung + gegenstaende + [glas], t=(dx, 0.0))
    if stufe == 3:                                 # Stoffnamen des Steinhauses (Brandschaden und Russ greifen nach ihnen)
        neu = {"Dachziegel": "Schiefer", "Firstziegel": "Firstblech", "Bruchstein": "Haustein"}
        for t in huelle + [scheiben] + zusatz:
            t.flaechen = [(pts, uv, neu.get(st, st), fa, gl) for pts, uv, st, fa, gl in t.flaechen]
    zaehle = lambda teile: {t.name: t.dreiecke() for t in teile}
    print("DREIECKE aussen:", zaehle(huelle + [scheiben]), "Summe", sum(t.dreiecke() for t in huelle + [scheiben]))
    print("DREIECKE Zustand und Besitz:", zaehle(zusatz + [fahne]))
    print("DREIECKE innen:", zaehle(raum + [glas]), "Summe", sum(t.dreiecke() for t in raum + [glas]))
    print("DREIECKE Einrichtung:", zaehle(einrichtung))
    print("DREIECKE Gegenstaende:", zaehle(gegenstaende))
    xs = [p.x for t in huelle for fl in t.flaechen for p in fl[0]]; ys = [p.y for t in huelle for fl in t.flaechen for p in fl[0]]; zs = [p.z for t in huelle for fl in t.flaechen for p in fl[0]]
    print("UMRISS Huelle: x %.2f..%.2f, y %.2f..%.2f, z bis %.2f" % (min(xs), max(xs), min(ys), max(ys), max(zs)))

    o_huelle = [t.objekt(aussen_stoffe) for t in huelle]
    o_zusatz = [t.objekt(aussen_stoffe) for t in zusatz]
    o_scheiben = scheiben.objekt(aussen_stoffe, mit_farbe=False)
    o_fahne = fahne.objekt(aussen_stoffe, mit_farbe=False)
    o_raum = [t.objekt(innen_stoffe, mit_farbe=False) for t in raum]
    o_einr = [t.objekt(innen_stoffe, mit_farbe=False) for t in einrichtung + gegenstaende]
    o_glas = glas.objekt(innen_stoffe, mit_farbe=False)
    for o in o_zusatz[1:]:
        o["zustand"] = True; o["sichtbar"] = False
    aussen = o_huelle + [o_scheiben] + o_zusatz + [o_fahne]
    innen = o_raum + o_einr + [o_glas]

    bpy.ops.mesh.primitive_plane_add(size=40, location=(0, 0, 0))
    boden = bpy.context.active_object; boden.name = "Kontrollboden"
    boden.data.materials.append(stoff("Kontrollboden", farbe=(0.33, 0.3, 0.24)))
    mit_ao = "ohne-ao" not in opt
    # UV2 und Verdeckung mit der berichtigten Hilfsfunktion des Hausskripts (Inselrand 0,003, Rand vor dem Backen, ein Backzug je
    # Gruppe, vierfach fein, Rand selbst gefuellt) – die eigene Fassung des Steinhauses (Inselrand 0,004, Backrand 1) ist ersetzt
    # Huelle in einer eigenen Atlasgruppe je Stufe (2 x 2 Zellen zu 1024 px): in der 512er-Zelle der Stufe 1 blieb die UV2-Dichte der
    # groesseren Haeuser unter 150 Bildpunkten je m2 (uv2_pruefung.uv2_dichte). Die Zellenzahl teilt die Kante ohne Rest (bei 3 x 3 in
    # 2048 lagen Bild und UV2 bis zu 2 Bildpunkte gegeneinander verschoben: kante // n gegen 1 / n).
    g1.uv2_und_ao([(o_huelle, [boden]), (o_zusatz, o_huelle + [boden])], f"wohnhaus_mensch_s{stufe}", name, mit_ao=mit_ao)
    g1.uv2_und_ao([(o_raum, []), (o_einr, o_raum)], "wohnhaus_mensch_innen", name, mit_ao=mit_ao and "innen-ao-aus" not in opt)   # --innen-ao-aus: Innenraum nicht neu backen. der Innen-Atlas und die _innen.glb bleiben unveraendert (Folge-Patch 0004: die _innen.glb wird dann nicht mehr geschrieben)

    tuer = next(f for f in M.FENSTER if f[3] == "tuer")
    x0, x1, y0, y1 = M.SCHORNSTEIN_RECHTS
    a_aussen = [s1.Anker("Anker_Tuer", ((tuer[1][0] + tuer[1][1]) / 2, M.VORN - 0.2, ZB + 0.02)),
                s1.Anker("Anker_Schornstein", ((x0 + x1) / 2, (y0 + y1) / 2, M.Z_FIRST - 0.6 + 0.3))]
    a_innen = anker_innen(tuer_mitten)
    for a in a_aussen + a_innen:
        a.versetze(t=(dx, 0.0))
    g1.exportiere(aussen + [a.objekt() for a in a_aussen], os.path.join(g1.ZIEL_MODELL, f"{name}.glb"))
    skelett = behang_skelett(o_raum, dx)          # Knochen des Wandbehangs (Stufe 3), nach dem Backen: AO und UV2 ohne Armatur
    if "innen-ao-aus" not in opt: g1.exportiere(innen + skelett + [a.objekt() for a in a_innen], os.path.join(g1.ZIEL_MODELL, f"{name}_innen.glb"))   # --innen-ao-aus: Innen-GLB bleibt, wie es ist

    if "bilder" in opt:
        o = opt["bilder"]; os.makedirs(o, exist_ok=True)
        vor = f"s{stufe}_g{form}_blender_"
        kb = g1.kontrollbild
        verschoben = lambda p: tuple(Vector(p) + Vector((dx, 0.0, 0.0)))
        g1_kb = lambda datei, auge, ziel, **kw: kb(datei, verschoben(auge), verschoben(ziel), **kw)
        fest = [x.name for x in innen] + ["Leerstand", "Geruest"]
        g1_kb(os.path.join(o, vor + "aussen_vorn.jpg"), (-9.0, -14.0, 9.0), (0.0, 0.3, 3.6), verbergen=fest)
        g1_kb(os.path.join(o, vor + "aussen_rechts.jpg"), (11.5, -11.0, 7.5), (0.0, 0.3, 3.4), verbergen=fest)
        g1_kb(os.path.join(o, vor + "aussen_hinten.jpg"), (8.0, 14.5, 9.0), (0.0, 0.3, 3.6), verbergen=fest)
        g1_kb(os.path.join(o, vor + "aussen_wie_artwork.jpg"), (13.0, -13.5, 12.0), (0.3, 0.6, 3.4), brennweite=45, verbergen=fest)   # Blick wie die Vorlagen (iso, Giebel links vorn)
        g1_kb(os.path.join(o, vor + "aussen_giebel_nah.jpg"), (-2.2, M.OVORN - 6.5, M.Z_TRAUFE + 0.6), (0.0, M.OVORN, M.Z_TRAUFE + 1.3), brennweite=30, verbergen=fest)
        g1_kb(os.path.join(o, vor + "aussen_spitze_front.jpg"), (-2.406, M.OVORN - 1.5, 6.18), (-2.406, M.OVORN, 6.18), brennweite=100, verbergen=fest)   # Masswerk der linken Seitenblende von vorn
        g1_kb(os.path.join(o, vor + "aussen_seitenblende.jpg"), (-4.6, M.OVORN - 3.2, M.Z_TRAUFE + 1.6), (-2.2, M.OVORN, M.Z_TRAUFE + 1.4), brennweite=50, verbergen=fest)   # Seitenblenden schraeg von der Seite (Dachvorderkante)
        g1_kb(os.path.join(o, vor + "aussen_giebel_hinten_nah.jpg"), (2.2, M.HINTEN + 6.5, M.Z_TRAUFE + 0.6), (0.0, M.HINTEN, M.Z_TRAUFE + 1.3), brennweite=30, verbergen=fest)
        g1_kb(os.path.join(o, vor + "aussen_tuer.jpg"), (3.4, -8.5, 2.4), (0.8, -2.6, 1.7), brennweite=40, verbergen=fest)
        gy = sum(GAUBE["y"]) / 2                           # Gaube und Schornstein: feste Blicke (alter und neuer Stand vergleichbar) und der Blick aus dem Gaubenfenster
        gz = M.z_dach(GAUBE["x_front"]) + 0.8
        g1_kb(os.path.join(o, vor + "aussen_gaube_nah.jpg"), (9.0, 8.5, gz + 2.5), (GAUBE["x_front"] + 0.7, 0.45, gz - 0.2), brennweite=35, verbergen=fest)
        g1_kb(os.path.join(o, vor + "aussen_gaube_front.jpg"), (GAUBE["x_front"] + 7.5, 0.45, gz + 0.9), (GAUBE["x_front"], 0.45, gz), brennweite=40, verbergen=fest)
        g1_kb(os.path.join(o, vor + "aussen_gaube_blick.jpg"), (GAUBE["x_front"] + 0.45, gy, gz - 0.15), (GAUBE["x_front"] + 4.5, gy + 0.2, gz - 0.45), brennweite=24, verbergen=fest)
        if "nur-aussen" in opt: return                     # Aussenbilder genuegen (die Innenbilder brauchen den Grossteil der Zeit)
        lampe = bpy.data.objects.new("Kontrolllampe", bpy.data.lights.new("Kontrolllampe", "POINT"))
        lampe.data.energy = 220; lampe.location = (dx, -0.3, 2.2)
        bpy.context.scene.collection.objects.link(lampe)
        weg = ["Dach", "Scheiben", "Wappen", "Fahne", "Leerstand", "Geruest", "Zimmertuer_Bibliothek"]   # beide Zimmertueren liegen am selben Ort
        auge, ziel = Vector(M.STUBE_AUGE), Vector(M.STUBE_ZIEL)
        g1_kb(os.path.join(o, vor + "innen_blick.jpg"), tuple(auge), tuple(ziel), brennweite=14, verbergen=weg)
        rechts = Vector(((M.STUBE_TUER[0] + M.STUBE_TUER[1]) / 2, M.STUBE_HINTEN, 1.15))
        g1_kb(os.path.join(o, vor + "innen_zimmertuer.jpg"), tuple(auge), tuple(rechts), brennweite=16, verbergen=weg)
        g1_kb(os.path.join(o, vor + "innen_links.jpg"), tuple(auge), (-IX, 0.9, 1.2), brennweite=16, verbergen=weg)
        g1_kb(os.path.join(o, vor + "innen_haustuer.jpg"), tuple(auge), (1.1, M.IV, 1.3), brennweite=16, verbergen=weg)
        g1_kb(os.path.join(o, vor + "innen_oben.jpg"), (1.5, -3.6, 6.8), (-0.2, 0.0, 0.6), brennweite=18, verbergen=weg + ["Haus", "Decke"])


if __name__ == "__main__":
    FACHWERK_ECHT = True                              # eigener Einstieg der Menschen (Nutzer 21.11.2026)
    FM.FACHWERK_ECHT = True
    main()
