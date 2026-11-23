"""Neubau der Gebaeude: Wohnhaus Elb, Stufe 1 (Gemeiner), Grundformen 1 bis 4 - aussen und innen.

    blender -b --python tools/modelle/neubau/wohnhaus_elb_s1.py -- --form=<1|2|3|4> [--bilder=<ordner>] [--ohne-ao]

Schreibt je Grundform
    game/assets/modelle/gebaeude/neubau/wohnhaus_elb_s1_g<n>.glb         Huelle (wie beim Menschen: Haus, Dach, Scheiben, Wappen, Fahne, Leerstand, Geruest, Anker)
    game/assets/modelle/gebaeude/neubau/wohnhaus_elb_s1_g<n>_innen.glb   Innenraum (im Spiel „InteriorRoot“)
    und seine Zelle in den beiden AO-Atlanten der Gruppe (ao_atlas.json: wohnhaus_elb mit Zellen von 1024 Bildpunkten, wohnhaus_elb_innen).

Die Elbenfassung ist eine Schicht auf den Bauskripten der Menschen wie die der Zwerge (wohnhaus_zwerg_s1.py): gleiche Grundrisse, Waende,
Fenster und Moebel (innen exakt massgleich mit aussen), aber nach dem Kunstwerk „Wohnhaus Elb, Stufe 1“ (wohnhaus_lvl1_elb.jpg,
wohnhaus_in_lvl1_elb.jpg):
  * Stoff trim_elb / innen_trim_elb (trimsheet_elb.py): helles, grau verwittertes Fachwerk, Kalkputz in blassem Gelbgruen,
    grau-olivgruene Holzschindeln, Herd und Schlot aus hellem Haustein (im Spiel K2), innen cremeweisser Putz und Dielen;
  * Schindeldach der Zwerge (zwerg_teile.dach) ohne Schwerstangen und Findlinge (das Kunstwerk zeigt keine), die Ortgangbretter
    schwingen an Traufe und First nach oben aus (elb_teile.ortgang_schwung);
  * gebogene statt gerader Streben in den Eckgefachen und im Giebel (elb_teile.BogenTeil);
  * Elbenmasse (elb_masse.py): Haustuer 2,10 m licht (Mensch 2,00), Herdhaube 1,95 m licht (Mensch 1,80), Blick aus 1,72 m. Tisch
    und Sitz der Menschen (0,74 / 0,44) liegen im Soll fuer einen Elb und bleiben.
Was das Kunstwerk sonst zeigt und hier NICHT gebaut ist: Spitzbogen von Haustuer und Giebelfenster (die Oeffnungen bleiben rechteckig
wie beim Menschen), Efeu und Moos, der Unterstand an der Seite (Grundform 3 hat den Schuppen der Menschen).
"""
import os
import sys

HIER = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HIER)
import bpy                                                         # noqa: E402,F401
import wohnhaus_mensch_s1_g1 as g1                                 # noqa: E402
import wohnhaus_mensch_s1 as s1                                    # noqa: E402
import zwerg_teile as zt                                           # noqa: E402
import elb_teile as et                                             # noqa: E402
from mathutils import Vector                                       # noqa: E402

VOLK = "elb"
ELB_HOEHE = 1.85
TUER_HOCH = 2.10                  # lichte Hoehe der Haustuer: groesster Elb 1,90 m + 0,20 (elb_masse.py); Menschen 2,00
HAUBE_HOEHER = 0.15               # Herdhaube 1,95 m statt 1,80 m ueber dem Fussboden (Elb 1,90 + 0,05)
BLICK_HOEHER = 0.12               # Augenhoehe 1,72 m statt 1,60 m (1,85 * 0,93)
EINFASSUNG_RAND = 0.08            # Rand um den Herdstein: was ganz darin liegt (bis unter die Haube), ist Einfassung aus Stein (Pruefung 31)
FAHNE_VOR_WALM = 0.45             # Grundform 4: Fahnenmast 0,45 m vor der Spitze des Walms (statt 0,45 m dahinter)
LATERNE_G2_Y = -1.00              # Grundform 2: Laterne an der Strassenwand zwischen Haustuer (-0,40..0,40) und Fenster (-2,35..-1,55)
WAPPEN_G4_TIEFER = 0.28           # Grundform 4: Hauswappen innen 0,28 m tiefer (Unterkante 2,75 m), unter dem Galerierand frei (Pruefung 34)
BLICK_G3 = ((-0.45, 0.50), (2.20, -0.30, 1.30))   # Grundform 3: Innenblick von vor der Galerie auf Herd und Haustuer (Pruefung 35). Vorher unter der Galerie
#                                                   (y 1,45): 0,33 m vor der Rueckwand, 4 cm neben einer Balkenseite, 0,17 m neben dem Eckstaender (Pruefung 38,
#                                                   Klicktest NAS 19.11.2026). Jetzt wie bei den Halblingen 1,28 m vor der Rueckwand; weiter vorn verdeckt die
#                                                   hintere Herdwange die Haustuer
_MASSE = []
FORM = 1                          # gebaute Grundform (main)
LETZT = {}                        # zuletzt gebaute Teile (Haus, Dach, Scheiben, Wappen) fuer die Wappenregel
LAEDEN_TEILE = []                 # Teile der Huelle mit geschlossenen Laeden (Zufallslaeden)


def umbenennen(text):
    return text.replace("mensch", VOLK)


