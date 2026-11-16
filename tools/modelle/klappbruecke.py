"""Klappbruecke Koeln: Doppelklappbruecke mit zwei Torhaeusern, Ketten, Gegengewichten und Zugwerk (Zwerge und Elben, Alternativwelt).

    blender -b --python tools/modelle/klappbruecke.py -- [--bilder=<ordner>] [--folge=<ordner>] [--detail=<ordner>] [--nah=<ordner>]
                                                         [--glb=<datei>] [--ohne-ao] [--uv2-rand=0.001] [--rampe=<m>]

Ohne Schalter: baut das Modell, legt UV2 an, backt die Verdeckung (Cycles) und schreibt
    game/assets/modelle/bruecke/klappbruecke.glb             (Knoten und Animationen Oeffnen/Schliessen, siehe unten)
    game/assets/texturen/bruecke/klappbruecke_ao_atlas.png   gebackene Verdeckung auf UV2 (2048 x 2048, eine Zelle)
Das Trim-Sheet (Albedo, Normal, ORM) macht klappbruecke_trim.py (game/assets/texturen/bruecke/bruecke_trim_*.png).
`--bilder`, `--folge`, `--detail`, `--nah` machen nur Kontrollbilder (Cycles, kein Spielbild; Stoffe mit Albedo, Normal, ORM und der Verdeckung auf UV2).

Hintergrund (Nutzer, 15.11.2026): Eine feste Rheinbruecke liesse keine Schiffe mit Mast durch; Zwerge und Elben haben den Koelnern eine groessere
Bruecke mit doppelter Klappbruecke in der Mitte gebaut. Zwergisch-solide im Grundbau (Quaderverband, Eichenbohlen und -balken mit Eisen), elbisch-fein
im Detail (Gelaenderstaebe, Ranken, Fensterstaebe, Eisenspitzen aus feinem dunklem Eisen).

Koordinaten: X = Brueckenachse (L bei -X = Westufer/Koeln, R bei +X = Deutz), Y = Flussrichtung (+Y Norden = Stroemung), Z oben, Wasserlinie z = 0,
Ursprung Mitte der Durchfahrt. Im glTF (Y nach oben) wird Blender-Y zu -Z.

Knoten (alle in der geschlossenen Pose gebaut, dann der Ursprung an den Drehpunkt gelegt):
    Klappe_L / Klappe_R         Fahrbahnklappe (Bohlen, Querbalken, Wangen, Beschlaege). Drehachse = Ursprung (x = -+6,5, z = 2,2), Achse Y;
                                 rotation_euler.y = s * theta (L: s = -1, R: s = +1), theta 0 geschlossen bis 75 Grad offen.
    Gelaender_L / Gelaender_R   Kind von Klappe_L/R: Pfosten, Handlauf, feine Staebe und Ranke (ohne UV2, kein eigener Verdeckungsplatz).
    Kette_{L,R}_{N,S}           Kettenstrang Klappe -> Kettenrad (Ursprung am Kettenrad vorn, Kette zeigt lokal -Z): Rotation Y + Skalierung Z.
    Gegenkette_{L,R}_{N,S}      Strang Kettenrad hinten -> Gegengewicht (Ursprung am Kettenrad hinten, zeigt -Z): Skalierung Z.
    Gegengewicht_{L,R}_{N,S}    Steinklotz im Eisenrahmen (Ursprung in der Mitte): Verschiebung Z.
    Zugwerk_L / Zugwerk_R       Achse mit zwei Kettenraedern und zwei Handspeichenraedern; Ursprung auf der Achse x = -+5,5, y = 0: Drehung um Y.
    Torhaus_L / Torhaus_R       zwei Turmpfeiler mit Durchgang, Daechern, Tuer (fest, mit UV2).
    Zierwerk_L / Zierwerk_R     feine Eisenarbeit am Torhaus (Ranken, Fensterstaebe, Spitzen, Schienen, Laschen; fest, ohne UV2).
    Pfeiler_L / Pfeiler_R       Widerlager mit Eisbrecherkeilen (fest, mit UV2).
    Schranke_L / Schranke_R     Schrankenbaum (Eichenbalken mit Gegengewicht), Ursprung = Drehpunkt am Pfosten bei x = -+11,5, y = +3,0, z = Fahrbahn + 1,05: Drehung um X;
                                 in der Datei steht er OBEN (offen, Fahrbahn frei), geschlossen = rotation_euler.x = 0 (Baum waagerecht quer ueber die Fahrbahn).
    Schrankenpfosten_L / _R     fester Pfosten dazu.
    Anker_Durchfahrt, Anker_Schranke_L/R, Anker_Warte_L/R   Leerknoten fuer die Ablaufphase.
Animationen "Oeffnen" und "Schliessen" (6 s, 24 Bilder/s) als NLA-Spuren aus der Kinematik (klappbruecke_masse.kinematik); "Schranke_zu" und "Schranke_auf"
(2 s) fahren die Schrankenbaeume (das Spiel stellt sie nach dem Kern selbst ein, die Animationen dienen Werkzeugen und Proben).

Stoffe (Namen fuer das Spiel): Bruecke (ein Stoff mit dem ganzen Trim-Sheet: Quader quader_a/b/nass, werkstein, Eichenholz bohlen/balken, Eisen
eisen_beschlag/rost/fein, Schiefer dach) und Fenster (nur Farbe). Quader und Schiefer sind in Lagen von 0,5 m gebaut (die Wand legt Lage auf Lage, jede mit eigenem Versatz).
UV2 (Verdeckung) haben nur die grossen Teile (Pfeiler, Torhaus, Klappe); Ketten, Gewichte, Zugwerk, Gelaender, Zierwerk nicht (zu fein: eigene Inseln
wuerden entarten, wie beim Steinhaus erst gemessen).
"""
import math
import os
import sys

import bpy
from mathutils import Matrix, Vector

HIER = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HIER)
sys.path.insert(0, os.path.join(HIER, "neubau"))
import klappbruecke_masse as K            # noqa: E402
import netzbau                            # noqa: E402,F401
from netzbau import Teil                  # noqa: E402
from klappbruecke_trim import BRUECKE_BAENDER as BB   # noqa: E402

WURZEL = os.path.dirname(os.path.dirname(HIER))
ZIEL_MODELL = os.path.join(WURZEL, "game", "assets", "modelle", "bruecke")
ZIEL_TEXTUR = os.path.join(WURZEL, "game", "assets", "texturen", "bruecke")
NAME = "klappbruecke"
AO_DATEI = "klappbruecke_ao_atlas.png"
AO_KANTE = 2048

V = Vector
HALB, DECK, ZP = K.HALB, K.DECK, K.ZP
BRUECKE, FENSTER = "Bruecke", "Fenster"              # ein Stoff fuer alles auf dem Trim-Sheet (weniger Zeichenaufrufe), die Fenster getrennt (Nachtlicht)
STEIN = HOLZ = EISEN = DACH = BRUECKE                  # die Baender (quader_*, bohlen, balken, eisen_*, dach) unterscheiden die Werkstoffe
BOHLEN, BALKEN = "bohlen", "balken"
E_BESCHLAG, E_ROST, E_FEIN = "eisen_beschlag", "eisen_rost", "eisen_fein"
LAGE = 0.5                                  # Hoehe einer Quader-/Schieferlage in Metern (Band = 256 Bildpunkte bei 512 je Meter)


# ── Hilfen ───────────────────────────────────────────────────────────────────────────────────────────────────────────────
def kast(t, stoff, a, b, band, **kw):
    mn = V(map(min, a, b)); mx = V(map(max, a, b))
    t.kasten(stoff, mn, mx, band, **kw)


def bewege(t, m):
    t.flaechen = [([m @ V(p) for p in pts], uv, st, f, g) for pts, uv, st, f, g in t.flaechen]


def wirf(ziel, quelle, m=None):
    if m is not None: bewege(quelle, m)
    ziel.dazu(quelle)


def _klip(pts, ax, grenze, ueber):
    """Sutherland-Hodgman: behaelt den Teil des Vielecks mit p.ax >= grenze (ueber) bzw. <= grenze."""
    aus = []
    for i, p in enumerate(pts):
        q = pts[(i + 1) % len(pts)]
        dp, dq = p.dot(ax) - grenze, q.dot(ax) - grenze
        if not ueber: dp, dq = -dp, -dq
        if dp >= 0: aus.append(p)
        if (dp >= 0) != (dq >= 0):
            aus.append(p + (q - p) * (dp / (dp - dq)))
    return aus


def _bereinige(pts, eps=1e-6):
    aus = []
    for p in pts:
        if not aus or (p - aus[-1]).length > eps: aus.append(p)
    if len(aus) > 1 and (aus[0] - aus[-1]).length <= eps: aus.pop()
    return aus


def _flaeche_inhalt(pts):
    n = V()
    for i in range(1, len(pts) - 1): n += (pts[i] - pts[0]).cross(pts[i + 1] - pts[0])
    return n


def kachel(t, stoff, pts, au, av, baender, weg_von=None, zu=None, farbe=(0, 0, 0, 1)):
    """Vieleck in Lagen von LAGE Metern (laengs `av`) auf Baender legen: je Lage ein Stueck mit eigenem Versatz und eigener Bandwahl
    (Quaderverband: wie beim Steinhaus legt die Wand Lage auf Lage). u laeuft entlang `au` (Meter / Bandbreite)."""
    pts = [V(p) for p in pts]
    au, av = V(au).normalized(), V(av).normalized()
    vs = [p.dot(av) for p in pts]
    for c in range(math.floor(min(vs) / LAGE + 1e-9), math.ceil(max(vs) / LAGE - 1e-9)):
        stueck = _bereinige(_klip(_klip(pts, av, c * LAGE, True), av, (c + 1) * LAGE, False))
        if len(stueck) < 3: continue
        n = _flaeche_inhalt(stueck)
        if n.length < 1e-6: continue
        band = baender[0] if len(baender) == 1 else baender[t.zufall.randrange(len(baender))]
        v0, v1, m = t.baender[band]
        off = t.zufall.random()
        uvs = [(p.dot(au) / m + off, v1 - ((p.dot(av) - c * LAGE) / LAGE) * (v1 - v0)) for p in stueck]
        mitte = sum(stueck, V()) / len(stueck)
        if weg_von is not None and n.dot(mitte - V(weg_von)) < 0: stueck, uvs = stueck[::-1], uvs[::-1]
        if zu is not None and n.dot(V(zu) - mitte) < 0: stueck, uvs = stueck[::-1], uvs[::-1]
        t.flaechen.append((stueck, uvs, stoff, farbe, False))


