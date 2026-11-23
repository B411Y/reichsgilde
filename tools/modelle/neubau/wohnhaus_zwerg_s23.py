"""Neubau der Gebaeude: Wohnhaus Zwerg, Stufe 2 (Buergerhaus) und Stufe 3 (Steinhaus), Grundformen 1 bis 4 - aussen und innen.

    blender -b --factory-startup --python tools/modelle/neubau/wohnhaus_zwerg_s23.py -- --stufe=<2|3> --form=<1|2|3|4> [--bilder=<ordner>] [--ohne-ao]

Wie bei Stufe 1 (wohnhaus_zwerg_s1.py) eine SCHICHT auf den Bauskripten der Menschen (wohnhaus_mensch_s23.py mit _masse und _formen,
unveraendert):
  * Stoff, Dateinamen, Atlasgruppen: trim_s2_zwerg bzw. trim_s3_zwerg (aussen), innen_trim_s23_zwerg (innen), Gruppen wohnhaus_zwerg_s<n>
    und wohnhaus_zwerg_innen; alle Dateien heissen wohnhaus_zwerg_s<stufe>_g<form>[_innen].glb. Im Spiel liegen die Blaetter als Ebenen 4 und 5
    im Haeuser-Array (Steinebenen.cs, trimsheet_ebenen.py: der alte dunkle Stein der Zwerge, im Steinhaus das Band „bruchstein“ heller Haustein).
  * Masse, Waende, Fenster, Tuer, Treppe, Raeume, Wappenregel (wappen_ort): die des Menschenhauses (innen exakt massgleich mit aussen; Menschen
    muessen hinein). Zwergenhoehen: Tisch 0,62, Sitz 0,36, Blick 1,25 m, Apotheke/Bauplan/Werkzeug auf Greifhoehe (wie Stufe 1).
  * Stufe 2 (Kunstwerk wohnhaus_lvl2_zwerg): Erdgeschoss aus dunklem Quader (Band „putz“ wie Stufe 1), Obergeschoss und Giebel in Fachwerk mit
    Steinfuellung nach der Konstruktionsregel des Menschen (Raster 0,95 m, ein Fenster je Gefach, Giebel bis zum First), Schieferdach.
  * Stufe 3 (Kunstwerk wohnhaus_lvl3_zwerg): GANZ STEIN. Mauer dunkler Quader (Band „putz“), Werkstein (Eckquader, Gewaende, Gesims,
    Treppengiebel, Schornstein) heller Haustein im Band „bruchstein“; der Mittelpfosten zwischen zwei Fenstern eines Paars ist ein
    HOLZPFOSTEN 25 x 10 cm (Nutzer 16.11.2026; kein Steinpfeiler mit tiefen Laibungen, keine Ankereisen), Laeden und Balkenkoepfe dunkles Holz.
  * vor der Tuer ein Amboss (Kunstwerke; nur Stufe 2, im Steinhaus reichen die Dreiecke nach den Eckquadern im OG nicht).
Messung: wohnhaus_zwerg_neu_messen.py; Soll/Ist: wohnhaus_zwerg_s23_masse.py.
"""
import math
import os
import sys

HIER = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HIER)
sys.path.insert(0, os.path.join(os.path.dirname(HIER), "raeume"))
import bpy                                                         # noqa: E402,F401
from mathutils import Vector                                       # noqa: E402
import wohnhaus_mensch_s1_g1 as g1                                 # noqa: E402
import wohnhaus_mensch_s1 as s1                                    # noqa: E402
import wohnhaus_mensch_s23 as S23                                  # noqa: E402
import zwerg_teile as zt                                           # noqa: E402
import wohnhaus_zwerg_s1 as Z1                                     # noqa: E402  (Konstanten der Stufe 1)
import wohnhaus_zwerg_s23_masse as ZM                              # noqa: E402

VOLK = "zwerg"
BLICK_TIEFER = Z1.BLICK_TIEFER
APOTHEKE_TIEFER, BAUPLAN_TIEFER = Z1.APOTHEKE_TIEFER, Z1.BAUPLAN_TIEFER


def umbenennen(text):
    return text.replace("mensch", VOLK)


