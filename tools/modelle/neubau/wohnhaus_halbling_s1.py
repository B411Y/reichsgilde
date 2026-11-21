"""Neubau der Gebaeude: Wohnhaus Halbling, Stufe 1 (Gemeiner), Grundformen 1 bis 4 - aussen und innen.

    blender -b --python tools/modelle/neubau/wohnhaus_halbling_s1.py -- --form=<1|2|3|4> [--bilder=<ordner>] [--ohne-ao]

Schreibt je Grundform
    game/assets/modelle/gebaeude/neubau/wohnhaus_halbling_s1_g<n>.glb         Huelle (wie beim Menschen: Haus, Dach, Scheiben, Wappen, Fahne, Leerstand, Geruest, Anker)
    game/assets/modelle/gebaeude/neubau/wohnhaus_halbling_s1_g<n>_innen.glb   Innenraum (im Spiel „InteriorRoot“)
    und seine Zelle in den beiden AO-Atlanten der Gruppe (ao_atlas.json: wohnhaus_halbling mit Zellen von 1024 Bildpunkten, wohnhaus_halbling_innen).

Die Halblingsfassung ist eine Schicht auf den Bauskripten der Menschen wie die der Zwerge und Elben (wohnhaus_zwerg_s1.py,
wohnhaus_elb_s1.py): gleiche Bauweise, Waende, Fenster und Moebel (innen exakt massgleich mit aussen), aber eine eigene Form nach dem
Kunstwerk „Wohnhaus Halbling, Stufe 1“ (wohnhaus_lvl1_halbling.jpg, wohnhaus_in_lvl1_halbling.jpg):
  * anderthalbgeschossiges Fachwerkhaus unter einem dicken Strohdach, das am Giebel als Wulst ueber die Kante laeuft (keine Windbretter,
    darunter Pfettenkoepfe); an einer Traufseite ein Zwerchhaus mit eigenem Strohdach und verbretterter Front (halbling_teile.py);
  * Haustuer mit Rundbogen (nur aussen), im Giebel ueber dem Kehlbalken ein Ochsenauge;
  * Grundformen 1 und 3 neu: fast quadratisch 4,4 x 4,4 m, Giebel mit der Haustuer zur Gasse (Kunstwerk); 2 und 4 auf den Grundrissen
    der Menschen; jede im Fussabdruck der Menschen-Grundform desselben Platzes (Pruefung 27);
  * Stoff trim_halbling / innen_trim_halbling (trimsheet_halbling.py): goldgelbes Stroh, warmes Eichenfachwerk, ockergelber Lehmputz,
    innen Dielenboden; Sockel, Herd und Schlot aus Stein (im Spiel K2);
  * Halblingsmasse (halbling_masse.py): Haustuer, Decken, Herdhaube und Dachgeschoss wie bei den Menschen (es passen Halbling, Zwerg und
    Mensch; das Kunstwerk zeigt ein Haus in Menschengroesse), Tisch, Sitz, Blick und Greifhoehe fuer den Halbling (1,10 m) wie bei den
    Zwergen.
"""
import os
import sys

HIER = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HIER)
import bpy                                                         # noqa: E402,F401
import wohnhaus_mensch_s1_g1 as g1                                 # noqa: E402
import wohnhaus_mensch_s1 as s1                                    # noqa: E402
import halbling_teile as ht                                        # noqa: E402
from mathutils import Vector                                       # noqa: E402

