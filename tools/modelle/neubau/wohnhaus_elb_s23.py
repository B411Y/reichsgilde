"""Neubau der Gebaeude: Wohnhaus Elb, Stufe 2 (Buergerhaus) und Stufe 3 (Steinhaus), Grundformen 1 bis 4 - aussen und innen.

    blender -b --factory-startup --python tools/modelle/neubau/wohnhaus_elb_s23.py -- --stufe=<2|3> --form=<1|2|3|4> [--bilder=<ordner>] [--ohne-ao]

Wie bei den Zwergen (wohnhaus_zwerg_s23.py) eine SCHICHT auf den Bauskripten der Menschen (wohnhaus_mensch_s23.py mit _masse und _formen,
unveraendert): gleiche Grundrisse, Geschosse, Waende, Fenster, Treppe und Raeume (Nutzer 17.11.2026: echtes Obergeschoss wie bei Menschen
und Zwergen, Fussabdruck wie das Menschenhaus derselben Grundform, Pruefung 27), aber in der elbischen Form der Stufe 1 und nach den
Kunstwerken wohnhaus_lvl2_elb / wohnhaus_lvl3_elb:
  * Stoff, Dateinamen, Atlasgruppen: trim_s2_elb bzw. trim_s3_elb (aussen), innen_trim_s23_elb (innen), Gruppen wohnhaus_elb_s<n> und
    wohnhaus_elb_innen; Dateien wohnhaus_elb_s<stufe>_g<form>[_innen].glb. Im Spiel Ebenen elb_s2 und elb_s3 im Haeuser-Array.
  * Elbenmasse (elb_masse.py, groesster Elb 1,90 m): Haustuer und Zimmertueren 2,10 m licht (Mensch 2,00), Herdhaube 1,95 m licht, Blick
    aus 1,72 m. Tisch und Sitz der Menschen liegen im Soll fuer Elben und bleiben.
  * Dach: Schiefer; hochgezogene Ecken und tief heruntergezogene Traufe NUR im Ueberstand (ueber den Waenden bleibt das Dach, wie es ist),
    aufgeschwungene Ortgangbretter an den Traufecken und am First (elb_teile.ortgang_schwung).
  * Spitzbogen aussen in Haustuer und Giebelfenstern (elb_teile.spitzbogen; die Oeffnungen bleiben rechteckig, innen = aussen).
  * Holzunterstand mit Pultdach ueber dem Scheitholz an der linken Wand (elb_teile.unterstand), in allen Grundformen.
  * Stufe 2: Erdgeschoss aus Kalkputz (Putzsockel; Eckquader, Gewaende und Schornstein aus Haustein K2), Obergeschoss und Giebel in Fachwerk,
    Quergiebel auf der rechten Dachseite (statt der Gaube der Menschen), Vordach ueber der Tuer; ohne Erker und Ornamente.
  * Stufe 3: ganz Stein wie das Steinhaus der Menschen (Mauer K2, Werkstein K5), vorn statt des Treppengiebels ein Schildgiebel, der dem
    Dach folgt, mit drei einfachen Fialen; ohne Arkade, Gauben und Erker. Die Bibliothek bleibt rechteckig im Obergeschoss (raeume_elb_s23.py),
    das Dachgeschoss bleibt leer. Wandbehang (Skelett „Behang“) aus dem Bauskript der Menschen.
  * Grundform 4 ohne Erker: das linke Fenster des Obergeschosses bleibt in der Wand.
  * Innen: Feuerstelle wie Stufe 1 (wohnhaus_elb_s1.innen_fertig): ganze Einfassung aus Stein, Schlot aus Stein ueber der Feuerstelle, kein
    Holz im Schlot (die Deckenbalken enden an ihm); Hauswappen hoch an der linken Wand (Regel der Stufe 1: hoch, nicht an der Haustuerwand).
Messung: wohnhaus_elb_messen.py; Soll/Ist: wohnhaus_elb_s23_masse.py.
"""
import os
import sys

HIER = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HIER)
sys.path.insert(0, os.path.join(os.path.dirname(HIER), "raeume"))
import bpy                                                         # noqa: E402,F401
from mathutils import Vector                                       # noqa: E402
from netzbau import Teil                                           # noqa: E402
import wohnhaus_mensch_s1_g1 as g1                                 # noqa: E402
import wohnhaus_mensch_s1 as s1                                    # noqa: E402
import wohnhaus_mensch_s23 as S23                                  # noqa: E402
import elb_teile as et                                             # noqa: E402
import wohnhaus_elb_s23_masse as EM                                # noqa: E402

VOLK = "elb"
TUER_HOCH = 2.10                  # Haustuer licht (groesster Elb 1,90 + 0,20; elb_masse.py), Menschen 2,00
ZIMMERTUER_HOCH = 2.10            # Zimmertueren ebenso (Mensch 2,00)
HAUBE_HOEHER = 0.15               # Herdhaube 1,95 m licht (Elb 1,90 + 0,05)
BLICK_HOEHER = 0.12               # Augenhoehe 1,72 m (1,85 * 0,93)
SCHWUNG = et.SCHWUNG              # so viel heben sich die Traufecken (wie Stufe 1)
TIEF = 0.20                       # so viel tiefer liegt die Traufe zwischen den Ecken (tief heruntergezogen, nur im Ueberstand)
SCHWUNG_LAENGE = et.SCHWUNG_LAENGE
SCHWUNG_SCHNITTE = (0.5, 1.2)            # Teilung der Traufe vom Giebel her (Schwung stueckweise linear; Dreiecke der Huelle)
BOGEN_STUECKE = 3                 # Spitzbogen: Abschnitte je Seite (Stufe 1: 6; Dreiecke der Huelle, Grenze 4.000)
BOGEN_RAND = et.BOGEN_RAND
QUERGIEBEL = dict(y=(0.55, 2.15), fenster=(0.66, 0.66))   # Stufe 2: Quergiebel in der Flucht der rechten Wand, hinter dem Schornstein
UNTERSTAND = dict(y0=0.75, y1=2.20, z=2.33, tiefe=0.60, fall=0.30)   # ueber dem Scheitholz an der linken Wand, unter der Auskragung
VORDACH_DICKE = 0.025             # Vordach als Platte: Untersicht so weit unter der Dachflaeche (vorher deckungsgleich, siehe vordach_platte)
STUFE = [2]
FORM = [1]
LETZT = {}


def licht_vor_den_herd(licht, auge):
    """Grundlicht der Wohnstube (Licht_Raum, OmniLight ohne Schatten, Stadtansicht.Neubau.cs) ueber dem Blickpunkt, in der Hoehe der
    Menschen (Klicktest 18.11.2026): In der Raummitte (y = -0,3 wie bei den Menschen) stand es hinter den Flaechen, die zur Kamera
    zeigen – rechte Seiten von Sturz, Wange und Sockel, die Seite des Unterzugs und die Seiten der Deckenbalken bekamen nur
    Umgebungslicht und waren im hellen Stein und Holz der Elben fast schwarz (bei den Menschen dunkles Holz, dort fiel es nicht auf).
    Ueber dem Blickpunkt beleuchtet es jede Flaeche, die die Kamera sieht, auch beim Umschauen (Fix 4: an der linken Wand lag es hinter
    der Seite des ersten Balkens; Pruefung 12c)."""
    return Vector((auge.x, auge.y, S23.M.Z_EG_DECKE - 0.35))   # Hoehe fest (wie gebaut); die Menschen haben das Licht seit 21.11.2026 in 1,70 m


# Efeu als Blattkarten (Nutzer 21.11.2026): so viele, wie die Dreiecksgrenze der Huelle (4.000) laesst. Dreiecke der Huelle ohne Efeu
# je Stufe und Grundform (Messung 20.11.2026); 24 Dreiecke Abstand, hoechstens 110 Karten. Bei wenig Platz groessere Karten.
EFEU_HUELLE = {(2, 1): 3630, (2, 2): 3908, (2, 3): 3942, (2, 4): 3630, (3, 1): 3586, (3, 2): 3738, (3, 3): 3898, (3, 4): 3586}
EFEU_HOECHSTENS = 110
EFEU_S3_KARTEN = 40


