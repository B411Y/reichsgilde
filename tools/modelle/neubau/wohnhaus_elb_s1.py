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
_MASSE = []
FORM = 1                          # gebaute Grundform (main)
LETZT = {}                        # zuletzt gebaute Teile (Haus, Dach, Scheiben, Wappen) fuer die Wappenregel


def umbenennen(text):
    return text.replace("mensch", VOLK)


def masse_anlegen():
    """Elbenmasse in den Modulen der Menschen (auch fuer die Messung, einmal): Haustuer 2,10 m, Herdhaube 1,95 m licht."""
    if _MASSE: return
    _MASSE.append(True)
    for m in (g1, s1):
        m.TUER_HOCH = TUER_HOCH
        m.Z_HAUBE = m.Z_HAUBE + HAUBE_HOEHER
    g1.FENSTER = [(w, a, (z[0], g1.Z_BODEN + TUER_HOCH), art) if art == "tuer" else (w, a, z, art) for w, a, z, art in g1.FENSTER]


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
    g1.anker = lambda name, ort, blick=None, **kw: alt_g1_anker(name, anker_ort(name, ort), blick, **kw)

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
    alt_haus1, alt_haus24 = g1.baue_haus, s1.baue_haus

    def haus1():
        h = alt_haus1()
        wand, (a0, a1), (z0, z1), _ = next(f for f in g1.FENSTER if f[3] == "tuer")
        (ox, oy), (ex, ey), _, n = g1.WAENDE[wand]
        start = ox if ex else oy
        et.spitzbogen(h, lambda s, z, w=wand: g1.punkt(w, s, z), (n[0], n[1], 0), a0 - start, a1 - start, z1)
        (lx0, lx1), (lz0, lz1) = g1.LUKE                          # Ladeluke im vorderen Giebel: vor den Fluegeln (6 cm)
        et.spitzbogen(h, lambda s, z: Vector((s, g1.OVORN, z)), (0, -1, 0), lx0, lx1, lz1, vor=0.065)
        if FORM == 1: et.unterstand(h, g1.BX, 0.35, 1.65, 2.30)   # ueber dem Scheitholz an der rechten Seite (Grundform 3 hat den Schuppen)
        return h
    g1.baue_haus = haus1

    def haus24():
        h = alt_haus24()
        F = s1.F
        for wand, a, z, art in F.FENSTER:
            if art != "tuer" and not wand.endswith("_gb"): continue
            (ox, oy), (ex, ey), _, n = F.WAENDE[wand]
            start = ox if ex else oy
            et.spitzbogen(h, lambda s, zz, w=wand: s1.punkt(w, s, zz), (n[0], n[1], 0), a[0] - start, a[1] - start, z[1])
        if F.nr == 4: et.unterstand(h, F.BX, 1.75, 3.05, 2.45)   # ueber dem Scheitholz an der rechten Seite
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
    g1.baue_fahne = lambda: (wappen_regel(), zt.fahne_verankern(alt_fahne1(), zt.LETZTES_DACH))[1]
    s1.baue_fahne = lambda x, y, z: (wappen_regel(), zt.fahne_verankern(alt_fahne24(x, y, z), zt.LETZTES_DACH))[1]
    alt_form = s1.form_setzen

    def form_setzen(n):
        alt_form(n)
        s1.F.name = umbenennen(s1.F.name)
    s1.form_setzen = form_setzen


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
    fenster = g1.FENSTER if form in (1, 3) else s1.F.FENSTER
    tuer = next(f for f in fenster if f[3] == "tuer")
    anker = {o.name: o.location.copy() for o in bpy.data.objects if o.type == "EMPTY"}
    ist = dict(tuer_h=tuer[2][1] - tuer[2][0], tuer_b=tuer[1][1] - tuer[1][0], raum_h=g1.Z_OG - z0, haube_h=(g1.Z_HAUBE if form in (1, 3) else s1.Z_HAUBE) - z0,
               sitz=g1.SITZ_HOEHE, tisch=g1.TISCH_HOEHE, auge=anker["Blick_Auge"].z - z0, stufe=z0)
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
    if form == 1: g1.main()
    else: s1.main()
    messen(form, opt.get("bilder"))


if __name__ == "__main__":
    main()