VOLK = "halbling"
HALBLING_HOEHE = 1.10
HALBLING_TISCH, HALBLING_SITZ = 0.56, 0.30      # Menschen 0,74 / 0,44; Soll Halbling 0,52-0,61 / 0,28-0,31 (halbling_masse.py)
BLICK_TIEFER = 0.58                             # Augenhoehe 1,02 m statt 1,60 m (1,10 * 0,93)
APOTHEKE_TIEFER, BAUPLAN_TIEFER = 0.42, 0.28    # Greifhoehe 1,10 * 1,25 = 1,38 m: Oberkante 1,77 -> 1,35 m und 1,65 -> 1,37 m
LATERNE_G2_Y = -1.00                            # Grundform 2: Laterne an der Strassenwand zwischen Haustuer (-0,40..0,40) und Fenster (wie bei den Elben)
BLICK_G3 = ((-0.45, 0.55), (2.02, -0.30, 1.30))  # Grundform 3: Innenblick von vor der Galerie auf Herd und Haustuer (Pruefung 35). Vorher stand das Auge
#                                                  unter dem vorderen Galeriebalken 1,0 m vor der Rueckwand (y 0,80; davor y 1,45): beim Blick nach rechts
#                                                  fuellte sie das halbe Bild (Klicktest NAS 19.11.2026). Jetzt 1,23 m vor ihr. Weiter vorn geht es nicht:
#                                                  ab etwa y 0,4 verdeckt die hintere Herdwange die Haustuer (35 verlangt die Mitte und sechs von neun
#                                                  Strahlen frei); die Tuer liegt 36 Grad rechts der Bildmitte (Grenze 45); y 0,55 liegt zwischen zwei Sparren
#                                                  (bei 0,50 streifte das Licht ueber dem Auge die Seite des Sparrens bei y 0,41 nur: 12c)
LICHT_RAUM = {1: (0.17, 2.0), 2: (0.06, None), 3: (0.0, 1.5), 4: (0.06, None)}   # Grundlicht: Versatz vom Blickpunkt zum Ziel (m), Hoehe (None: wie bei den Menschen).
#                                                  Grundformen 1 und 3 (Feinschliff 20.11.2026): unter der Galerie (in 3,4 bzw. 2,6 m stand es ueber ihr:
#                                                  Unterseite ohne Licht, Balkenseiten nur gestreift). Grundform 1: 0,35 m unter den Balken wie bei Menschen
#                                                  und Elben, mitten zwischen vorderem Balken und Wandbalken (galerie_balken). Grundform 3: ueber dem Auge
#                                                  in 1,5 m – so bekommen auch die Rueckseiten der Leiterholme Licht, die schraeg nach unten zeigen (12c)
FAHNE_VOR_WALM = 0.45                           # Grundform 4: Fahnenmast 0,45 m vor der Spitze des Walms (Pruefung 36)
WULST_RUECK = 0.10                              # Grundformen 2 und 4: Ortgang um so viel zurueck, damit der Wulst im Fussabdruck bleibt (Pruefung 27)
EINFASSUNG_RAND = 0.08                         # Rand um den Herdstein: was ganz darin liegt (bis unter die Haube), ist Einfassung aus Stein (Pruefung 31)
_MASSE = []
FORM = 1
LETZT = {}

# ── Eigene Form der Halblinge (Kunstwerk): anderthalb Geschosse, der Kniestock niedriger als bei den Menschen (0,60 statt 0,80), das
#    Dach so steil wie bei ihnen; Kehlbalken so hoch, dass im Dachgeschoss auch ein Mensch aufrecht steht. ──
KNIE = 0.60
NEIGUNG_GRAD = 55                               # steil wie im Kunstwerk (Menschen 50 Grad); das Giebelfenster bleibt frei vom Ortgang
KEHL = 1.45                                     # Kehlbalken 1,93 m ueber dem Boden des Dachgeschosses (Mensch 1,75 + 0,10 + Reserve)
UEBER = {1: 0.24, 2: 0.32, 3: 0.24, 4: 0.38}   # Grundformen 1 und 3 (5,0 m breit) bis an den Fussabdruck    # Dachueberstand an der Traufe je Grundform (bis an den Fussabdruck)
ZWERCH_W = 0.80                                 # halbe Breite des Zwerchhauses


def umbenennen(text):
    return text.replace("mensch", VOLK)