def efeu_setzen(h):
    """Efeu an den Aussenwaenden beider Geschosse (efeu_karten.bewachsen liest aus dem Haus, wo eine Karte sitzen darf)."""
    import efeu_karten
    efeu_karten.stoff_anlegen(g1, f"trim_s{STUFE[0]}")
    M = S23.M
    budget = max(0, min(EFEU_HOECHSTENS, (4000 - 24 - EFEU_HUELLE[(STUFE[0], FORM[0])]) // 2))
    if STUFE[0] == 3: budget = EFEU_S3_KARTEN      # Patrizier: Efeu beschraenkt (Entwurf), die 4.000er Grenze gilt hier nicht mehr
    L = M.HINTEN - M.VORN
    ebenen = [((-M.BX, M.VORN), (1, 0), 2 * M.BX, (0, -1)), ((-M.OX, M.HINTEN), (1, 0), 2 * M.OX, (0, 1)),
              ((-M.BX, M.VORN), (0, 1), L, (-1, 0)), ((M.BX, M.VORN), (0, 1), L, (1, 0))]
    if abs(M.OVORN - M.VORN) > 0.02: ebenen.append(((-M.OX, M.OVORN), (1, 0), 2 * M.OX, (0, -1)))
    if abs(M.OX - M.BX) > 0.02:
        ebenen += [((-M.OX, M.OVORN), (0, 1), M.HINTEN - M.OVORN, (-1, 0)), ((M.OX, M.OVORN), (0, 1), M.HINTEN - M.OVORN, (1, 0))]
    teile = [h]
    try: teile.append(S23.baue_wappen())                             # das Wappen bleibt frei
    except Exception as fehler: print("EFEU ohne Wappen:", fehler)   # noqa: BLE001

    def z_grenze(nr, s_):
        (ox, oy), (ex, ey), _l, _n = ebenen[nr]
        if abs(ey) > 0.5: return M.Z_TRAUFE - 0.25
        return M.z_dach(ox + ex * s_) - 0.35
    n = efeu_karten.bewachsen(h, teile, ebenen, budget, 53 + 10 * STUFE[0] + FORM[0], z_grenze, gross=1.35 if budget < 45 else 1.0)
    print(f"EFEU Karten: {n} (Budget {budget})")


def umbenennen(text):
    return text.replace("mensch", VOLK)


# ── Hilfen fuer Flaechen ─────────────────────────────────────────────────────────────────────────────────────────────────

def _lerp(a, b, t):
    return a + (b - a) * t


def _uv_lerp(a, b, t):
    return (a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t)


def schnitt(pts, uv, f, werte):
    """Teilt ein Viereck entlang eines Kantenpaars an den Stellen, an denen die Groesse f(p) die `werte` annimmt (beide Kanten des Paars
    muessen den Wert kreuzen). Liefert die Teilvierecke (Punkte, UV) in gleicher Drehrichtung oder None."""
    if len(pts) != 4: return None
    for dreh in (0, 1):
        P = [Vector(p) for p in pts[dreh:] + pts[:dreh]]
        U = list(uv[dreh:] + uv[:dreh])
        a0, a1, b0, b1 = f(P[0]), f(P[1]), f(P[3]), f(P[2])
        ts = []
        for w in werte:
            if min(a0, a1) + 1e-4 < w < max(a0, a1) - 1e-4 and min(b0, b1) + 1e-4 < w < max(b0, b1) - 1e-4:
                ts.append(((w - a0) / (a1 - a0), (w - b0) / (b1 - b0)))
        if not ts: continue
        ts = [(0.0, 0.0)] + sorted(ts) + [(1.0, 1.0)]
        teile = []
        for (ta, tb), (tc, td) in zip(ts[:-1], ts[1:]):
            q = [_lerp(P[0], P[1], ta), _lerp(P[0], P[1], tc), _lerp(P[3], P[2], td), _lerp(P[3], P[2], tb)]
            u = [_uv_lerp(U[0], U[1], ta), _uv_lerp(U[0], U[1], tc), _uv_lerp(U[3], U[2], td), _uv_lerp(U[3], U[2], tb)]
            teile.append((q, u))
        return teile
    return None


def flaechen_ersetzen(teil, neu_je_flaeche):
    """Ersetzt jede Flaeche i durch die Liste neu_je_flaeche[i] und verschiebt die Bereiche der Holzakzente mit (versteinern liest sie)."""
    start, neu = [], []
    for liste in neu_je_flaeche:
        start.append(len(neu)); neu += liste
    start.append(len(neu))
    teil.flaechen = neu
    if hasattr(teil, "holz_akzente"):
        teil.holz_akzente = [(start[a], start[e]) for a, e in teil.holz_akzente]


def band_umlegen(teil, ab, band, stoff=None, bis=None):
    """Legt die UV der Flaechen ab Index `ab` (bis `bis`) auf das Band `band` (gleiche relative Lage im Band) und setzt den Stoff."""
    v0n, v1n, _ = teil.baender[band]
    for i in range(ab, len(teil.flaechen) if bis is None else bis):
        pts, uv, st, fa, gl = teil.flaechen[i]
        alt = S23._band_von(teil, uv)
        v0, v1, _ = teil.baender[alt] if alt else (0.0, 1.0, 1)
        uv = [(u, v0n + (v - v0) / max(v1 - v0, 1e-6) * (v1n - v0n)) for u, v in uv]
        teil.flaechen[i] = (pts, uv, stoff or st, fa, gl)


def gespiegelt(quelle, ziel):
    """Haengt die Flaechen von `quelle` an x = 0 gespiegelt an `ziel` (Drehrichtung umgekehrt)."""
    for pts, uv, st, fa, gl in quelle.flaechen:
        ziel.flaechen.append(([Vector((-p.x, p.y, p.z)) for p in pts][::-1], list(uv)[::-1], st, fa, gl))


# ── Dach: Schwung nur im Ueberstand ────────────────────────────────────────────────────────────────────────────────────

def heben(p, ox, xe, dv, dh, vorn):
    """Hebung eines Dachpunkts (Kunstwerk: hochgezogene Ecken, tief heruntergezogene Traufe): nur ausserhalb der Wandflucht (|x| > ox),
    linear zur Traufe hin; an den Giebelenden (ueber SCHWUNG_LAENGE geglaettet) +SCHWUNG, dazwischen -TIEF. `vorn`: auch am vorderen Giebel
    (im Steinhaus steht dort der Schildgiebel: kein Schwung)."""
    ax = abs(p.x)
    if ax <= ox + 1e-6: return 0.0
    tx = min((ax - ox) / max(xe - ox, 1e-6), 1.0)
    nv = (1.0 - min(max((p.y - dv) / SCHWUNG_LAENGE, 0.0), 1.0)) if vorn else 0.0
    nh = 1.0 - min(max((dh - p.y) / SCHWUNG_LAENGE, 0.0), 1.0)
    g = et._glatt(max(nv, nh))
    schwung = SCHWUNG if vorn else 0.0          # Steinhaus: die Ecken bleiben, die Traufe dazwischen sinkt (siehe dach_schwingen)
    return tx * (schwung * g - TIEF * (1.0 - g))


def dach_schwingen(d, vorn):
    """Teilt die Dachflaechen an der Wandflucht (|x| = OX) und im Ueberstand an den Schnitten vom Giebel her und hebt alle Punkte nach
    `heben`. Ueber den Waenden bleibt alles, wie es war (Pruefung 26 b). Im Steinhaus (ohne `vorn`) heben sich die Ecken nicht ueber die
    Dachlinie (am Steingiebel hinten oeffnete das einen Spalt unter dem Ortgang, Pruefung 16); geschwungen ist die Traufe dort, weil sie
    zwischen den Ecken um TIEF absinkt (die Ecken stehen 0,20 m hoeher als die Traufmitte, Pruefung 26 a)."""
    M = S23.M
    ox, xe = M.OX, M.OX + M.UEBER
    dv, dh = M.OVORN - M.UEBER_ORTGANG_VORN, M.HINTEN + M.UEBER_ORTGANG
    ys = [dh - s for s in SCHWUNG_SCHNITTE] + ([dv + s for s in SCHWUNG_SCHNITTE] if vorn else [])
    neu_je = []
    for pts, uv, st, fa, gl in d.flaechen:
        stuecke = [(pts, uv)]
        quer = min(p.x for p in pts) < -0.05 and max(p.x for p in pts) > 0.05
        if len(pts) == 4 and not quer:
            r = schnitt(pts, uv, lambda p: abs(p.x), [ox])
            if r: stuecke = r
            tmp = []
            for q, u in stuecke:
                if min(abs(p.x) for p in q) >= ox - 1e-4 and max(p.y for p in q) - min(p.y for p in q) > 0.3:
                    r = schnitt(q, u, lambda p: p.y, ys)
                    tmp += r if r else [(q, u)]
                else:
                    tmp.append((q, u))
            stuecke = tmp
        neu_je.append([([Vector((p.x, p.y, p.z + heben(p, ox, xe, dv, dh, vorn))) for p in q], list(u), st, fa, gl) for q, u in stuecke])
    flaechen_ersetzen(d, neu_je)


class _DachMass:
    """Adapter fuer elb_teile.ortgang_schwung aus den Massen der Menschen (wohnhaus_mensch_s23_masse)."""

    def __init__(self, M):
        self.OX, self.Z_FIRST = M.OX, M.Z_FIRST
        self.DV, self.DH = M.OVORN - M.UEBER_ORTGANG_VORN, M.HINTEN + M.UEBER_ORTGANG
        self.dach_z = M.z_dach
        self.walm = None


def ortgang_schwingen(d, vorn):
    """Aufgeschwungene Ortgangbretter (elb_teile.ortgang_schwung) auf dem Ortgangbrett der Menschen (Mitte 6 cm unter der Dachlinie);
    ohne `vorn` nur am hinteren Giebel. Holzakzent (bleibt im Steinhaus Holz)."""
    M = S23.M
    tmp = Teil("_", d.baender, saat=29)
    et.ortgang_schwung(tmp, _DachMass(M), M.OX + M.UEBER - (0.08 if vorn else 0.03), -0.01, -0.01, None)   # 8 (Steinhaus 3) cm eingerueckt: der Haken bleibt im Fussabdruck (Pruefung 27) und setzt im Brett an (kein Spalt, Pruefung 16)
    ym = (M.OVORN + M.HINTEN) / 2
    # 3 cm zur Hausmitte: Sichtseite in der Flucht des Ortgangbretts der Menschen (sonst 2 cm ueber den Fussabdruck hinaus)
    # Rueckseite der Bretter (elb_teile.streifen baut nur Sicht- und Laengsseiten): das Brett ist geschlossen, durch den Spalt zum
    # Ortgangbrett der Menschen sah man sonst durch das Brett hindurch (Pruefung 16)
    zurueck = []
    for pts, uv, st, fa, gl in tmp.flaechen:
        if len(pts) == 4 and max(p.y for p in pts) - min(p.y for p in pts) < 1e-4:
            aussen = 1.0 if pts[0].y > ym else -1.0
            zurueck.append(([Vector((p.x, p.y - aussen * 0.05, p.z)) for p in pts][::-1], list(uv)[::-1], st, fa, gl))
    tmp.flaechen += zurueck
    zur = 0.03 if vorn else 0.025         # Steinhaus (Ortgang 0,10 m): 2,5 cm (Sichtseite nicht in der Ebene des Ortgangbretts)
    tmp.flaechen = [([Vector((p.x, p.y - zur if p.y > ym else p.y + zur, p.z)) for p in pts], uv, st, fa, gl) for pts, uv, st, fa, gl in tmp.flaechen]
    S23.holz_akzent(d, lambda: d.flaechen.extend(f for f in tmp.flaechen if vorn or sum(p.y for p in f[0]) / len(f[0]) > ym))


# ── Stufe 3: Schildgiebel mit Fialen statt des Treppengiebels ───────────────────────────────────────────────────────────

BRUSTUNG = 0.25            # so weit steht der Schildgiebel ueber der Dachdeckung
FIALE = dict(schaft=0.22, hoch=0.70, spitze=0.55)


def schildgiebel(h, stufen=None, dicke=0.36):
    """Stufe 3 (Kunstwerk wohnhaus_lvl3_elb: Steingiebel mit Fialen): Giebelmauer aus Haustein vor dem Dach zur Gasse, die Oberkante folgt
    dem Dach BRUSTUNG ueber der Deckung, mit Abdeckplatten aus Werkstein und drei einfachen Fialen (Fuss links und rechts, First). Gleiche
    Mauerdicke, Lage und Fenster wie der Treppengiebel der Menschen (das Dach endet dahinter). In Lagen von Bandhoehe gemauert."""
    M, BA, W = S23.M, S23.BA, S23.WERKSTEIN
    y0, y1 = M.OVORN - 0.04, M.OVORN - 0.04 + dicke
    zt = M.Z_TRAUFE
    w0 = M.OX + 0.06
    zp = lambda x: M.z_dach(x) + 0.10 + BRUSTUNG                    # Oberkante ueber der Stelle x
    xp = lambda z: min(w0, max(M.OX - (z - M.Z_TRAUFE - 0.10 - BRUSTUNG) / M.NEIGUNG, 0.0))   # halbe Breite in der Hoehe z
    zs, zf = zp(w0), zp(0.0)
    v0, v1, m = BA["bruchstein"]
    kurs = (v1 - v0) * m
    mitte = Vector((0, y0 + 2, zt + 1))
    loch = [(a0, a1, z0_, z1_) for wd, (a0, a1), (z0_, z1_), art in M.FENSTER if wd == "vorn_tg"]
    z = zt
    while z < zf - 1e-4:
        zn = min(z + kurs, zf)
        wa, wb = xp(z), xp(zn)
        if wb > 1e-3:
            h.wand("Bruchstein", (-wb, y0, 0), (1, 0, 0), 2 * wb, z, zn, "bruchstein", loecher=[(a0 + wb, a1 + wb, z0_, z1_) for a0, a1, z0_, z1_ in loch],
                   zbasis=z, hoehe=kurs, weg_von=mitte, u_start=(z * 3.7) % 1)
        if wa - wb > 1e-3:
            for sx in (-1, 1):
                pts = [(sx * wb, y0, z), (sx * wa, y0, z), (sx * wb, y0, zn)]
                h.welt("Bruchstein", pts, "bruchstein", (-w0 - (z * 3.7) % 1 * m, y0, 0), (1, 0, 0), z, kurs, weg_von=mitte, m=None)
        z = zn
    # Stirnseiten (x = +-w0) bis unter die Abdeckung, Rueckseite ueber dem Dach (von hinten sichtbar)
    for sx in (-1, 1):
        h.welt("Bruchstein", [(sx * w0, y0, zt), (sx * w0, y1, zt), (sx * w0, y1, zs), (sx * w0, y0, zs)], "bruchstein", (sx * w0, y0, 0), (0, 1, 0), zt, kurs,
               weg_von=Vector((0, (y0 + y1) / 2, zs)))
        dach = lambda x: M.z_dach(x) + 0.10 - 0.03
        pts = [(0.0, y1, dach(0.0)), (sx * w0, y1, dach(w0)), (sx * w0, y1, zs), (0.0, y1, zf)]
        h.welt("Bruchstein", pts, "bruchstein", (-w0, y1, 0), (1, 0, 0), dach(w0), kurs, weg_von=Vector((0, y1 - 1, zf - 0.5)))
    # Fensterlaibung, Sohlbank und Sturz (wie im Treppengiebel)
    for a0, a1, z0_, z1_ in loch:
        for p_, richtung, laenge in (((a0, y0, z0_), Vector((1, 0, 0)), a1 - a0), ((a0, y0, z1_), Vector((1, 0, 0)), a1 - a0),
                                     ((a0, y0, z0_), S23.EZ, z1_ - z0_), ((a1, y0, z0_), S23.EZ, z1_ - z0_)):
            h.rechteck("Bruchstein", p_, richtung, (0, 1, 0), laenge, dicke, "bruchstein", zu=Vector(((a0 + a1) / 2, y0 + dicke / 2, (z0_ + z1_) / 2)), anteil=(0.0, 0.6))
        h.kasten("Bruchstein", (a0 - 0.08, y0 - 0.06, z0_ - 0.08), (a1 + 0.08, y0 + 0.02, z0_ - 0.005), W, ohne=("+y",), laengs="x")
        h.kasten("Bruchstein", (a0 - 0.12, y0 - 0.04, z1_ + 0.005), (a1 + 0.12, y0 + 0.02, z1_ + 0.16), W, ohne=("+y",), laengs="x")
    # Abdeckplatten aus Werkstein auf der Schraege (je Seite ein Prisma, 8 cm dick, 5 cm ueber die Mauer)
    for sx in (-1, 1):
        a, b = Vector((sx * w0, 0, zs)), Vector((0, 0, zf))      # genau auf der Oberkante der Mauer, buendig mit der Stirnseite
        ya, yb = y0 - 0.05, y1 + 0.03
        ecken = lambda yy, dz: [Vector((a.x, yy, a.z + dz)), Vector((b.x, yy, b.z + dz))]
        u0, u1, _ = BA[W]
        lang = (b - a).length / BA[W][2]
        for (p, q), (r, s) in (((ecken(ya, 0.08)), (ecken(yb, 0.08))), ((ecken(ya, 0.0)), (ecken(ya, 0.08))), ((ecken(yb, 0.08)), (ecken(yb, 0.0)))):
            h.vieleck("Bruchstein", [p, q, s, r], [(0, u1), (lang, u1), (lang, u0), (0, u0)], weg_von=Vector((0, (ya + yb) / 2, (a.z + b.z) / 2 - 1)))
        # Unterseite nur, wo die Platte ueber die Mauer steht (vorn 5 cm, hinten 3 cm): von unten sah man sonst in die offene Platte
        # (Pruefung 16); auf der Mauer laege sie verdeckt und backte schwarz (Pruefung 12)
        for yu0, yu1 in ((ya, y0), (y1, yb)):
            h.vieleck("Bruchstein", [ecken(yu0, 0.0)[0], ecken(yu0, 0.0)[1], ecken(yu1, 0.0)[1], ecken(yu1, 0.0)[0]], [(0, u1), (lang, u1), (lang, u0), (0, u0)],
                      weg_von=Vector((0, (ya + yb) / 2, (a.z + b.z) / 2 + 1)))
        h.vieleck("Bruchstein", [Vector((a.x, ya, a.z)), Vector((a.x, yb, a.z)), Vector((a.x, yb, a.z + 0.08)), Vector((a.x, ya, a.z + 0.08))],
                  [(0, u1), (0.1, u1), (0.1, u1 - 0.02), (0, u1 - 0.02)], weg_von=Vector((0, (ya + yb) / 2, a.z)))
    # Fialen (einfach: Schaft und Pyramide) am Fuss links und rechts und auf dem First
    ym = (y0 + y1) / 2
    # die Fialen am Fuss 14 cm von der Kante: ihre Pyramide laege sonst in der Flucht der Traufwand (Pruefung 9 liest dort die Traufhoehe)
    xf = w0 - FIALE["schaft"] / 2 - 0.14
    for x in (-xf, xf, 0.0):
        fiale(h, x, ym, zp(abs(x) + FIALE["schaft"] / 2) + 0.08 - 0.02, W)     # Fuss 2 cm unter der Abdeckplatte (an ihrer tiefsten Stelle)
    if S23.GURT_WERK: giebel_zier(h, y0, loch, xp, zt)                  # Patrizier: Kielbogen, Astwerk
    # Gesims an der Traufe
    h.kasten("Bruchstein", (-M.OX - 0.08, y0 - 0.08, zt - 0.10), (M.OX + 0.08, y1, zt), "bruchstein", ohne=("+y",), laengs="x")


def fiale(h, x, y, z, band):
    """Einfache Fiale: quadratischer Schaft (vier Seiten) und Pyramide; 12 Dreiecke."""
    s, hoch, spitze = FIALE["schaft"] / 2, FIALE["hoch"], FIALE["spitze"]
    mitte = Vector((x, y, z + hoch / 2))
    v0, v1, m = S23.BA[band]
    ecken = [(x - s, y - s), (x + s, y - s), (x + s, y + s), (x - s, y + s)]
    for i in range(4):
        (xa, ya), (xb, yb) = ecken[i], ecken[(i + 1) % 4]
        h.vieleck("Bruchstein", [(xa, ya, z), (xb, yb, z), (xb, yb, z + hoch), (xa, ya, z + hoch)],
                  [(i * 0.07, v1), (i * 0.07 + 2 * s / m, v1), (i * 0.07 + 2 * s / m, v1 - hoch / m * 0.5), (i * 0.07, v1 - hoch / m * 0.5)], weg_von=mitte)
        h.vieleck("Bruchstein", [(xa, ya, z + hoch), (xb, yb, z + hoch), (x, y, z + hoch + spitze)],
                  [(i * 0.07, v1), (i * 0.07 + 2 * s / m, v1), (i * 0.07 + s / m, v0)], weg_von=Vector((x, y, z + hoch)))
    if S23.GURT_WERK and abs(x) > 0.01: knospe(h, x, y, z + hoch + spitze - 0.015, band, s=0.05)      # Stufe 3 Patrizier: Knospe auf der Spitze



# ── Holzunterstand, Spitzbogen ──────────────────────────────────────────────────────────────────────────────────────────

def unterstand_links(h):
    """Holzunterstand mit Pultdach ueber dem Scheitholz an der linken Wand (Kunstwerk; wie Stufe 1 der Elben): an der rechten Seite gebaut
    und gespiegelt. Im Steinhaus Holzakzent."""
    M = S23.M
    tmp = Teil("_", h.baender, saat=43)
    u = UNTERSTAND
    et.unterstand(tmp, M.BX, u["y0"], u["y1"], u["z"], tiefe=u["tiefe"], fall=u["fall"])
    S23.holz_akzent(h, lambda: gespiegelt(tmp, h))


def spitzbogen(teil, P, n, s0, s1, oben, vor=0.01):
    """Spitzbogen NUR AUSSEN in einer rechteckigen Oeffnung wie elb_teile.spitzbogen (Stufe 1: gleiche Boegen, Stich 1,3 x halbe Weite,
    Zwickel aus Putz), aber schlank fuer die groesseren Haeuser: BOGEN_STUECKE Abschnitte je Seite und ein flaches Bogenbrett (nur
    Sichtseite, 6 mm vor den Zwickeln) INNEN an der Bogenlinie – es bleibt ganz in der Oeffnung. 18 statt 84 Dreiecke."""
    import math
    n = Vector(n).normalized()
    a = (s1 - s0) / 2
    hh = 1.3 * a
    r = (a * a + hh * hh) / (2 * a)
    zs = oben - hh
    winkel = math.atan2(hh, a - r)
    links = []
    for k in range(BOGEN_STUECKE + 1):
        t = math.pi + (winkel - math.pi) * k / BOGEN_STUECKE
        links.append((s0 + r + r * math.cos(t), zs + r * math.sin(t), s0 + r, zs))
    rechts = [(s0 + s1 - s, z, s0 + s1 - cx, cz) for s, z, cx, cz in links]
    Q = lambda s, z, dv=0.0: Vector(P(s, z)) + n * (vor + dv)
    hinten = Q((s0 + s1) / 2, oben) - n
    v0, v1, m = teil.baender["balken_c"]
    for seite, ecke in ((links, (s0, oben)), (rechts, (s1, oben))):
        for (sa, za, _a, _b), (sb, zb, _c, _d) in zip(seite[:-1], seite[1:]):
            teil.welt("Lehmputz", [Q(*ecke), Q(sa, za), Q(sb, zb)], "putz", Q(s0, 0.0), (Q(s1, 0.0) - Q(s0, 0.0)).normalized(), 0.0, 2.2, weg_von=hinten)
        innen = []
        for s, z, cx, cz in seite:                 # Punkt auf der Bogenlinie und BOGEN_RAND zum Kreismittelpunkt hin
            d = Vector((cx - s, cz - z)); d.normalize()
            innen.append((s + d.x * BOGEN_RAND, z + d.y * BOGEN_RAND))
        u = 0.0
        for i in range(BOGEN_STUECKE):
            (sa, za, *_), (sb, zb, *_) = seite[i], seite[i + 1]
            l = (Vector((sb - sa, zb - za))).length / m
            teil.vieleck("Fachwerk", [Q(sa, za, 0.006), Q(sb, zb, 0.006), Q(*innen[i + 1], 0.006), Q(*innen[i], 0.006)],
                         [(u, v1), (u + l, v1), (u + l, v0), (u, v0)], weg_von=hinten)
            u += l


# ── Stufe 3, Patrizierhaus der Elben (Nutzer 23.11.2026, Entwurf „Haus aus Stein, Holz und Licht“): Kielbogen, Astwerk, Knospenfialen, Laterne ──
#    Vorbilder: Astwerkgewoelbe Ellwangen (CC0), Kielbogenfenster Pforzheim (Public domain), Knochenhaueramtshaus Hildesheim (CC0).
RELIEF_ELB = 0.05           # Relieftiefe des Zierwerks vor der Giebelmauer (Pixel-Look: 5 cm und mehr sind lesbar)


def kielbogen(x0, x1, zs, hoch, n=10):
    """Kielbogen (Eselsruecken) ueber x0..x1 mit Kaempfer zs und Scheitelhoehe `hoch`: je Haelfte eine S-Kurve (senkrecht am Kaempfer, konvex,
    dann konkav in die Spitze). Punkte vom linken Kaempfer ueber die Spitze zum rechten."""
    xm = (x0 + x1) / 2
    links = []
    for k in range(n + 1):
        t = k / n
        links.append((x0 + (xm - x0) * (3 * t * t - 2 * t ** 3), zs + hoch * t))
    return links + [(2 * xm - x, z) for x, z in reversed(links[:-1])]


def _stange(h, pts, y, dick=0.05, band=None):
    """Streckenzug aus Werkstein-Staeben (Relief) vor der Ebene y."""
    band = band or S23.WERKSTEIN
    for (xa, za), (xb, zb) in zip(pts, pts[1:]):
        h.stab("Bruchstein", Vector((xa, y, za)), Vector((xb, y, zb)), dick, band, breite=dick)


def knospe(h, x, y, z, band=None, s=0.05):
    """Kleine Knospe (Raute aus acht Dreiecken) als Spitze der Kielboegen und Fialen."""
    band = band or S23.WERKSTEIN
    v0, v1, m = h.baender[band]
    o, u, a, b = Vector((x, y, z)), Vector((0, 0, 1)), Vector((1, 0, 0)), Vector((0, 1, 0))
    oben, unten = o + u * (1.6 * s), o - u * (0.4 * s)
    ring = [o + a * s, o + b * s, o - a * s, o - b * s]
    for i in range(4):
        for spitze in (oben, unten):
            h.vieleck("Bruchstein", [ring[i], ring[(i + 1) % 4], spitze] if spitze is oben else [ring[(i + 1) % 4], ring[i], spitze],
                      [(0, v1), (0.1, v1), (0.05, v0)], weg_von=o)


def astwerk(h, x, y, z):
    """Astwerk-Relief (verzweigte Aeste statt Okulus) ueber dem Giebelfenster: Stamm, zwei Hauptaeste, vier Zweige, Knospen an den Spitzen."""
    stamm = [(x, z), (x, z + 0.18), (x - 0.03, z + 0.38)]
    _stange(h, stamm, y, 0.045)
    for sx in (-1, 1):
        ast = [(x + sx * 0.0, z + 0.22), (x + sx * 0.13, z + 0.36), (x + sx * 0.24, z + 0.55), (x + sx * 0.25, z + 0.78)]
        _stange(h, ast, y, 0.04)
        _stange(h, [(x + sx * 0.14, z + 0.37), (x + sx * 0.36, z + 0.43), (x + sx * 0.42, z + 0.58)], y, 0.03)
        _stange(h, [(x + sx * 0.24, z + 0.55), (x + sx * 0.12, z + 0.66), (x + sx * 0.08, z + 0.88)], y, 0.03)
        for kx, kz in ((x + sx * 0.25, z + 0.80), (x + sx * 0.42, z + 0.60), (x + sx * 0.08, z + 0.90)):
            knospe(h, kx, y, kz, s=0.035)
    knospe(h, x - 0.03, y, z + 0.40, s=0.04)


def giebel_zier(h, y0, loch, xp, zt):
    """Schaugiebel der Elben: Kielbogen-Blenden links und rechts vom Giebelfenster (erhabener Rahmen mit Pfosten), Kielbogen mit Knospe ueber
    dem Fenster und darueber Astwerk (nur im Mittelfeld)."""
    y = y0 - RELIEF_ELB / 2
    for a0, a1, z0_, z1_ in loch:
        xm = (a0 + a1) / 2
        bogen = kielbogen(a0 - 0.12, a1 + 0.12, z1_ + 0.18, 0.34)
        _stange(h, bogen, y, 0.055)
        knospe(h, xm, y, z1_ + 0.18 + 0.34 + 0.02, s=0.05)
        astwerk(h, xm, y, z1_ + 0.58)
        for xc in (-1.0, 1.0):                                                  # Blenden neben dem Fenster
            zb, zk = z0_ - 0.05, z0_ + 0.95
            if xp(zk + 0.3) < abs(xc) + 0.30: continue
            xa, xb = xc - 0.24, xc + 0.24
            _stange(h, [(xa, zb), (xa, zk)], y, 0.05)
            _stange(h, [(xb, zb), (xb, zk)], y, 0.05)
            _stange(h, [(xa, zb), (xb, zb)], y, 0.05)
            _stange(h, kielbogen(xa, xb, zk, 0.30, n=8), y, 0.05)
            knospe(h, xc, y, zk + 0.32, s=0.04)
            _stange(h, [(xc, zb + 0.05), (xc, zk + 0.05)], y, 0.025)           # Mittelsprosse


def kielbogenfries(h):
    """Fries unter dem Gesims der Gassenseite (statt Rundbogenfries der Menschen): Deckband aus Werkstein, darunter eine Reihe kleiner Kielboegen."""
    M = S23.M
    z0, z1 = M.Z_EG_DECKE - 0.12, M.Z_EG_DECKE - 0.06
    x0, x1 = -M.BX + 0.52, M.BX - 0.52
    h.kasten("Bruchstein", (x0, M.VORN - S23.RELIEF, z0), (x1, M.VORN, z1), S23.WERKSTEIN, ohne=("+y",), laengs="x", ohne_streckung=True)
    n = int((x1 - x0) / 0.36)
    rand = (x1 - x0 - n * 0.36) / 2
    for k in range(n):
        xa = x0 + rand + k * 0.36
        _stange(h, kielbogen(xa + 0.03, xa + 0.33, z0 - 0.16, 0.13, n=3), M.VORN - 0.02, 0.03)


def vorder_zier(h):
    """Gassenseite: Kielbogen mit Knospe ueber jedem Fenster, Laterne auf einer Konsole rechts der Haustuer (nur Modell, ohne Lichtanbindung)."""
    M = S23.M
    for wand, (a0, a1), (b0, b1), art in M.FENSTER:
        if wand not in ("vorn_eg", "vorn_og") or art != "fenster": continue
        y = (M.VORN if wand == "vorn_eg" else M.OVORN) - 0.03
        _stange(h, kielbogen(a0 - 0.17, a1 + 0.17, b1 + 0.17, 0.20, n=5), y, 0.04)
        knospe(h, (a0 + a1) / 2, y, b1 + 0.17 + 0.21, s=0.035)
    tuer = next(f for f in M.FENSTER if f[3] == "tuer" and f[0] == "vorn_eg")
    (a0, a1), (b0, b1) = tuer[1], tuer[2]
    x = a1 + 0.35 if a1 + 0.5 < M.BX - 0.55 else a0 - 0.35
    y = M.VORN
    z = b1 - 0.25
    h.kasten("Bruchstein", (x - 0.06, y - 0.22, z - 0.12), (x + 0.06, y, z), S23.WERKSTEIN, ohne=("+y",), ohne_streckung=True)            # Konsole
    h.kasten("Eisen", (x - 0.012, y - 0.28, z), (x + 0.012, y - 0.04, z + 0.03), "eisen")                                                  # Arm
    h.kasten("Eisen", (x - 0.012, y - 0.30, z - 0.02), (x + 0.012, y - 0.28, z + 0.40), "eisen")                                           # Haken
    for dx, dy in ((-0.06, -0.06), (0.06, -0.06), (-0.06, 0.06), (0.06, 0.06)):                                                           # Laterne: vier Stangen
        h.kasten("Eisen", (x + dx - 0.007, y - 0.36 + dy - 0.007, z + 0.02), (x + dx + 0.007, y - 0.36 + dy + 0.007, z + 0.22), "eisen")
    h.kasten("Eisen", (x - 0.08, y - 0.44, z + 0.22), (x + 0.08, y - 0.28, z + 0.25), "eisen")                                             # Dach
    h.kasten("Eisen", (x - 0.08, y - 0.44, z - 0.01), (x + 0.08, y - 0.28, z + 0.02), "eisen")                                             # Boden


def spitzboegen(h):
    """Spitzbogen aussen in der Haustuer und in den Giebelfenstern (vorn und hinten, im Steinhaus auch im Fenster des Schildgiebels); im
    Steinhaus aus Werkstein (Zwickel und Bogen), sonst Putz und Holz wie Stufe 1."""
    M = S23.M
    for wand, (a0, a1), (z0, z1), art in M.FENSTER:
        if art != "tuer" and not wand.endswith(("_gb", "_tg")): continue
        (ox, oy), (ex, ey), _, n = S23.WAENDE[wand]
        start = ox if ex else oy
        ab = len(h.flaechen)
        vor = 0.01
        spitzbogen(h, lambda s, zz, w=wand: S23.punkt(w, s, zz), (n[0], n[1], 0), a0 - start, a1 - start, z1, vor=vor)
        if S23.ist_stein():
            band_umlegen(h, ab, S23.WERKSTEIN, "Bruchstein")
            if not hasattr(h, "holz_akzente"): h.holz_akzente = []
            h.holz_akzente.append((ab, len(h.flaechen)))           # versteinern() laesst den Werkstein, wie er ist


# ── Innen: Feuerstelle, Decke, Hauswappen ───────────────────────────────────────────────────────────────────────────────

def schlot_grundriss():
    """Grundriss des Schlots in der Wohnstube (wie wohnhaus_mensch_s23.baue_fensterseiten): x0, x1, y0, y1."""
    kx0, kx1, ky0, ky1 = g1.KAMIN
    hy0, hy1 = ky0 - 0.3 + S23.DY_RECHTS, ky1 + 0.3 + S23.DY_RECHTS
    return kx0 - 0.16 + S23.DX_RECHTS, S23.IX, hy0 + 0.06, hy1 - 0.06


def baue_decke():
    """Decke der Wohnstube wie bei den Menschen, aber kein Holz im Schlot: Balken und Unterzug enden an ihm."""
    M, IX, IV, IH, S = S23.M, S23.IX, S23.IV, S23.IH, S23.S
    sx0, sx1, sy0, sy1 = schlot_grundriss()
    r = Teil("Decke", S23.BI, saat=33)
    r.rechteck(S, (-IX, IV, M.Z_EG_DECKE), (0, 1, 0), (1, 0, 0), IH - IV, 2 * IX, "bohlen", zu=(0, 0, 0), anteil=(0.0, 1.0))
    for i in range(6):
        x = -IX + 0.40 + i * (2 * IX - 0.80) / 5
        stuecke = [(IV, sy0), (sy1, IH)] if x + 0.08 > sx0 else [(IV, IH)]
        for ya, yb in stuecke:
            r.kasten(S, (x - 0.08, ya, M.Z_EG_DECKE - 0.16), (x + 0.08, yb, M.Z_EG_DECKE), "balken", ohne=("+z", "-y", "+y"), laengs="y")
    xe = sx0 if sy0 < 0.10 and sy1 > -0.10 else IX
    r.kasten(S, (-IX, -0.10, M.Z_EG_DECKE - 0.24), (xe, 0.10, M.Z_EG_DECKE - 0.16), "balken", ohne=("-x", "+x"), laengs="x")   # Unterzug
    # Geschnitzte Kopfbaender (Nutzer 18.11.2026): unter dem Unterzug an der linken Wand und unter den Balken der linken Haelfte an der
    # Rueckwand (dort liegen weder Fenster noch Tueren; rechts stehen Herd, Schlot und die Zimmertuer)
    zu, zb = M.Z_EG_DECKE - 0.24, M.Z_EG_DECKE - 0.16
    kopfband(r, S, Vector((-IX, 0.0, zu)), Vector((1, 0, 0)))
    for i in range(3):
        x = -IX + 0.40 + i * (2 * IX - 0.80) / 5
        kopfband(r, S, Vector((x, IH, zb)), Vector((0, -1, 0)))
    return r


KOPFBAND = dict(laenge=0.50, bogen=0.10, dicke=0.08, breite=0.10, stuecke=4)


def kopfband(r, S, ecke, raus):
    """Geschwungenes Kopfband in der Ecke zwischen Wand und Balken: `ecke` = Unterkante des Balkens an der Wand, `raus` = waagrecht
    von der Wand in den Raum. Ein Bogen (quadratische Bezierkurve, zur Ecke hin gewoelbt) aus KOPFBAND["stuecke"] Kantholzstuecken
    im Band „balken“ (geschnitzte Ranke); beide Enden stecken 5 cm in Wand bzw. Balken."""
    L, b = KOPFBAND["laenge"], KOPFBAND["bogen"]
    wand = ecke - raus * 0.05 + Vector((0, 0, -L))
    balken = ecke + raus * L + Vector((0, 0, 0.05))
    mitte = ecke + raus * (L / 2 - b) + Vector((0, 0, -(L / 2 - b)))
    pkt = [wand * (1 - t) ** 2 + mitte * 2 * t * (1 - t) + balken * t * t for t in (k / KOPFBAND["stuecke"] for k in range(KOPFBAND["stuecke"] + 1))]
    quer = raus.cross(Vector((0, 0, 1)))
    for a, c in zip(pkt[:-1], pkt[1:]):
        r.stab(S, a, c, KOPFBAND["dicke"], "balken", breite=KOPFBAND["breite"], oben=quer)


def feuerstelle(raum):
    """Wie wohnhaus_elb_s1.innen_fertig (Rueckmeldung des Nutzers zur Stufe 1): Schlot aus Stein, Holz des Herds im Schlot entfaellt, alle
    Holzflaechen des Herds und der Taefelung im Grundriss des Herdsteins (bis unter die Haube) werden Stein (ganze Einfassung)."""
    BI = S23.BI
    holz = [BI[b] for b in ("bohlen", "balken")]
    stein = BI["bruchstein"]
    sx0, sx1, sy0, sy1 = schlot_grundriss()
    im_schlot = lambda p, r=0.01: sx0 - r < p.x < sx1 + r and sy0 - r < p.y < sy1 + r
    ist_holz = lambda uv: any(all(v0 - 1e-4 <= v <= v1 + 1e-4 for _, v in uv) for v0, v1, _ in holz)
    herd = [t for t in raum if t.name.startswith("Herd")]
    herdstein = [p for t in herd for pts, uv, *_ in t.flaechen if all(stein[0] - 1e-4 <= v <= stein[1] + 1e-4 for _, v in uv) for p in pts]
    hx0, hx1 = min(p.x for p in herdstein) - 0.03, max(p.x for p in herdstein) + 0.03
    hy0, hy1 = min(p.y for p in herdstein) - 0.03, max(p.y for p in herdstein) + 0.03
    z_haube = g1.Z_HAUBE
    in_herd = lambda p: hx0 <= p.x <= hx1 and hy0 <= p.y <= hy1 and p.z <= z_haube + 0.35
    # Sturz und Haube ueber der Feuerstelle (stehen ueber den Herdstein vor): auch Stein – die ganze Einfassung (Nutzer, Stufe 1)
    in_haube = lambda p: hx0 - 0.15 <= p.x <= hx1 + 0.15 and hy0 - 0.15 <= p.y <= hy1 + 0.15 and z_haube - 0.40 <= p.z

    def zu_stein(pts, uv):
        v0, v1 = next((a, b) for a, b, _ in holz if all(a - 1e-4 <= v <= b + 1e-4 for _, v in uv))
        return [(u, stein[0] + (v - v0) / (v1 - v0) * (stein[1] - stein[0])) for u, v in uv]
    weg = neu_stein = leiste = 0
    for t in raum:
        if t.name.startswith("Herd"):
            bleib = []
            for pts, uv, st, fa, gl in t.flaechen:
                if ist_holz(uv) and max(p.z for p in pts) > z_haube and any(im_schlot(p) for p in pts):
                    weg += 1; continue
                if ist_holz(uv) and (all(in_herd(p) for p in pts) or all(im_schlot(p, 0.06) for p in pts) or all(in_haube(p) for p in pts)):
                    uv = zu_stein(pts, uv); neu_stein += 1
                bleib.append((pts, uv, st, fa, gl))
            t.flaechen = bleib
        elif t.name == "Raum":                   # Taefelung der rechten Wand hinter dem Herd: an der Einfassung teilen, dort Stein
            neu_je = []
            for pts, uv, st, fa, gl in t.flaechen:
                stuecke = [(pts, uv)]
                if ist_holz(uv) and all(abs(p.x - S23.IX) < 0.05 for p in pts):
                    r = schnitt(pts, uv, lambda p: p.y, [hy0, hy1])
                    if r: stuecke = r
                liste = []
                for q, u in stuecke:
                    # Leisten der Taefelung (stehen vor der Wand) im Grundriss der Feuerstelle entfallen: die Brueststange lief sonst hell
                    # quer durch die Feueroeffnung (Klicktest 18.11.2026); die Wand dahinter ist geschlossen, der Herd deckt die Enden
                    if (ist_holz(u) and hy0 < sum(p.y for p in q) / len(q) < hy1 and all(abs(p.x - S23.IX) < 0.05 and p.z <= z_haube for p in q)
                            and any(S23.IX - p.x > 0.004 for p in q)):
                        leiste += 1; continue
                    if ist_holz(u) and all(in_herd(p) or (abs(p.x - S23.IX) < 0.05 and hy0 - 1e-3 <= p.y <= hy1 + 1e-3 and p.z <= z_haube) for p in q):
                        u = zu_stein(q, u); neu_stein += 1
                    liste.append((q, list(u), st, fa, gl))
                neu_je.append(liste)
            flaechen_ersetzen(t, neu_je)
        elif t.name == "Fensterseiten":          # Schlot aus Stein (bei den Menschen verputzt)
            for i, (pts, uv, st, fa, gl) in enumerate(t.flaechen):
                if min(p.z for p in pts) >= z_haube + 0.2 - 1e-3 and all(im_schlot(p, 0.17) for p in pts):
                    v0, v1, _ = BI["lehmwand"]
                    if all(v0 - 1e-4 <= v <= v1 + 1e-4 for _, v in uv):
                        t.flaechen[i] = (pts, [(u, stein[0] + (v - v0) / (v1 - v0) * (stein[1] - stein[0])) for u, v in uv], st, fa, gl); neu_stein += 1
    print(f"INNEN Feuerstelle: {weg} Holzflaechen im Schlot weg, {neu_stein} Flaechen zu Stein (Einfassung, Taefelung, Schlot), "
          f"{leiste} Leistenflaechen in der Feueroeffnung weg")


def baue_hauswappen_klein():
    """Hauswappen innen nach der Regel der Stufe 1 (Runde 5): hoch (Unterkante ueber dem Kopf eines Elben), nicht an der Haustuerwand – an der
    linken Wand vor ihrem Fenster, unter dem Unterzug."""
    M, BI = S23.M, S23.BI
    c = Teil("Einrichtung_Hauswappen", BI, saat=87)
    g = 0.34
    o = (-S23.IX, WAPPEN_INNEN_Y, M.Z_EG_DECKE - 0.30)              # ueber dem Schrank der Guten Stube (bis 2,03 m), unter der Decke
    eu, ev, n = (0, 1, 0), (0, 0, 1), (1, 0, 0)
    g1.prisma(c, "Innen", [(x * 1.2 * g, y * 1.14 * g) for x, y in g1.SCHILD], o, eu, ev, n, 0.0, 0.05, "balken")
    g1.prisma(c, "Hausfarbe", [(x * g, y * g) for x, y in g1.SCHILD], o, eu, ev, n, 0.05, 0.08, "balken", farbe=(1.0, 1.0, 1.0, 1.0))
    return c


WAPPEN_INNEN_Y = -0.45


# ── Vordach als Platte, Dach frei vom Schornstein ────────────────────────────────────────────────────────────────────────

def vordach_platte(h, ab):
    """Vordach der Menschen (Dachflaeche und Untersicht auf denselben Punkten, ohne Dicke) als Platte (Klicktest 18.11.2026): Beim
    Verschmelzen der Punkte vor dem Backen (wohnhaus_mensch_s1_g1.verschmelzen) wurden die beiden Flaechen deckungsgleich und Blender
    loeschte je Paar eine – welche, hing von der Reihenfolge ab (G2: rechte Dachflaeche weg, das Spiel zeigte dort die Rueckseite der
    Untersicht, dunkel bzw. durchsichtig). Die Untersicht liegt jetzt VORDACH_DICKE unter der Dachflaeche, Traufe und Stirn sind
    geschlossen (Pruefung „Rueckseite im Blick“, wohnhaus_elb_messen.py)."""
    neu = []
    for i in range(ab, len(h.flaechen)):
        pts, uv, st, fa, gl = h.flaechen[i]
        if st != "Dachziegel" or len(pts) != 4: continue
        n = (pts[1] - pts[0]).cross(pts[2] - pts[0]).normalized()
        if n.z < 0: n = -n
        schluessel = sorted((round(p.x, 4), round(p.y, 4), round(p.z, 4)) for p in pts)
        for k in range(ab, len(h.flaechen)):
            q, u, st2, fa2, gl2 = h.flaechen[k]
            if st2 == "Fachwerk" and len(q) == 4 and sorted((round(p.x, 4), round(p.y, 4), round(p.z, 4)) for p in q) == schluessel:
                h.flaechen[k] = ([p - n * VORDACH_DICKE for p in q], u, st2, fa2, gl2)
                break
        else:
            continue
        # Holzschindeln wie das Pultdach des Holzunterstands (Band „planken“, Stoff Fachwerk) statt Schiefer (Entscheidung des Nutzers
        # 18.11.2026): der dunkle Schiefer wirkte unter der Auskragung fast schwarz
        vs0, vs1, ms = h.baender["stroh"]
        vp0, vp1, mp = h.baender["planken"]
        vs_min = min(v for _, v in uv)
        uv = [(u * ms / mp, vp0 + (v - vs_min) / max(vs1 - vs_min, 1e-6) * (vp1 - 0.003 - vp0)) for u, v in uv]
        h.flaechen[i] = (pts, uv, "Fachwerk", fa, gl)
        mitte = sum(pts, Vector()) / 4 - n * (VORDACH_DICKE / 2)
        zmin, ymin = min(p.z for p in pts), min(p.y for p in pts)
        for a, b in zip(pts, pts[1:] + pts[:1]):
            traufe = abs(a.z - zmin) < 1e-3 and abs(b.z - zmin) < 1e-3
            stirn = abs(a.y - ymin) < 1e-3 and abs(b.y - ymin) < 1e-3
            if traufe or stirn:
                neu.append(([a, b, b - n * VORDACH_DICKE, a - n * VORDACH_DICKE], mitte))
    for q, mitte in neu:
        h.vieleck("Fachwerk", q, [(0.1, 0.772), (0.3, 0.772), (0.3, 0.766), (0.1, 0.766)], weg_von=mitte)   # im Band „planken“ (Pruefung 17)
    print(f"VORDACH Platte: {len(neu)} Stirnflaechen, Untersicht {VORDACH_DICKE * 100:.1f} cm unter der Dachflaeche")


def schlot_frei(d):
    """Dachflaechen im Grundriss des Schornsteins entfallen (Klicktest 18.11.2026): Das Dach lief durch den Schornstein, das Stueck darin
    backte schwarz, und seine Verdeckung lief ueber die Bildpunkte am Rand als dunkler, fleckiger Fleck neben dem Schornstein aufs Dach.
    Die Flaechen werden an den Fluchten des Schornsteins geteilt, die Stuecke darin entfallen."""
    x0, x1, y0, y1 = S23.M.SCHORNSTEIN_RECHTS
    z_oben = S23.M.Z_FIRST - 0.6
    drin = lambda p: x0 - 1e-3 <= p.x <= x1 + 1e-3 and y0 - 1e-3 <= p.y <= y1 + 1e-3 and p.z <= z_oben + 1e-3
    neu_je, weg, geteilt = [], 0, 0
    for pts, uv, st, fa, gl in d.flaechen:
        stuecke = [(pts, uv)]
        if (min(p.x for p in pts) < x1 and max(p.x for p in pts) > x0 and min(p.y for p in pts) < y1 and max(p.y for p in pts) > y0
                and min(p.z for p in pts) < z_oben):
            for f, werte in ((lambda p: p.y, (y0, y1)), (lambda p: p.x, (x0, x1))):
                naechste = []
                for q, u in stuecke:
                    r = schnitt(q, u, f, werte)
                    naechste += r if r else [(q, u)]
                stuecke = naechste
            geteilt += 1
        liste = []
        for q, u in stuecke:
            if all(drin(p) for p in q):
                weg += 1; continue
            liste.append((list(q), list(u), st, fa, gl))
        neu_je.append(liste)
    flaechen_ersetzen(d, neu_je)
    print(f"DACH Schornstein: {geteilt} Flaechen geteilt, {weg} Stuecke im Schornstein weg")


# ── Schicht ─────────────────────────────────────────────────────────────────────────────────────────────────────────────

_MASSE = []


def masse_anlegen(stufe, tuer_hoch=TUER_HOCH):
    """Elbenmasse in den Modulen der Menschen (auch fuer die Messung; einmal): Haustuer und Zimmertueren 2,10 m (`tuer_hoch`: Gegenprobe mit
    den Massen der Menschen), Herdhaube 1,95 m licht, Grundform 4 ohne Erker, Stufe 2 mit dem Quergiebel statt der Gaube."""
    if _MASSE: return
    _MASSE.append(stufe)
    M = S23.M
    M.TUER_LICHT = (M.TUER_LICHT[0], tuer_hoch)
    M.ZIMMERTUER = (M.ZIMMERTUER[0], ZIMMERTUER_HOCH if tuer_hoch == TUER_HOCH else tuer_hoch)
    alt_fenster = M.fenster

    def fenster(form, stufe=2):
        liste = alt_fenster(1 if form == 4 else form, stufe)          # ohne Erker: das linke Fenster des OG bleibt in der Wand
        return [(w, a, (z[0], M.Z_BODEN + tuer_hoch), art) if art == "tuer" else (w, a, z, art) for w, a, z, art in liste]
    M.fenster = fenster
    M.FORMEN[4] = {k: v for k, v in M.FORMEN[4].items() if k != "erker"}
    g1.Z_HAUBE = g1.Z_HAUBE + HAUBE_HOEHER
    if stufe == 2:
        S23.GAUBE.update(y=QUERGIEBEL["y"], x_front=M.OX, fenster=QUERGIEBEL["fenster"])


def laeden_zu_anlegen(stufe):
    """Geschlossene Laeden im festen Zufall (zwerg_teile.laeden_zu, Verfahren der Zeilenmodule; wie bei den Zwergen): im Fachwerk des
    Buergerhauses auf freien Gefachen zwischen Brust- und Sturzriegel, in Putzsockel und Steinwaenden auf fensterlosen Achsfeldern.
    Steht die Huelle (Unterstand, Schuppen, Schlot, Vordach), faellt weg, was verstellt waere."""
    import zeile_module as Z
    import zwerg_teile as zt
    M = S23.M
    haus = lambda: f"wohnhaus_elb_s{stufe}_g{FORM[0]}"

    def fensterhoehe(wand, z0):
        fen = [o for o in S23.loecher(wand) if o[4] == "fenster"]
        if fen: return min(f[2] for f in fen), max(f[3] for f in fen)
        if wand.endswith("_eg"): return next(f[2] for f in M.FENSTER if f[0].endswith("_eg") and f[3] == "fenster")
        return z0 + M.OG_FENSTER_Z[0] - M.Z_EG_DECKE, z0 + M.OG_FENSTER_Z[1] - M.Z_EG_DECKE

    alt_lage = S23.fachwerk_lage

    def fachwerk_lage(h, wand, z0, z1, raehm=True):
        alt_lage(h, wand, z0, z1, raehm)
        (ox, oy), (ex, ey), breite, _ = S23.WAENDE[wand]
        t0, t1 = fensterhoehe(wand, z0)
        felder = zt.felder_der_lage(S23.LAGEN[wand], breite, S23.loecher(wand), t0, t1)
        zt.laeden_zu(h, S23.punkt(wand, 0.0, 0.0), (ex, ey, 0), S23.nvec(wand), felder, t0 + 0.04, t1 - 0.04, f"{haus()}|{wand}", Z.LADEN_OG, nebeneinander=False)
    S23.fachwerk_lage = fachwerk_lage
    alt_mauer = S23.steinwand

    def steinwand(h, stoff, wand, z0, z1, *a, **kw):
        alt_mauer(h, stoff, wand, z0, z1, *a, **kw)
        if not wand.endswith(("_eg", "_og", "_o2")): return
        (ox, oy), (ex, ey), breite, _ = S23.WAENDE[wand]
        t0, t1 = fensterhoehe(wand, z0)
        achsen, _ = M.raster(breite)
        felder = zt.felder_der_mauer(achsen, M.BW, breite, S23.loecher(wand))
        zt.laeden_zu(h, S23.punkt(wand, 0.0, 0.0), (ex, ey, 0), S23.nvec(wand), felder, t0, t1, f"{haus()}|{wand}",
                     Z.LADEN_EG if wand.endswith("_eg") else Z.LADEN_OG, nebeneinander=False)
    S23.steinwand = steinwand
    alt_fahne = S23.baue_fahne

    def fahne():
        zt.laeden_zu_pruefen([et.ohne_efeu(LETZT["haus"]), LETZT["dach"]])       # Efeu darf vor einem Laden haengen
        return alt_fahne()
    S23.baue_fahne = fahne


def schicht_anlegen(stufe):
    """Stoff, Dateinamen, Atlasgruppen, Elbenmasse und die Bauteile der Elben; die Skripte der Menschen bleiben unveraendert."""
    # Echtes Fachwerk im Buergerhaus (Holzlage wie bei Menschen, Halblingen und Zwergen, Nutzer 23.11.2026), die Streben gebogen
    S23.FACHWERK_ECHT = S23.FM.FACHWERK_ECHT = stufe == 2
    et.bogen_in_holzlage()
    if stufe == 3:
        # Steinhaus: Putzflaechen (Fluegel der Grundform 2, Zwickel der Spitzboegen) entstehen gleich aus den Lagen des Hausteins –
        # versteinern() legte den Putz sonst ueber die ganze Wandhoehe gestreckt auf das Steinband (wie bei den Zwergen)
        import zwerg_teile
        zwerg_teile.quader_in_lagen(ziel=("Bruchstein", "bruchstein"))
    S23.LICHT_DECKE = False                                # kein Deckenlicht (nur Menschen und Zwerge, 21.11.2026)
    STUFE[0] = stufe
    M = S23.M
    alt_stoff, alt_uv, alt_exp = g1.stoff, g1.uv2_und_ao, g1.exportiere
    g1.stoff = lambda name, blatt=None, **kw: alt_stoff(name, (blatt + "_" + VOLK) if blatt else None, **kw)   # trim_s2 -> trim_s2_elb
    g1.uv2_und_ao = lambda gruppen, gruppe, modell, mit_ao=True: alt_uv(gruppen, umbenennen(gruppe), umbenennen(modell), mit_ao)
    g1.exportiere = lambda objekte, datei: alt_exp(objekte, umbenennen(datei))
    masse_anlegen(stufe)
    alt_anker, alt_init = g1.anker, s1.Anker.__init__

    def ort(name, o):
        o = Vector(o)
        if name in ("Blick_Auge", "Blick_Ziel"): o.z += BLICK_HOEHER
        if name == "Blick_Auge": LETZT["auge"] = o.copy()
        if name == "Licht_Raum" and "auge" in LETZT: o = licht_vor_den_herd(o, LETZT["auge"])
        return o
    g1.anker = lambda name, o, blick=None, **kw: alt_anker(name, ort(name, o), blick, **kw)
    s1.Anker.__init__ = lambda self, name, o, blick=None, **ex: alt_init(self, name, ort(name, o), blick, **ex)
    # Vordach auf der Hoehe der Menschen (die Tuer ist 10 cm hoeher; sonst stiesse es an die Auskragung)
    alt_vordach = S23.vordach

    def vordach(h, x, z):
        ab = len(h.flaechen)
        alt_vordach(h, x, z - (TUER_HOCH - 2.00))
        # die Untersicht der Menschen reicht mit v 0,78 eine Spur ueber das Band „planken“ (bis 0,775) ins Band „farbholz“: Pruefung 17
        v0, v1, _ = h.baender["planken"]
        for i in range(ab, len(h.flaechen)):
            pts, uv, st, fa, gl = h.flaechen[i]
            if st == "Fachwerk" and all(v0 - 1e-4 <= v <= 0.78 + 1e-4 for _, v in uv) and max(v for _, v in uv) > v1:
                h.flaechen[i] = (pts, [(u, min(v, v1 - 0.002)) for u, v in uv], st, fa, gl)
        vordach_platte(h, ab)
    S23.vordach = vordach
    if stufe == 2:
        # Putzsockel: Erdgeschoss aus Kalkputz der Elben (Band „putz“), Eckquader, Gewaende und Schornstein aus Haustein
        alt_wand = S23.steinwand
        S23.steinwand = lambda h, stoff, wand, z0, z1, band="putz", laibung=True: alt_wand(h, "Lehmputz" if band == "putz" else stoff, wand, z0, z1, band, laibung)
    else:
        S23.treppengiebel = schildgiebel
        # Patrizierhaus der Elben (Entwurf 23.11.2026): Kalkputz mit Hausteinrahmen, Gurtgesims und Werkstein aus Haustein (K2), Kielbogenfries
        S23.GURT_WERK = True
        S23.WERKSTEIN = "bruchstein"
        S23.OBEN.update(band="putz", lage=None)
        S23.rundbogenfries = kielbogenfries
        alt_wand3 = S23.steinwand
        S23.steinwand = lambda h, stoff, wand, z0, z1, band="putz", laibung=True, lage=None: alt_wand3(h, "Lehmputz" if band == "putz" else stoff, wand, z0, z1, band, laibung, lage)
        alt_versteinern = S23.versteinern
        # Schuppen (Grundform 3) bleibt Holz: der Holzunterstand der Elben (bei den Menschen Pfeiler aus Haustein)
        S23.versteinern = lambda teil, ab=0, bis=None, z_min=0.7, ganz=False: None if ganz else alt_versteinern(teil, ab, bis, z_min, ganz)
    # Huelle
    alt_haus = S23.baue_haus

    def haus():
        h = alt_haus()
        vorher = h.dreiecke()
        spitzboegen(h)
        print("DREIECKE Spitzbogen:", h.dreiecke() - vorher)
        if FORM[0] in (1, 4):                                    # wie Stufe 1: Grundform 3 hat den Schuppen, 2 keinen Unterstand
            vorher = h.dreiecke()
            unterstand_links(h)
            print("DREIECKE Unterstand:", h.dreiecke() - vorher)
        if stufe == 3:
            vorher = h.dreiecke()
            vorder_zier(h)
            print("DREIECKE Kielboegen und Laterne:", h.dreiecke() - vorher)
        efeu_setzen(h)
        LETZT["haus"] = h
        return h
    S23.baue_haus = haus
    alt_dach, alt_gaube = S23.baue_dach, S23.gaube

    def dach(*a, **kw):
        S23.gaube = lambda d, stoff: d                           # Quergiebel erst nach dem Schwung (er bleibt, wie er ist)
        try:
            d = alt_dach(*a, **kw)
        finally:
            S23.gaube = alt_gaube
        vorher = d.dreiecke()
        ortgang_schwingen(d, vorn=stufe == 2)
        dach_schwingen(d, vorn=stufe == 2)
        print("DREIECKE Dachschwung:", d.dreiecke() - vorher)
        if stufe == 2:
            vorher = d.dreiecke()
            alt_gaube(d, a[0] if a else kw.get("stoff", "Dachziegel"))
            print("DREIECKE Quergiebel:", d.dreiecke() - vorher)
        vorher = d.dreiecke()
        schlot_frei(d)
        print("DREIECKE Schornstein frei:", d.dreiecke() - vorher)
        LETZT["dach"] = d
        return d
    S23.baue_dach = dach
    alt_schuppen = S23.FM.schuppen

    def schuppen(h, d, box, ziegelflaeche):
        """Grundform 3 (Pruefung 12b, 18.11.2026): Die Lagen des Pultdachs zeigen bei den Menschen von der Hausmitte weg (weg_von=MITTE,
        z 3,0) – beim Schuppen (Dach um 2 m) also nach unten: von oben sah man nur ihre Rueckseiten (im Spiel durchsichtig). Hier nach oben."""
        ab = len(d.flaechen)
        alt_schuppen(h, d, box, ziegelflaeche)
        gedreht = 0
        for i in range(ab, len(d.flaechen)):
            pts, uv, st, fa, gl = d.flaechen[i]
            if st == "Dachziegel" and len(pts) >= 3 and (pts[1] - pts[0]).cross(pts[2] - pts[0]).normalized().z < -0.5:
                d.flaechen[i] = (pts[::-1], list(uv)[::-1], st, fa, gl); gedreht += 1
        print(f"SCHUPPEN Dachlagen nach oben gedreht: {gedreht}")
    S23.FM.schuppen = schuppen
    alt_scheiben = S23.baue_scheiben

    def scheiben():
        s = alt_scheiben()
        if stufe == 3: s.flaechen = s.flaechen[:-2]             # ohne Gaube: ihr Glas entfaellt
        LETZT["scheiben"] = s
        return s
    S23.baue_scheiben = scheiben
    alt_wappen = S23.baue_wappen

    def wappen():
        w = alt_wappen()
        et.laeden_am_wappen_weg(w)                                # kein geschlossener Laden im Feld des Wappens
        g1.wappen_frei_ruecken(w, [LETZT["haus"], LETZT["dach"], LETZT["scheiben"]])   # Wappenregel (Spitzbogen, Unterstand)
        return w
    S23.baue_wappen = wappen
    # Innen
    # main() baut erst die Moebel (Herd), dann Raum, Fensterseiten (Schlot) und Decke: die Feuerstelle entsteht mit der Decke
    S23.baue_hauswappen_klein = baue_hauswappen_klein
    alt_moebel, alt_fs, alt_raum = S23.moebel, S23.baue_fensterseiten, S23.baue_raum

    def moebel():
        m_raum, gegen, einr = alt_moebel()
        LETZT["m_raum"] = m_raum
        return m_raum, gegen, einr
    S23.moebel = moebel
    S23.baue_fensterseiten = lambda: LETZT.__setitem__("fs", alt_fs()) or LETZT["fs"]
    S23.baue_raum = lambda: LETZT.__setitem__("raum", alt_raum()) or LETZT["raum"]

    def decke():
        feuerstelle(LETZT["m_raum"] + [LETZT["raum"], LETZT["fs"]])
        return baue_decke()
    S23.baue_decke = decke
    laeden_zu_anlegen(stufe)


def messen(stufe, form, ausgabe_ordner=None):
    """Ist-Masse aus der gebauten Szene und den Massen der Mensch-Skripte; Soll gegen Ist: wohnhaus_elb_s23_masse.pruefe."""
    M = S23.M
    objs = bpy.data.objects
    z0 = M.Z_BODEN
    anker = {o.name: o.location.copy() for o in objs if o.type == "EMPTY"}
    tuer = next(f for f in M.FENSTER if f[3] == "tuer")
    loch = M.TREPPE_LOCH_X[1]
    stufe_hoch = (M.TREPPE_FUSS_X - loch) / M.AUFTRITT * M.STEIGUNG
    ist = dict(tuer_h=tuer[2][1] - tuer[2][0], tuer_b=tuer[1][1] - tuer[1][0], raum_h=M.Z_EG_DECKE - z0, haube_h=g1.Z_HAUBE - z0, sitz=g1.SITZ_HOEHE,
               tisch=g1.TISCH_HOEHE, auge=anker["Blick_Auge"].z - z0, stufe=M.STUFE_VOR_TUER, zimmertuer=M.ZIMMERTUER, treppe_steigung=M.STEIGUNG,
               treppe_kopfhoehe=M.Z_EG_DECKE - (z0 + stufe_hoch), og_raum_h=M.Z_OG_DECKE - M.Z_OG)
    zeilen = []
    _z, fehler = EM.pruefe(stufe, form, ist, ausgabe=zeilen.append)
    for z in zeilen: print(z)
    print(f"SOLLIST Fehler: {fehler}")
    if ausgabe_ordner:
        os.makedirs(ausgabe_ordner, exist_ok=True)
        open(os.path.join(ausgabe_ordner, f"soll_ist_s{stufe}_g{form}.txt"), "w", encoding="utf-8").write("\n".join(z.replace("SOLLIST ", "", 1) for z in zeilen) + "\n")


def main():
    args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    opt = dict((a[2:].split("=", 1) + ["1"])[:2] for a in args if a.startswith("--"))
    stufe = int(opt.get("stufe", "2"))
    FORM[0] = int(opt.get("form", "1"))
    if stufe == 3: sys.argv.append("--dach-alt")               # Schildgiebel statt Blendnischen: die Dachvorderkante bleibt, wo sie war (sonst Durchblick am First)
    schicht_anlegen(stufe)
    if os.environ.get("REICHSGILDE_ZAEHLEN"):                  # Messung der Dreiecke je Bauteil (dreiecke_zaehler.py)
        import dreiecke_zaehler
        dreiecke_zaehler.anlegen(S23, sys.modules[__name__])
    S23.main()
    messen(stufe, int(opt.get("form", "1")), opt.get("bilder"))


if __name__ == "__main__":
    main()
