"""Neubau der Gebaeude: Wohnhaus Zwerg, Stufe 1 (Gemeiner), Grundformen 1 bis 4 - aussen und innen.

    blender -b --python tools/modelle/neubau/wohnhaus_zwerg_s1.py -- --form=<1|2|3|4> [--bilder=<ordner>] [--ohne-ao]

Schreibt je Grundform
    game/assets/modelle/gebaeude/neubau/wohnhaus_zwerg_s1_g<n>.glb         Huelle (wie beim Menschen: Haus, Dach, Scheiben, Wappen, Fahne, Leerstand, Geruest, Anker)
    game/assets/modelle/gebaeude/neubau/wohnhaus_zwerg_s1_g<n>_innen.glb   Innenraum (im Spiel „InteriorRoot“)
    und seine Zelle in den beiden AO-Atlanten der Gruppe (ao_atlas.json: wohnhaus_zwerg, wohnhaus_zwerg_innen).

Die Zwergenfassung ist eine Schicht auf den Bauskripten der Menschen (wohnhaus_mensch_s1_g1.py = Grundform 1 und Bauteile,
wohnhaus_mensch_s1.py = Grundformen 2 bis 4): gleiche Masse, gleiche Waende, Fenster und Tuer an derselben Stelle (innen exakt
maßgleich mit aussen), aber der Stoff ist ein anderer (trim_zwerg: behauener Stein, Schindeldach, innen Steinwand und Pflaster) und
die Dinge im Haus sind die der Zwerge: Schmiedegeraet, Etagenbett, Zwergenhoehen bei Tisch und Sitz. Die Schicht ersetzt einzelne
Funktionen der Menschenskripte (`ersetze`), ohne sie zu aendern; Vorbild ist das Kunstwerk „Wohnhaus Zwerg“ (Stufe 1).
Massbelege (Soll gegen Ist): tools/modelle/neubau/zwerg_masse.py.
"""
import os
import sys

HIER = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HIER)
import bpy                                                         # noqa: E402,F401
import wohnhaus_mensch_s1_g1 as g1                                 # noqa: E402
import wohnhaus_mensch_s1 as s1                                    # noqa: E402
import zwerg_teile as zt                                           # noqa: E402
from mathutils import Vector                                       # noqa: E402

VOLK = "zwerg"
# Soll-Masse der Zwerge (docs/bereiche/darstellung.md, Passanten-Probe: Zwerg 1,30 bis 1,40 m, Zwerginnen mal 0,95 – gerechnet mit 1,35 m)
ZWERG_HOEHE = 1.35
ZWERG_TISCH, ZWERG_SITZ = 0.62, 0.36          # Menschen: 0,74 und 0,44; Verhaeltnis 0,84 und 0,82
BLICK_TIEFER = 0.35                           # Augenhoehe 1,25 m statt 1,60 m
APOTHEKE_TIEFER, BAUPLAN_TIEFER = 0.35, 0.12       # Oberkante 1,77 -> 1,42 m und 1,65 -> 1,53 m ueber dem Fussboden
SCHLOT_HOEHER = 1.10                          # der Rauch steigt aus der hoeheren Schlothaube


def umbenennen(text):
    return text.replace("mensch", VOLK)


FORM = 1                # Grundform des laufenden Baus (main); Teil der festen Saat der Zufallslaeden
LAEDEN_TEILE = []        # Teile der Huelle mit geschlossenen Laeden (Zufallslaeden)