def halbling_form(n, alt):
    """Grundform n der Halblinge (Baurahmen wie wohnhaus_mensch_s1): 2 und 4 mit den Grundrissen der Menschen, 1 und 3 neu: 4,4 x 4,4 m,
    Giebel zur Gasse; 1 mit der Haustuer im Giebel (rechts der Mitte wie im Kunstwerk), 3 an der Traufseite (gespiegelt und verschoben
    wie die Grundform 3 der Menschen). Dazu Lage des Zwerchhauses (F.ZWERCH) und des Ochsenauges (F.OCHSENAUGE)."""
    alt(n if n in (2, 4) else 4)
    F = s1.F
    F.nr, F.name = n, f"wohnhaus_mensch_s1_g{n}"
    s1.UEBER = UEBER[n]
    if n in (1, 3):
        F.BX, F.VORN, F.HINTEN = 2.5, -2.4, 2.0
        _q = lambda k: g1.gefach(-F.BX, 2 * F.BX, k)               # Giebelseiten 5,0 m: fuenf Gefache (wie bei den Elben)
        _l = lambda k: g1.gefach(F.VORN, F.HINTEN - F.VORN, k)     # Traufseiten 4,4 m: vier Gefache
        F.DACH_VORN, F.DACH_HINTEN = -2.80, 2.40                   # Ortgang 0,40 m vor dem Giebel, der Wulst steht 0,22 m weiter vor
        F.STRASSE = "vorn_eg"
        F.WELT = (((1, 0), (0, 1)), (0.0, 0.0)) if n == 1 else (((-1, 0), (0, 1)), (1.05, 0.0))
        tuer, eg, gb = (s1.Z_BODEN, s1.Z_BODEN + s1.TUER_HOCH), s1.EG_FENSTER, s1.GB_FENSTER
        # Oeffnungen wie bei den Elben (frei von den Moebeln der Wohnstube): Haustuer rechts der Mitte (Kunstwerk), Fenster links,
        # Giebelfenster im mittleren Gefach
        if n == 1:
            F.FENSTER = [("vorn_eg", _q(3), tuer, "tuer"), ("vorn_eg", _q(1), eg, "fenster"), ("vorn_gb", _q(2), gb, "fenster"),
                         ("links_eg", _l(3), eg, "fenster"), ("hinten_eg", _q(2), eg, "fenster")]
        else:
            F.FENSTER = [("rechts_eg", _l(3), tuer, "tuer"), ("vorn_eg", _q(1), eg, "fenster"),
                         ("vorn_gb", _q(2), gb, "fenster"), ("hinten_gb", _q(2), gb, "fenster"), ("links_eg", _l(3), eg, "fenster")]
        F.WALM, F.LAUBE = None, False
        F.GALERIE_AB, F.LEITER_X = 0.75, -1.0
        F.KAMIN = (F.BX - s1.T - 0.60, F.BX - s1.T, -0.10, 0.60)
        F.MATTEN = ((-0.6, -1.5, 1.5, 1.6), (-0.4, 1.15, 1.5, 0.6))
        F.FAHNE = (0.0, F.DACH_HINTEN - 0.55)
        F.WAPPEN = ((0.0 if n == 1 else sum(_q(3)) / 2, F.VORN, 1.53), (1, 0, 0), (0, -1, 0))   # im freien Gefach (wie bei den Elben)
        F.BANK = ("vorn_eg", -1.55, -0.35)                         # unter dem Fenster zur Gasse
        F.FASS = (-2.05, F.VORN - 0.35)                            # Regenfass vor der Giebelwand
        F.SCHEIT = ("hinten_eg", 1.30)
        F.GERUEST = dict(y=F.DACH_VORN + 0.08, x0=-2.4, x1=2.4, ebenen=(1.9, 3.6), innen=4.2, aussen=4.8, leiter_x=1.75)
        F.ZWERCH = (1, -1.25, ZWERCH_W)                            # rechts wie im Kunstwerk, vor dem Schlot (der steht bei y -0,10..0,60)
        F.OCHSENAUGE = "vorn_gb"
    elif n == 2:
        F.ZWERCH = (-1, 1.90, ZWERCH_W)                            # zur Gasse, ueber dem rechten Fenster
        F.OCHSENAUGE = "hinten_gb"
        F.DACH_VORN, F.DACH_HINTEN = F.DACH_VORN + WULST_RUECK, F.DACH_HINTEN - WULST_RUECK
    else:
        F.ZWERCH = (-1, 1.00, ZWERCH_W)                            # an der linken Traufseite (der Schlot steht rechts)
        F.OCHSENAUGE = "hinten_gb"
        F.DACH_HINTEN -= WULST_RUECK
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
    if n == 4:                                                     # Fahne vor die Spitze des Walms (frei vom Schlot im Stadtblick, wie bei den Elben)
        F.FAHNE = (0.0, F.DACH_VORN + F.D_WALM - FAHNE_VOR_WALM)