def gewaende_zwerg(h, wand, spannen, z0, z1):
    """Gewaende des Steinhauses wie bei den Menschen (Haustein: Gewaendesteine, Sohlbank, Sturz), aber der Mittelpfosten eines
    Fensterpaars ist ein Holzpfosten 25 x 10 cm (deckt beide Laibungskanten mit je 4 cm, 10 cm vor der Mauer, von der Sohlbank bis zum
    Sturz). Er ist ein Holzakzent: versteinern() laesst ihn Holz."""
    st, band = "Bruchstein", S23.WERKSTEIN
    links, rechts = spannen[0][0], spannen[-1][1]
    for a, b in ((links - 0.13, links), (rechts, rechts + 0.13)):
        S23.wkasten(h, st, wand, a, b, 0.0, 0.05, z0 - 0.02, z1 + 0.02, band, ohne=("-z", "+z"), laengs="z")

    def pfosten():
        for i in range(len(spannen) - 1):
            mitte = (spannen[i][1] + spannen[i + 1][0]) / 2
            S23.wkasten(h, "Fachwerk", wand, mitte - 0.125, mitte + 0.125, 0.0, 0.10, z0 - 0.005, z1 + 0.005, "balken_c", laengs="z")
    if len(spannen) > 1:
        S23.holz_akzent(h, pfosten)
    S23.wkasten(h, st, wand, links - 0.17, rechts + 0.17, 0.0, 0.08, z0 - 0.11, z0 - 0.005, band, laengs="x")
    S23.wkasten(h, st, wand, links - 0.17, rechts + 0.17, 0.0, 0.06, z1 + 0.005, z1 + 0.17, band, laengs="x")


GAUBE_ZWERG = dict(y=(0.45, 1.65))
FORM = 1                # Grundform des laufenden Baus (main); Teil der festen Saat der Zufallslaeden


def laeden_zu_anlegen(stufe):
    """Geschlossene Laeden im festen Zufall (zwerg_teile.laeden_zu, Verfahren der Zeilenmodule): im Fachwerk des Buergerhauses auf freien
    Gefachen zwischen Brust- und Sturzriegel, in den Steinwaenden (Erdgeschoss, Steinhaus) auf fensterlosen Achsfeldern."""
    import zeile_module as Z
    M = S23.M
    haus = lambda: f"wohnhaus_zwerg_s{stufe}_g{FORM}"

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
        # Steinhaus: versteinern() macht den Rahmen (Band balken_c) zu Werkstein, die Fluegel (Band planken) bleiben Holz
        zt.laeden_zu(h, S23.punkt(wand, 0.0, 0.0), (ex, ey, 0), S23.nvec(wand), felder, t0, t1, f"{haus()}|{wand}",
                     Z.LADEN_EG if wand.endswith("_eg") else Z.LADEN_OG, nebeneinander=False)
    S23.steinwand = steinwand


# ── Stufe 3, Patrizierhaus der Zwerge (Nutzer 23.11.2026, Entwurf „Steinernes Haus“): Zinnen, stumpfe Boegen, Hammer und Schlaegel,
#    Kragsteinfries, Eisengitter. Vorbild Steinernes Haus Frankfurt (Zinnen, Ecktuermchen) und Kamenny dum Kutna Hora (Public domain / CC BY 4.0).
GIEBEL_LAGE = 0.6            # Massstab des Bandes „bruchstein“ im Treppengiebel (Lagenhoehe 0,24 m statt 0,40 m)
SEGMENT_H = 0.25            # Pfeilhoehe der stumpfen Boegen je Spannweite (Mensch 0,866 Spitzbogen, Halbling 0,5 Halbkreis)


def segmentbogen(x0, x1, zs, n=4):
    """Kreissegment ueber x0..x1 mit Kaempfer zs und Pfeil SEGMENT_H * Spannweite; gleich viele Punkte wie S23.spitzbogen (2n + 1)."""
    w = x1 - x0
    s_ = SEGMENT_H * w
    r = (w * w / 4 + s_ * s_) / (2 * s_)
    alpha = math.degrees(math.asin(min(0.999, (w / 2) / r)))
    xm, zc = (x0 + x1) / 2, zs + s_ - r
    pts = []
    for k in range(2 * n + 1):
        t = math.radians(90 + alpha - 2 * alpha * k / (2 * n))
        pts.append((xm + r * math.cos(t), zc + r * math.sin(t)))
    return pts