def schicht_anlegen():
    """Stoff, Dateinamen und Atlasgruppen auf den Zwerg umstellen; die Bauteile der Menschen bleiben unveraendert."""
    g1.FACHWERK_ECHT = True                           # echtes Fachwerk im Obergeschoss und Giebel (Holzlage, Nutzer 22.11.2026)
    zt.quader_in_lagen()                              # Gefache und Giebel aus dunklem Quader in Lagen der Bandhoehe (keine Streckung)
    alt_stoff, alt_uv = g1.stoff, g1.uv2_und_ao
    g1.stoff = lambda name, blatt=None, **kw: alt_stoff(name, (blatt + "_" + VOLK) if blatt else None, **kw)
    g1.uv2_und_ao = lambda gruppen, gruppe, modell, mit_ao=True: alt_uv(gruppen, umbenennen(gruppe), umbenennen(modell), mit_ao)
    g1.NAME = umbenennen(g1.NAME)
    alt_bild = g1.kontrollbild

    def kontrollbild(*a, **kw):                                    # Blender 4.5 mit deutscher Oberflaeche: Knotennamen der Welt
        welt = bpy.context.scene.world
        if welt is not None:
            welt.use_nodes = True
            for knoten in welt.node_tree.nodes:                    # Knotennamen sind je nach Spracheinstellung uebersetzt
                if knoten.type == "BACKGROUND": knoten.name = "Background"
        datei = a[0]
        if "innen_blick" in os.path.basename(datei) and not os.path.basename(datei).startswith("g"):   # Grundform 1: der Blick aus Zwergenaugenhoehe
            a = (a[0], (a[1][0], a[1][1], a[1][2] - BLICK_TIEFER), (a[2][0], a[2][1], a[2][2] - BLICK_TIEFER)) + tuple(a[3:])
        if objs_zwerg_figuren():
            pass
        r = alt_bild(*a, **kw)
        if "aussen_vorn" in os.path.basename(datei):               # dazu die Ansichten von der Spielkamera (schraeg von oben) und wie im Kunstwerk
            verb = kw.get("verbergen", a[4] if len(a) > 4 else ())
            for name, auge, ziel in (("spiel_rechts", (9.5, -12.5, 11.0), (0.0, 0.2, 2.2)), ("spiel_links", (-9.5, -12.5, 11.0), (0.0, 0.2, 2.2)),
                                     ("spiel_hinten", (8.0, 13.0, 10.0), (0.0, 0.2, 2.2))):
                alt_bild(datei.replace("aussen_vorn", name), auge, ziel, 35, verb)
        return r
    g1.kontrollbild = kontrollbild
    # ── Zwergenmasse: Tisch und Sitz fuer eine Figur von 1,35 m (Menschen 0,74 / 0,44), Blick aus Augenhoehe 1,25 m (Menschen 1,60 m) ──
    g1.TISCH_HOEHE, g1.SITZ_HOEHE = ZWERG_TISCH, ZWERG_SITZ
    alt_g1_anker, alt_init = g1.anker, s1.Anker.__init__

    def anker_ort(name, ort):
        ort = Vector(ort)
        if name in ("Blick_Auge", "Blick_Ziel"): ort.z -= BLICK_TIEFER
        if name == "Anker_Schornstein": ort.z += SCHLOT_HOEHER
        return ort
    g1.anker = lambda name, ort, blick=None, **kw: alt_g1_anker(name, anker_ort(name, ort), blick, **kw)

    def anker_init(self, name, ort, blick=None, **extras):
        alt_init(self, name, anker_ort(name, ort), blick, **extras)
    s1.Anker.__init__ = anker_init
    # ── Dinge der Zwerge statt der der Menschen ──
    # Hausapotheke und Bauplan haengen auf Zwergenreichweite (Greifhoehe 1,35 m * 1,25 = 1,69 m, Zwergin 1,60 m): tiefer als bei den Menschen
    alt_apotheke, alt_bauplan = g1.baue_hausapotheke, g1.baue_bauplan
    g1.baue_hausapotheke = lambda: s1.versetze(alt_apotheke(), dz=-APOTHEKE_TIEFER)
    g1.baue_bauplan = lambda: s1.versetze(alt_bauplan(), dz=-BAUPLAN_TIEFER)
    g1.baue_werkzeug = lambda: zt.werkzeug(g1.IX)
    g1.baue_vorrat = lambda zimmertuer_frei=False: zt.vorrat(g1.IX, g1.KAMIN, zimmertuer_frei, g1.IH)
    g1.baue_dach = lambda: zt.dach(zt.DachMass(1, g1.OX, g1.DACH_VORN, g1.DACH_HINTEN, g1.Z_FIRST, g1.MITTE, g1.dach_z, g1.NEIGUNG))

    def dach_24():
        F = s1.F
        walm = dict(walm=F.WALM, X_WALM=F.X_WALM, Z_WALM=F.Z_WALM, D_WALM=F.D_WALM) if F.WALM else {}
        return zt.dach(zt.DachMass(F.nr, F.OX, F.DACH_VORN, F.DACH_HINTEN, F.Z_FIRST, F.MITTE, s1.dach_z, s1.NEIGUNG, **walm))
    s1.baue_dach = dach_24
    alt_raum1, alt_raum24 = g1.baue_raum, s1.baue_raum
    g1.baue_raum = lambda: zt.boden_pflastern(alt_raum1())
    s1.baue_raum = lambda: zt.boden_pflastern(alt_raum24())
    alt_fach = s1.fachwerk

    def fachwerk(h, wand, z0, z1, ganz=True, raehm=True):
        """Erdgeschoss der Zwerge: Stein ohne Fachwerk (Kunstwerk); nur die Tuer bekommt Pfosten und Sturz aus Holz."""
        if z0 > s1.Z_OG - 0.01: return alt_fach(h, wand, z0, z1, ganz, raehm)
        _, _, breite, n = s1.F.WAENDE[wand]
        n3 = (n[0], n[1], 0)
        P = lambda s, z: s1.punkt(wand, s, z)
        for s0, s1_, za, zb, art in s1.loecher(wand):
            if art != "tuer": continue
            for s in (s0 - 0.07, s1_ + 0.07): h.brett("Fachwerk", P(s, za), P(s, zb + 0.14), 0.14, n3, "balken_c", laengs="v")
            h.brett("Fachwerk", P(s0 - 0.2, zb + 0.08), P(s1_ + 0.2, zb + 0.08), 0.17, n3, "balken_c", tiefe=0.07)
    s1.fachwerk = fachwerk
    alt_haus1, alt_haus24 = g1.baue_haus, s1.baue_haus

    def haus1():
        h = alt_haus1()
        x0, x1, y0, y1 = g1.KAMIN
        zt.schlot_aufsatz(h, g1.KAMIN, g1.Z_FIRST - 0.35)
        zt.schmiedegeraet(h, (2.05, g1.VORN - 0.42), (0, -1), (1, 0))
        if getattr(s1.F, "nr", 1) == 3:   # Grundform 3: an der rechten Seite steht der Schuppen; die Kohle liegt vor seiner Tuer
            zt.kohlensack(h, (g1.BX + 1.95, -1.80)); zt.kohlensack(h, (g1.BX + 1.33, -1.75), hoehe=0.44)   # 0,62 m neben dem ersten: die Bodenscheiben decken sich nicht
        else:
            zt.kohlensack(h, (g1.BX + 0.42, -0.55)); zt.kohlensack(h, (g1.BX + 0.40, -0.02), hoehe=0.44)
        return h
    g1.baue_haus = haus1

    def haus24():
        h = alt_haus24()
        F = s1.F
        zt.schlot_aufsatz(h, F.KAMIN, F.Z_KAMIN)
        zt.schmiedegeraet(h, *F.AMBOSS)
        for ort, hoch in F.KOHLE: zt.kohlensack(h, ort, hoehe=hoch)
        return h
    s1.baue_haus = haus24
    alt_form = s1.form_setzen

    def form_setzen(n):
        alt_form(n)
        F = s1.F
        F.name = umbenennen(F.name)
        # Amboss (Fusspunkt, Richtung von der Wand weg, Richtung entlang der Wand) und Kohlensaecke, im Baurahmen
        if n == 2: F.AMBOSS, F.KOHLE = (((-F.BX - 0.42, 1.05), (-1, 0), (0, 1))), [((-F.BX - 0.40, -3.0), 0.50), ((-F.BX - 0.38, -2.45), 0.44)]
        if n == 4: F.AMBOSS, F.KOHLE = (((1.95, F.VORN - 0.42), (0, -1), (1, 0))), [((-F.BX - 0.40, F.VORN + 1.35), 0.50), ((-F.BX - 0.40, F.VORN + 1.85), 0.44)]
        # Wappen (Regel wie wohnhaus_mensch_s23.wappen_ort: 5 cm zu Laden, Fenster, Holz, Tuer): Grundform 2 zwischen Tuersturz der Zwerge
        # (Pfosten und Sturz bis 0,20 m neben die Tuer) und dem Laden des Fensters daneben
        if n == 2:
            frei = (0.40 + 0.20 + zt_abstand(), 1.5486 - 0.03 - 0.40 - zt_abstand())   # Sturzende .. linker Laden des Fensters (Breite 0,40)
            F.WAPPEN_G = min(WAPPEN_G, (frei[1] - frei[0]) / SCHILD_BREITE)
            F.WAPPEN = ((-F.BX, (frei[0] + frei[1]) / 2, 1.53), (0, -1, 0), (-1, 0, 0))
    s1.form_setzen = form_setzen

    # ── Luecken (luecken_pruefung, Pruefung 16): Scheitholz mit 1 cm Fuge, gefuellte Laubenfelder (Grundform 4) und Windbrett am
    #    Schuppen (3) baut das Mensch-Skript seit dem Mensch-Nachtrag (#501) selbst; die Umhuellungen der Zwerge sind gestrichen ──
    # ── Wappen Grundform 1 und 3: im Brustfeld des mittleren Gefachs, 5 cm unter dem Brustriegel und ueber der Schwelle; das Schild
    #    wird kleiner, wo die Hoehe nicht reicht (wie wappen_ort) ──
    g1.baue_wappen = wappen_g1
    alt_s1_wappen = s1.baue_wappen

    def wappen24():
        w = alt_s1_wappen()
        g = getattr(s1.F, "WAPPEN_G", WAPPEN_G)
        if abs(g - WAPPEN_G) > 1e-6:                    # Schild um den Ursprung verkleinern
            o = Vector(s1.F.WAPPEN[0])
            w.flaechen = [([o + (p - o) * (g / WAPPEN_G) for p in pts], uv, st, fa, gl) for pts, uv, st, fa, gl in w.flaechen]
        return w
    s1.baue_wappen = wappen24
    # ── Fahnenmast auf dem First verankert (er schwebte ueber dem duennen Schindeldach: der Mast der Menschen steht 25 cm ueber der
    #    Unterseite des Firsts, das Stroh ist 28 cm dick, die Schindeln 1,5 bis 7 cm) ──
    alt_fahne1, alt_fahne24 = g1.baue_fahne, s1.baue_fahne

    def fahne(mast):
        zt.laeden_zu_pruefen(LAEDEN_TEILE + [zt.LETZTES_DACH])     # die Huelle steht (Schuppen, Laube, Schlot): verstellte Laeden weg
        return zt.fahne_verankern(mast, zt.LETZTES_DACH)
    g1.baue_fahne = lambda: fahne(alt_fahne1())
    s1.baue_fahne = lambda x, y, z: fahne(alt_fahne24(x, y, z))
    # Geschlossene Laeden im festen Zufall (zwerg_teile.laeden_zu, Verfahren der Zeilenmodule): auf freien Gefachen des Fachwerks
    alt_lage = g1.fachwerk_wand_lage

    def fachwerk_wand_lage(h, P, breite, n3, z0, z1, oeffnungen, n=None, raehm=True, riegel=True, z_fenster=g1.OG_FENSTER, stoss=None):
        alt_lage(h, P, breite, n3, z0, z1, oeffnungen, n, raehm, riegel, z_fenster, stoss)
        if not riegel: return
        import zeile_module as Z
        if h not in LAEDEN_TEILE: LAEDEN_TEILE.append(h)
        fen = [o for o in oeffnungen if o[4] == "fenster"]
        t0, t1 = (min(f[2] for f in fen), max(f[3] for f in fen)) if fen else z_fenster
        p0, p1 = Vector(P(0.0, 0.0)), Vector(P(1.0, 0.0))
        felder = zt.felder_der_lage(g1.LAGEN[-1][4], breite, oeffnungen, t0, t1)
        schluessel = f"wohnhaus_zwerg_s1_g{FORM}|{p0.x:.2f},{p0.y:.2f}|{n3[0]:.0f},{n3[1]:.0f}|{z0:.2f}"
        zt.laeden_zu(h, p0, p1 - p0, n3, felder, t0 + 0.04, t1 - 0.04, schluessel, Z.LADEN_OG, nebeneinander=False)
    g1.fachwerk_wand_lage = fachwerk_wand_lage