def masse_anlegen():
    """Halblingsmasse und -formen in den Modulen der Menschen (auch fuer die Messung, einmal): Kniestock, Dachneigung und Kehlbalken,
    die Grundformen (halbling_form); Tisch und Sitz fuer den Halbling. Haustuer und Herdhaube bleiben die der Menschen."""
    import math
    if _MASSE: return
    _MASSE.append(True)
    s1.KNIE, s1.KEHL = KNIE, KEHL
    s1.Z_TRAUFE = s1.Z_OG + KNIE
    s1.NEIGUNG = math.tan(math.radians(NEIGUNG_GRAD))
    s1.GB_FENSTER = (3.40, s1.Z_TRAUFE + KEHL - 0.08)
    s1.GALERIE_BALKEN = galerie_balken
    g1.TISCH_HOEHE, g1.SITZ_HOEHE = HALBLING_TISCH, HALBLING_SITZ
    alt_form = s1.form_setzen
    s1.form_setzen = lambda n: halbling_form(n, alt_form)
    alt_24 = s1.baue_form24

    def form24():
        t = alt_24()
        F = s1.F
        if F.nr in (1, 3):                             # Hauswappen innen im Dachgeschoss neben dem Giebelfenster (wie bei den Elben; Pruefungen 4, 24, 32)
            for teil in t["einrichtung"]:
                if teil.name == "Einrichtung_Hauswappen": s1.versetze(teil, ((1, 0), (0, 1)), (1.15, 0.0))
        if F.nr == 2:                                  # Laterne hing vor dem Tuerblatt (wie bei den Elben; Pruefung 33)
            laterne_versetzen(t)
        if F.nr == 3:                                  # Haustuer im Innenblick (lag neben der Kamera)
            for a in t["anker_innen"]:
                if a.name == "Blick_Auge": a.ort = Vector((*BLICK_G3[0], s1.Z_BODEN + 1.60 - BLICK_TIEFER))
                if a.name == "Blick_Ziel": a.ort = Vector((BLICK_G3[1][0], BLICK_G3[1][1], s1.Z_BODEN + BLICK_G3[1][2] - BLICK_TIEFER))
        if F.nr == 3: rueckwand_einrichten(t)          # Vorraete an der Rueckwand unter der Galerie (Nutzer 20.11.2026)
        licht_zum_auge(t)
        innen_fertig(t)
        return t
    s1.baue_form24 = s1.baue_form3 = form24            # auch die Grundform 3 entsteht mit dem Zusammenbau von 2 und 4


class OhneSpitze:
    """Stellvertreter fuer das Haus-Teil beim Giebelfachwerk des Giebels mit dem Ochsenauge: Hoelzer ganz ueber dem Kehlbalken
    (Spitzsaeule, Hahnenbalken, Streben dort) entfallen, an ihrer Stelle sitzt das runde Fenster (Kunstwerk)."""

    def __init__(self, teil, z_grenze):
        self._teil, self._z = teil, z_grenze

    def brett(self, stoff, a, b, *args, **kw):
        if min(a[2], b[2]) > self._z: return None
        return self._teil.brett(stoff, a, b, *args, **kw)

    def __getattr__(self, name):
        return getattr(self._teil, name)