def zinnen_fiale(h, x, y_vorn, z, gross=False, tief=None):
    """Statt der Fiale: Zinnenkopf auf jeder Stufenecke (Sockel und drei Zinnen 0,12 x 0,28 m mit 4 cm Luecke), auf der Spitze das Ecktuermchen mit
    Kragkranz und Zinnen 0,16 x 0,32 m (Entwurf 0,25 x 0,35 m; die Stufen sind nur etwa 0,45 m breit, darum drei schmale Zinnen je Stufe).
    Pruefer 23.11.2026: die ersten Zinnen (0,10 x 0,16 m) waren im Bild nicht lesbar."""
    tief = tief or 0.2
    y0, y1 = y_vorn, y_vorn + tief
    kk = lambda mn, mx, **kw: h.kasten("Bruchstein", mn, mx, S23.WERKSTEIN, ohne_streckung=True, **kw)
    if not gross:
        kk((x - 0.22, y0 - 0.03, z), (x + 0.22, y1 + 0.03, z + 0.12), laengs="x")                     # Sockel
        for k in (-1, 0, 1):
            kk((x + k * 0.16 - 0.06, y0 - 0.03, z + 0.12), (x + k * 0.16 + 0.06, y1 + 0.03, z + 0.40), laengs="z")   # Zinne
        return
    kk((x - 0.26, y0 - 0.04, z), (x + 0.26, y1 + 0.04, z + 1.00), laengs="z")                    # Schaft des Tuermchens
    kk((x - 0.34, y0 - 0.10, z + 1.00), (x + 0.34, y1 + 0.10, z + 1.12), laengs="x")             # Kragkranz
    for sx in (-1, 1):                                                                            # vier Eckzinnen 0,12 breit, dazu je eine Zinne in der Mitte vorn und hinten
        for sy in (y0 - 0.10, y1 + 0.10 - 0.16):
            kk((x + sx * 0.20 - 0.06, sy, z + 1.12), (x + sx * 0.20 + 0.06, sy + 0.16, z + 1.44), laengs="z")
    kk((x - 0.05, y0 - 0.10, z + 1.12), (x + 0.05, y0 + 0.06, z + 1.44), laengs="z")             # mittlere Zinne vorn (Luecken je 9 cm)
    kk((x - 0.05, y1 - 0.06, z + 1.12), (x + 0.05, y1 + 0.10, z + 1.44), laengs="z")             # und hinten


def hammer_schlaegel(h, P, xm, zm, r, breite, tiefe, n=8, band=None):
    """Statt des Okulus: Ring (wie bisher) mit gekreuztem Schlaegel und Eisen, die Zeichen der Bergleute, als Relief aus Werkstein: Stiele 4,5 cm,
    Koepfe 9 cm, 8 cm tief, Ausdehnung etwa 0,4 x 0,4 m (Entwurf 0,5 x 0,5 m; mehr Platz haben die Bogenfelder nicht)."""
    S23._kreis_ring_alt(h, P, xm, zm, r, breite, tiefe, n=n, band=band)
    d = max((r - breite) * 0.78, 0.17)
    t = 0.08
    st = lambda a, b, dick: h.stab("Bruchstein", P(a[0], a[1], t / 2), P(b[0], b[1], t / 2), dick, S23.WERKSTEIN, breite=dick, kappen=True)
    st((xm - d, zm - d), (xm + d, zm + d), 0.06)                                   # Stiel des Schlaegels
    st((xm - d, zm + d), (xm + d, zm - d), 0.06)                                   # Stiel des Eisens
    st((xm + d * 0.45, zm + d * 1.20), (xm + d * 1.20, zm + d * 0.45), 0.14)       # Kopf des Schlaegels (quer zum Stiel)
    st((xm - d * 0.55, zm + d * 1.15), (xm - d * 1.15, zm + d * 0.55), 0.08)       # Schneide des Eisens