STEIN_BAENDER = ("quader_a", "quader_b")
NASS_BAENDER = ("quader_nass",)
BLOCK_BAENDER = ("werkstein",)


def steinkasten(t, mn, mx, ohne=(), baender=STEIN_BAENDER, stoff=STEIN):
    """Achsparalleler Kasten aus Quaderlagen (senkrechte Seiten) und Quaderstreifen (waagerechte Seiten)."""
    mn, mx = V(mn), V(mx)
    mn, mx = V(map(min, mn, mx)), V(map(max, mn, mx))
    mitte = (mn + mx) / 2
    for achse in range(3):
        a, b = [i for i in range(3) if i != achse]
        for seite, wert in (("-", mn), ("+", mx)):
            if seite + "xyz"[achse] in ohne: continue
            o = V(mn); o[achse] = wert[achse]
            ea, eb = V(), V(); ea[a] = mx[a] - mn[a]; eb[b] = mx[b] - mn[b]
            pts = [o, o + ea, o + ea + eb, o + eb]
            if achse == 2: au, av = V((1, 0, 0)), V((0, 1, 0))
            elif achse == 0: au, av = V((0, 1, 0)), V((0, 0, 1))
            else: au, av = V((1, 0, 0)), V((0, 0, 1))
            kachel(t, stoff, pts, au, av, baender, weg_von=mitte)


def welle(t, stoff, a, b, amp, n, oben, dicke=0.03, band=E_FEIN):
    """Ranke: Zickzack-Welle von a nach b (elbisch-fein): n Segmente, seitlich um `oben` * amp versetzt."""
    a, b, oben = V(a), V(b), V(oben).normalized()
    pts = [a + (b - a) * (i / n) + oben * (amp if i % 2 else -amp) * (0 if i in (0, n) else 1) for i in range(n + 1)]
    for p, q in zip(pts[:-1], pts[1:]): t.stab(stoff, p, q, dicke, band, oben=V((0, 1, 0)) if abs((q - p).y) < 0.5 else V((1, 0, 0)))


def rad(stoff, r, b, zaehne, band=E_BESCHLAG, speichen=0, nabe=0.09, rand=0.07, saat=3):
    """Rad mit Achse Z (spaeter nach Y gekippt): Felge aus Kanthoelzern, Zaehne oder Speichen mit Knaufenden. Ursprung in der Mitte."""
    t = Teil("rad", BB, saat=saat)
    n = max(zaehne, 12)
    for i in range(n):
        a0, a1 = 2 * math.pi * i / n, 2 * math.pi * (i + 1) / n
        p, q = V((r * math.cos(a0), r * math.sin(a0), 0)), V((r * math.cos(a1), r * math.sin(a1), 0))
        t.stab(stoff, p, q, rand, band, breite=b, oben=V((0, 0, 1)))
    for i in range(zaehne):
        w = 2 * math.pi * i / zaehne
        z = Teil("z", BB, saat=4)
        z.kasten(stoff, V((r, -0.04, -b / 2)), V((r + 0.07, 0.04, b / 2)), band)
        bewege(z, Matrix.Rotation(w, 4, "Z")); t.dazu(z)
    for i in range(speichen):
        w = 2 * math.pi * i / speichen
        d = V((math.cos(w), math.sin(w), 0))
        t.stab(stoff, d * nabe, d * (r + 0.28), 0.05, band, breite=0.06, oben=V((0, 0, 1)))
        t.stab(stoff, d * (r + 0.2), d * (r + 0.32), 0.09, band, breite=0.09, oben=V((0, 0, 1)))   # Knauf zum Anfassen
    t.dreh(stoff, V((0, 0, -b / 2)), [(0, 0), (nabe + 0.04, 0), (nabe + 0.04, b), (0, b)], band, seiten=8)
    return t


def rad_y(t_rad, mitte):
    """Radachse Z -> Y (Drehung um X um -90 Grad) und an `mitte` setzen."""
    bewege(t_rad, Matrix.Translation(mitte) @ Matrix.Rotation(-math.pi / 2, 4, "X"))
    return t_rad


# ── Stoffe (Albedo, Normal, ORM, Verdeckung UV2) ─────────────────────────────────────────────────────────────────────
def stoff(name, trim=True, farbe=(0.5, 0.5, 0.5), rauheit=0.8, ao_bild=None):
    """Stoff fuers Kontrollbild: Trim-Sheet mit Albedo, Normal (OpenGL) und ORM (R Verdeckung, G Rauheit, B Metall), dazu die gebackene Verdeckung auf UV2.
    Die GLB bekommt keine Bilder (das Spiel setzt sie beim Laden); die Knoten hier dienen nur den Bildern."""
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    bn = m.node_tree.nodes
    bsdf = next(n for n in bn if n.type == "BSDF_PRINCIPLED")
    bsdf.inputs["Base Color"].default_value = (*farbe, 1.0)
    bsdf.inputs["Roughness"].default_value = rauheit
    if not trim: return m
    lk = m.node_tree.links

    def bild(art, farbraum):
        b = bpy.data.images.load(os.path.join(ZIEL_TEXTUR, f"bruecke_trim_{art}.png"), check_existing=True)
        b.colorspace_settings.name = farbraum
        k = bn.new("ShaderNodeTexImage"); k.image = b; k.name = "Trim_" + art
        uv = bn.new("ShaderNodeUVMap"); uv.uv_map = "UVMap"
        lk.new(uv.outputs["UV"], k.inputs["Vector"])
        return k
    alb, nor, orm = bild("albedo", "sRGB"), bild("normal", "Non-Color"), bild("orm", "Non-Color")
    nm = bn.new("ShaderNodeNormalMap"); nm.uv_map = "UVMap"; nm.inputs["Strength"].default_value = 1.0
    lk.new(nor.outputs["Color"], nm.inputs["Color"])
    lk.new(nm.outputs["Normal"], bsdf.inputs["Normal"])
    sep = bn.new("ShaderNodeSeparateColor"); lk.new(orm.outputs["Color"], sep.inputs["Color"])
    lk.new(sep.outputs["Green"], bsdf.inputs["Roughness"])
    lk.new(sep.outputs["Blue"], bsdf.inputs["Metallic"])
    ao_wert = bn.new("ShaderNodeMath"); ao_wert.operation = "MULTIPLY"
    lk.new(sep.outputs["Red"], ao_wert.inputs[0])
    if ao_bild is not None:
        ak = bn.new("ShaderNodeTexImage"); ak.image = ao_bild; ak.name = "AO_UV2"
        uv2 = bn.new("ShaderNodeUVMap"); uv2.uv_map = "UV2"
        lk.new(uv2.outputs["UV"], ak.inputs["Vector"])
        sa = bn.new("ShaderNodeSeparateColor"); lk.new(ak.outputs["Color"], sa.inputs["Color"])
        lk.new(sa.outputs["Red"], ao_wert.inputs[1])
    else:
        ao_wert.inputs[1].default_value = 1.0
    grau = bn.new("ShaderNodeCombineColor")
    for i in range(3): lk.new(ao_wert.outputs["Value"], grau.inputs[i])
    mul = bn.new("ShaderNodeMix"); mul.data_type = "RGBA"; mul.blend_type = "MULTIPLY"; mul.inputs[0].default_value = 1.0
    lk.new(alb.outputs["Color"], mul.inputs[6]); lk.new(grau.outputs["Color"], mul.inputs[7])
    lk.new(mul.outputs[2], bsdf.inputs["Base Color"])
    return m


# ── Fest: Pfeiler ───────────────────────────────────────────────────────────────────────────────────────────────────────
def baue_pfeiler(s):
    t = Teil(f"Pfeiler_{'L' if s < 0 else 'R'}", BB, saat=20 + int(s))
    x0, x1 = HALB, K.PYL_X[1]                 # Pfeiler und Torhaus gleich tief (4 m): das Mittelteil ist 21 m lang, das Bogenfeld des Wasserplans folgt bei 10,5 m
    yb = 6.6
    steinkasten(t, (s * x0, -yb, -1.5), (s * x1, yb, DECK - 0.25), ohne=("+z", "-y", "+y"), baender=NASS_BAENDER)   # die Stirnseiten deckt der Eisbrecherkeil
    steinkasten(t, (s * x0, -yb - 0.1, DECK - 0.25), (s * x1, yb + 0.1, DECK), baender=BLOCK_BAENDER)   # Abdeckplatte
    for sy in (-1, 1):                          # Eisbrecherkeile (Stromteiler) vor Nord- und Sued-Ende
        mx = s * (x0 + x1) / 2
        basis = [V((s * x0, sy * yb, -1.5)), V((s * x1, sy * yb, -1.5))]
        spitze = V((mx, sy * (yb + 2.6), -1.5))
        o = [V((p.x, p.y, DECK - 0.25)) for p in basis]; so = V((mx, spitze.y, 1.3))
        kachel(t, STEIN, [basis[0], spitze, so, o[0]], (0, 1, 0), (0, 0, 1), NASS_BAENDER, weg_von=(mx, 0, 0))
        kachel(t, STEIN, [basis[1], spitze, so, o[1]], (0, 1, 0), (0, 0, 1), NASS_BAENDER, weg_von=(mx, 0, 0))
        kachel(t, STEIN, [o[0], o[1], so], (1, 0, 0), (0, 1, 0), BLOCK_BAENDER, zu=(mx, sy * 3, 9))
    return t