def schicht_anlegen():
    """Stoff, Dateinamen, Atlasgruppen und Bauteile auf den Halbling umstellen; die Skripte der Menschen bleiben unveraendert."""
    masse_anlegen()
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
        figuren_setzen()
        return alt_bild(*a, **kw)
    g1.kontrollbild = kontrollbild
    alt_g1_anker, alt_init = g1.anker, s1.Anker.__init__

    def anker_ort(name, ort):
        ort = Vector(ort)
        if name in ("Blick_Auge", "Blick_Ziel"): ort.z -= BLICK_TIEFER
        return ort
    g1.anker = lambda name, ort, blick=None, **kw: alt_g1_anker(name, anker_ort(name, ort), blick, **kw)

    def anker_init(self, name, ort, blick=None, **extras):
        alt_init(self, name, anker_ort(name, ort), blick, **extras)
    s1.Anker.__init__ = anker_init
    # ── Hausapotheke und Bauplan auf Halblingsreichweite (wie bei den Zwergen) ──
    alt_apotheke, alt_bauplan = g1.baue_hausapotheke, g1.baue_bauplan
    g1.baue_hausapotheke = lambda: s1.versetze(alt_apotheke(), dz=-APOTHEKE_TIEFER)
    g1.baue_bauplan = lambda: s1.versetze(alt_bauplan(), dz=-BAUPLAN_TIEFER)

    # ── Dach: dickes Stroh mit Wulst am Ortgang, Pfettenkoepfe, Zwerchdach ──
    def dach_24():
        F = s1.F
        d = ht.strohdach(s1)
        for y, aussen, art in ((F.VORN, -1, F.DACH_VORN), (F.HINTEN, 1, F.DACH_HINTEN)):
            if F.WALM and aussen < 0 or abs(art - y) < 0.30: continue   # Grundform 4 vorn Walm; kurzer Ortgang (2, 4): keine Koepfe
            ht.pfettenkoepfe(d, s1, y, aussen, abs(art - y) - 0.06)
        ht.zwerchdach(d, s1, ht.zwerch_masse(s1, *F.ZWERCH))
        return merke("dach", d)
    s1.baue_dach = dach_24
    s1.scheitholz = lambda h, wand, a_start: ht.scheitholz(s1, h, wand, a_start)
    # ── Giebel mit dem Ochsenauge: ohne Spitzsaeule und Hahnenbalken ──
    alt_giebel = g1.giebel_fachwerk

    def giebel(h, y, aussen, ox, zt, zf, neigung, oeffnung, z_oeffnung, n=None):
        F = s1.F
        wand_y = F.WAENDE[F.OCHSENAUGE][0][1]
        if abs(y - wand_y) < 1e-6:
            h = OhneSpitze(h, z_oeffnung[1] + 0.12)
        return alt_giebel(h, y, aussen, ox, zt, zf, neigung, oeffnung, z_oeffnung, n)
    g1.giebel_fachwerk = giebel
    # ── Haus: Rundbogen aussen an der Haustuer, Ochsenauge, Waende des Zwerchhauses ──
    alt_haus24 = s1.baue_haus

    def haus24():
        h = alt_haus24()
        F = s1.F
        for wand, a, z, art in F.FENSTER:
            if art != "tuer": continue
            (ox, oy), (ex, ey), _, n = F.WAENDE[wand]
            start = ox if ex else oy
            ht.rundbogen(h, lambda s, zz, w=wand: s1.punkt(w, s, zz), (n[0], n[1], 0), a[0] - start, a[1] - start, z[1])
        (ox, oy), (ex, ey), breite, n = F.WAENDE[F.OCHSENAUGE]
        zk = s1.GB_FENSTER[1] + 0.16                               # Oberkante des Kehlbalkens im Giebel
        ht.ochsenauge(h, lambda s, zz, w=F.OCHSENAUGE: s1.punkt(w, s, zz), (n[0], n[1], 0), breite / 2, zk + 0.42 * (F.Z_FIRST - zk))
        ht.zwerchhaus_waende(h, s1, ht.zwerch_masse(s1, *F.ZWERCH))
        return merke("haus", h)
    s1.baue_haus = haus24
    alt_form = s1.form_setzen

    def form_setzen(n):
        alt_form(n)
        s1.F.name = umbenennen(s1.F.name)
    s1.form_setzen = form_setzen