def kragsteinfries(h):
    """Kragsteinfries statt Rundbogenfries: Deckband unter dem Gesims der Gassenseite, darunter Konsolen alle 0,6 m (zweistufig)."""
    M = S23.M
    z0, z1 = M.Z_EG_DECKE - 0.12, M.Z_EG_DECKE - 0.06
    x0, x1 = -M.BX + 0.52, M.BX - 0.52
    h.kasten("Bruchstein", (x0, M.VORN - S23.RELIEF, z0), (x1, M.VORN, z1), S23.WERKSTEIN, ohne=("+y",), laengs="x", ohne_streckung=True)
    n = max(2, int((x1 - x0) / 0.6))
    rand = (x1 - x0 - (n - 1) * 0.6) / 2
    for k in range(n):
        x = x0 + rand + k * 0.6
        h.kasten("Bruchstein", (x - 0.10, M.VORN - 0.18, z0 - 0.12), (x + 0.10, M.VORN, z0), S23.WERKSTEIN, ohne=("+y", "+z"), ohne_streckung=True)
        h.kasten("Bruchstein", (x - 0.07, M.VORN - 0.14, z0 - 0.22), (x + 0.07, M.VORN, z0 - 0.12), S23.WERKSTEIN, ohne=("+y", "+z"), ohne_streckung=True)


def tuerbeschlaege(h):
    """Haustuer: drei Eisenbaender quer ueber das Blatt mit Nagelkoepfen (Entwurf Zwerg 4: Tuerbeschlaege aus Eisen), 2 cm vor dem Tuerblatt."""
    M = S23.M
    tuer = next(f for f in M.FENSTER if f[3] == "tuer" and f[0] == "vorn_eg")
    (a0, a1), (b0, b1) = tuer[1], tuer[2]
    y = M.VORN - 0.075
    for fz in (0.22, 0.52, 0.82):
        zc = b0 + (b1 - b0) * fz
        h.kasten("Eisen", (a0 + 0.04, y - 0.014, zc - 0.06), (a1 - 0.04, y + 0.004, zc + 0.06), "eisen", laengs="x")
        for k in range(5):
            xn = a0 + 0.12 + k * ((a1 - a0 - 0.24) / 4)
            h.kasten("Eisen", (xn - 0.02, y - 0.024, zc - 0.02), (xn + 0.02, y - 0.014, zc + 0.02), "eisen")


def eisengitter(h):
    """Fenstergitter vor den Erdgeschossfenstern der Gassenseite: senkrechte Stangen alle 0,11 m, zwei Querbaender (Stoff Eisen)."""
    M = S23.M
    for wand, (a0, a1), (b0, b1), art in M.FENSTER:
        if wand != "vorn_eg" or art != "fenster": continue
        y = M.VORN - 0.07
        for i in range(int((a1 - a0) / 0.11) + 1):
            x = a0 + 0.04 + i * 0.11
            if x > a1 - 0.03: break
            h.kasten("Eisen", (x - 0.008, y - 0.008, b0 - 0.02), (x + 0.008, y + 0.008, b1 + 0.02), "eisen", laengs="z")
        for zq in (b0 + 0.1, b1 - 0.1):
            h.kasten("Eisen", (a0 - 0.01, y - 0.012, zq - 0.012), (a1 + 0.01, y + 0.012, zq + 0.012), "eisen", laengs="x")