# ── Fest: Torhaus ───────────────────────────────────────────────────────────────────────────────────────────────────────
def bogen_z(y):
    w, h, f = K.BOGEN
    R = ((w / 2) ** 2 + f * f) / (2 * f)
    return DECK + h + (math.sqrt(R * R - y * y) - math.sqrt(R * R - (w / 2) ** 2))


def baue_torhaus(s):
    tag = "L" if s < 0 else "R"
    t = Teil(f"Torhaus_{tag}", BB, saat=30 + int(s))
    z = Teil(f"Zierwerk_{tag}", BB, saat=35 + int(s))
    xa, xb = HALB, K.PYL_X[1]
    ya, yb = K.PYL_Y
    z_top = K.ZW + 1.0
    z_traufe = DECK + 7.0
    for sy in (-1, 1):
        steinkasten(t, (s * xa, sy * ya, DECK), (s * xb, sy * yb, z_top), ohne=("+x" if s > 0 else "-x", "-z"))
        _tuer_wand(t, s, sy, xb, ya, yb, z_top)
        steinkasten(t, (s * (xa - 0.15), sy * (ya - 0.15), z_top), (s * (xb + 0.15), sy * (yb + 0.15), z_top + 0.25), baender=BLOCK_BAENDER)   # Gesims
        for z0, z1 in ((DECK + 4.2, DECK + 5.2), (DECK + 6.0, DECK + 7.0)):                  # Fensterschlitze mit elbischen Staeben
            kast(t, FENSTER, (s * (xa - 0.02), sy * 4.65, z0), (s * (xa + 0.03), sy * 5.15, z1), E_FEIN)
            for dy in (-0.12, 0.0, 0.12):
                z.stab(EISEN, (s * (xa - 0.04), sy * (4.9 + dy), z0 + 0.05), (s * (xa - 0.04), sy * (4.9 + dy), z1 - 0.05), 0.018, E_FEIN)
            z.stab(EISEN, (s * (xa - 0.04), sy * 4.65, (z0 + z1) / 2), (s * (xa - 0.04), sy * 5.15, (z0 + z1) / 2), 0.018, E_FEIN)
        yk = sy * K.YK
        kast(t, HOLZ, (s * (xa - 1.7), yk - 0.25, K.ZW + 0.45), (s * (xa + 0.3), yk + 0.25, K.ZW + 0.8), BALKEN)       # Kranbalken
        steinkasten(t, (s * (xa - 0.05), yk - 0.4, K.ZW + 0.2), (s * (xa + 0.05), yk + 0.4, K.ZW + 0.9), baender=BLOCK_BAENDER)
        for dy in (-0.15, 0.15):                                                                                   # Eisenlaschen zum Lager
            kast(z, EISEN, (s * (xa - 0.9 - 0.12), yk + dy - 0.015, K.ZW - 0.02), (s * (xa - 0.9 + 0.12), yk + dy + 0.015, K.ZW + 0.45), E_ROST)
        for dy in (-0.42, 0.42):                                                                                   # Fuehrungsschienen der Gewichte
            kast(z, EISEN, (s * (xa - 0.09), yk + dy - 0.035, DECK + 0.5), (s * xa, yk + dy + 0.035, K.ZW), E_ROST)
        _walmdach(t, z, s, sy, xa, xb, ya, yb, z_top + 0.25)
    _torbogen(t, z, s, xa, xb, z_traufe)
    return t, z


def _tuer_wand(t, s, sy, xb, ya, yb, z_top):
    """Landseitige Wand des Turmpfeilers mit Tuer (1,3 x 2,9 m) bei |y| = 4,9; Laibung, Tuerblatt aus Bohlen, Beschlaege, Sturz."""
    tw, th = K.TUER
    y0, y1 = 4.9 - tw / 2, 4.9 + tw / 2
    z0, z1 = DECK, DECK + th
    X = s * xb
    P = lambda y, zz, x=X: V((x, sy * y, zz))
    ref = (s * (xb - 2), sy * 4.9, DECK + 4)
    for pts in ([P(ya, z0), P(y0, z0), P(y0, z_top), P(ya, z_top)], [P(y1, z0), P(yb, z0), P(yb, z_top), P(y1, z_top)], [P(y0, z1), P(y1, z1), P(y1, z_top), P(y0, z_top)]):
        kachel(t, STEIN, pts, (0, 1, 0), (0, 0, 1), STEIN_BAENDER, weg_von=ref)
    tief = 0.3
    XT = s * (xb - tief)
    innen = (X, sy * 4.9, DECK + 1)
    kachel(t, STEIN, [P(y0, z0), P(y0, z1), P(y0, z1, XT), P(y0, z0, XT)], (1, 0, 0), (0, 0, 1), BLOCK_BAENDER, zu=innen)
    kachel(t, STEIN, [P(y1, z0), P(y1, z1), P(y1, z1, XT), P(y1, z0, XT)], (1, 0, 0), (0, 0, 1), BLOCK_BAENDER, zu=innen)
    kachel(t, STEIN, [P(y0, z1), P(y1, z1), P(y1, z1, XT), P(y0, z1, XT)], (1, 0, 0), (0, 1, 0), BLOCK_BAENDER, zu=innen)
    for k in range(4):                                                    # Tuerblatt: vier senkrechte Bohlen
        ya_ = y0 + k * tw / 4; yb_ = y0 + (k + 1) * tw / 4
        t.rechteck(HOLZ, P(ya_ + 0.01, z0 + 0.02, XT), V((0, sy, 0)), V((0, 0, 1)), (yb_ - ya_ - 0.02), th - 0.02, BOHLEN, laengs="v", zu=(X, sy * 4.9, DECK + 1))
    for zz in (DECK + 0.5, DECK + 1.5, DECK + 2.4):                      # Beschlagbaender quer
        a = P(y0 + 0.02, zz, XT); b = P(y1 - 0.02, zz, XT)
        t.stab(EISEN, a + V((s * 0.03, 0, 0)), b + V((s * 0.03, 0, 0)), 0.03, E_BESCHLAG, breite=0.1, oben=V((1, 0, 0)))
    kast(t, HOLZ, (min(X, XT) - 0.02, min(sy * (y0 - 0.1), sy * (y1 + 0.1)), z1 + 0.03), (max(X, XT) + 0.02, max(sy * (y0 - 0.1), sy * (y1 + 0.1)), z1 + 0.2), BALKEN)   # Sturz (3 cm ueber der Laibung, nicht deckungsgleich)


def _walmdach(t, z, s, sy, xa, xb, ya, yb, z0):
    """Walmdach (Schiefer) ueber dem Turmpfeiler: Traufe z0, Firsthoehe +3,0 m; Eisenspitze mit Rauten (feines Eisen, Zierwerk)."""
    ue = 0.25
    x0, x1 = s * (xa - ue), s * (xb + ue)
    y0, y1 = sy * (ya - ue), sy * (yb + ue)
    ym = (y0 + y1) / 2
    h = 3.0
    ax, bx = (x0 + (x1 - x0) * 0.22, x0 + (x1 - x0) * 0.78)
    A = [V((x0, y0, z0)), V((x1, y0, z0)), V((x1, y1, z0)), V((x0, y1, z0))]
    F0, F1 = V((ax, ym, z0 + h)), V((bx, ym, z0 + h))
    mitte = V(((x0 + x1) / 2, ym, z0 - 1))
    for pts, rand_y in (([A[0], A[1], F1, F0], y0), ([A[3], A[2], F1, F0], y1)):
        kachel(t, DACH, pts, (1, 0, 0), V((0, ym - rand_y, h)), ("dach",), weg_von=mitte)
    for pts, rand_x, F in (([A[0], A[3], F0], x0, F0), ([A[1], A[2], F1], x1, F1)):
        kachel(t, DACH, pts, (0, 1, 0), V((F.x - rand_x, 0, h)), ("dach",), weg_von=mitte)
    v0, v1 = BB[E_FEIN][0], BB[E_FEIN][1]
    for F in (F0, F1):
        z.stab(EISEN, F, F + V((0, 0, 1.0)), 0.035, E_FEIN)
        for zz in (0.45, 0.8):
            p = F + V((0, 0, zz))
            raute = [p + V((0.0, 0.0, 0.14)), p + V((0.0, 0.1, 0.0)), p + V((0.0, 0.0, -0.14)), p + V((0.0, -0.1, 0.0))]
            uvr = [(0.1, v0 + 0.01), (0.2, v0 + 0.01), (0.2, v1 - 0.01), (0.1, v1 - 0.01)]
            z.vieleck(EISEN, raute, uvr, zu=p + V((3, 0, 0)))
            z.vieleck(EISEN, raute[::-1], uvr, zu=p + V((-3, 0, 0)))