def innen_fertig(t):
    """Wohnstube wie bei den Elben (wohnhaus_elb_s1.innen_fertig, Rueckmeldungen des Nutzers 17.11.2026): (1) Dachschraegen innen
    geschlossen – je Dachseite eine verputzte Flaeche 5 mm unter dem Dach, ueber den Sparren; (2) Kamin und Schlot ganz aus Stein: Holz,
    das in den Schlot laeuft, faellt dort weg, Holzteile des Herds im Grundriss des Schlots und die ganze Einfassung der Feuerstelle
    (Sturz, Seiten, bis unter die Haube) werden Stein. Der Schlot steht ueber der Feuerstelle (Pruefung 30)."""
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
    herdstein = [p for x in t["raum"] if x.name.startswith("Herd") for pts, uv, *_ in x.flaechen
                 if all(stein[0] - 1e-4 <= v <= stein[1] + 1e-4 for _, v in uv) for p in pts]
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


def galerie_balken(ga, ih):
    """Balken unter der Schlafgalerie (s1.GALERIE_BALKEN). Grundformen 1 und 3: Die Galerie ist nur 1,03 m tief (0,75 bis 1,78 m); mit den
    drei Balken der Menschen blieb zwischen dem mittleren und dem hinteren ein Spalt von 13 cm, 2 cm vor dem Wandbalken. In Grundform 1 steht
    das Auge genau unter diesem Spalt: Das Grundlicht streift die Balkenseiten nur, ihre gebackene Verdeckung lag bei 0,03 bis 0,08 –
    schwarze Seiten beim Blick nach links und rechts oben (Klicktest NAS 19.11.2026). Jetzt tragen der vordere Balken und der Wandbalken
    die Bohlen (12 cm stark, 0,76 m frei). Grundformen 2 und 4 (Galerie 2,1 bis 2,4 m tief): die drei Balken der Menschen."""
    if s1.F.nr in (1, 3): return (ga + 0.09,)
    return (ga + 0.09, (ga + ih) / 2, ih - 0.2)