# ── Eigene elbische Form (Rueckmeldung des Nutzers 17.11.2026: „Elben eigene Form“): niedrig und breit wie im Kunstwerk, ein Geschoss
#    und Dachgeschoss unter einem steilen, tief heruntergezogenen, geschwungenen Dach. Alle vier Grundformen entstehen mit dem Bauskript
#    der anderthalbgeschossigen Menschenhaeuser (wohnhaus_mensch_s1.py: Erdgeschoss in Fachwerk, Kniestock, Dachgeschoss); jede bleibt
#    im Fussabdruck der Menschen-Grundform desselben Platzes (Pruefung 27). ──
KNIE = 0.45                       # Kniestock (Menschen 0,80): das Dach setzt tief an (Traufe 3,00 m statt 3,35 m)
NEIGUNG_GRAD = 55                 # steiler als bei den Menschen (50 Grad)
KEHL = 1.75                       # Kehlbalken ueber der Traufe: 2,08 m ueber dem Boden des Dachgeschosses (Elb 1,90 + Kopf)
UEBER = {1: 0.29, 2: 0.38, 3: 0.29, 4: 0.38}   # Dachueberstand an der Traufe je Grundform (bis an den Fussabdruck)


def elb_form(n, alt):
    """Grundform n der Elben (Baurahmen wie wohnhaus_mensch_s1): 2 und 4 mit den Grundrissen der Menschen (breite Traufseite zur
    Gasse bzw. Laube unter dem Vordach), 1 und 3 neu: 5,0 x 4,4 m, Giebel zur Gasse, Holzunterstand unter dem tiefen hinteren Dach
    (Kunstwerk); 1 mit der Tuer im Giebel, 3 an der Traufseite (gespiegelt und verschoben wie die Grundform 3 der Menschen)."""
    alt(n if n in (2, 4) else 4)
    F = s1.F
    F.nr, F.name = n, f"wohnhaus_mensch_s1_g{n}"
    s1.UEBER = zt.UEBER = UEBER[n]
    if n in (1, 3):
        F.BX, F.VORN, F.HINTEN = 2.5, -2.4, 2.0
        _q = lambda k: g1.gefach(-F.BX, 2 * F.BX, k)               # Giebelseiten 5,0 m: fuenf Gefache
        _l = lambda k: g1.gefach(F.VORN, F.HINTEN - F.VORN, k)     # Traufseiten 4,4 m: vier Gefache
        F.DACH_VORN, F.DACH_HINTEN = -3.0, 2.58                    # hinten 0,58 m Ueberstand: darunter der Holzunterstand
        F.STRASSE = "vorn_eg"
        F.WELT = (((1, 0), (0, 1)), (0.0, 0.0)) if n == 1 else (((-1, 0), (0, 1)), (1.05, 0.0))
        tuer, eg, gb = (s1.Z_BODEN, s1.Z_BODEN + TUER_HOCH), s1.EG_FENSTER, s1.GB_FENSTER
        if n == 1:
            # die Fenster liegen frei von den Moebeln der Wohnstube (Lage wie Grundform 4: Bauplan, Geld und Werkzeug links vorn, Herd
            # rechts, Hausrat hinten links)
            F.FENSTER = [("vorn_eg", _q(3), tuer, "tuer"), ("vorn_eg", _q(1), eg, "fenster"), ("vorn_gb", _q(2), gb, "fenster"),
                         ("links_eg", _l(3), eg, "fenster"), ("hinten_eg", _q(2), eg, "fenster")]
        else:
            F.FENSTER = [("rechts_eg", _l(3), tuer, "tuer"), ("vorn_eg", _q(1), eg, "fenster"),
                         ("vorn_gb", _q(2), gb, "fenster"), ("hinten_gb", _q(2), gb, "fenster"), ("links_eg", _l(3), eg, "fenster")]
        F.WALM, F.LAUBE = None, False
        F.GALERIE_AB, F.LEITER_X = 0.75, -1.0                      # die Galerie beginnt hinter dem Schlot (kein Holz am Kamin)
        F.KAMIN = (F.BX - s1.T - 0.60, F.BX - s1.T, -0.10, 0.60)
        F.MATTEN = ((-0.6, -1.5, 1.5, 1.6), (-0.4, 1.15, 1.5, 0.6))
        F.FAHNE = (0.0, F.DACH_HINTEN - 0.55)
        F.WAPPEN = ((0.0 if n == 1 else sum(_q(3)) / 2, F.VORN, 1.53), (1, 0, 0), (0, -1, 0))   # Grundform 3: im freien rechten Gefach
        F.BANK = ("vorn_eg", -1.55, -0.35)
        F.FASS = (-2.05, F.VORN - 0.35)                             # Regenfass vor der Giebelwand (an der Seite laege es ausserhalb des Fussabdrucks)
        F.SCHEIT = ("hinten_eg", 1.30)                             # an der Ecke, wo das Dach am tiefsten ist
        F.GERUEST = dict(y=F.DACH_VORN + 0.08, x0=-2.6, x1=2.6, ebenen=(1.9, 3.6), innen=4.2, aussen=4.8, leiter_x=1.75)
    # Der Schlot bleibt an der Stelle der Menschenhaeuser (0,60 m tief an der Wand): Rueckmeldung des Nutzers 17.11.2026 nach Runde 4,
    # die Vertiefung auf 0,98 m war ein Missverstaendnis; gemeint war die Einfassung der Feuerstelle (innen_fertig)
    # Masse aus dem Grundriss (wie am Ende von wohnhaus_mensch_s1.form_setzen, mit den Elben-Hoehen)
    F.OX = F.BX
    F.Z_FIRST = s1.Z_TRAUFE + F.OX * s1.NEIGUNG
    F.IX, F.IV, F.IH = F.BX - s1.T, F.VORN + s1.T, F.HINTEN - s1.T
    F.XK = F.OX - s1.KEHL / s1.NEIGUNG
    F.ZK = s1.Z_TRAUFE + s1.KEHL
    F.MITTE = Vector((0, (F.VORN + F.HINTEN) / 2, 3.0))
    F.RAUM = Vector((0, (F.VORN + F.HINTEN) / 2, 1.6))
    bx, v, h, xk = F.BX, F.VORN, F.HINTEN, F.XK
    F.WAENDE = {
        "vorn_eg": ((-bx, v), (1, 0), 2 * bx, (0, -1)), "hinten_eg": ((-bx, h), (1, 0), 2 * bx, (0, 1)),
        "links_eg": ((-bx, v), (0, 1), h - v, (-1, 0)), "rechts_eg": ((bx, v), (0, 1), h - v, (1, 0)),
        "vorn_og": ((-bx, v), (1, 0), 2 * bx, (0, -1)), "hinten_og": ((-bx, h), (1, 0), 2 * bx, (0, 1)),
        "links_og": ((-bx, v), (0, 1), h - v, (-1, 0)), "rechts_og": ((bx, v), (0, 1), h - v, (1, 0)),
        "vorn_gb": ((-xk, v), (1, 0), 2 * xk, (0, -1)), "hinten_gb": ((-xk, h), (1, 0), 2 * xk, (0, 1)),
    }
    if F.WALM:
        xe = F.OX + s1.UEBER
        F.X_WALM = xe / 3
        F.Z_WALM = F.Z_FIRST - F.X_WALM * s1.NEIGUNG
        F.D_WALM = (F.Z_FIRST - F.Z_WALM) / F.WALM
    if n == 4:
        # Fahne vor die Spitze des Walms (Rueckmeldung des Nutzers 17.11.2026: „die Fahne sitzt direkt am Schlot“ – im Stadtblick nach
        # Nordosten lag das Tuch vor dem Schlot); der Mast steckt im Walm (Pruefung 22), Abstand im Bild Pruefung 36
        F.FAHNE = (0.0, F.DACH_VORN + F.D_WALM - FAHNE_VOR_WALM)