def _torbogen(t, z, s, xa, xb, z_traufe):
    w, h, f = K.BOGEN
    n = 14
    ys = [-w / 2 + w * i / n for i in range(n + 1)]
    mitte = V((s * (xa + xb) / 2, 0, DECK + 2))
    for xw in (xa, xb):
        X = s * xw
        for y0, y1 in zip(ys[:-1], ys[1:]):
            q = [V((X, y0, bogen_z(y0))), V((X, y1, bogen_z(y1))), V((X, y1, z_traufe)), V((X, y0, z_traufe))]
            kachel(t, STEIN, q, (0, 1, 0), (0, 0, 1), STEIN_BAENDER, weg_von=mitte)
    for y0, y1 in zip(ys[:-1], ys[1:]):          # Laibung (Gewoelbe), von unten sichtbar
        q = [V((s * xa, y0, bogen_z(y0))), V((s * xb, y0, bogen_z(y0))), V((s * xb, y1, bogen_z(y1))), V((s * xa, y1, bogen_z(y1)))]
        kachel(t, STEIN, q, (1, 0, 0), (0, 1, 0), STEIN_BAENDER, zu=V((s * (xa + xb) / 2, (y0 + y1) / 2, DECK + 1)))
    for xw in (xa, xb):                          # Eisenband der Bogenkante und Ranke (elbisch)
        X = s * xw
        for y0, y1 in zip(ys[:-1], ys[1:]):
            z.stab(EISEN, V((X, y0, bogen_z(y0) + 0.06)), V((X, y1, bogen_z(y1) + 0.06)), 0.05, E_ROST, breite=0.05, oben=V((1, 0, 0)))
        welle(z, EISEN, V((X, -w / 2 + 0.1, bogen_z(-w / 2 + 0.1) + 0.4)), V((X, w / 2 - 0.1, bogen_z(w / 2 - 0.1) + 0.4)), 0.07, 40, (0, 0, 1), 0.02)
    ue = 0.3                                     # schwaches Satteldach (First entlang y) zwischen den Turmpfeilern
    xl, xr = s * (xa - ue), s * (xb + ue)
    ym0, ym1 = -K.PYL_Y[0] - 0.05, K.PYL_Y[0] + 0.05
    xm = (xl + xr) / 2
    zf = z_traufe + 1.5
    mitte2 = V((xm, 0, z_traufe - 1))
    for xe in (xl, xr):
        kachel(t, DACH, [V((xe, ym0, z_traufe)), V((xe, ym1, z_traufe)), V((xm, ym1, zf)), V((xm, ym0, zf))], (0, 1, 0), V((xm - xe, 0, zf - z_traufe)), ("dach",), weg_von=mitte2)
    for yg in (ym0, ym1):
        kachel(t, STEIN, [V((s * xa, yg, z_traufe)), V((s * xb, yg, z_traufe)), V((xm, yg, zf - 0.1))], (1, 0, 0), (0, 0, 1), STEIN_BAENDER, weg_von=mitte2)


# ── Seitenspannen und Ufer-Enden (Parameter RAMPE, UFER_TIEF, FELD, ZWISCHENPFEILER in klappbruecke_masse) ─────────────────
# Das Spiel (Stadtansicht.Klappbruecke.cs) setzt sie nach dem Wasserplan zusammen: von jedem Ufer Ende, Bogenfeld, Zwischenpfeiler, Bogenfeld ... bis zum
# Mittelteil. Knoten liegen zum Bau abseits (y = -40, -50, -60), das Spiel setzt sie neu und blendet sie am Modell aus; ihr Ursprung ist
#   Bogenfeld        Mitte des Feldes (x = -4..4), Wasserlinie z = 0
#   Zwischenpfeiler  Mitte des Pfeilers (x = -1..1)
#   Ende             an der Uferkante: x = -RAMPE (Rampe, Gelaende) bis 0 (Fahrbahn auf Deckhoehe); das Bogenfeld folgt bei x >= 0
BR_Y, BR_I, BRUEST_H = 3.6, 3.2, 0.9             # halbe Aussenbreite (= Innenseite der Turmpfeiler), Innenkante Bruestung, Bruestungshoehe
FELD_SPRUNG, FELD_STICH = 0.6, 1.3               # Bogen: Kaempferhoehe und Stich ueber der Wasserlinie


def feld_z(x):
    h = K.FELD / 2
    r = (h * h + FELD_STICH ** 2) / (2 * FELD_STICH)
    return FELD_SPRUNG + math.sqrt(r * r - x * x) - math.sqrt(r * r - h * h)


def bruestung(t, x0, x1, z0, ohne=()):
    for sy in (-1, 1):
        a, b = sorted((sy * BR_I, sy * BR_Y))
        steinkasten(t, (x0, a, z0), (x1, b, z0 + BRUEST_H), ohne=("-z",) + tuple(ohne))
        steinkasten(t, (x0, a - 0.05, z0 + BRUEST_H), (x1, b + 0.05, z0 + BRUEST_H + 0.1), baender=BLOCK_BAENDER)


def baue_bogenfeld():
    t = Teil("Bogenfeld", BB, saat=80)
    h = K.FELD / 2
    n = 12
    xs = [-h + K.FELD * i / n for i in range(n + 1)]
    for sy in (-1, 1):                          # Stirnwaende ueber dem Bogen
        y = sy * BR_Y
        for x0, x1 in zip(xs[:-1], xs[1:]):
            q = [V((x0, y, feld_z(x0))), V((x1, y, feld_z(x1))), V((x1, y, DECK)), V((x0, y, DECK))]
            kachel(t, STEIN, q, (1, 0, 0), (0, 0, 1), STEIN_BAENDER, weg_von=(0, 0, 1))
    for x0, x1 in zip(xs[:-1], xs[1:]):          # Laibung des Tonnengewoelbes
        q = [V((x0, -BR_Y, feld_z(x0))), V((x0, BR_Y, feld_z(x0))), V((x1, BR_Y, feld_z(x1))), V((x1, -BR_Y, feld_z(x1)))]
        kachel(t, STEIN, q, (0, 1, 0), (1, 0, 0), STEIN_BAENDER, zu=((x0 + x1) / 2, 0, 0.2))
    kachel(t, STEIN, [V((-h, -BR_I, DECK)), V((h, -BR_I, DECK)), V((h, BR_I, DECK)), V((-h, BR_I, DECK))], (1, 0, 0), (0, 1, 0), BLOCK_BAENDER, weg_von=(0, 0, 0))
    bruestung(t, -h, h, DECK)
    return t


def pfeilerkeile(t, x0, x1, ytiefe, z_oben):
    """Eisbrecherkeile (Stromteiler) an beiden Stirnseiten (+-y) eines Pfeilers von x0 bis x1."""
    mx = (x0 + x1) / 2
    for sy in (-1, 1):
        basis = [V((x0, sy * BR_Y, -1.5)), V((x1, sy * BR_Y, -1.5))]
        spitze = V((mx, sy * (BR_Y + ytiefe), -1.5))
        o = [V((p.x, p.y, z_oben)) for p in basis]
        so = V((mx, spitze.y, 1.3))
        kachel(t, STEIN, [basis[0], spitze, so, o[0]], (0, 1, 0), (0, 0, 1), NASS_BAENDER, weg_von=(mx, 0, 0))
        kachel(t, STEIN, [basis[1], spitze, so, o[1]], (0, 1, 0), (0, 0, 1), NASS_BAENDER, weg_von=(mx, 0, 0))
        kachel(t, STEIN, [o[0], o[1], so], (1, 0, 0), (0, 1, 0), BLOCK_BAENDER, zu=(mx, sy * 3, 9))


def baue_zwischenpfeiler():
    t = Teil("Zwischenpfeiler", BB, saat=81)
    hp = K.ZWISCHENPFEILER / 2
    steinkasten(t, (-hp, -BR_Y, -1.5), (hp, BR_Y, DECK - 0.3), ohne=("+z", "-y", "+y"), baender=NASS_BAENDER)
    steinkasten(t, (-hp, -BR_Y, DECK - 0.3), (hp, BR_Y, DECK), baender=BLOCK_BAENDER)
    pfeilerkeile(t, -hp, hp, 2.0, DECK - 0.3)
    bruestung(t, -hp, hp, DECK)
    return t


def baue_ende():
    """Brueckenende am Ufer: Rampe RAMPE m lang von der Uferhoehe (Fahrbahn - UFER_TIEF) hinauf auf die Fahrbahn, seitlich Stuetzmauern mit Bruestung."""
    t = Teil("Ende", BB, saat=82)
    r = K.RAMPE
    zg = DECK - K.UFER_TIEF
    hang = V((r, 0, K.UFER_TIEF))
    kachel(t, STEIN, [V((-r, -BR_I, zg)), V((0, -BR_I, DECK)), V((0, BR_I, DECK)), V((-r, BR_I, zg))], (0, 1, 0), hang, BLOCK_BAENDER, zu=(-r / 2, 0, zg + 5))
    kachel(t, STEIN, [V((-r, -BR_I, zg - 0.4)), V((-r, BR_I, zg - 0.4)), V((-r, BR_I, zg)), V((-r, -BR_I, zg))], (0, 1, 0), (0, 0, 1), STEIN_BAENDER, weg_von=(0, 0, zg))
    for sy in (-1, 1):                           # Stuetzmauer mit Bruestung (Querschnitt: Viereck mit schraeger Oberkante)
        pts = [(-r, zg - 0.4), (-r, zg + BRUEST_H), (0, DECK + BRUEST_H), (0, zg - 0.4)]
        mitte = V((-r / 2, sy * (BR_I + BR_Y) / 2, zg + 0.3))
        for y, au in ((sy * BR_Y, (1, 0, 0)), (sy * BR_I, (1, 0, 0))):
            kachel(t, STEIN, [V((x, y, z)) for x, z in pts], au, (0, 0, 1), STEIN_BAENDER, weg_von=mitte if y == sy * BR_Y else None, zu=mitte if y == sy * BR_I else None)
        ya, yb = sorted((sy * BR_I, sy * BR_Y))
        kachel(t, STEIN, [V((-r, ya, zg + BRUEST_H)), V((0, ya, DECK + BRUEST_H)), V((0, yb, DECK + BRUEST_H)), V((-r, yb, zg + BRUEST_H))], (0, 1, 0), hang, BLOCK_BAENDER, zu=(-r / 2, 0, 9))
        kachel(t, STEIN, [V((-r, ya, zg - 0.4)), V((-r, yb, zg - 0.4)), V((-r, yb, zg + BRUEST_H)), V((-r, ya, zg + BRUEST_H))], (0, 1, 0), (0, 0, 1), STEIN_BAENDER, weg_von=(0, 0, zg))
    return t


# ── Schranken ───────────────────────────────────────────────────────────────────────────────────────────────────────────
SCHRANKE_Y = 3.0                 # Pfosten auf der Nordseite der Fahrbahn (innerhalb der Bruestung bei 3,2)
SCHRANKE_Z = 1.05                # Baum 1,05 m ueber der Fahrbahn: ueber der Bruestung (1,0 m), unter dem Baum kommt keiner durch
SCHRANKE_AUF = 80.0              # Grad: Baum steht offen fast senkrecht