def schicht_anlegen(stufe):
    """Stoff, Dateinamen und Atlasgruppen auf den Zwerg umstellen; Zwergenhoehen; Stein der Zwerge; Dinge der Zwerge."""
    # Echtes Fachwerk im Buergerhaus (Holzlage wie bei Menschen und Halblingen, Nutzer 22.11.2026); das Steinhaus bleibt ganz aus Stein
    S23.FACHWERK_ECHT = S23.FM.FACHWERK_ECHT = stufe == 2
    # Gaube hinter dem Schornstein (er steht aussen an der rechten Wand bei y -0,70..0,20 und verstellte der Gaube den Blick)
    S23.GAUBE.update(GAUBE_ZWERG)
    # Gefache, Giebel, Gaube und Erker aus dunklem Quader: in Lagen der Bandhoehe statt ueber die Geschosshoehe gezogen
    zt.quader_in_lagen(ziel=("Bruchstein", "bruchstein") if stufe == 3 else None)   # Steinhaus: Gaube aus den Lagen des Werksteins
    alt_stoff, alt_uv, alt_exp = g1.stoff, g1.uv2_und_ao, g1.exportiere
    g1.stoff = lambda name, blatt=None, **kw: alt_stoff(name, (blatt + "_" + VOLK) if blatt else None, **kw)   # trim_s2 -> trim_s2_zwerg, innen_trim_s23 -> ..._zwerg
    g1.uv2_und_ao = lambda gruppen, gruppe, modell, mit_ao=True: alt_uv(gruppen, umbenennen(gruppe), umbenennen(modell), mit_ao)
    g1.exportiere = lambda objekte, datei: alt_exp(objekte, umbenennen(datei))
    g1.TISCH_HOEHE, g1.SITZ_HOEHE = Z1.ZWERG_TISCH, Z1.ZWERG_SITZ
    alt_anker, alt_init = g1.anker, s1.Anker.__init__

    def ort(name, o):
        o = Vector(o)
        if name in ("Blick_Auge", "Blick_Ziel"): o.z -= BLICK_TIEFER
        return o
    g1.anker = lambda name, o, blick=None, **kw: alt_anker(name, ort(name, o), blick, **kw)
    s1.Anker.__init__ = lambda self, name, o, blick=None, **ex: alt_init(self, name, ort(name, o), blick, **ex)
    # Greifhoehe wie Stufe 1
    a_apo, a_plan = g1.baue_hausapotheke, g1.baue_bauplan
    # tiefer gehaengt laege die Seitenwand der Apotheke in der Ebene der Riegelstuetze an der Haustuer (deckungsgleich): 1,5 cm zur Seite
    g1.baue_hausapotheke = lambda: s1.versetze(a_apo(), t=(0.015, 0.0), dz=-APOTHEKE_TIEFER)
    g1.baue_bauplan = lambda: s1.versetze(a_plan(), dz=-BAUPLAN_TIEFER)
    g1.baue_werkzeug = lambda: zt.werkzeug(g1.IX)
    # Innen: Steinpflaster statt Dielen (Stufe 3 behaelt das Schachbrett aus dunklem und hellem Stein)
    alt_streifen = S23.streifen_boden
    S23.streifen_boden = lambda r, band, *a, **kw: alt_streifen(r, "lehmboden" if band == "bohlen" else band, *a, **kw)
    # Mauer aus dunklem Quader der Zwerge (Band „putz“ wie im Erdgeschoss der Stufe 1); bei den Menschen Bruchstein bzw. K1
    alt_wand = S23.steinwand
    S23.steinwand = lambda h, stoff, wand, z0, z1, band="putz", laibung=True, lage=None: alt_wand(h, stoff, wand, z0, z1, band, laibung, lage)   # lage: seit Patrizier D (Menschen)
    if stufe == 3:
        # Werkstein (Eckquader, Gewaende, Abdeckplatten) im Band „bruchstein“: der helle Haustein der Zwerge (Ebene zwerg_s3)
        S23.WERKSTEIN = "bruchstein"
        S23.OBEN.update(band="putz", lage=None)             # Obergeschoss bleibt dunkler Quader der Zwerge (Entwurf: dunkle Mauer)
        S23.gewaende = gewaende_zwerg
        # Patrizierhaus der Zwerge (Entwurf 23.11.2026)
        S23.GURT_WERK = True                                # Gurtgesims, Giebelfuss-Gesims, Tuerstufe aus Werkstein
        S23.GIEBEL_LAGEN = True                             # Grund der Blendnischen in Lagen (keine gestreckte Textur, Befund Pruefer 23.11.2026)
        S23.spitzbogen = segmentbogen                       # stumpfe Boegen (Kreissegment) in Blendnischen und Giebelfenster
        S23.BOGEN_H = SEGMENT_H                             # Lage von Mittelnische, Okulus und Fenstergewaende folgt der Pfeilhoehe
        S23.fiale = zinnen_fiale                            # Zinnen und Ecktuermchen statt Fialen
        S23._kreis_ring_alt = S23.kreis_ring
        S23.kreis_ring = hammer_schlaegel                   # Schlaegel und Eisen im Bogenfeld
        S23.rundbogenfries = kragsteinfries
        alt_treppengiebel = S23.treppengiebel

        def treppengiebel_dichter(h, *args, **kw):
            """Treppengiebel mit demselben Band wie die Ecken („bruchstein“, K2; Nutzer 23.11.2026), aber in niedrigeren Lagen: Das Band zeigt je Lage seine Lagerfugen
            an Ober- und Unterkante, mit dem Massstab GIEBEL_LAGE (0,6) statt 1 sind die Lagen 0,24 m hoch und die Fugen dichter und besser zu sehen (die Flaeche war vorher
            fast fugenlos). Nur waehrend dieses Aufrufs; Ecken, Gewaende und Gesimse behalten den Massstab 1."""
            v0, v1, m = h.baender["bruchstein"]
            h.baender["bruchstein"] = (v0, v1, m * GIEBEL_LAGE)
            try: return alt_treppengiebel(h, *args, **kw)
            finally: h.baender["bruchstein"] = (v0, v1, m)
        S23.treppengiebel = treppengiebel_dichter
    # Aussen: Amboss neben der Haustuer an der Gassenwand (Kunstwerke Stufe 2 und 3)
    alt_haus = S23.baue_haus

    def haus():
        h = alt_haus()
        S23._zwerg_haus = h
        vorher = h.dreiecke()
        tuer = next(f for f in S23.M.FENSTER if f[3] == "tuer")
        wand, (a0, a1) = tuer[0], tuer[1]
        (ox, oy), (ex, ey), laenge, (nx, ny) = S23.WAENDE[wand]
        start = ox if ex else oy
        s0, s1_ = a0 - start, a1 - start
        rechts = laenge - s1_ >= s0                          # auf der Seite mit mehr Platz
        sa = s1_ + 0.85 if rechts else s0 - 0.85
        lage = lambda s, raus: (ox + ex * s + nx * raus, oy + ey * s + ny * raus)
        if stufe == 2:      # Steinhaus: ohne Amboss (Dreiecke fuer die Eckquader des Obergeschosses, Grenze 4.000)
            zt.schmiedegeraet(h, lage(sa, 0.50), (nx, ny), (ex * (1 if rechts else -1), ey * (1 if rechts else -1)), schlank=True, nur_amboss=True)
            print("DREIECKE Schmiedegeraet:", h.dreiecke() - vorher)
        if stufe == 3:
            vorher = h.dreiecke()
            weg = eckquader_neu(h)
            print("DREIECKE Eckquader (EG ersetzt, OG neu):", h.dreiecke() - vorher, "- entfernte Flaechen", weg)
            vorher = h.dreiecke()
            eisengitter(h)
            tuerbeschlaege(h)
            print("DREIECKE Eisengitter:", h.dreiecke() - vorher)
        if stufe == 3: zt.entflimmern(h)                  # Geometriepruefung: deckungsgleiche gleichlaufende Flaechen
        return h
    S23.baue_haus = haus
    # Fahnenmast: auf dem First verankert (stand bisher 25 cm ueber der Unterseite des Firsts frei ueber der Deckung)
    alt_dach, alt_fahne = S23.baue_dach, S23.baue_fahne

    def dach(*a, **kw):
        d = alt_dach(*a, **kw)
        S23._zwerg_dach = d
        return d
    S23.baue_dach = dach

    def fahne():
        zt.laeden_zu_pruefen([S23._zwerg_haus, S23._zwerg_dach])   # die Huelle steht (Anbau, Schlot, Vordach): verstellte Laeden weg
        return zt.fahne_verankern(alt_fahne(), S23._zwerg_dach)
    S23.baue_fahne = fahne
    laeden_zu_anlegen(stufe)