def masse_anlegen():
    """Elbenmasse und -formen in den Modulen der Menschen (auch fuer die Messung, einmal): Haustuer 2,10 m, Herdhaube 1,95 m licht,
    Kniestock, Dachneigung und Kehlbalken der Elben, die Grundformen (elb_form)."""
    import math
    if _MASSE: return
    _MASSE.append(True)
    s1.KNIE, s1.KEHL = KNIE, KEHL
    s1.Z_TRAUFE = s1.Z_OG + KNIE
    s1.NEIGUNG = math.tan(math.radians(NEIGUNG_GRAD))
    s1.GB_FENSTER = (3.50, s1.Z_TRAUFE + KEHL - 0.08)
    alt_form = s1.form_setzen
    s1.form_setzen = lambda n: elb_form(n, alt_form)
    alt_24 = s1.baue_form24

    def form24():
        t = alt_24()
        if s1.F.nr in (1, 3):                          # Hauswappen innen im Dachgeschoss neben dem Giebelfenster (wie Runde 3; Pruefung 32)
            for teil in t["einrichtung"]:
                if teil.name == "Einrichtung_Hauswappen": s1.versetze(teil, ((1, 0), (0, 1)), (1.15, 0.0))
        if s1.F.nr == 4:                               # unter dem Galerierand im Innenblick ganz zu sehen, weiter im Dachgeschoss (Pruefung 32, 34)
            for teil in t["einrichtung"]:
                if teil.name == "Einrichtung_Hauswappen": s1.versetze(teil, dz=-WAPPEN_G4_TIEFER)
        if s1.F.nr == 2:                               # Laterne hing vor dem Tuerblatt (Rueckmeldung 17.11.2026; Pruefung 33)
            laterne_versetzen(t)
        if s1.F.nr == 3:                               # Haustuer im Innenblick (lag neben der Kamera hinter dem Herd)
            for a in t["anker_innen"]:
                if a.name == "Blick_Auge": a.ort = Vector((*BLICK_G3[0], s1.Z_BODEN + 1.60 + BLICK_HOEHER))
                if a.name == "Blick_Ziel": a.ort = Vector((BLICK_G3[1][0], BLICK_G3[1][1], s1.Z_BODEN + BLICK_G3[1][2] + BLICK_HOEHER))
        # Grundlicht unter die Galerie, in der Lage im Spiel (s1.F.WELT, Drehung bzw. Spiegelung + Verschiebung) auf die Hoehe (y) des Blickpunkts
        (a_, b_), (c_, d_) = s1.F.WELT[0]
        tx, ty = s1.F.WELT[1]
        welt = lambda p: Vector((a_ * p.x + b_ * p.y + tx, c_ * p.x + d_ * p.y + ty, p.z))
        zurueck = lambda p: Vector((a_ * (p.x - tx) + c_ * (p.y - ty), b_ * (p.x - tx) + d_ * (p.y - ty), p.z))   # orthogonal: Inverse = Transponierte
        auge = welt(next(a.ort for a in t["anker_innen"] if a.name == "Blick_Auge"))
        for a in t["anker_innen"]:
            if a.name == "Licht_Raum": a.ort = zurueck(licht_unter_empore(welt(a.ort), auge))
        if s1.F.nr == 3: rueckwand_einrichten(t)       # Regal, Kraeuter und Truhe an der Rueckwand unter der Galerie (Nutzer 21.11.2026)
        efeu_setzen(t)
        innen_fertig(t)
        return t
    s1.baue_form24 = s1.baue_form3 = form24            # auch die Grundform 3 entsteht mit dem Zusammenbau von 2 und 4
    for m in (g1, s1):
        m.TUER_HOCH = TUER_HOCH
        m.Z_HAUBE = m.Z_HAUBE + HAUBE_HOEHER
    g1.FENSTER = [(w, a, (z[0], g1.Z_BODEN + TUER_HOCH), art) if art == "tuer" else (w, a, z, art) for w, a, z, art in g1.FENSTER]