def baue_schranke(s):
    """Baum (waagerecht, geschlossene Lage) und Pfosten. Pfosten bei y = +SCHRANKE_Y, Baum reicht bis zur Suedbruestung (y = -(BR_I - 0,05))."""
    tag = "L" if s < 0 else "R"
    x = s * K.SCHRANKE_X
    zb = DECK + SCHRANKE_Z
    baum = Teil(f"Schranke_{tag}", BB, saat=90 + int(s))
    ende = -(BR_I - 0.05)
    baum.stab(HOLZ, V((x, SCHRANKE_Y + 0.7, zb)), V((x, ende, zb)), 0.13, BALKEN, breite=0.16, oben=V((0, 0, 1)), kappen=True)      # Baum mit kurzem Gegengewichtsarm hinter dem Drehpunkt
    kast(baum, EISEN, (x - 0.11, SCHRANKE_Y + 0.62, zb - 0.1), (x + 0.11, SCHRANKE_Y + 0.8, zb + 0.1), E_ROST)                 # Gegengewicht
    for y in (1.5, -0.2, -1.9, -3.4 + 0.3):                                                                                       # Eisenbaender als Streifen
        kast(baum, EISEN, (x - 0.095, y, zb - 0.085), (x + 0.095, y + 0.12, zb + 0.085), E_BESCHLAG)
    pfosten = Teil(f"Schrankenpfosten_{tag}", BB, saat=95 + int(s))
    kast(pfosten, HOLZ, (x - 0.17, SCHRANKE_Y - 0.17, DECK), (x + 0.17, SCHRANKE_Y + 0.17, zb + 0.25), BALKEN)
    kast(pfosten, EISEN, (x - 0.19, SCHRANKE_Y - 0.19, zb + 0.25), (x + 0.19, SCHRANKE_Y + 0.19, zb + 0.31), E_FEIN)
    return baum, pfosten, V((x, SCHRANKE_Y, zb))


# ── Beweglich: Klappe ────────────────────────────────────────────────────────────────────────────────────────────────
def baue_klappe(s):
    """Klappe in der geschlossenen Pose, Weltkoordinaten. u: Abstand von der Drehachse (x = s * (HALB - u)). Gibt (Klappe, Gelaender) zurueck."""
    tag = 'L' if s < 0 else 'R'
    t = Teil(f"Klappe_{tag}", BB, saat=40 + int(s))
    g = Teil(f"Gelaender_{tag}", BB, saat=45 + int(s))
    L, B = K.KLAPPE_L, K.KLAPPE_B
    X = lambda u: s * (HALB - u)
    hb = B / 2
    n = int(L / 0.45)                                 # Bohlen quer zur Fahrt: 0,45 m breit, Fuge 1,2 cm
    for i in range(n):
        u0 = i * L / n; u1 = (i + 1) * L / n - 0.012
        kast(t, HOLZ, (X(u0), -hb, DECK - 0.08), (X(u1), hb, DECK), BOHLEN, laengs="y", ohne=("-z",))
    for i in range(int(L / 0.9) + 1):                 # Querbalken unter den Bohlen
        u = min(i * 0.9 + 0.1, L - 0.1)
        kast(t, HOLZ, (X(u - 0.1), -hb + 0.25, DECK - 0.33), (X(u + 0.1), hb - 0.25, DECK - 0.08), BALKEN, ohne=("+z",))
    for sy in (-1, 1):                                # Wangen und ihre Eisenbaender
        kast(t, HOLZ, (X(0.0), sy * (hb - 0.25), DECK - 0.6), (X(L), sy * hb, DECK - 0.08), BALKEN)
        for u in (0.1, 1.4, 2.7, 4.0, 5.3):
            kast(t, EISEN, (X(u), sy * (hb - 0.255), DECK - 0.55), (X(u + 0.16), sy * (hb + 0.005), DECK - 0.1), E_BESCHLAG)
    for u in (0.5, 1.9, 3.3, 4.7, 6.0):               # Eisenbaender quer ueber die Bohlen
        kast(t, EISEN, (X(u), -hb - 0.01, DECK), (X(u + 0.1), hb + 0.01, DECK + 0.012), E_BESCHLAG, ohne=("-z",))
    kast(t, EISEN, (X(L - 0.05), -hb - 0.01, DECK - 0.45), (X(L + 0.01), hb + 0.01, DECK + 0.02), E_BESCHLAG)     # Stossbalken mit Eisenschuh
    for sy in (-1, 1):                                # Radabweiser
        kast(t, HOLZ, (X(0.0), sy * (hb - 0.45), DECK), (X(L), sy * (hb - 0.3), DECK + 0.14), BALKEN)
    for sy in (-1, 1):                                # Zapfen der Drehachse
        t.stab(EISEN, V((X(0.0), sy * (hb - 0.5), ZP)), V((X(0.0), sy * (hb + 0.3), ZP)), 0.14, E_BESCHLAG, breite=0.14)
    for sy in (-1, 1):                                # Zugarme mit Oese
        kast(t, HOLZ, (X(K.UA - 0.15), sy * (hb - 0.05), DECK - 0.5), (X(K.UA + 0.15), sy * (K.YK + 0.1), DECK - 0.2), BALKEN)
        t.dreh(EISEN, V((X(K.UA), sy * K.YK, DECK - 0.2)), [(0.0, -0.15), (0.07, -0.15), (0.07, 0.0), (0, 0.0)], E_BESCHLAG, seiten=8)
    for sy in (-1, 1):                                # Gelaender: Eichenpfosten mit Eisenkappe, Handlauf; feine Staebe und Ranke (elbisch)
        y = sy * (hb - 0.12)
        pf = [0.2, 1.85, 3.5, 5.15, L - 0.2]
        for u in pf:
            kast(g, HOLZ, (X(u - 0.09), y - 0.09, DECK), (X(u + 0.09), y + 0.09, DECK + 1.15), BALKEN)
            kast(g, EISEN, (X(u - 0.11), y - 0.11, DECK + 1.15), (X(u + 0.11), y + 0.11, DECK + 1.22), E_FEIN)
        g.stab(HOLZ, V((X(0.2), y, DECK + 1.1)), V((X(L - 0.2), y, DECK + 1.1)), 0.07, BALKEN, breite=0.09, oben=V((0, 0, 1)))
        g.stab(HOLZ, V((X(0.2), y, DECK + 0.14)), V((X(L - 0.2), y, DECK + 0.14)), 0.05, BALKEN, breite=0.07, oben=V((0, 0, 1)))
        u = 0.2
        while u < L - 0.3:
            u += 0.24
            if any(abs(u - p) < 0.17 for p in pf) or u > L - 0.3: continue
            g.stab(EISEN, V((X(u), y, DECK + 0.16)), V((X(u), y, DECK + 1.08)), 0.018, E_FEIN)
        welle(g, EISEN, V((X(0.3), y, DECK + 0.62)), V((X(L - 0.3), y, DECK + 0.62)), 0.17, 30, (0, 0, 1), 0.016)
    return t, g


# ── Beweglich: Gegengewichte, Ketten, Zugwerk ──────────────────────────────────────────────────────────────────────────
def baue_gewicht(s, sy):
    zc = K.ZW - 0.9 - K.GEW[2] / 2
    xc = s * (HALB - K.AW + K.RW)
    yc = sy * K.YK
    t = Teil(f"Gegengewicht_{'L' if s < 0 else 'R'}_{'N' if sy > 0 else 'S'}", BB, saat=50 + int(s) + sy)
    gx, gy, gz = K.GEW
    steinkasten(t, (xc - gx / 2, yc - gy / 2, zc - gz / 2), (xc + gx / 2, yc + gy / 2, zc + gz / 2), baender=STEIN_BAENDER)
    for dz in (-0.38, 0.0, 0.38):
        kast(t, EISEN, (xc - gx / 2 - 0.02, yc - gy / 2 - 0.02, zc + dz - 0.05), (xc + gx / 2 + 0.02, yc + gy / 2 + 0.02, zc + dz + 0.05), E_ROST)
    kast(t, EISEN, (xc - 0.08, yc - 0.03, zc + gz / 2), (xc + 0.08, yc + 0.03, zc + gz / 2 + 0.22), E_ROST)
    return t, V((xc, yc, zc))


def baue_kette(name, n=18, dicke=0.075):
    """Einheitskette: Laenge 1 m entlang -Z, n Glieder (abwechselnd flach/hochkant); Ursprung am oberen Ende."""
    t = Teil(name, BB, saat=60)
    gl = 1.0 / n
    for i in range(n):
        z1 = -i * gl; z0 = -(i + 1) * gl + 0.012
        if i % 2 == 0: kast(t, EISEN, (-dicke / 2, -dicke * 0.3, z0), (dicke / 2, dicke * 0.3, z1), E_ROST)
        else: kast(t, EISEN, (-dicke * 0.3, -dicke / 2, z0), (dicke * 0.3, dicke / 2, z1), E_ROST)
    return t


def baue_zugwerk(s):
    t = Teil(f"Zugwerk_{'L' if s < 0 else 'R'}", BB, saat=70)
    xc = s * (HALB - K.AW)
    for sy in (-1, 1):
        wirf(t, rad_y(rad(EISEN, K.RW, 0.14, 10, band=E_ROST), V((xc, sy * K.YK, K.ZW))))
        wirf(t, rad_y(rad(HOLZ, K.RH, 0.1, 0, band=BALKEN, speichen=8, nabe=0.1, rand=0.08), V((xc, sy * (K.YK + 0.7), K.ZW))))
        t.stab(EISEN, V((xc, sy * (K.YK - 0.38), K.ZW)), V((xc, sy * (K.YK + 0.95), K.ZW)), 0.12, E_ROST)
        kast(t, EISEN, (xc - 0.18, sy * (K.YK + 0.88) - 0.05, K.ZW - 0.06), (xc + 0.18, sy * (K.YK + 0.88) + 0.05, K.ZW + 0.06), E_ROST)
    return t, V((xc, 0.0, K.ZW))