def flaechen_entfernen(h, weg):
    """Entfernt Flaechen (Indexmenge) aus einem Teil und verschiebt die Bereiche der Holzakzente (versteinern() liest sie nach Index)
    passend mit."""
    alt = h.flaechen
    neu_index, n = [], 0
    for i in range(len(alt) + 1):
        neu_index.append(n)
        if i < len(alt) and i not in weg: n += 1
    h.flaechen = [fl for i, fl in enumerate(alt) if i not in weg]
    if hasattr(h, "holz_akzente"):
        h.holz_akzente = [(neu_index[a], neu_index[e]) for a, e in h.holz_akzente]


FUGE = 0.009     # Lagerfuge der Eckquader: 9 mm (bei 15 mm sah die Lueckenpruefung durch die Fuge an der Ecke einen Durchblick)


def eckquader_l(h, x_ecke, ys, z_unten, z_oben, ende):
    """Eckquader als Werkstuecke aus hellem Haustein (Band WERKSTEIN), Form wie im Erdgeschoss der Menschen (Laeufer 0,48 x 0,22 und
    Binder 0,22 x 0,48 im Wechsel, Hoehe 0,34 bzw. 0,26 m, 3 cm vor der Mauer; Fuge FUGE), aber jeder Quader als ein L-Prisma ueber die
    Ecke: Stirn, Seite, zwei Enden und Oberseite (die Unterseite liegt auf dem Quader darunter bzw. dem Gesims). So kostet ein Quader
    12 statt 18 Dreiecke – noetig fuer die Grenze von 4.000 mit Eckquadern im Erd- und Obergeschoss."""
    band = S23.WERKSTEIN
    st = "Bruchstein"
    EZ = Vector((0, 0, 1))
    for sx in (-1, 1):
        for y, sy in ys:
            z, i = z_unten, 0
            while z < ende:
                hq = 0.34 if i % 2 == 0 else 0.26
                lx, ly = (0.48, 0.22) if i % 2 == 0 else (0.22, 0.48)
                zb = min(z + hq, z_oben)
                za, ze, hz = z, zb - FUGE, zb - FUGE - z
                xa, xi = sx * (x_ecke + 0.03), sx * (x_ecke - lx)          # aussen, innen entlang x
                yf, yw, yi = y + sy * 0.03, y - sy * 0.01, y - sy * ly     # Stirnflaeche, hinter der Mauerflucht, innen entlang y
                xw = sx * (x_ecke - 0.01)
                kern = Vector((sx * (x_ecke - 0.05), y - sy * 0.05, (za + ze) / 2))
                h.rechteck(st, Vector((min(xa, xi), yf, za)), (1, 0, 0), EZ, abs(xa - xi), hz, band, weg_von=kern)          # Stirn
                h.rechteck(st, Vector((xa, min(yf, yi), za)), (0, 1, 0), EZ, abs(yf - yi), hz, band, weg_von=kern)          # Seite
                h.rechteck(st, Vector((xi, min(yf, yw), za)), (0, 1, 0), EZ, abs(yf - yw), hz, band, zu=kern + Vector((sx * 5, 0, 0)))   # Ende entlang x
                h.rechteck(st, Vector((min(xa, xw), yi, za)), (1, 0, 0), EZ, abs(xa - xw), hz, band, zu=kern + Vector((0, sy * 5, 0)))   # Ende entlang y
                h.rechteck(st, Vector((min(xa, xi), min(yf, yw), ze)), (1, 0, 0), (0, 1, 0), abs(xa - xi), abs(yf - yw), band, zu=kern + EZ * 9)
                h.rechteck(st, Vector((min(xa, xw), min(yw, yi), ze)), (1, 0, 0), (0, 1, 0), abs(xa - xw), abs(yw - yi), band, zu=kern + EZ * 9)
                z = zb
                i += 1