def rueckwand_einrichten(t):
    """Grundform 3: Vorraete an der Rueckwand unter der Galerie (Nutzer 20.11.2026, „Halbling A“: die Wand soll beim Blick nach rechts
    nicht kahl wirken; die Haustuer bleibt im Standardbild). Alles im Teil Hausrat_Set, im Baurahmen (in der Lage im Spiel gespiegelt:
    x im Spiel = 1,05 - x): ein Wandbord mit drei Kruegen und einem Faesschen ueber der Schlafmatte (Unterkante 1,30 m – der Platz davor
    bleibt fuer eine Figur frei, Halbling bis 1,15 m), unter dem Bord zwei Zwiebelzoepfe, links vom Platz auf der Matte ein stehendes
    Fass und ein Kornsack (im Blick nach rechts unter dem Bord; rechts neben der Matte laegen sie ausserhalb des Bilds), rechts ein
    zweiter Sack vor dem Besen der Menschen."""
    c = next((x for x in t["raum"] if x.name == "Hausrat_Set"), None)
    if c is None: return
    S, feld = "Innen", g1.feld
    yw, zb = s1.F.HINTEN - s1.T, s1.Z_BODEN                       # Rueckwand innen, Fussboden
    # Wandbord auf zwei Knaggen
    c.kasten(S, (-0.40, yw - 0.22, zb + 1.30), (1.00, yw, zb + 1.335), "bohlen", ohne=("+y",), laengs="x")
    for x in (-0.22, 0.82):
        c.stab(S, (x, yw - 0.03, zb + 1.08), (x, yw - 0.19, zb + 1.30), 0.045, "balken", kappen=True)
    c.dreh(S, (-0.24, yw - 0.11, zb + 1.335), [(0.06, 0.0), (0.095, 0.05), (0.105, 0.13), (0.07, 0.21), (0.062, 0.24), (0.072, 0.26)], "kleinteile", seiten=8, deckel=False, u_bereich=feld("ton"))
    c.dreh(S, (0.02, yw - 0.12, zb + 1.335), [(0.05, 0.0), (0.08, 0.04), (0.085, 0.10), (0.055, 0.16), (0.06, 0.18)], "kleinteile", seiten=7, deckel=False, u_bereich=feld("ton"))
    c.dreh(S, (0.72, yw - 0.11, zb + 1.335), [(0.07, 0.0), (0.11, 0.06), (0.12, 0.16), (0.08, 0.27), (0.07, 0.30), (0.082, 0.33)], "kleinteile", seiten=8, deckel=False, u_bereich=feld("ton"))
    c.dreh(S, (0.36, yw - 0.12, zb + 1.335), [(0.085, 0.0), (0.105, 0.10), (0.085, 0.20)], "balken", seiten=8, deckel=True, gedreht=True)     # Faesschen
    # Zwiebelzoepfe unter dem Bord
    for x, farbe in ((-0.33, "zwiebel_gelb"), (0.93, "zwiebel_rot")):
        for i in range(4):
            m = Vector((x + 0.028 * ((i % 2) * 2 - 1), yw - 0.055 - 0.02 * (i % 2), zb + 1.20 - 0.082 * i))
            c.dreh(S, m, [(0.0, -0.045), (0.03, -0.035), (0.046, -0.005), (0.034, 0.028), (0.0, 0.052)], "kleinteile", seiten=5, deckel=False, u_bereich=feld(farbe))
        c.stab(S, (x, yw - 0.06, zb + 1.22), (x, yw - 0.06, zb + 1.30), 0.015, "stroh")
    # Fass und ein Kornsack links vom Platz auf der Matte (im Blick nach rechts unter dem Bord), ein Sack rechts davon
    c.dreh(S, (1.23, yw - 0.30, zb), [(0.21, 0.0), (0.26, 0.22), (0.26, 0.44), (0.21, 0.66)], "balken", seiten=10, deckel=True, gedreht=True)
    for (x, y), dreh in (((0.77, yw - 0.27), 1.0), ((-0.55, yw - 0.28), 0.9)):
        c.dreh(S, (x, y, zb), [(0.10 * dreh, 0.0), (0.22 * dreh, 0.05), (0.24 * dreh, 0.25), (0.18 * dreh, 0.43), (0.07 * dreh, 0.50), (0.10 * dreh, 0.57)], "stroh", seiten=8, deckel=True, u_bereich=(0.0, 0.3))
    print("INNEN Rueckwand eingerichtet: Wandbord, Kruege, Faesschen, Zwiebelzoepfe, Fass, zwei Saecke")