# ── Aufbau ────────────────────────────────────────────────────────────────────────────────────────────────────────────────
def lege_ursprung(obj, punkt):
    obj.data.transform(Matrix.Translation(-V(punkt)))
    obj.location = V(punkt)


def animiere_schranken(objekte):
    """NLA-Spuren Schranke_zu (Baum senkt sich, 2 s) und Schranke_auf (Baum hebt sich) fuer Schranke_L/R."""
    for name, von, bis in (("Schranke_zu", -math.radians(SCHRANKE_AUF), 0.0), ("Schranke_auf", 0.0, -math.radians(SCHRANKE_AUF))):
        for tag in ("L", "R"):
            o = objekte[f"Schranke_{tag}"]
            if o.animation_data is None: o.animation_data_create()
            o.animation_data.action = None
            for frame, w in ((1, von), (49, bis)):
                o.rotation_euler = (w, 0, 0); o.keyframe_insert("rotation_euler", index=0, frame=frame)
            a = o.animation_data.action; a.name = f"{name}_Schranke_{tag}"
            spur = o.animation_data.nla_tracks.new(); spur.name = name
            spur.strips.new(name, 1, a)
            o.animation_data.action = None
    for tag in ("L", "R"): objekte[f"Schranke_{tag}"].rotation_euler = (math.radians(-SCHRANKE_AUF), 0, 0)


def verschmelzen(obj):
    import bmesh
    bm = bmesh.new(); bm.from_mesh(obj.data)
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=0.0005)
    bm.to_mesh(obj.data); bm.free()


def baue_alles(stoffe):
    objekte, zaehl = {}, {}

    def neu(t, ursprung=None):
        o = t.objekt(stoffe, mit_farbe=False); objekte[t.name] = o; zaehl[t.name] = t.dreiecke()
        if ursprung is not None: lege_ursprung(o, ursprung)
        return o
    for s in (-1, 1):
        tag = "L" if s < 0 else "R"
        neu(baue_pfeiler(s))
        th, zi = baue_torhaus(s); neu(th); neu(zi)
        k, g = baue_klappe(s)
        ok = neu(k, (s * HALB, 0, ZP))
        og = neu(g, (s * HALB, 0, ZP))
        og.parent = ok; og.location = (0, 0, 0)          # Netz liegt schon um den Drehpunkt verschoben: relativ zur Klappe im Ursprung
        z, mz = baue_zugwerk(s); neu(z, mz)
        for sy in (-1, 1):
            nm = "N" if sy > 0 else "S"
            gw, mg = baue_gewicht(s, sy); neu(gw, mg)
            kt = baue_kette(f"Kette_{tag}_{nm}"); o = neu(kt); o.location = V((s * (HALB - K.AW - K.RW), sy * K.YK, K.ZW))
            gk = baue_kette(f"Gegenkette_{tag}_{nm}"); o = neu(gk); o.location = V((s * (HALB - K.AW + K.RW), sy * K.YK, K.ZW))
    for s in (-1, 1):
        baum, pfosten, dreh = baue_schranke(s)
        ob = neu(baum, dreh); neu(pfosten)
        ob.rotation_mode = "XYZ"; ob.rotation_euler = (math.radians(-SCHRANKE_AUF), 0, 0)      # in der Datei offen (Baum oben)
    for fn, y in ((baue_bogenfeld, -40), (baue_zwischenpfeiler, -50), (baue_ende, -60)):
        o = neu(fn()); o.location = V((0, y, 0))
    for nm, p in (("Anker_Durchfahrt", (0, 0, 0)), ("Anker_Warte_L", (-K.WARTE_X, 0, DECK)), ("Anker_Warte_R", (K.WARTE_X, 0, DECK)),
                  ("Anker_Schranke_L", (-K.SCHRANKE_X, 0, DECK)), ("Anker_Schranke_R", (K.SCHRANKE_X, 0, DECK))):
        e = bpy.data.objects.new(nm, None); e.location = p; e.empty_display_type = "ARROWS"
        if nm == "Anker_Durchfahrt":              # Masse fuers Spiel (glTF-extras): das Modell richtet sich nach dem Wasserplan, nicht umgekehrt
            for k, v in (("durchfahrt", K.DURCH), ("mittelteil", K.MITTELTEIL), ("deck", K.DECK), ("rampe", K.RAMPE), ("ufer_tief", K.UFER_TIEF),
                         ("feld", K.FELD), ("zwischenpfeiler", K.ZWISCHENPFEILER), ("breite", 2 * BR_Y), ("lichte_breite", K.LICHT_B), ("schranke_auf_winkel", SCHRANKE_AUF), ("schranke_dauer", 2.0)):
                e[k] = float(v)
        bpy.context.scene.collection.objects.link(e); objekte[nm] = e
    return objekte, zaehl


def pose(objekte, theta):
    t = math.radians(theta)
    l0 = K.kinematik(0)[2]
    aA, zA, l1 = K.kinematik(theta)
    hub = l0 - l1
    for s in (-1, 1):
        tag = "L" if s < 0 else "R"
        objekte[f"Klappe_{tag}"].rotation_euler = (0, s * t, 0)
        objekte[f"Zugwerk_{tag}"].rotation_euler = (0, s * hub / K.RW, 0)
        for sy in (-1, 1):
            nm = "N" if sy > 0 else "S"
            aF = K.AW + K.RW
            dx = -s * (aA - aF); dz = zA - K.ZW
            ketten = objekte[f"Kette_{tag}_{nm}"]
            ketten.rotation_euler = (0, math.atan2(-dx, -dz), 0); ketten.scale = (1, 1, l1)
            objekte[f"Gegengewicht_{tag}_{nm}"].location.z = K.ZW - 0.9 - K.GEW[2] / 2 - hub
            objekte[f"Gegenkette_{tag}_{nm}"].scale = (1, 1, 0.9 + hub)


def animiere(objekte):
    szene = bpy.context.scene
    szene.render.fps = 24
    bewegt = [n for n in objekte if n.startswith(("Klappe_", "Kette_", "Gegenkette_", "Gegengewicht_", "Zugwerk_"))]
    glatt = lambda x: x * x * (3 - 2 * x)
    for richtung, name in ((1, "Oeffnen"), (-1, "Schliessen")):
        for n in bewegt:
            o = objekte[n]; o.rotation_mode = "XYZ"
            if o.animation_data is None: o.animation_data_create()
            o.animation_data.action = None
        for i in range(13):
            x = i / 12 if richtung > 0 else 1 - i / 12
            pose(objekte, K.THETA * glatt(x))
            frame = 1 + i * 12
            for n in bewegt:
                o = objekte[n]
                if n.startswith(("Klappe_", "Zugwerk_")): o.keyframe_insert("rotation_euler", index=1, frame=frame)
                elif n.startswith("Kette_"):
                    o.keyframe_insert("rotation_euler", index=1, frame=frame); o.keyframe_insert("scale", index=2, frame=frame)
                elif n.startswith("Gegenkette_"): o.keyframe_insert("scale", index=2, frame=frame)
                else: o.keyframe_insert("location", index=2, frame=frame)
        for n in bewegt:
            o = objekte[n]
            a = o.animation_data.action
            a.name = f"{name}_{n}"
            for fc in a.fcurves:
                for kp in fc.keyframe_points: kp.interpolation = "LINEAR"
            spur = o.animation_data.nla_tracks.new(); spur.name = name
            spur.strips.new(name, 1, a)
            o.animation_data.action = None
    pose(objekte, 0)
    szene.frame_start, szene.frame_end = 1, 145


# ── UV2 und Verdeckung ───────────────────────────────────────────────────────────────────────────────────────────────────
UV2_TEILE = ("Pfeiler_", "Torhaus_", "Klappe_", "Bogenfeld", "Zwischenpfeiler", "Ende")         # nur die grossen Teile bekommen UV2 und Verdeckung


def uv2_und_ao(objekte, rand=0.001, backen=True, kante=AO_KANTE):
    """UV2 (Smart UV Project, Inselrand `rand`, alle Teile in einem Atlas) und die Verdeckung dorthin backen (Cycles, Backrand 1 Bildpunkt, Reichweite
    1,1 m: nur Kontaktschatten). Wie beim Steinhaus (wohnhaus_mensch_s23.uv2_und_ao): schmaler Rand, damit Smart UV nicht ganze Flaechen auf einen
    Bildpunkt presst."""
    import numpy as np
    teile = [o for n, o in objekte.items() if o.type == "MESH" and n.startswith(UV2_TEILE)]
    bpy.ops.object.select_all(action="DESELECT")
    for o in teile:
        uv2 = o.data.uv_layers.new(name="UV2"); o.data.uv_layers.active = uv2
        o.select_set(True)
    bpy.context.view_layer.objects.active = teile[0]
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.uv.smart_project(angle_limit=math.radians(66), island_margin=rand, area_weight=0.0, correct_aspect=True, scale_to_bounds=False)
    bpy.ops.object.mode_set(mode="OBJECT")
    for o in teile: o.data.uv_layers.active_index = 0
    pfad = os.path.join(ZIEL_TEXTUR, AO_DATEI)
    if not backen: return pfad
    bild = bpy.data.images.new("AO", kante, kante, alpha=False)
    bild.colorspace_settings.name = "Non-Color"; bild.generated_color = (1, 1, 1, 1)
    szene = bpy.context.scene
    szene.render.engine = "CYCLES"; szene.cycles.device = "CPU"; szene.cycles.samples = 64
    szene.render.bake.margin = 1; szene.render.bake.use_clear = False
    if szene.world is None: szene.world = bpy.data.worlds.new("Welt")
    szene.world.light_settings.distance = 1.1
    bpy.ops.mesh.primitive_plane_add(size=60, location=(0, 0, -0.5))      # Wasser unter der Bruecke (Verdecker, nicht im Export)
    wasser = bpy.context.active_object; wasser.name = "BackWasser"
    for o in teile:
        for m in o.data.materials:
            kn = m.node_tree.nodes
            ziel = kn.get("AO-Ziel") or kn.new("ShaderNodeTexImage"); ziel.name = "AO-Ziel"; ziel.image = bild; kn.active = ziel
    for o in teile:
        bpy.ops.object.select_all(action="DESELECT")
        o.select_set(True); bpy.context.view_layer.objects.active = o
        o.data.uv_layers.active_index = 1
        bpy.ops.object.bake(type="AO", uv_layer="UV2")
        o.data.uv_layers.active_index = 0
    feld = np.empty(kante * kante * 4, dtype=np.float32); bild.pixels.foreach_get(feld)
    grau = feld.reshape(kante, kante, 4)[..., 0]
    grau = np.clip((grau + np.roll(grau, 1, 0) + np.roll(grau, -1, 0) + np.roll(grau, 1, 1) + np.roll(grau, -1, 1)) / 5, 0, 1)
    ganz = np.ones((kante, kante, 4), dtype=np.float32)
    ganz[..., :3] = grau[..., None]
    atlas = bpy.data.images.new("AO-Atlas", kante, kante, alpha=False)
    atlas.colorspace_settings.name = "Non-Color"; atlas.pixels.foreach_set(ganz.ravel())
    os.makedirs(ZIEL_TEXTUR, exist_ok=True)
    atlas.filepath_raw = pfad; atlas.file_format = "PNG"; atlas.save()
    bpy.data.objects.remove(wasser, do_unlink=True)
    print("geschrieben", pfad)
    return pfad