WAPPEN_G = 0.62                                   # Groesse des Schilds der Menschen (g1.baue_wappen)
SCHILD_BREITE = 2 * 0.34 * 1.16                   # Rahmenbreite je Einheit g (g1.SCHILD mal 1,16)
SCHILD_UNTEN, SCHILD_OBEN = 0.44 * 1.12, 0.58     # unter dem Ursprung (Rahmen) bzw. darueber (Haken), je Einheit g


def zt_abstand():
    return 0.05 + 0.005


def wappen_g1():
    """Wappen der Grundformen 1 und 3 wie g1.baue_wappen, aber mit 5 cm Abstand zu Schwelle und Brustriegel (Regel wappen_ort)."""
    from netzbau import Teil
    from trimsheet import BAENDER_AUSSEN
    w = Teil("Wappen", BAENDER_AUSSEN, saat=61)
    unten = g1.Z_OG + 0.20 + zt_abstand()                                  # Oberkante der Schwelle
    oben = g1.OG_FENSTER[0] - g1.RIEGEL - zt_abstand()                     # Unterkante des Brustriegels
    g = min(WAPPEN_G, (oben - unten) / (SCHILD_UNTEN + SCHILD_OBEN))
    zw = unten + SCHILD_UNTEN * g
    o = (0.0, g1.OVORN, zw)
    eu, ev, n = (1, 0, 0), (0, 0, 1), (0, -1, 0)
    g1.prisma(w, "Fachwerk", [(x * 1.16 * g, y * 1.12 * g) for x, y in g1.SCHILD], o, eu, ev, n, 0.0, 0.045, "balken_c")
    g1.prisma(w, "Hausfarbe", [(x * g, y * g) for x, y in g1.SCHILD], o, eu, ev, n, 0.045, 0.075, "farbholz", farbe=(1.0, 1.0, 1.0, 1.0))
    w.kasten("Bruchstein", (-0.02, g1.OVORN - 0.05, zw + 0.47 * g), (0.02, g1.OVORN, zw + 0.58 * g), "eisen", ohne=("+y",))
    return w