LETZT_AUGE = {}
LICHT_UNTER_OG = 0.55             # Grundlicht so weit unter der Unterkante des Obergeschosses: 0,35 m unter den Deckenbalken (2,35 m)


def rueckwand_einrichten(t):
    """Grundform 3: Einrichtung an der Rueckwand unter der Galerie (Nutzer 21.11.2026, wie „Halbling A“: die Wand soll beim Blick nach
    rechts nicht kahl wirken; die Haustuer bleibt im Standardbild). Alles im Teil Hausrat_Set, im Baurahmen (in der Lage im Spiel
    gespiegelt: x im Spiel = 1,05 - x): links vom Platz auf der Matte ein Standregal (0,85 m breit, 1,70 m hoch) mit Kruegen, Flaschen
    und einem Korb, rechts eine Truhe; darueber an einer Stange Kraeuterbuendel (Unterkante 1,96 m – frei ueber dem Kopf eines Elben,
    bis 1,90 m). Der Platz vor der Matte bleibt fuer eine Figur frei."""
    c = next((x for x in t["raum"] if x.name == "Hausrat_Set"), None)
    if c is None: return
    S, feld = "Innen", g1.feld
    yw, zb = s1.F.HINTEN - s1.T, s1.Z_BODEN
    # Standregal: zwei Wangen, vier Boeden
    x0, x1, tief = 0.65, 1.50, 0.28
    for xa in (x0, x1 - 0.03):
        c.kasten(S, (xa, yw - tief, zb), (xa + 0.03, yw, zb + 1.70), "bohlen", ohne=("+y", "-z"), laengs="z")
    for z in (0.22, 0.68, 1.14, 1.60):
        c.kasten(S, (x0 + 0.03, yw - tief + 0.01, zb + z), (x1 - 0.03, yw, zb + z + 0.035), "bohlen", ohne=("+y", "-x", "+x"), laengs="x")
    krug = lambda x, z, g_: c.dreh(S, (x, yw - 0.14, zb + z + 0.035), [(0.06 * g_, 0.0), (0.095 * g_, 0.05 * g_), (0.105 * g_, 0.13 * g_), (0.07 * g_, 0.21 * g_), (0.062 * g_, 0.24 * g_), (0.072 * g_, 0.26 * g_)],
                                   "kleinteile", seiten=8, deckel=False, u_bereich=feld("ton"))
    flasche = lambda x, z, farbe: c.dreh(S, (x, yw - 0.13, zb + z + 0.035), [(0.045, 0.0), (0.05, 0.10), (0.02, 0.17), (0.018, 0.25), (0.024, 0.27)], "kleinteile", seiten=6, deckel=True, u_bereich=feld(farbe))
    krug(0.86, 0.68, 1.15); krug(1.22, 0.68, 0.9)
    flasche(0.80, 1.14, "kraut"); flasche(0.97, 1.14, "zwiebel_gelb"); flasche(1.14, 1.14, "kraut"); flasche(1.32, 1.14, "zwiebel_rot")
    krug(1.08, 1.60, 1.0)
    c.dreh(S, (1.08, yw - 0.14, zb + 0.255), [(0.13, 0.0), (0.17, 0.10), (0.18, 0.22), (0.165, 0.24)], "stroh", seiten=8, deckel=False, u_bereich=(0.0, 0.3))   # Korb
    # Kraeuterbuendel an einer Stange
    c.stab(S, (0.55, yw - 0.06, zb + 2.27), (-0.85, yw - 0.06, zb + 2.27), 0.03, "balken", kappen=True)
    for i, x in enumerate((0.42, 0.18, -0.07, -0.33, -0.58, -0.78)):
        c.dreh(S, (x, yw - 0.07, zb + 1.96 + 0.02 * (i % 2)), [(0.0, 0.0), (0.06, 0.05), (0.036, 0.21), (0.009, 0.30)], "kleinteile", seiten=5, deckel=False, u_bereich=feld("kraut"))
    # Truhe rechts neben der Matte
    c.kasten(S, (-0.87, yw - 0.47, zb), (-0.25, yw - 0.04, zb + 0.40), "bohlen", ohne=("-z",), laengs="x")
    c.kasten(S, (-0.89, yw - 0.49, zb + 0.40), (-0.23, yw - 0.02, zb + 0.47), "balken", laengs="x")
    for x in (-0.76, -0.36):
        c.kasten(S, (x - 0.02, yw - 0.495, zb + 0.02), (x + 0.02, yw - 0.47, zb + 0.475), "balken", ohne=("+y", "-z"), laengs="z")
    print("INNEN Rueckwand eingerichtet: Standregal, Kruege, Flaschen, Korb, Kraeuter, Truhe")


EFEU_HOECHSTENS = 120            # Blattkarten je Haus (je 2 Dreiecke), wenn die Dreiecksgrenze der Huelle (4.000) mehr zuliesse


# Karten je Grundform, wie sie die fruehere Dreiecksgrenze (4.000) ergab (Stand main 22.11.2026); die Grenze ist jetzt 8.000,
# der Efeu soll aber nicht mehr werden
EFEU_KARTEN = {1: 120, 2: 102, 3: 120, 4: 120}