def eckquader_neu(h):
    """Steinhaus der Zwerge (Rueckmeldung des Nutzers 16.11.2026): Eckquader an allen vier Ecken des Erdgeschosses UND des
    Obergeschosses, gleich in Form, Stein und Verzahnung. Die Eckquader der Menschen im Erdgeschoss (zwei Kaesten je Lage) werden durch
    die L-Prismen ersetzt (gleiche Lage und Masse); im Obergeschoss vom Gesims ueber der Auskragung bis unter die Traufe. Giebel und Dach
    bekommen keine (sie laufen im Muster des Erdgeschosses nicht mit)."""
    M = S23.M
    v0, v1, _ = h.baender[S23.WERKSTEIN]
    ecke = lambda pts: (all(abs(abs(p.x) - M.BX) < 0.52 and p.z <= M.Z_EG_DECKE - 0.02 + 1e-3 for p in pts)
                        and all(min(abs(p.y - M.VORN), abs(p.y - M.HINTEN)) < 0.52 for p in pts))
    weg = {i for i, fl in enumerate(h.flaechen) if fl[2] == "Bruchstein" and ecke(fl[0]) and all(v0 - 1e-4 <= uv[1] <= v1 + 1e-4 for uv in fl[1])}
    flaechen_entfernen(h, weg)
    eckquader_l(h, M.BX, ((M.VORN, -1), (M.HINTEN, 1)), 0.0, M.Z_EG_DECKE - 0.02, M.Z_EG_DECKE - 0.3)
    eckquader_l(h, M.OX, ((M.OVORN, -1), (M.HINTEN, 1)), M.Z_EG_DECKE + 0.12, M.Z_TRAUFE - 0.02, M.Z_TRAUFE - 0.2)
    return len(weg)