_FIGUREN = []


def objs_zwerg_figuren():
    """Legt einmal die Zwergenmessfiguren in die Szene: eine neben der Menschenfigur vor der Tuer (die Menschenfigur zeigt, dass 1,75 m
    durch die Tuer passen), eine am Herd im Raum. Nur fuer die Kontrollbilder (hide_render in den Exportlisten nicht noetig)."""
    if _FIGUREN: return False
    anker = {o.name: o.location.copy() for o in bpy.data.objects if o.type == "EMPTY"}
    if "Anker_Tuer" not in anker: return False
    tuer = anker["Anker_Tuer"]
    ort = [(tuer.x - 0.80, tuer.y - 0.45, 0.0)]
    if "Platz_Partner" in anker: ort.append((anker["Platz_Partner"].x, anker["Platz_Partner"].y, g1.Z_BODEN))
    if "Platz_Oberhaupt" in anker: ort.append((anker["Platz_Oberhaupt"].x, anker["Platz_Oberhaupt"].y + 0.0, g1.Z_BODEN + g1.SITZ_HOEHE * 0.0))
    fig = zt.massfigur_zwerg(ort[:2], ZWERG_HOEHE)
    o = fig.objekt({"MassfigurZwerg": g1.stoff("MassfigurZwerg", farbe=(0.20, 0.42, 0.85))}, mit_farbe=False)
    _FIGUREN.append(o)
    return True