def efeu_setzen(t):
    """Efeu als Blattkarten an allen vier Aussenwaenden, auch ueber dem Fachwerkholz (Nutzer 21.11.2026: der Efeu im Putzband ist im
    Pixel-Look nicht zu sehen). efeu_karten.bewachsen liest aus der Huelle, wo eine Karte sitzen darf; Oberkante unter der Traufe bzw.
    unter dem Ortgang. So viele Karten, wie die Dreiecksgrenze laesst (30 Dreiecke Abstand), hoechstens EFEU_HOECHSTENS."""
    import efeu_karten
    efeu_karten.stoff_anlegen(g1, "trim")
    F = s1.F
    haus = t["huelle"][0]
    summe = sum(x.dreiecke() for x in t["huelle"]) + t["scheiben"].dreiecke()
    budget = EFEU_KARTEN[F.nr]                                      # fest: so viele wie bisher (der Efeu bleibt, wie entschieden)
    ebenen, gesehen = [], set()
    for (ox, oy), (ex, ey), laenge, (nx, ny) in F.WAENDE.values():
        kenn = (round(ox * nx + oy * ny, 2), round(nx), round(ny))
        if kenn in gesehen: continue
        gesehen.add(kenn); ebenen.append(((ox, oy), (ex, ey), laenge, (nx, ny)))

    def z_grenze(nr, s_):
        (ox, oy), (ex, ey), _l, _n = ebenen[nr]
        if abs(ey) > 0.5: return s1.Z_TRAUFE - 0.25                 # Traufwand
        return s1.dach_z(ox + ex * s_) - 0.35                       # Giebelwand: unter dem Ortgang
    n = efeu_karten.bewachsen(haus, t["huelle"] + [t["scheiben"], t["zusatz"][0]], ebenen, budget, 41 + F.nr, z_grenze)
    print(f"EFEU Karten: {n} (Budget {budget}, Huelle vorher {summe} Dreiecke)")


def licht_unter_empore(licht, auge):
    """Grundlicht der Wohnstube (Licht_Raum, OmniLight ohne Schatten) unter die Schlafgalerie und auf die Hoehe (y) des Blickpunkts
    (12c mit Unterseiten, 18.11.2026): in 3,4–3,6 m stand es ueber der Galerie (Unterkante 2,55 m) und den Deckenbalken (2,35 m) –
    deren Unterseiten und die Flaechen zur Kamera bekamen kein direktes Licht, im hellen Holz der Elben fast schwarz. In x bleibt es."""
    return Vector((licht.x, auge.y, min(licht.z, g1.Z_OG - LICHT_UNTER_OG)))


def elb_dach(g, yw0, yw1):
    """Schindeldach der Zwerge ohne Schwerstangen und Findlinge, mit aufgeschwungenen Ortgangbrettern und hochgezogenen Ecken
    (elb_teile.dach_schwingen; yw0, yw1: Giebelwaende, ueber denen das Dach unveraendert bleibt)."""
    alt = zt.dachsteine
    zt.dachsteine = lambda *a, **kw: None
    try:
        d = zt.dach(g)
    finally:
        zt.dachsteine = alt
    et.ortgang_schwung(d, g, g.OX + zt.UEBER, 0.07, 0.015, g.walm)
    et.dach_schwingen(d, g, yw0, yw1, g.OX + zt.UEBER)
    return d