def exportiere(objekte, datei):
    bpy.ops.object.select_all(action="DESELECT")
    for o in objekte: o.select_set(True)
    os.makedirs(os.path.dirname(datei), exist_ok=True)
    bpy.ops.export_scene.gltf(filepath=datei, export_format="GLB", use_selection=True, export_yup=True, export_apply=False,
                              export_texcoords=True, export_normals=True, export_vertex_color="NONE", export_all_vertex_colors=False,
                              export_image_format="NONE", export_materials="EXPORT", export_extras=True, export_cameras=False, export_lights=False,
                              export_animations=True, export_animation_mode="NLA_TRACKS", export_force_sampling=True, export_frame_range=True,
                              export_nla_strips_merged_animation_name="Klappbruecke")
    print("geschrieben", datei, os.path.getsize(datei), "Bytes")


# ── Kontrollbilder ───────────────────────────────────────────────────────────────────────────────────────────────────────
def figur(name, hoehe, farbe, ort, breite=0.25):
    m = bpy.data.materials.new(name); m.use_nodes = True
    next(n for n in m.node_tree.nodes if n.type == "BSDF_PRINCIPLED").inputs["Base Color"].default_value = (*farbe, 1)
    bpy.ops.mesh.primitive_cylinder_add(radius=breite, depth=hoehe * 0.80, location=(ort[0], ort[1], ort[2] + hoehe * 0.40))
    k = bpy.context.object; k.name = name; k.data.materials.append(m)
    bpy.ops.mesh.primitive_uv_sphere_add(radius=hoehe * 0.075, location=(ort[0], ort[1], ort[2] + hoehe * 0.905))
    h = bpy.context.object; h.name = name + "_Kopf"; h.data.materials.append(m)


def szene_kontrolle(offen, mit_schiff=True, mit_figuren=True, ufer=None):
    szene = bpy.context.scene
    glb = os.path.join(WURZEL, "game", "assets", "modelle")
    w = bpy.data.materials.new("Wasser"); w.use_nodes = True
    b = next(n for n in w.node_tree.nodes if n.type == "BSDF_PRINCIPLED")
    b.inputs["Base Color"].default_value = (0.10, 0.18, 0.20, 1); b.inputs["Roughness"].default_value = 0.25
    bpy.ops.mesh.primitive_plane_add(size=300, location=(0, 0, 0)); bpy.context.object.data.materials.append(w); bpy.context.object.name = "Kontrollwasser"
    land = bpy.data.materials.new("Land"); land.use_nodes = True
    next(n for n in land.node_tree.nodes if n.type == "BSDF_PRINCIPLED").inputs["Base Color"].default_value = (0.20, 0.17, 0.11, 1)
    strasse = bpy.data.materials.new("Strasse"); strasse.use_nodes = True
    next(n for n in strasse.node_tree.nodes if n.type == "BSDF_PRINCIPLED").inputs["Base Color"].default_value = (0.30, 0.25, 0.17, 1)
    for s in (-1, 1):
        x0 = ufer if ufer is not None else HALB + 5.5                      # Uferkante (Gesamtansicht) bzw. Ende des Pfeilers
        zt = DECK - K.UFER_TIEF if ufer is not None else DECK - 0.05      # Oberkante des Landes: Uferhoehe bzw. Fahrbahn
        bpy.ops.mesh.primitive_cube_add(location=(s * (x0 + 25), 0, zt - 8)); c = bpy.context.object
        c.scale = (25, 150, 8.0); c.data.materials.append(land); c.name = "KontrollLand"
        bpy.ops.mesh.primitive_cube_add(location=(s * (x0 + (K.RAMPE if ufer is not None else 0) + 12), 0, zt + 0.01)); zz = bpy.context.object
        zz.scale = (12, K.KLAPPE_B / 2, 0.02); zz.data.materials.append(strasse); zz.name = "KontrollZufahrt"
    if mit_schiff:
        bpy.ops.import_scene.gltf(filepath=os.path.join(glb, "wasser", "wasser_schiff.glb"))
        ziel = list(bpy.context.selected_objects)
        wurzel = bpy.data.objects.new("Schiff", None); szene.collection.objects.link(wurzel)
        for o in ziel:
            if o.parent is None: o.parent = wurzel
        wurzel.rotation_euler = (0, 0, math.radians(90))
        wurzel.location = (0, -6 if offen else -30, 0)
        for o in ziel:
            if o.type == "MESH" and any(k in o.name.lower() for k in ("verdraenger", "stange")): o.hide_render = True
    if mit_figuren:
        bpy.ops.import_scene.gltf(filepath=os.path.join(glb, "karren", "karren_pferd.glb"))
        kz = list(bpy.context.selected_objects)
        kw = bpy.data.objects.new("KarrenWurzel", None); szene.collection.objects.link(kw)
        for o in kz:
            if o.parent is None: o.parent = kw
        kw.rotation_euler = (0, 0, math.radians(-90))
        kw.location = (HALB + 5.5 + 11.0, -1.55, DECK - 0.01)
        fx = HALB + 6.5
        figur("Mensch_1_75", 1.75, (0.70, 0.55, 0.35), (fx, 1.4, DECK))
        figur("Zwerg_1_35", 1.35, (0.55, 0.25, 0.20), (fx, 2.2, DECK), 0.26)
        figur("Elb_1_85", 1.85, (0.30, 0.50, 0.35), (fx, 3.0, DECK), 0.2)
        figur("Oger_2_50", 2.5, (0.50, 0.55, 0.30), (HALB + 4.0 + 1.0, 4.9, DECK), 0.34)
        figur("Mensch_Klappe", 1.75, (0.70, 0.55, 0.35), (-HALB + 2.5, 0.0, DECK) if not offen else (-HALB - 7.0, -1.0, DECK))
    sonne = bpy.data.objects.new("Sonne", bpy.data.lights.new("Sonne", "SUN")); sonne.data.energy = 3.4
    sonne.rotation_euler = (math.radians(50), 0, math.radians(-35)); szene.collection.objects.link(sonne)
    szene.world = szene.world or bpy.data.worlds.new("Welt"); szene.world.use_nodes = True
    hg = next((n for n in szene.world.node_tree.nodes if n.type == "BACKGROUND"), None) or szene.world.node_tree.nodes.new("ShaderNodeBackground")
    if not any(l.to_node.type == "OUTPUT_WORLD" for l in szene.world.node_tree.links):
        ausg = next((n for n in szene.world.node_tree.nodes if n.type == "OUTPUT_WORLD"), None) or szene.world.node_tree.nodes.new("ShaderNodeOutputWorld")
        szene.world.node_tree.links.new(hg.outputs[0], ausg.inputs[0])
    hg.inputs[0].default_value = (0.62, 0.68, 0.76, 1); hg.inputs[1].default_value = 1.3
    szene.render.engine = "CYCLES"; szene.cycles.device = "CPU"; szene.cycles.samples = 40; szene.cycles.use_denoising = True
    szene.render.image_settings.file_format = "PNG"; szene.render.film_transparent = False


def gesamtbruecke(objekte, weite=64.09, pfeiler=(-21.3, -8.5, 8.5, 21.3)):
    """Die ganze Bruecke wie die Stadtansicht sie nach den Pfeilern des Wasserplans (Koeln: Weite 64,09 m, Pfeiler bei -21,3 / -8,5 / 8,5 / 21,3 m von der
    Mitte) zusammensetzt: Enden mit Rampe an den Uferkanten, Bogenfelder (gestreckt) und Zwischenpfeiler. Klappbrueckenlage.Aus im Kern rechnet dasselbe."""
    tor_b = (K.MITTELTEIL - K.DURCH) / 2
    ha = weite / 2
    kopien = []

    def kopie(name, x, drehung=0.0, streck=1.0):
        o = objekte[name].copy(); o.data = objekte[name].data
        bpy.context.scene.collection.objects.link(o)
        o.location = V((x, 0, 0)); o.rotation_euler = (0, 0, drehung); o.scale = (streck, 1, 1)
        kopien.append(o)
    kopie("Ende", -ha); kopie("Ende", ha, math.pi)
    for seite in (-1, 1):
        stellen = [p for p in pfeiler if p * seite > 0 and abs(p) > K.DURCH / 2 + tor_b]
        stellen.sort(key=abs)
        start = ha                                  # Abstand von der Mitte nach aussen: von der Uferkante nach innen
        grenzen = [ha] + [abs(p) + K.ZWISCHENPFEILER / 2 for p in sorted(stellen, key=abs, reverse=True)]
        # Felder: zwischen Uferkante, Zwischenpfeilern und dem Torhaus (Aussenkante bei K.MITTELTEIL / 2)
        pos = sorted([abs(p) for p in stellen], reverse=True)
        kanten = [ha]
        for c in pos: kanten += [c + K.ZWISCHENPFEILER / 2, c - K.ZWISCHENPFEILER / 2]
        kanten.append(K.MITTELTEIL / 2)
        for i in range(0, len(kanten), 2):
            a, b = kanten[i], kanten[i + 1]
            kopie("Bogenfeld", seite * (a + b) / 2, 0.0, (a - b) / K.FELD)
        for c in pos: kopie("Zwischenpfeiler", seite * c)
    for n in ("Bogenfeld", "Zwischenpfeiler", "Ende"): objekte[n].hide_render = True
    return kopien