def messen(form, ausgabe_ordner=None):
    """Ist-Masse aus der gebauten Szene (Anker, Netze) und den Massen der Bauskripte; Soll gegen Ist: zwerg_masse.pruefe."""
    import zwerg_masse
    objs = bpy.data.objects
    z0 = g1.Z_BODEN
    quelle = g1 if form in (1, 3) else s1
    fenster = g1.FENSTER if form in (1, 3) else s1.F.FENSTER
    tuer = next(f for f in fenster if f[3] == "tuer")
    bruestung = min(f[2][0] for f in fenster if f[3] == "fenster" and f[2][0] < g1.Z_OG)
    anker = {o.name: o.location.copy() for o in objs if o.type == "EMPTY"}

    def oben(name):
        o = objs.get(name)
        if o is None: return None
        return max((o.matrix_world @ Vector(c)).z for c in o.bound_box) - z0
    greif = {n[n.index("_") + 1:]: oben(n) for n in ("Gegenstand_Werkzeug", "Gegenstand_Truhe", "Gegenstand_Familienbuch", "Gegenstand_Bauplan", "Gegenstand_Geld", "Einrichtung_Hausapotheke")}
    greif = {k: v for k, v in greif.items() if v is not None}
    if form in (1, 3): zf, neig, zg, halb = g1.Z_FIRST, g1.NEIGUNG, g1.Z_GALERIE, g1.OX - g1.T
    else: zf, neig, zg, halb = s1.F.Z_FIRST, s1.NEIGUNG, s1.Z_GALERIE, s1.F.OX - s1.T
    stehbreite = {f: max(0.0, 2 * min(halb, (zf - zg - zwerg_hoehe(f) - 0.05) / neig)) for f in ("Zwerg", "Zwergin", "Mensch")}
    ist = dict(tuer_h=tuer[2][1] - tuer[2][0], tuer_b=tuer[1][1] - tuer[1][0], raum_h=g1.Z_OG - z0, haube_h=g1.Z_HAUBE - z0, sitz=g1.SITZ_HOEHE,
               tisch=g1.TISCH_HOEHE, auge=anker["Blick_Auge"].z - z0, bruestung=bruestung - z0, stufe=z0, greif=greif, galerie_stehbreite=stehbreite)
    zeilen = []
    zwerg_masse.pruefe(form, ist, ausgabe=zeilen.append)
    for z in zeilen: print(z)
    if ausgabe_ordner:
        os.makedirs(ausgabe_ordner, exist_ok=True)
        open(os.path.join(ausgabe_ordner, f"soll_ist_g{form}.txt"), "w", encoding="utf-8").write("\n".join(z.replace("SOLLIST ", "", 1) for z in zeilen) + "\n")


def zwerg_hoehe(figur):
    import zwerg_masse
    return zwerg_masse.FIGUREN[figur][0]


def main():
    args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    opt = dict((a[2:].split("=", 1) + ["1"])[:2] for a in args if a.startswith("--"))
    global FORM
    form = FORM = int(opt.get("form", "1"))
    schicht_anlegen()
    if form == 1: g1.main()
    else:
        s1.GALERIE_LICHT = True                                    # Grundlicht unter der Galerie wie bei den Menschen
        s1.main()
    messen(form, opt.get("bilder"))


if __name__ == "__main__":
    main()