def schicht_anlegen():
    """Stoff, Dateinamen, Atlasgruppen und Bauteile auf den Elb umstellen; die Skripte der Menschen bleiben unveraendert."""
    masse_anlegen()
    g1.FACHWERK_ECHT = True                                        # echtes Fachwerk (Holzlage, Nutzer 23.11.2026), die Streben gebogen
    et.bogen_in_holzlage(stueck_hoch=0.30)
    alt_stoff, alt_uv = g1.stoff, g1.uv2_und_ao
    g1.stoff = lambda name, blatt=None, **kw: alt_stoff(name, (blatt + "_" + VOLK) if blatt else None, **kw)
    g1.uv2_und_ao = lambda gruppen, gruppe, modell, mit_ao=True: alt_uv(gruppen, umbenennen(gruppe), umbenennen(modell), mit_ao)
    g1.NAME = umbenennen(g1.NAME)
    alt_bild = g1.kontrollbild

    def kontrollbild(*a, **kw):                                    # Blender 4.5 mit deutscher Oberflaeche: Knotennamen der Welt
        welt = bpy.context.scene.world
        if welt is not None:
            welt.use_nodes = True
            for knoten in welt.node_tree.nodes:
                if knoten.type == "BACKGROUND": knoten.name = "Background"
        datei = a[0]
        if "innen_blick" in os.path.basename(datei) and not os.path.basename(datei).startswith("g"):   # Grundform 1: Blick aus Elbenaugenhoehe
            a = (a[0], (a[1][0], a[1][1], a[1][2] + BLICK_HOEHER), (a[2][0], a[2][1], a[2][2] + BLICK_HOEHER)) + tuple(a[3:])
        figuren_setzen()
        return alt_bild(*a, **kw)
    g1.kontrollbild = kontrollbild
    alt_g1_anker, alt_init = g1.anker, s1.Anker.__init__

    def anker_ort(name, ort):
        ort = Vector(ort)
        if name in ("Blick_Auge", "Blick_Ziel"): ort.z += BLICK_HOEHER
        return ort

    def g1_anker_ort(name, ort):                 # Grundform 1 (Lage im Spiel = Baurahmen); 2 bis 4: form24
        ort = anker_ort(name, ort)
        if name == "Blick_Auge": LETZT_AUGE["o"] = ort.copy()
        if name == "Licht_Raum" and "o" in LETZT_AUGE: ort = licht_unter_empore(ort, LETZT_AUGE["o"])
        return ort
    g1.anker = lambda name, ort, blick=None, **kw: alt_g1_anker(name, g1_anker_ort(name, ort), blick, **kw)

    def anker_init(self, name, ort, blick=None, **extras):
        alt_init(self, name, anker_ort(name, ort), blick, **extras)
    s1.Anker.__init__ = anker_init
    # ── Dach: Schindeln, Ortgang aufgeschwungen ──
    g1.baue_dach = lambda: merke("dach", elb_dach(zt.DachMass(1, g1.OX, g1.DACH_VORN, g1.DACH_HINTEN, g1.Z_FIRST, g1.MITTE, g1.dach_z, g1.NEIGUNG), g1.OVORN, g1.HINTEN))

    def dach_24():
        F = s1.F
        walm = dict(walm=F.WALM, X_WALM=F.X_WALM, Z_WALM=F.Z_WALM, D_WALM=F.D_WALM) if F.WALM else {}
        return merke("dach", elb_dach(zt.DachMass(F.nr, F.OX, F.DACH_VORN, F.DACH_HINTEN, F.Z_FIRST, F.MITTE, s1.dach_z, s1.NEIGUNG, **walm), F.VORN, F.HINTEN))
    s1.baue_dach = dach_24
    # ── Fachwerk: gebogene Streben (Eckgefache, Giebel) ──
    alt_wand, alt_giebel = g1.fachwerk_wand, g1.giebel_fachwerk
    g1.fachwerk_wand = lambda h, *a, **kw: alt_wand(et.BogenTeil(h), *a, **kw)
    g1.giebel_fachwerk = lambda h, *a, **kw: alt_giebel(et.BogenTeil(h), *a, **kw)
    # ── fuer die Wappenregel: Haus, Scheiben und Wappen merken ──
    for modul in (g1, s1):
        for name, art in (("baue_haus", "haus"), ("baue_scheiben", "scheiben"), ("baue_wappen", "wappen")):
            alt = getattr(modul, name, None)
            if alt is None: continue
            setattr(modul, name, (lambda alt_, art_: lambda *a, **kw: merke(art_, alt_(*a, **kw)))(alt, art))
    # ── Spitzbogen aussen an Haustuer und Giebeloeffnung, Holzunterstand (Kunstwerk; Rueckmeldung des Nutzers 17.11.2026) ──
    alt_haus24 = s1.baue_haus

    def haus24():
        h = alt_haus24()
        F = s1.F
        for wand, a, z, art in F.FENSTER:
            if art != "tuer" and not wand.endswith("_gb"): continue
            (ox, oy), (ex, ey), _, n = F.WAENDE[wand]
            start = ox if ex else oy
            et.spitzbogen(h, lambda s, zz, w=wand: s1.punkt(w, s, zz), (n[0], n[1], 0), a[0] - start, a[1] - start, z[1])
        # Holzunterstand: Pfosten und Pfette unter dem tiefen Dach ueber dem Scheitholz (Kunstwerk), im Fussabdruck
        walm = dict(walm=F.WALM, X_WALM=F.X_WALM, Z_WALM=F.Z_WALM, D_WALM=F.D_WALM) if F.WALM else {}
        g = zt.DachMass(F.nr, F.OX, F.DACH_VORN, F.DACH_HINTEN, F.Z_FIRST, F.MITTE, s1.dach_z, s1.NEIGUNG, **walm)
        xe = F.OX + zt.UEBER
        unter = lambda x, y: s1.dach_z(abs(x)) + et.dach_heben(x, y, g, F.VORN, F.HINTEN, xe, bool(F.WALM) and y < F.VORN)
        wand, a = F.SCHEIT
        (ox, oy), (ex, ey), _, (nx, ny) = F.WAENDE[wand]
        if ex:                                                     # Giebelwand (Grundform 1, 3): Pfosten 0,55 m vor der Wand
            y = oy + ny * 0.55
            et.traufstuetzen(h, [(a - 0.06, y), (a + 0.78, y)], unter)
        else:                                                      # Traufwand (2, 4): Pfosten unter der Traufe
            x = ox + nx * (zt.UEBER - 0.08)
            et.traufstuetzen(h, [(x, a - 0.06), (x, a + 0.78)], unter)
        return h
    s1.baue_haus = haus24
    alt_versetze = s1.versetze_alle

    def versetze_alle(dinge, m, t, dz=0.0):
        r = alt_versetze(dinge, m, t, dz)
        namen = {getattr(d, "name", None): d for d in dinge if not isinstance(d, s1.Anker)}
        if "Wappen" in namen:                                       # die Huelle in der Lage im Spiel (Grundformen 2 bis 4)
            for art, name in (("haus", "Haus"), ("dach", "Dach"), ("scheiben", "Scheiben"), ("wappen", "Wappen")):
                if name in namen: LETZT[art] = namen[name]
        return r
    s1.versetze_alle = versetze_alle
    # ── Wappenregel (wappen_ort, 5 cm frei) und Fahnenmast im Dach verankert (Pruefung 22), beides vor dem Mast ──
    alt_fahne1, alt_fahne24 = g1.baue_fahne, s1.baue_fahne

    def wappen_regel():
        g1.wappen_frei_ruecken(LETZT["wappen"], [LETZT[k] for k in ("haus", "dach", "scheiben")])   # Feinschliff der Menschen (#504); nach dem Dach der Elben noch einmal

    def laeden_pruefen():                                           # die Huelle steht (Unterstand, Schlot): verstellte Laeden weg
        zt.laeden_zu_pruefen([et.ohne_efeu(t) for t in LAEDEN_TEILE] + [zt.LETZTES_DACH])   # Efeu darf vor einem Laden haengen
    # schon vor der Wappenregel der Menschen (main: wappen_frei_ruecken vor der Fahne): sonst rueckte das Wappen einem Laden aus dem Weg,
    # der danach wegfaellt, und sass dann auf Holz und Efeu (Pruefung 19, Grundform 3)
    alt_ruecken = g1.wappen_frei_ruecken
    g1.wappen_frei_ruecken = lambda w, teile, *a, **kw: (laeden_pruefen(), et.laeden_am_wappen_weg(w), alt_ruecken(w, teile, *a, **kw))[2]
    g1.baue_fahne = lambda: (laeden_pruefen(), wappen_regel(), zt.fahne_verankern(alt_fahne1(), zt.LETZTES_DACH))[2]
    s1.baue_fahne = lambda x, y, z: (laeden_pruefen(), wappen_regel(), zt.fahne_verankern(alt_fahne24(x, y, z), zt.LETZTES_DACH))[2]
    # Geschlossene Laeden im festen Zufall (zwerg_teile.laeden_zu, Verfahren der Zeilenmodule): auf freien Gefachen des Fachwerks
    alt_lage = g1.fachwerk_wand_lage

    def fachwerk_wand_lage(h, P, breite, n3, z0, z1, oeffnungen, n=None, raehm=True, riegel=True, z_fenster=g1.OG_FENSTER, stoss=None):
        alt_lage(h, P, breite, n3, z0, z1, oeffnungen, n, raehm, riegel, z_fenster, stoss)
        if not riegel: return
        import zeile_module as Z
        h = getattr(h, "_teil", h)                               # das Teil selbst, nicht der Stellvertreter BogenTeil (Laeden fallen spaeter weg)
        if h not in LAEDEN_TEILE: LAEDEN_TEILE.append(h)
        fen = [o for o in oeffnungen if o[4] == "fenster"]
        t0, t1 = (min(f[2] for f in fen), max(f[3] for f in fen)) if fen else z_fenster
        p0, p1 = Vector(P(0.0, 0.0)), Vector(P(1.0, 0.0))
        felder = zt.felder_der_lage(g1.LAGEN[-1][4], breite, oeffnungen, t0, t1)
        schluessel = f"wohnhaus_elb_s1_g{FORM}|{p0.x:.2f},{p0.y:.2f}|{n3[0]:.0f},{n3[1]:.0f}|{z0:.2f}"
        zt.laeden_zu(h, p0, p1 - p0, n3, felder, t0 + 0.04, t1 - 0.04, schluessel, Z.LADEN_OG)
    g1.fachwerk_wand_lage = fachwerk_wand_lage
    alt_form = s1.form_setzen

    def form_setzen(n):
        alt_form(n)
        s1.F.name = umbenennen(s1.F.name)
    s1.form_setzen = form_setzen