def kamera(ort, ziel, brennweite=35, ortho=None):
    szene = bpy.context.scene
    k = bpy.data.objects.get("KK")
    if k is None:
        k = bpy.data.objects.new("KK", bpy.data.cameras.new("KK")); szene.collection.objects.link(k)
    k.location = ort
    k.rotation_euler = (V(ziel) - V(ort)).to_track_quat("-Z", "Y").to_euler()
    k.data.type = "ORTHO" if ortho else "PERSP"
    if ortho: k.data.ortho_scale = ortho
    k.data.lens = brennweite
    szene.camera = k


def rendere(datei, breite=1440, hoehe=810, samples=None):
    szene = bpy.context.scene
    szene.render.resolution_x, szene.render.resolution_y = breite, hoehe
    if samples: szene.cycles.samples = samples
    szene.render.filepath = datei
    bpy.ops.render.render(write_still=True)


def main():
    args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    opt = dict((a[2:].split("=", 1) + ["1"])[:2] for a in args if a.startswith("--"))
    if opt.get("rampe"): K.RAMPE = float(opt["rampe"])        # Gegenprobe-Szenario: laengere Rampe (der Wasserplan soll etwa 12 m geben); das Spiel streckt das Ende nach dem Plan
    bpy.ops.wm.read_factory_settings(use_empty=True)
    if bpy.context.scene.world is None: bpy.context.scene.world = bpy.data.worlds.new("Welt")
    nur_bilder = any(opt.get(k) for k in ("bilder", "folge", "detail", "nah", "gesamt"))
    ao_bild = None
    if nur_bilder and os.path.exists(os.path.join(ZIEL_TEXTUR, AO_DATEI)):
        ao_bild = bpy.data.images.load(os.path.join(ZIEL_TEXTUR, AO_DATEI)); ao_bild.colorspace_settings.name = "Non-Color"
    stoffe = {BRUECKE: stoff(BRUECKE, ao_bild=ao_bild),
              FENSTER: stoff(FENSTER, trim=False, farbe=(0.04, 0.05, 0.06), rauheit=0.2)}
    objekte, zaehl = baue_alles(stoffe)
    for o in objekte.values():
        if o.type == "MESH": verschmelzen(o)
    tri = {n: sum(len(p.vertices) - 2 for p in o.data.polygons) for n, o in sorted(objekte.items()) if o.type == "MESH"}
    print("DREIECKE gesamt:", sum(tri.values()), tri)
    rand = float(opt.get("uv2-rand", 0.001))
    uv2_und_ao(objekte, rand=rand, backen=not (nur_bilder or opt.get("ohne-ao")))
    if opt.get("gesamt"):                       # die ganze Bruecke mit Enden und Rampen (Wasserplan-Pfeiler), geschlossen und offen
        ordner = opt["gesamt"]; os.makedirs(ordner, exist_ok=True)
        gesamtbruecke(objekte)
        ha = 64.09 / 2
        szene_kontrolle(False, mit_schiff=True, mit_figuren=False, ufer=ha)
        glb = os.path.join(WURZEL, "game", "assets", "modelle", "karren", "karren_pferd.glb")
        bpy.ops.import_scene.gltf(filepath=glb)
        kz = list(bpy.context.selected_objects)
        kw = bpy.data.objects.new("KarrenWurzel", None); bpy.context.scene.collection.objects.link(kw)
        for o in kz:
            if o.parent is None: o.parent = kw
        kw.rotation_euler = (0, 0, math.radians(-90)); kw.location = (ha + K.RAMPE + 9.5, -1.55, DECK - K.UFER_TIEF)
        figur("Mensch_1_75", 1.75, (0.70, 0.55, 0.35), (ha + K.RAMPE + 1.5, 2.0, DECK - K.UFER_TIEF))
        pose(objekte, 0)
        for tag, w in (("zu", 0.0), ("auf", -math.radians(SCHRANKE_AUF))):          # Schrankenbaum geschlossen (quer ueber der Fahrbahn) und offen (oben)
            for t in ("L", "R"): objekte[f"Schranke_{t}"].rotation_euler = (w, 0, 0)
            kamera((19.0, -7.5, 4.6), (11.5, 0.3, 3.0), 34); rendere(os.path.join(ordner, f"schranke_{tag}.png"), 1440, 810, samples=40)
        for t in ("L", "R"): objekte[f"Schranke_{t}"].rotation_euler = (-math.radians(SCHRANKE_AUF), 0, 0)
        for tag, th in (("geschlossen", 0), ("offen", K.THETA)):
            pose(objekte, th)
            kamera((-48, -62, 26), (0, 0, 3), 32); rendere(os.path.join(ordner, f"gesamt_{tag}_schraeg.png"), 1440, 810, samples=32)
            kamera((0, -120, 6), (0, 0, 5), 45, ortho=76); rendere(os.path.join(ordner, f"gesamt_{tag}_seite.png"), 1440, 600, samples=24)
            if tag == "geschlossen":
                kamera((ha + 9, -9, 3.6), (ha + 0.5, 0.5, 1.8), 38); rendere(os.path.join(ordner, "ende_rampe_ost.png"), 1440, 810, samples=40)
                kamera((ha + 1.0, -14, 1.9), (ha, 0, 1.9), 40, ortho=9); rendere(os.path.join(ordner, "ende_rampe_seite.png"), 1440, 700, samples=32)
        return
    if opt.get("detail"):
        ordner = opt["detail"]; os.makedirs(ordner, exist_ok=True)
        szene_kontrolle(False)
        kamera((0.5, -15.5, 6.0), (5.8, -4.3, 6.2), 38)
        for tag, th in (("geschlossen", 0), ("offen", K.THETA)):
            pose(objekte, th); rendere(os.path.join(ordner, f"{tag}_ketten_gewicht.png"), 1440, 810, samples=40)
        return
    if opt.get("nah"):                          # Nahaufnahmen der Materialien: Bohlen, Eisen, Quader, Gelaender, Zierwerk, Tuer
        ordner = opt["nah"]; os.makedirs(ordner, exist_ok=True)
        szene_kontrolle(False, mit_schiff=False, mit_figuren=False)
        pose(objekte, 0)
        for name, ort, ziel, lens in (("nah_bohlen_eisen", (-5.2, -4.4, 3.4), (-3.5, -2.6, 2.4), 45), ("nah_quader", (14.5, -9.0, 4.0), (10.5, -4.5, 3.4), 40),
                                      ("nah_gelaender", (-0.6, -2.4, 3.3), (-4.5, -3.4, 3.0), 42), ("nah_torhaus_zierwerk", (4.0, -6.5, 6.5), (7.0, -4.3, 6.0), 40),
                                      ("nah_tuer_holz", (15.0, 1.5, 3.4), (10.5, 4.9, 2.2), 40)):
            kamera(ort, ziel, lens); rendere(os.path.join(ordner, name + ".png"), 1440, 810, samples=48)
        return
    if opt.get("folge"):
        ordner = opt["folge"]; os.makedirs(ordner, exist_ok=True)
        szene_kontrolle(True)
        kamera((-27, -34, 13), (3, 0, 4.5), 35)
        for i in range(10):
            pose(objekte, K.THETA * i / 9); rendere(os.path.join(ordner, f"folge_{i:02d}.png"), 800, 450, samples=20)
        return
    if opt.get("bilder"):
        ordner = opt["bilder"]; os.makedirs(ordner, exist_ok=True)
        for offen in (False, True):
            if offen: pose(objekte, K.THETA)
            szene_kontrolle(offen)
            tag = "offen" if offen else "geschlossen"
            kamera((0, -75, 7), (0, 0, 6), 45, ortho=34); rendere(os.path.join(ordner, f"{tag}_seite.png"))
            kamera((-27, -34, 13), (3, 0, 4.5), 35); rendere(os.path.join(ordner, f"{tag}_schraeg.png"))
            kamera((-1.5, -8.5, 8.5), (7.5, 2.5, 6.8), 32); rendere(os.path.join(ordner, f"{tag}_zugwerk_nah.png"))
            kamera((30, 3.2, 3.0), (10, 3.2, 2.95), 40, ortho=8.5); rendere(os.path.join(ordner, f"{tag}_tuer_figuren.png"))
            kamera((-5, 12, 8), (3, -3, 3.5), 35); rendere(os.path.join(ordner, f"{tag}_von_norden.png"))
            if not offen:
                for o in list(bpy.data.objects):
                    if o.name not in objekte and not o.name.startswith("KK"): bpy.data.objects.remove(o, do_unlink=True)
        return
    animiere(objekte)
    animiere_schranken(objekte)
    ziel = opt.get("glb")
    exportiere(list(objekte.values()), ziel if isinstance(ziel, str) and ziel != "1" else os.path.join(ZIEL_MODELL, NAME + ".glb"))


main()