def messen(stufe, form, ausgabe_ordner=None):
    """Ist-Masse aus der gebauten Szene (Anker, Netze) und den Massen der Mensch-Skripte; Soll gegen Ist: wohnhaus_zwerg_s23_masse.pruefe."""
    M = S23.M
    objs = bpy.data.objects
    z0 = M.Z_BODEN
    anker = {o.name: o.location.copy() for o in objs if o.type == "EMPTY"}
    tuer = next(f for f in M.FENSTER if f[3] == "tuer")
    bruestung = min(f[2][0] for f in M.FENSTER if f[3] == "fenster" and f[2][0] < M.Z_EG_DECKE)

    def oben(name):
        o = objs.get(name)
        return None if o is None else max((o.matrix_world @ Vector(c)).z for c in o.bound_box) - z0
    greif = {n[n.index("_") + 1:]: oben(n) for n in ("Gegenstand_Truhe", "Gegenstand_Familienbuch", "Gegenstand_Bauplan", "Gegenstand_Werkzeug", "Gegenstand_Geld", "Einrichtung_Hausapotheke")}
    greif = {k: v for k, v in greif.items() if v is not None}
    loch = M.TREPPE_LOCH_X[1]
    stufe_hoch = (M.TREPPE_FUSS_X - loch) / M.AUFTRITT * M.STEIGUNG
    ist = dict(tuer_h=tuer[2][1] - tuer[2][0], tuer_b=tuer[1][1] - tuer[1][0], raum_h=M.Z_EG_DECKE - z0, haube_h=g1.Z_HAUBE - z0, sitz=g1.SITZ_HOEHE,
               tisch=g1.TISCH_HOEHE, auge=anker["Blick_Auge"].z - z0, bruestung=bruestung - z0, stufe=M.STUFE_VOR_TUER, greif=greif, galerie_stehbreite={},
               zimmertuer=M.ZIMMERTUER, treppe_steigung=M.STEIGUNG, treppe_kopfhoehe=M.Z_EG_DECKE - (z0 + stufe_hoch), og_raum_h=M.Z_OG_DECKE - M.Z_OG,
               dach_bib_stehbreite=None)
    zeilen = []
    ZM.pruefe(stufe, form, ist, ausgabe=zeilen.append)
    for z in zeilen: print(z)
    if ausgabe_ordner:
        os.makedirs(ausgabe_ordner, exist_ok=True)
        open(os.path.join(ausgabe_ordner, f"soll_ist_s{stufe}_g{form}.txt"), "w", encoding="utf-8").write("\n".join(z.replace("SOLLIST ", "", 1) for z in zeilen) + "\n")


def main():
    args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    opt = dict((a[2:].split("=", 1) + ["1"])[:2] for a in args if a.startswith("--"))
    global FORM
    stufe, FORM = int(opt.get("stufe", "2")), int(opt.get("form", "1"))
    if os.environ.get("REICHSGILDE_ZAEHLEN"):                  # Messung der Dreiecke je Bauteil (dreiecke_zaehler.py; vor schicht_anlegen, damit die ersetzten Teile gezaehlt werden)
        import dreiecke_zaehler
        dreiecke_zaehler.anlegen(S23, sys.modules[__name__])
    schicht_anlegen(stufe)
    S23.main()
    if opt.get("bilder"):
        import bild_zusatz
        bild_zusatz.zeichne(g1, S23.M, opt["bilder"], stufe, FORM if isinstance(FORM, int) else FORM[0], S23.M.FORMEN[FORM if isinstance(FORM, int) else FORM[0]]["dx"], bpy)
    messen(stufe, int(opt.get("form", "1")), opt.get("bilder"))


if __name__ == "__main__":
    main()