def innen_fertig(t):
    """Wohnstube nach der Rueckmeldung des Nutzers (17.11.2026, Bild innen): (1) Dachschraegen innen geschlossen – je Dachseite eine
    verputzte Flaeche 5 mm unter dem Dach, ueber den Sparren (vorher sah man zwischen den Sparren ins Leere); (4) Kamin und Schlot ganz
    aus Stein: Holz, das in den Schlot laeuft (Sparren, Balken), faellt dort weg, Holzteile des Herds im Grundriss des Schlots werden
    Stein, ebenso die ganze Einfassung der Feuerstelle (Sturz, Seiten). Der Schlot steht ueber der Feuerstelle (Pruefung 30)."""
    from trimsheet import BAENDER_INNEN
    F = s1.F
    raum = next(x for x in t["raum"] if x.name == "Raum")
    IX, IV, IH = F.IX, F.IV, F.IH
    zw = s1.dach_z(IX) - 0.005
    zf = F.Z_FIRST - 0.005
    unten = F.RAUM + Vector((0, 0, 1.0))
    for sx in (-1, 1):
        raum.welt("Innen", [(sx * IX, IV, zw), (sx * IX, IH, zw), (0, IH, zf), (0, IV, zf)], "lehmwand", (-IX, IV, 0), (0, 1, 0), zw, zf - zw, zu=unten)
    kx0, kx1, ky0, ky1 = F.KAMIN
    holz = [BAENDER_INNEN[b] for b in ("bohlen", "balken")]
    stein = BAENDER_INNEN["bruchstein"]
    im_schlot = lambda p, r=0.01: kx0 - r < p.x < kx1 + r and ky0 - r < p.y < ky1 + r

    def ist_holz(uv):
        return any(all(v0 - 1e-4 <= v <= v1 + 1e-4 for _, v in uv) for v0, v1, _ in holz)
    weg = 0
    neu = []
    for f in raum.flaechen:
        pts, uv = f[0], f[1]
        if ist_holz(uv) and max(p.z for p in pts) > s1.Z_HAUBE and any(im_schlot(p) for p in pts):
            weg += 1
            continue
        neu.append(f)
    raum.flaechen = neu
    # Einfassung der Feuerstelle (Rueckmeldung nach Runde 4: links Stein, Sturz und rechte Seite Holz): alle Holzflaechen des Herds und
    # des Raums im Grundriss des Herdsteins (bis unter die Haube) werden Stein
    herdstein = [p for x in t["raum"] if x.name.startswith("Herd") for pts, uv, *_ in x.flaechen
                 if all(stein[0] - 1e-4 <= v <= stein[1] + 1e-4 for _, v in uv) for p in pts]
    # 8 cm Rand (Rueckmeldung 17.11.2026 nach Runde 5): der Balken unter der Haube (Sturz) steht 4 cm ueber den Herdstein vor und
    # blieb mit 3 cm Holz
    hx0, hx1 = min(p.x for p in herdstein) - EINFASSUNG_RAND, max(p.x for p in herdstein) + EINFASSUNG_RAND
    hy0, hy1 = min(p.y for p in herdstein) - EINFASSUNG_RAND, max(p.y for p in herdstein) + EINFASSUNG_RAND
    in_herd = lambda p: hx0 <= p.x <= hx1 and hy0 <= p.y <= hy1 and p.z <= s1.Z_HAUBE + 0.35
    stein_neu = 0
    for x in t["raum"]:
        if not (x.name.startswith("Herd") or x.name == "Raum"): continue
        for i, (pts, uv, st, fa, gl) in enumerate(x.flaechen):
            if ist_holz(uv) and (all(in_herd(p) for p in pts) or (x.name.startswith("Herd") and all(im_schlot(p, 0.06) for p in pts))):
                v0, v1 = next((a, b) for a, b, _ in holz if all(a - 1e-4 <= v <= b + 1e-4 for _, v in uv))
                uv = [(u, stein[0] + (v - v0) / (v1 - v0) * (stein[1] - stein[0])) for u, v in uv]
                x.flaechen[i] = (pts, uv, st, fa, gl); stein_neu += 1
    print(f"INNEN Dachschraegen geschlossen, Holz am Schlot: {weg} Flaechen weg, {stein_neu} Flaechen des Herds zu Stein")