def licht_zum_auge(t):
    """Grundlicht der Wohnstube (Licht_Raum, OmniLight ohne Schatten) ueber den Blickpunkt, LICHT_RAUM[form] zum Ziel hin versetzt (wie
    wohnhaus_halbling_s23.licht_vor_den_herd; Klicktest NAS 19.11.2026, G1: schwarze Seiten der Balken unter der Galerie). Bei den
    Menschen steht es 1,0 m hinter der Gassenwand, die Kamera hinten unter der Galerie: alle Flaechen, die zur Kamera zeigen (Balken unter
    der Galerie, Herdseiten, Pfosten), lagen hinter dem Licht und bekamen nur Umgebungslicht (Pruefung 12c, alle vier Grundformen: 21
    bis 24 Flaechen). Grundformen 2 und 4: 6 cm zum Ziel hin, in der Hoehe der Menschen. Grundformen 1 und 3 (Feinschliff 20.11.2026): unter
    der Galerie (darueber bekam ihre Unterseite kein Licht, die Balkenseiten wurden nur gestreift); in Grundform 1 in 2,0 m mitten zwischen
    dem vorderen Balken und dem Wandbalken (0,17 m zum Ziel), in Grundform 3 in 1,5 m ueber dem Auge vor der Galerie: jede senkrechte
    Flaeche, die das Auge sieht, zeigt dann auch zum Licht, und die Rueckseiten der Leiterholme neben dem Auge bekommen es von unten."""
    an = {a.name: a for a in t["anker_innen"] if a.name in ("Blick_Auge", "Blick_Ziel", "Licht_Raum")}
    if len(an) < 3: return
    l, auge, ziel = an["Licht_Raum"], Vector(an["Blick_Auge"].ort), Vector(an["Blick_Ziel"].ort)
    versatz, hoehe = LICHT_RAUM[s1.F.nr]
    d = Vector((ziel.x - auge.x, ziel.y - auge.y, 0.0)).normalized()
    alt = Vector(l.ort)
    l.ort = Vector((auge.x + d.x * versatz, auge.y + d.y * versatz, hoehe if hoehe is not None else alt.z))
    print(f"INNEN Licht_Raum: ({alt.x:.2f}, {alt.y:.2f}, {alt.z:.2f}) -> ({l.ort.x:.2f}, {l.ort.y:.2f}, {l.ort.z:.2f})")


def laterne_versetzen(t):
    """Grundform 2: Die Laterne des Hauswaechters hing an der Strassenwand mitten vor dem Tuerblatt (wie bei den Elben,
    wohnhaus_elb_s1.laterne_versetzen); sie rueckt an derselben Wand nach LATERNE_G2_Y, zwischen Tuer und Fenster."""
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
    """Legt einmal die Halblingsmessfiguren (1,10 m) in die Szene: in der Haustuer und am Tisch. Nur fuer die Kontrollbilder."""
    if _FIGUREN: return
    anker = {o.name: o.location.copy() for o in bpy.data.objects if o.type == "EMPTY"}
    if "Anker_Tuer" not in anker: return
    tuer = anker["Anker_Tuer"]
    orte = [(tuer.x, tuer.y + 0.05, g1.Z_BODEN)]
    if "Platz_Partner" in anker: orte.append((anker["Platz_Partner"].x, anker["Platz_Partner"].y, g1.Z_BODEN))
    fig = ht.massfigur_halbling(orte, HALBLING_HOEHE)
    _FIGUREN.append(fig.objekt({"MassfigurHalbling": g1.stoff("MassfigurHalbling", farbe=(0.80, 0.55, 0.15))}, mit_farbe=False))


def messen(form, ausgabe_ordner=None):
    """Ist-Masse aus der gebauten Szene und den Massen der Bauskripte; Soll gegen Ist: halbling_masse.pruefe."""
    import halbling_masse
    z0 = g1.Z_BODEN
    fenster = s1.F.FENSTER
    tuer = next(f for f in fenster if f[3] == "tuer")
    anker = {o.name: o.location.copy() for o in bpy.data.objects if o.type == "EMPTY"}
    ist = dict(tuer_h=tuer[2][1] - tuer[2][0], tuer_b=tuer[1][1] - tuer[1][0], raum_h=g1.Z_OG - z0, haube_h=s1.Z_HAUBE - z0,
               dg_h=s1.Z_TRAUFE + s1.KEHL - s1.Z_GALERIE,
               dg_breite=lambda h: max(0.0, 2 * min(s1.F.OX - s1.T, (s1.F.Z_FIRST - s1.Z_GALERIE - h) / s1.NEIGUNG)), sitz=g1.SITZ_HOEHE, tisch=g1.TISCH_HOEHE, auge=anker["Blick_Auge"].z - z0, stufe=z0)
    zeilen = []
    _z, fehler = halbling_masse.pruefe(form, ist, ausgabe=zeilen.append)
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
    s1.main()                                                       # alle vier Grundformen der Halblinge (halbling_form)
    messen(form, opt.get("bilder"))


if __name__ == "__main__":
    main()