def laterne_versetzen(t):
    """Grundform 2: Die Laterne des Hauswaechters (Probe: (-1,66, IV + 0,13), dort versetzt wie in wohnhaus_mensch_s1.baue_form24)
    hing an der Strassenwand bei y = -0,13 – mitten vor dem Tuerblatt. Sie rueckt an derselben Wand nach LATERNE_G2_Y, zwischen Tuer
    und Fenster. Ausgewaehlt wie in teile_auf: Schwerpunkt der Flaeche hoechstens 12 cm von der Laterne."""
    F = s1.F
    lx, ly = (s1.G1_IV + 0.11) + (-F.IX - s1.G1_IV), 1.66 - 1.79      # Lage nach der Drehung der Grundform 2 (Baurahmen)
    w = next(x for x in t["einrichtung"] if x.name == "Einrichtung_Hauswaechter")
    neu, n = [], 0
    for pts, uv, st, fa, gl in w.flaechen:
        m = sum(pts, Vector()) / len(pts)
        if (m.x - lx) ** 2 + (m.y - ly) ** 2 < 0.12 ** 2 and m.z > 1.3:
            pts = [Vector((p.x, p.y + LATERNE_G2_Y - ly, p.z)) for p in pts]; n += 1
        neu.append((pts, uv, st, fa, gl))
    w.flaechen = neu
    print(f"INNEN Laterne: {n} Flaechen von y {ly:.2f} nach {LATERNE_G2_Y:.2f}")


def merke(art, teil):
    LETZT[art] = teil
    return teil


_FIGUREN = []


def figuren_setzen():
    """Legt einmal die Elbenmessfiguren (1,85 m, gruen) in die Szene: in der Haustuer und am Tisch. Nur fuer die Kontrollbilder."""
    if _FIGUREN: return
    anker = {o.name: o.location.copy() for o in bpy.data.objects if o.type == "EMPTY"}
    if "Anker_Tuer" not in anker: return
    tuer = anker["Anker_Tuer"]
    orte = [(tuer.x, tuer.y + 0.05, g1.Z_BODEN)]
    if "Platz_Partner" in anker: orte.append((anker["Platz_Partner"].x, anker["Platz_Partner"].y, g1.Z_BODEN))
    fig = et.massfigur_elb(orte, ELB_HOEHE)
    _FIGUREN.append(fig.objekt({"MassfigurElb": g1.stoff("MassfigurElb", farbe=(0.20, 0.62, 0.30))}, mit_farbe=False))


def messen(form, ausgabe_ordner=None):
    """Ist-Masse aus der gebauten Szene und den Massen der Bauskripte; Soll gegen Ist: elb_masse.pruefe."""
    import elb_masse
    z0 = g1.Z_BODEN
    fenster = s1.F.FENSTER
    tuer = next(f for f in fenster if f[3] == "tuer")
    anker = {o.name: o.location.copy() for o in bpy.data.objects if o.type == "EMPTY"}
    ist = dict(tuer_h=tuer[2][1] - tuer[2][0], tuer_b=tuer[1][1] - tuer[1][0], raum_h=g1.Z_OG - z0, haube_h=s1.Z_HAUBE - z0,
               dg_h=s1.Z_TRAUFE + s1.KEHL - s1.Z_GALERIE,
               dg_breite=lambda h: max(0.0, 2 * min(s1.F.OX - s1.T, (s1.F.Z_FIRST - s1.Z_GALERIE - h) / s1.NEIGUNG)), sitz=g1.SITZ_HOEHE, tisch=g1.TISCH_HOEHE, auge=anker["Blick_Auge"].z - z0, stufe=z0)
    zeilen = []
    _z, fehler = elb_masse.pruefe(form, ist, ausgabe=zeilen.append)
    for z in zeilen: print(z)
    print(f"SOLLIST Fehler: {fehler}")
    if ausgabe_ordner:
        os.makedirs(ausgabe_ordner, exist_ok=True)
        open(os.path.join(ausgabe_ordner, f"soll_ist_g{form}.txt"), "w", encoding="utf-8").write("\n".join(z.replace("SOLLIST ", "", 1) for z in zeilen) + "\n")


def main():
    args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    opt = dict((a[2:].split("=", 1) + ["1"])[:2] for a in args if a.startswith("--"))
    form = int(opt.get("form", "1"))
    global FORM
    FORM = form
    schicht_anlegen()
    s1.main()                                                       # alle vier Grundformen der Elben (elb_form)
    messen(form, opt.get("bilder"))


if __name__ == "__main__":
    main()
