"""Kölner Dom, fertig gebaut (Sonderbau, nur für Köln). Belegte Fläche 14 × 34 m.

    blender -b --python tools/modelle/koelner_dom.py -- [--schnell] [--ohne-bilder] [--nur-anker] [--nur-nacht]
    python tools/modelle/koelner_dom.py [--schnell] [--ohne-bilder]

Schreibt game/assets/modelle/gebaeude/koelner_dom.glb und Bilder nach docs/modelle/. Der Dom der übrigen Karten (dom.glb) bleibt.

Alternativgeschichte (Entscheidung des Nutzers, 19.11.2026): Zwerge und Elben haben den Dom schneller vollendet – im Spiel steht der FERTIGE
Dom, ohne Kran, Baustelle, Bretterwand und Notdach. Die elbischen Zutaten bleiben (docs/bereiche/leveldesign.md „Köln-Wahrzeichen“, 27.10.2026):
    Chor        Hochchor und Umgang, sehr hoch, heller, leicht silbriger Stein („Elbenstein“), kupfergrünes Dach, schlanke Fialen,
                Strebewerk mit fliegenden Bögen, Maßwerk mit Ranken, grünlich-gläserne Fenster, Dachreiter
    Langhaus    Mittelschiff in Höhe des Hochchors unter durchgehendem Dach, niedrigere Seitenschiffe mit Pultdach, Strebewerk wie am Chor,
                Querhausarme als Giebel (nur 0,8 m vorspringend, die Fläche ist 14 m breit)
    Westfassade Hauptportal zwischen den Türmen mit gestuftem Gewände, Blendbögen, hohem Maßwerkfenster mit Ranken und Wimperg
    Türme       zwei gleiche, vollendete Türme: Quadratgeschoss mit Maßwerkfenstern, Blendbogenreihen und Eckfialen, darüber das Achteck
                mit acht Fenstern und der steinerne Helm mit Rippen, Wimpergen und Kreuzblume
Alles ist ein Stein: Menschenwerk und Elbenwerk unterscheiden sich nur noch in der Feinheit des Maßwerks.

Objekte im GLB:
    Chor, Langhausdach          (unter „Dach“: beim Hineinsehen aus)
    Wand_links, Wand_rechts     Seitenschiffwand, Hochwand und Strebewerk des Langhauses je Seite (blendet das Spiel nach der Kamera aus)
    Wand_vorne                  Innenseite der Westwand zwischen den Seitenwänden
    Nordturm, Suedturm, Portal  (unter „Turm“: beim Hineinsehen aus)
    Anker_Tuer                  vor dem Hauptportal zwischen den Türmen (Westseite, −Y)
    Anker_EwigesLicht           im Bogen des Hauptportals (wie beim Dom der übrigen Karten)
Keine Zustände und keine Hausfarbe: Der Dom gehört der Stadt (`pruefen.py … 14 34 --sonderbau`).

Aufbau (Blender: Z oben, Westseite mit den Türmen bei −Y, Chor bei +Y, Süden = +X; Ursprung in der Mitte der Fläche):
    y −16,4 … −11,6   zwei Türme (x ±1,6 … ±6,3, 58 m hoch) mit der Portalwand dazwischen
    y −11,6 … 3       Langhaus: Mittelschiff x ±3 (Traufe 28 m), Seitenschiffe bis x ±6 (Traufe 11,5 m), Querhausarme bis x ±6,8
    y 3 … 16          Chor: Hochchor 6 m breit, Traufe 28 m, Dach bis 32 m, Dachreiter bis etwa 41 m; Umgang mit Pultdach;
                      zwölf Strebepfeiler mit Fialen und fliegenden Bögen
"""

import math
import os
import random
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bausteine as bs  # noqa: E402
import dom  # noqa: E402  (Spitzbogen, Gewände)
from bausteine import Matrix, Vector  # noqa: E402

NAME = "koelner_dom"
FLAECHE = (14.0, 34.0)

# Farben nur dieses Modells: der eine Dom-Stein (Drachenfels-Trachyt, warmes Hellgrau; Stoffname „Elbenstein“ für alle Steinteile), grünliches Glas, kupfergrünes Dach des Chors
bs.FARBEN.update({"Elbenstein": (0.76, 0.72, 0.65), "Elbenglas": (0.10, 0.25, 0.21), "Elbendach": (0.42, 0.58, 0.52)})
bs.RAUHEIT.update({"Elbenglas": 0.2})
bs.BIBLIOTHEK_JE_FLAECHE = True              # Material-Bibliothek je Fläche (Material „Farben“)

# ── Maße (Meter) ─────────────────────────────────────────────────────────────
R_H = 3.0                    # Hochchor: Außenfläche der Wand bei x ±3
R_U = 6.0                    # Umgang: Außenfläche der Wand bei x ±6
Y_W = 3.0                    # Westwand des Chors
Y_A = 10.2                   # Beginn der Apsis (Mittelpunkt des Halbkreises)
Z_H = 28.0                   # Traufe des Hochchors
Z_U = 11.5                   # Traufe (Wandoberkante) des Umgangs
Z_UM = 14.5                  # Anschluss des Umgangdachs an den Hochchor
NEIGUNG = 55                 # Dach des Chors
APSIS = (0, 36, 72, 108, 144, 180)     # Ecken der Apsis (Grad von +X)
STATIONEN = (3.5, 5.9, 8.3, Y_A)       # Strebepfeiler der geraden Seiten
STATIONEN_L = (-4.2, -6.6, -9.0)  # Strebepfeiler des Langhauses (zwischen −3,6 und 3,1 liegt das Querhaus)
Y_Q0, Y_Q1 = -3.2, 2.4       # Querhausarme: y-Bereich, x von 6 bis 6,8
Y_L0 = -11.0                 # Westende des Langhauses (Ostkante der Türme)
Y_T0 = -16.4                 # Westkante der Türme (Strebepfeiler ragen bis −17)
H_Q = 34.0                   # Quadratgeschoss der Türme
H_ACHT = 8.0                # Achteck darüber
H_HELM = 15.5                # steinerner Helm: Spitze bei 58 m
SUED = (1.4, 6.4)            # Türme: x-Bereich, 5 m breit, y −16,4 … −11 (Strebepfeiler ragen bis ±6,95)
NORD = (-6.4, -1.4)
STUFEN = ((0.0, 14.0, 0.0), (14.0, 26.0, 0.25), (26.0, H_Q, 0.5))   # Turmstufen: von z, bis z, Einzug an Außen- und Westseite
# Nur noch Vorgaben für kran_bauen (St. Severin und Kölner Stadtbauten rufen es mit eigenen Werten auf); der Dom hat keinen Kran mehr
H_SUED = 13.5
KRAN = Vector((3.95, -14.0, 0.0))


def wm(punkt, phi):
    """Wandsystem an `punkt` (Fuß der Außenfläche) für die Außennormale in Richtung `phi` (Grad von +X, gegen den Uhrzeigersinn)."""
    return Matrix.Translation(Vector(punkt)) @ Matrix.Rotation(math.radians(phi + 90), 4, "Z")


def apsis_punkt(r, w, z=0.0):
    return Vector((r * math.cos(math.radians(w)), Y_A + r * math.sin(math.radians(w)), z))


def gruppe(name, kinder):
    """Leerer Knoten `name` mit den Kindern: Das Spiel blendet beim Hineinsehen „Dach“ und „Turm“ als Ganzes aus (DomBauen)."""
    import bpy
    k = bpy.data.objects.new(name, None)
    k.empty_display_type = "PLAIN_AXES"
    bpy.context.scene.collection.objects.link(k)
    for o in kinder:
        o.parent = k
    return k


def bauen():
    bs.neue_szene()
    st = bs.Stoffe()
    # Ein einheitlicher Quaderstein für den ganzen Bau (Entscheidung des Nutzers, 28.10.2026)
    for alt in ("Haustein", "Bruchstein", "Bruchstein dunkel"):
        st[alt] = st["Elbenstein"]
    chor = bs.Netz("Chor")
    chor_bauen(chor, st)
    wand_l, wand_r, wand_v = bs.Netz("Wand_links"), bs.Netz("Wand_rechts"), bs.Netz("Wand_vorne")
    dach_l, portal = bs.Netz("Langhausdach"), bs.Netz("Portal")
    langhaus_bauen(wand_l, wand_r, wand_v, dach_l, st)
    portal_bauen(portal, st)
    nord, sued = bs.Netz("Nordturm"), bs.Netz("Suedturm")
    turm_fertig(nord, st, NORD[0], NORD[1])
    turm_fertig(sued, st, SUED[0], SUED[1])
    o = {n.name: n.objekt() for n in (chor, wand_l, wand_r, wand_v, dach_l, portal, nord, sued)}
    # Gruppen für die Innenansicht: „Dach“ (Chor und Langhausdach) und „Turm“ (Türme, Portalwand) werden beim Hineinsehen ausgeblendet,
    # dazu die Wände zur Kamera (Wand_vorne = Westwand des Langhauses)
    dach = gruppe("Dach", [o["Chor"], o["Langhausdach"]])
    turm = gruppe("Turm", [o["Nordturm"], o["Suedturm"], o["Portal"]])
    return [dach, turm, o["Wand_links"], o["Wand_rechts"], o["Wand_vorne"],
            bs.anker("Anker_Tuer", (0.0, Y_T0 + 0.15, 0.3)),
            bs.anker("Anker_EwigesLicht", (0.0, Y_T0 + 0.7 - 0.12, 4.3), 180.0)]


# ─────────────────────────────────────────────────────────────────────────────
#  Kleinteile des Elbenwerks
# ─────────────────────────────────────────────────────────────────────────────

def fiale(netz, st, x, y, z0, r, h, stoff="Elbenstein"):
    """Schlanke Fiale: Sockelblock und vierseitige Spitze (ohne Boden)."""
    s = h * 0.2
    netz.quader((x, y, z0 + s / 2), (2 * r, 2 * r, s), st[stoff], ohne=("-z",))
    spitze = Vector((x, y, z0 + h))
    ecken = [Vector((x + sx * r * 0.82, y + sy * r * 0.82, z0 + s)) for sx, sy in ((-1, -1), (1, -1), (1, 1), (-1, 1))]
    for k in range(4):
        a, b = ecken[k], ecken[(k + 1) % 4]
        netz.vieleck_nach([a, b, spitze], (a + b) / 2 - Vector((x, y, z0 + s - 0.6)), st[stoff])


def elbenfenster(netz, st, M, u, v, b, h, glas, ranken=False, stein="Elbenstein", pfosten=1):
    """Hohes Spitzbogenfenster: Glas, Gewände und Mittelpfosten aus `stein`, auf Wunsch Ranken im Bogenfeld
    (zwei Blattbögen und zwei geschwungene Ranken aus dünnen Stäben)."""
    bs.platte(netz, M, dom.spitzbogen(u, v, b, h), -0.012, -0.002, st[glas])
    dom.spitzring(netz, M, u, v, b, h, 0.13, -0.10, -0.004, st[stein])
    k = v + h - b * math.sin(math.radians(60))
    if pfosten:
        netz.quader((u, -0.06, (v + k) / 2 + 0.25), (0.07, 0.1, k - v + 0.5), st[stein], M, ohne=("+y",))
    netz.quader((u, -0.07, v - 0.06), (b + 0.4, 0.14, 0.12), st[stein], M, ohne=("+y",))
    if not ranken:
        return
    oben = v + h - 0.35
    unten = k + 0.15

    def linie(punkte):
        for p, q in zip(punkte, punkte[1:]):
            netz.stab((p[0], -0.05, p[1]), (q[0], -0.05, q[1]), 0.07, 0.08, st[stein], matrix=M, quer=(0, 1, 0),
                      ohne=("+y", "+x", "-x"), mehr=0.06)

    for sg in (-1, 1):                                    # Blattbögen
        linie([(u + sg * 0.3 * b * math.sin(math.pi * i / 4), unten + (oben - unten) * i / 4) for i in range(5)])
        linie([(u + sg * 0.46 * b * (1 - math.cos(math.pi * i / 3)) / 2, k - 0.05 + 0.9 * i / 3) for i in range(4)])   # Ranke


# ─────────────────────────────────────────────────────────────────────────────
#  Chor
# ─────────────────────────────────────────────────────────────────────────────

def chor_bauen(netz, st):
    bays = [(a + b) / 2 for a, b in zip(STATIONEN, STATIONEN[1:])]
    laenge = Y_A - Y_W
    # Hochchor: gerade Wände, Apsis, Gesims, Fenster
    for s in (-1, 1):
        netz.quader((s * (R_H - 0.4), (Y_W + Y_A) / 2, Z_H / 2), (0.8, laenge, Z_H), st["Elbenstein"], ohne=("-z",))
        netz.quader((s * (R_U - 0.3), (Y_W + Y_A) / 2, Z_U / 2), (0.6, laenge, Z_U), st["Elbenstein"], ohne=("-z",))
        netz.quader((s * (R_U + 0.04), (Y_W + Y_A) / 2, 0.45), (0.2, laenge, 0.9), st["Elbenstein"], ohne=("-z",))   # Sockel
        netz.quader((s * (R_H + 0.08), (Y_W + Y_A) / 2, Z_H - 0.3), (0.3, laenge + 0.1, 0.5), st["Elbenstein"], ohne=("-z",))
        for (a_, b_), y in zip(zip(STATIONEN, STATIONEN[1:]), bays):      # Fensterbreite nach dem freien Raum zwischen den Pfeilern (0,8 m breit)
            frei = b_ - a_ - 0.8
            Mw = wm((s * R_H, y, 0), 0 if s > 0 else 180)
            elbenfenster(netz, st, Mw, 0.0, 15.3, min(1.3, frei - 0.32), 11.3, "Fenster", ranken=True)
            Mu = wm((s * R_U, y, 0), 0 if s > 0 else 180)
            elbenfenster(netz, st, Mu, 0.0, 3.0, min(1.1, frei - 0.4), 7.2, "Elbenglas")
    for i in range(5):                                    # Apsis des Hochchors und des Umgangs
        a, b = APSIS[i], APSIS[i + 1]
        for r, h, d in ((R_H, Z_H, 0.8), (R_U, Z_U, 0.6)):
            p, q = apsis_punkt(r, a), apsis_punkt(r, b)
            sehne = (q - p).length
            M = wm((p + q) / 2, (a + b) / 2)
            netz.quader((0, d / 2, h / 2), (sehne, d, h), st["Elbenstein"], M, ohne=("-z",))
            if r == R_H:
                netz.quader((0, -0.08, Z_H - 0.28), (sehne + 0.2, 0.3, 0.5), st["Elbenstein"], M, ohne=("+y", "-z"))
                elbenfenster(netz, st, M, 0.0, 15.3, 1.2, 11.3, "Fenster", ranken=True)
            else:
                netz.quader((0, -0.04, 0.45), (sehne + 0.2, 0.2, 0.9), st["Elbenstein"], M, ohne=("+y", "-z"))
                elbenfenster(netz, st, M, 0.0, 3.0, 1.5, 7.2, "Elbenglas")
    # Dach des Hochchors (kupfergrün) mit Kegel über der Apsis
    drehung = Matrix.Rotation(math.radians(90), 4, "Z")      # lokal x → Welt y
    dach = bs.satteldach(netz, st, Y_W - 0.2, Y_A - 0.15, -R_H, R_H, Z_H, NEIGUNG, traufe=0.2, ortgang=0.15, dicke=0.18,
                         reihe=0.5, stufe=0.03, deckung="Elbendach", kante="Elbendach", first="Elbenstein", first_dicke=0.2,
                         enden="Elbendach", matrix=drehung)
    zf = dach["z_first"]
    rb = R_H + 0.2
    zb = dach["oben_vorn"](-R_H - 0.2)
    spitze = Vector((0, Y_A, zf))
    for i in range(5):
        a, b = apsis_punkt(rb, APSIS[i], zb), apsis_punkt(rb, APSIS[i + 1], zb)
        netz.vieleck_nach([a, b, spitze], (a + b) / 2 - Vector((0, Y_A, zb - 1.0)), st["Elbendach"])
    # Pultdach des Umgangs: Rechtecke an den geraden Seiten, Trapeze um die Apsis
    zl = Z_U + 0.1 - 0.3 * (Z_UM - Z_U) / (R_U - R_H)
    ro = R_U + 0.3
    for s in (-1, 1):
        netz.vieleck_nach([(s * ro, 2.8, zl), (s * ro, Y_A, zl), (s * R_H, Y_A, Z_UM), (s * R_H, 2.8, Z_UM)], (s * 1, 0, 1), st["Elbendach"])
    for i in range(5):
        a, b = APSIS[i], APSIS[i + 1]
        na, nb = apsis_punkt(ro, a, zl), apsis_punkt(ro, b, zl)
        ia, ib = apsis_punkt(R_H, a, Z_UM), apsis_punkt(R_H, b, Z_UM)
        m = (na + nb) / 2
        netz.vieleck_nach([na, nb, ib, ia], Vector((m.x, m.y - Y_A, 0)).normalized() + Vector((0, 0, 1)), st["Elbendach"])
    # Strebewerk: Pfeiler an den geraden Seiten und rund um die Apsis
    for s in (-1, 1):
        for y in STATIONEN:
            strebe(netz, st, Vector((s * R_U, y, 0)), Vector((s, 0, 0)))
    for w in APSIS[1:-1]:
        strebe(netz, st, apsis_punkt(R_U, w), Vector((math.cos(math.radians(w)), math.sin(math.radians(w)), 0)))
    # Dachreiter: schlanke Laterne und Helm über dem Dach
    y_r = 5.0
    z0 = zf - 0.4
    bs.achteck(netz, st, (0, y_r, z0), 0.95, 0.0, 3.6, "Elbenstein")
    bs.achteck(netz, st, (0, y_r, z0), 1.05, 0.0, 0.3, "Elbenstein")
    bs.achteck(netz, st, (0, y_r, z0), 1.05, 3.4, 3.7, "Elbenstein")
    for k in range(8):                                    # schmale Öffnungen der Laterne (Seiten des Achtecks bei 0°, 45° …)
        w = 45 * k
        M = wm((0.878 * math.cos(math.radians(w)), y_r + 0.878 * math.sin(math.radians(w)), z0), w)
        bs.platte(netz, M, dom.spitzbogen(0.0, 0.8, 0.45, 2.3), -0.012, -0.002, st["Elbenglas"])
    for sx, sy in ((-1, -1), (1, -1), (1, 1), (-1, 1)):
        fiale(netz, st, sx * 0.9, y_r + sy * 0.9, z0 + 3.5, 0.2, 3.0)
    bs.turmhelm(netz, st, Vector((0, y_r, 0)), 0.9, z0 + 3.7, 9.0, stoff="Elbendach", n=8)
    # Fialen auf dem Gesims des Hochchors an den Ecken der Apsis
    for w in APSIS[1:-1]:
        p = apsis_punkt(R_H + 0.15, w)
        fiale(netz, st, p.x, p.y, Z_H - 0.6, 0.3, 3.8)


def strebe(netz, st, basis, richt):
    """Strebepfeiler an der Umgangwand `basis` (Fuß der Außenfläche) nach außen `richt`: zwei Absätze, Fiale, fliegender Bogen
    zum Pfeilerstumpf an der Hochchorwand und dessen Fiale."""
    w = math.atan2(richt.y, richt.x)
    T = Matrix.Translation(basis) @ Matrix.Rotation(w, 4, "Z")          # x nach außen, y quer
    T3 = T.to_3x3()
    quer = T3 @ Vector((0, 1, 0))
    netz.quader((0.4, 0, 6.1), (1.0, 0.8, 12.2), st["Elbenstein"], T, ohne=("-z",))
    netz.quader((0.4, 0, 12.15), (1.05, 0.9, 0.12), st["Elbenstein"], T)
    netz.quader((0.5, 0, 14.9), (0.8, 0.62, 5.5), st["Elbenstein"], T, ohne=("-z",))
    netz.quader((0.5, 0, 17.7), (0.95, 0.76, 0.12), st["Elbenstein"], T)
    f = T @ Vector((0.5, 0, 17.76))
    fiale(netz, st, f.x, f.y, f.z, 0.32, 4.6)
    # Stumpf an der Hochchorwand (liegt um R_U − R_H weiter innen) mit Fiale
    d = R_U - R_H
    netz.quader((-d + 0.55, 0, 22.6), (1.1, 0.8, 9.0), st["Elbenstein"], T, ohne=("-z",))
    netz.quader((-d + 0.55, 0, 27.15), (1.25, 0.94, 0.12), st["Elbenstein"], T)
    f = T @ Vector((-d + 0.55, 0, 27.2))
    fiale(netz, st, f.x, f.y, f.z, 0.34, 4.2)
    # fliegender Bogen vom Pfeiler (x ≈ 0,1) zum Stumpf (x ≈ −d + 1,1), steigt zuerst steil und läuft flach in den Stumpf
    a, b = Vector((0.12, 0, 15.2)), Vector((-d + 1.05, 0, 21.4))
    n = 5
    punkte = []
    for i in range(n + 1):
        s = i / n
        punkte.append(Vector((a.x + (b.x - a.x) * s, 0, a.z + (b.z - a.z) * math.sin(math.pi * s / 2))))
    for p, q in zip(punkte, punkte[1:]):
        netz.stab(T @ p, T @ q, 0.5, 0.42, st["Elbenstein"], quer=quer, ohne=("+x", "-x"), mehr=0.12)
    netz.stab(T @ (punkte[0] + Vector((0, 0, -0.3))), T @ (punkte[-1] + Vector((0.0, 0, -1.3))), 0.18, 0.2, st["Elbenstein"],
              quer=quer, ohne=("+x", "-x"))                      # Stütze unter dem Bogen


# ─────────────────────────────────────────────────────────────────────────────
#  Langhaus und Querhausarme
# ─────────────────────────────────────────────────────────────────────────────

def arkade(netz, st, s_, laenge, ym):
    """Hochwand des Langhauses mit offener Arkade zu den Seitenschiffen (Innenraum des Spiels: Mittel- und Seitenschiffe als ein Raum):
    unten Pfeiler und Spitzbogenöffnungen (Scheitel 9,6 m), darüber die geschlossene Wand mit den Obergadenfenstern. Von innen gerechnet
    liegt die Westwand bei y −10,55, die Chorwand bei y 3."""
    vt, h_bogen = 10.5, 9.6
    elbenstein = st["Elbenstein"]
    x = s_ * (R_H - 0.4)
    netz.quader((x, (Y_L0 + Y_L0 + 0.45) / 2, vt / 2), (0.8, 0.45, vt), elbenstein, ohne=("-z",))      # Wandstück vor der Westwand
    netz.quader((x, ym, (vt + Z_H) / 2), (0.8, laenge, Z_H - vt), elbenstein)                           # geschlossene Wand darüber
    rand = [Y_L0 + 0.45]
    pfeiler = [-8.08, -5.21, -2.34, 0.53]
    for y in pfeiler:
        netz.quader((x, y, vt / 2), (0.8, 0.8, vt), elbenstein, ohne=("-z",))
    grenzen = [(rand[0], pfeiler[0] - 0.4)] + [(a + 0.4, b - 0.4) for a, b in zip(pfeiler, pfeiler[1:])] + [(pfeiler[-1] + 0.4, Y_W)]
    for a, b in grenzen:                                          # Zwickel über jeder Öffnung (Spitzbogen ausgespart)
        w = b - a
        Mw = wm((s_ * R_H, (a + b) / 2, 0), 0 if s_ > 0 else 180)
        bogen = dom.spitzbogen(0.0, 0.0, w, h_bogen)
        bs.platte(netz, Mw, [(-w / 2, vt), (w / 2, vt)] + bogen[2:], 0.0, 0.8, elbenstein)


def langhaus_bauen(wand_l, wand_r, wand_v, dach, st):
    """Langhaus von der Ostkante der Türme bis zum Chor, aufgeteilt in die Teile, die das Spiel beim Hineinsehen einzeln ausblendet:
    Wand_links/Wand_rechts (Seitenschiffwand, Hochwand mit Fenstern, Strebewerk, Querhausarm), Wand_vorne (Westwand zwischen den
    Seitenwänden) und Langhausdach (Satteldach des Mittelschiffs, Pultdächer der Seitenschiffe, Giebeldächer der Querhausarme)."""
    ym, laenge = (Y_L0 + Y_W) / 2, Y_W - Y_L0
    bays = [(a + b) / 2 for a, b in zip(STATIONEN_L, STATIONEN_L[1:])] + [(STATIONEN_L[-1] - 0.4 + Y_L0) / 2]   # letzte Achse zwischen Pfeiler und Turm
    yq = (Y_Q0 + Y_Q1) / 2
    elbenstein = st["Elbenstein"]
    for s_, netz in ((-1, wand_l), (1, wand_r)):
        arkade(netz, st, s_, laenge, ym)                                                                    # Hochwand mit offener Arkade
        netz.quader((s_ * (R_U - 0.3), ym, Z_U / 2), (0.6, laenge, Z_U), elbenstein, ohne=("-z",))
        netz.quader((s_ * (R_U + 0.04), ym, 0.45), (0.2, laenge, 0.9), elbenstein, ohne=("-z",))                  # Sockel
        netz.quader((s_ * (R_H + 0.08), ym, Z_H - 0.3), (0.3, laenge + 0.1, 0.5), elbenstein, ohne=("-z",))        # Gesims
        for y in bays + [yq + 0.05]:                              # Obergaden: wie am Hochchor, über dem Querhaus ein Fenster mehr
            Mw = wm((s_ * R_H, y, 0), 0 if s_ > 0 else 180)
            elbenfenster(netz, st, Mw, 0.0, 15.3, 1.3, 11.3, "Fenster", ranken=True)
        for y in bays:                                            # Seitenschiffe
            Mu = wm((s_ * R_U, y, 0), 0 if s_ > 0 else 180)
            elbenfenster(netz, st, Mu, 0.0, 3.0, 1.1, 7.2, "Elbenglas")
        for y in STATIONEN_L:                                     # Strebewerk wie am Chor
            strebe(netz, st, Vector((s_ * R_U, y, 0)), Vector((s_, 0, 0)))
        # Querhausarm: Giebelwand mit hohem Fenster, springt nur 0,8 m vor (die Fläche ist 14 m breit)
        netz.quader((s_ * 6.4, yq, Z_U / 2), (0.8, Y_Q1 - Y_Q0, Z_U), elbenstein, ohne=("-z",))
        netz.quader((s_ * 6.84, yq, 0.45), (0.2, Y_Q1 - Y_Q0 + 0.2, 0.9), elbenstein, ohne=("-z",))
        Ma = wm((s_ * 6.8, yq, 0), 0 if s_ > 0 else 180)
        elbenfenster(netz, st, Ma, 0.0, 1.5, 2.2, 8.2, "Elbenglas", ranken=True)
        x0, x1 = (5.9, 6.65) if s_ > 0 else (-6.65, -5.9)
        arm = bs.satteldach(dach, st, x0, x1, Y_Q0, Y_Q1, Z_U, NEIGUNG, traufe=0.2, ortgang=0.15, dicke=0.18, reihe=0.5, stufe=0.03,
                            deckung="Elbendach", kante="Elbendach", first="Elbenstein", first_dicke=0.2, enden="Elbenstein")
        fiale(dach, st, s_ * 6.75, yq, arm["z_first"] - 0.1, 0.2, 2.4)
        # Pultdach des Seitenschiffs
        zl = Z_U + 0.1 - 0.3 * (Z_UM - Z_U) / (R_U - R_H)
        dach.vieleck_nach([(s_ * (R_U + 0.3), Y_L0 + 0.05, zl), (s_ * (R_U + 0.3), Y_W - 0.2, zl),
                           (s_ * R_H, Y_W - 0.2, Z_UM), (s_ * R_H, Y_L0 + 0.05, Z_UM)], (s_ * 1, 0, 1), st["Elbendach"])
    # Satteldach des Mittelschiffs, endet 0,15 m vor dem Dach des Chors (das beginnt bei Y_W − 0,35)
    drehung = Matrix.Rotation(math.radians(90), 4, "Z")
    bs.satteldach(dach, st, Y_L0 + 0.15, Y_W - 0.5, -R_H, R_H, Z_H, NEIGUNG, traufe=0.2, ortgang=0.15, dicke=0.18,
                  reihe=0.5, stufe=0.03, deckung="Elbendach", kante="Elbendach", first="Elbenstein", first_dicke=0.2,
                  enden="Elbenstein", matrix=drehung)
    # Westwand des Langhauses zwischen den Türmen (von außen verdeckt, im Einblick zu sehen)
    wand_v.quader((0, Y_L0 + 0.2, Z_H / 2), (2 * (R_H - 0.4), 0.5, Z_H), elbenstein, ohne=("-z",))
    for s_ in (-1, 1):
        wand_v.quader((s_ * 4.25, Y_L0 + 0.2, Z_U / 2), (2.9, 0.5, Z_U), elbenstein, ohne=("-z",))


# ─────────────────────────────────────────────────────────────────────────────
#  Hauptportal zwischen den Türmen
# ─────────────────────────────────────────────────────────────────────────────

def portal_bauen(netz, st):
    """Westfassade zwischen den Türmen: Portalwand (2,8 m breit, 0,7 m hinter den Turmflächen) mit Spitzbogenportal, gestuftem Gewände,
    Blendbögen, hohem Maßwerkfenster mit Ranken, Gesims und Wimperg mit Fialen und Kreuzblume."""
    yp = Y_T0 + 0.7
    elbenstein = st["Elbenstein"]
    netz.quader((0, (yp + Y_L0) / 2, Z_H / 2), (2.9, Y_L0 - yp, Z_H), elbenstein, ohne=("-z",))
    Mp = bs.wandmatrix((0, yp, 0), "vorne")
    bs.platte(netz, Mp, dom.spitzbogen(0.0, 0.0, 1.7, 5.0), -0.03, -0.002, st["Holz alt"])
    dom.spitzring(netz, Mp, 0.0, 0.0, 1.7, 5.0, 0.2, -0.16, -0.004, elbenstein)
    dom.spitzring(netz, Mp, 0.0, 0.0, 2.1, 5.9, 0.18, -0.30, -0.10, elbenstein)
    for v in (0.9, 2.1, 3.2, 4.0):
        netz.quader((0, -0.06, v), (1.4, 0.03, 0.09), st["Eisen"], Mp, ohne=("+y",))
    netz.quader((0, -0.25, 0.075), (2.6, 0.5, 0.15), elbenstein, Mp, ohne=("+y", "-z"))
    for u in (-0.8, 0.0, 0.8):                                    # Blendbögen über dem Portal
        elbenfenster(netz, st, Mp, u, 7.6, 0.6, 2.4, "Elbenglas", stein="Elbenstein", pfosten=0)
    elbenfenster(netz, st, Mp, 0.0, 11.0, 1.6, 14.0, "Elbenglas", ranken=True)
    netz.quader((0, -0.1, Z_H - 0.25), (2.9, 0.3, 0.5), elbenstein, Mp, ohne=("+y", "-z"))      # Gesims
    bs.platte(netz, Mp, [(-1.45, Z_H), (1.45, Z_H), (0.0, Z_H + 1.8)], -0.2, 1.0, elbenstein)  # Wimperg
    for x in (-1.0, 1.0):
        fiale(netz, st, x, yp + 0.1, Z_H, 0.25, 3.4)
    fiale(netz, st, 0.0, yp + 0.4, Z_H + 1.7, 0.2, 2.2)


# ─────────────────────────────────────────────────────────────────────────────
#  Türme
# ─────────────────────────────────────────────────────────────────────────────

def turm_pfeiler(netz, st, cx, sx, hoehe, koerper, kappe, fiale_r, fiale_h, fiale_stoff):
    """Strebepfeilerpaar an der westlichen Außenecke (cx = x der Ecke, sx = Richtung nach außen): je einer an der Gassenseite und an
    der Außenseite, zwei Absätze, Pultkappe und Fiale."""
    y0 = Y_T0
    for art in ("west", "seite"):
        for k, (b, tief, h) in enumerate(((0.8, 0.55, hoehe * 0.62), (0.6, 0.35, hoehe * 0.85))):
            if art == "west":
                netz.quader((cx - sx * 0.45, y0 - tief / 2 + 0.05, h / 2), (b, tief + 0.1, h), st[koerper], ohne=("-z",))
            else:
                netz.quader((cx + sx * (tief / 2 - 0.05), y0 + 0.45, h / 2), (tief + 0.1, b, h), st[koerper], ohne=("-z",))
        hh = hoehe * 0.85
        if art == "west":
            mitte = Vector((cx - sx * 0.45, y0 - 0.2, hh))
            netz.quader(mitte + Vector((0, 0, 0.07)), (0.74, 0.5, 0.14), st[kappe])
        else:
            mitte = Vector((cx + sx * 0.2, y0 + 0.45, hh))
            netz.quader(mitte + Vector((0, 0, 0.07)), (0.5, 0.74, 0.14), st[kappe])
        fiale(netz, st, mitte.x, mitte.y, hh + 0.14, fiale_r, fiale_h, fiale_stoff)


def turm_aussen(netz, st, x0, x1, hoehe, hohl, krone, koerper, band, rahmen, zier, baender=None):
    """Gotischer Turm: Sockel, Gesimsbänder (`baender`: Höhen, sonst bei 36 und 70 Prozent), Eckstrebepfeiler, spitzbogige Maßwerkfenster
    und, wenn `krone` > 0, eine zerbrochene Mauerkrone (Steine bis `krone` Meter hoch). `zier`: elbische Zierde (Blendbogenreihe, Band,
    Fialen, Fenster mit Ranken); über 25 m Höhe kommen zwei weitere Fensterreihen dazu."""
    y0, y1 = Y_T0, Y_L0
    xm, ym = (x0 + x1) / 2, (y0 + y1) / 2
    if hohl:
        t = 1.2
        netz.quader((xm, y0 + t / 2, hoehe / 2), (x1 - x0, t, hoehe), st[koerper], ohne=("-z",))
        netz.quader((xm, y1 - t / 2, hoehe / 2), (x1 - x0, t, hoehe), st[koerper], ohne=("-z",))
        netz.quader((x0 + t / 2, ym, hoehe / 2), (t, y1 - y0 - 2 * t, hoehe), st[koerper], ohne=("-z",))
        netz.quader((x1 - t / 2, ym, hoehe / 2), (t, y1 - y0 - 2 * t, hoehe), st[koerper], ohne=("-z",))
    else:
        netz.quader((xm, ym, hoehe / 2), (x1 - x0, y1 - y0, hoehe), st[koerper], ohne=("-z",))

    def ring(z, h, vor, stoff):
        """Band um den Turm, `vor` Meter vor der Wand."""
        d = 2 * vor
        netz.quader((xm, y0, z), (x1 - x0 + d, d, h), st[stoff], ohne=("-z",))
        netz.quader((xm, y1, z), (x1 - x0 + d, d, h), st[stoff], ohne=("-z",))
        netz.quader((x0, ym, z), (d, y1 - y0 - d, h), st[stoff], ohne=("-z",))
        netz.quader((x1, ym, z), (d, y1 - y0 - d, h), st[stoff], ohne=("-z",))

    ring(0.55, 1.1, 0.1, "Bruchstein dunkel")
    for z_band in (baender or (hoehe * 0.36, hoehe * 0.7)):
        ring(z_band, 0.3, 0.08, band)
    sued = x0 > 0
    sx = 1 if sued else -1
    cx = x1 if sued else x0
    turm_pfeiler(netz, st, cx, sx, hoehe, koerper if not zier else koerper, band,
                 0.3 if zier else 0.24, 3.0 if zier else 1.5, "Elbenstein" if zier else band)
    west = wm((xm, y0, 0), -90)
    aussen = wm((cx, ym, 0), 0 if sued else 180)
    innen = wm((x0 if sued else x1, ym, 0), 180 if sued else 0)
    glas = "Glas"
    # Westseite (Gasse): zwei Lanzetten unten, darüber hohes Maßwerkfenster, beim Nordturm ein zweites mit Ranken
    for u in (-1.05, 1.05):
        elbenfenster(netz, st, west, u, 2.0, 0.8, 3.2, glas, stein=rahmen, pfosten=0)
    elbenfenster(netz, st, west, 0.0, 6.0, 1.5, 4.8 if zier else 4.4, glas, stein=rahmen)
    # Außenseite und Innenseite zum Portal: Lanzette unten, Fenster darüber
    elbenfenster(netz, st, aussen, 0.0, 2.2, 0.9, 3.4, glas, stein=rahmen, pfosten=0)
    elbenfenster(netz, st, aussen, 0.0, 6.4, 1.3, 4.2, glas, stein=rahmen)
    if krone or not zier:                                      # Innenseiten liegen am Portal: nur beim Turmstumpf Fenster unten
        elbenfenster(netz, st, innen, 0.0, 2.2, 0.8, 3.2, glas, stein=rahmen, pfosten=0)
        elbenfenster(netz, st, innen, 0.0, 6.6, 0.9, 3.6, glas, stein=rahmen, pfosten=0)
    if zier:
        for M in (west, aussen, innen):
            if not krone and M is innen:                      # hinter der Portalwand erst ab 30 m sichtbar
                continue
            elbenfenster(netz, st, M, 0.0, 11.6, 1.3, 5.0, "Elbenglas", ranken=(M is west), stein="Elbenstein")
            for u in (-1.35, 0.0, 1.35):                  # Blendbogenreihe unter dem Rand
                elbenfenster(netz, st, M, u, 17.5, 0.9, 2.0, "Elbenglas", stein="Elbenstein", pfosten=0)
        if hoehe > 25:                                        # zwei weitere Reihen: Fenster bei 22 m und 30,8 m
            for M in (west, aussen, innen):
                if krone or M is not innen:
                    elbenfenster(netz, st, M, 0.0, 22.0, 1.3, 6.2, "Elbenglas", ranken=(M is west), stein="Elbenstein")
                elbenfenster(netz, st, M, 0.0, 30.8, 1.3, 5.0, "Elbenglas", stein="Elbenstein")
        ring(hoehe - 0.7, 0.45, 0.1, "Elbenstein")
        for qx in (x0 + 0.5, x1 - 0.5):
            for qy in (y0 + 0.5, y1 - 0.5):
                fiale(netz, st, qx, qy, hoehe - 0.45, 0.28, 3.4)
    if not krone:
        return
    # Mauerkrone: Steine in unterschiedlicher Höhe, die Mauer ist nie fertig geworden
    rnd = random.Random(5 if hohl else 9)
    n = 5
    rand = []
    for k in range(n):
        f = (k + 0.5) / n
        rand += [(x0 + 0.6 + f * (x1 - x0 - 1.2), y0 + 0.6), (x0 + 0.6 + f * (x1 - x0 - 1.2), y1 - 0.6)]
        g = y0 + 0.6 + f * (y1 - y0 - 1.2)
        rand += [(x0 + 0.6, g), (x1 - 0.6, g)]
    for (px, py) in rand:
        h = 0.3 + rnd.random() * (krone - 0.3)
        netz.quader((px, py, hoehe + h / 2 - 0.05), (0.85, 0.85, h), st[koerper] if rnd.random() < 0.6 else st[band], ohne=("-z",))


def turm_gestuft(netz, st, x0, x1):
    """Gestufter Turmunterbau (Westfassade nach Fassadenriss F): drei Stufen (`STUFEN`), jede an der Außen- und der Westseite eingezogen,
    mit Gesims, Maßwerkfenstern und Strebepfeilern an der Westecke außen. Die Fenster liegen zwischen den Pfeilern (Pfeiler 0,8 m breit an
    der Ecke, das nächste Fenster endet deutlich davor). Gibt die Mitte der obersten Stufe (x, y) zurück."""
    sued = x0 > 0
    sx = 1 if sued else -1
    cx = x1 if sued else x0                      # Außenkante der untersten Stufe
    xin = x0 if sued else x1                     # Seite zum Portal
    y0, y1 = Y_T0, Y_L0
    el = st["Elbenstein"]
    letzte = None
    for k, (z0, z1, e) in enumerate(STUFEN):
        xo = cx - sx * e
        ya = y0 + e
        xa, xb = sorted((xin, xo))
        xm, ym = (xa + xb) / 2, (ya + y1) / 2
        netz.quader((xm, ym, (z0 + z1) / 2), (xb - xa, y1 - ya, z1 - z0), el, ohne=("-z",))
        g = 0.12                                  # Gesims über der Stufe, nach außen und Westen vorkragend
        ga, gb = (xa, xb + g) if sued else (xa - g, xb)
        netz.quader(((ga + gb) / 2, (ya - g + y1) / 2, z1 - 0.2), (gb - ga, y1 - ya + g, 0.4), el, ohne=("-z",))
        west = wm((xm, ya, 0), -90)
        aussen = wm((xo, ym, 0), 0 if sued else 180)
        innen = wm((xin, ym, 0), 180 if sued else 0)
        if k == 0:
            netz.quader(((ga + gb) / 2, (ya - 0.1 + y1) / 2, 0.5), (gb - ga, y1 - ya + 0.1, 1.0), el, ohne=("-z",))   # Sockel
            for u in (-0.95, 0.95):
                elbenfenster(netz, st, west, u, 2.0, 0.7, 3.4, "Elbenglas", stein="Elbenstein", pfosten=0)
            elbenfenster(netz, st, west, 0.0, 6.4, 1.5, 6.0, "Elbenglas", stein="Elbenstein")
            elbenfenster(netz, st, aussen, 0.0, 2.0, 0.9, 3.4, "Elbenglas", stein="Elbenstein", pfosten=0)
            elbenfenster(netz, st, aussen, 0.0, 6.4, 1.5, 6.0, "Elbenglas", stein="Elbenstein")
        elif k == 1:
            for u in (-1.05, 1.05):
                elbenfenster(netz, st, west, u, 15.6, 1.0, 8.2, "Elbenglas", ranken=True, stein="Elbenstein")
            elbenfenster(netz, st, aussen, 0.0, 15.6, 1.3, 8.2, "Elbenglas", stein="Elbenstein")
        else:
            for M in (west, aussen, innen):       # innen sieht man erst über der Portalwand (28 m)
                elbenfenster(netz, st, M, 0.0, 29.0 if M is innen else 27.4, 1.3, 3.8 if M is innen else 5.6, "Elbenglas", ranken=(M is west), stein="Elbenstein")
            for qx in (xa + 0.45, xb - 0.45):
                for qy in (ya + 0.45, y1 - 0.45):
                    fiale(netz, st, qx, qy, z1 + 0.05, 0.28, 3.4)
        letzte = (xm, ym)
    turm_pfeiler(netz, st, cx, sx, H_Q - 4.0, "Haustein", "Bruchstein", 0.3, 3.0, "Elbenstein")
    return letzte


def turm_fertig(netz, st, x0, x1):
    """Vollendeter Turm (Nord- und Südturm gleich): Quadratgeschoss `H_Q`, darüber das Achteck mit acht Fenstern und der steinerne Helm
    mit Rippen, vier Wimpergen, Eckfialen und Kreuzblume. Die Spitzbögen der Turmfenster haben 4 statt 6 Teile je Bogen (spart Dreiecke;
    aus der Ferne nicht zu sehen)."""
    standard = dom.spitzbogen.__defaults__
    dom.spitzbogen.__defaults__ = (4,)
    try:
        _turm_fertig(netz, st, x0, x1)
    finally:
        dom.spitzbogen.__defaults__ = standard


def _turm_fertig(netz, st, x0, x1):
    xm, ym = turm_gestuft(netz, st, x0, x1)
    z_acht = H_Q + H_ACHT
    r = 2.45
    ap = r * math.cos(math.radians(22.5))                         # Mittelpunkt der Achteckseiten
    mitte = (xm, ym, H_Q)
    bs.achteck(netz, st, mitte, 2.75, -0.1, 0.25, "Elbenstein")
    bs.achteck(netz, st, mitte, r, 0.0, H_ACHT - 0.05, "Elbenstein")
    bs.achteck(netz, st, mitte, 2.65, H_ACHT - 0.4, H_ACHT, "Elbenstein")
    for k in range(8):
        w = 45 * k
        M = wm((xm + ap * math.cos(math.radians(w)), ym + ap * math.sin(math.radians(w)), H_Q), w)
        elbenfenster(netz, st, M, 0.0, 1.2, 0.9, 5.0, "Elbenglas", stein="Elbenstein")
        a = math.radians(22.5 + w)
        fiale(netz, st, xm + r * math.cos(a), ym + r * math.sin(a), z_acht, 0.16, 2.4)
    bs.turmhelm(netz, st, Vector((xm, ym, 0)), 2.55, z_acht, H_HELM, stoff="Elbenstein", n=8)
    spitze = Vector((xm, ym, z_acht + H_HELM))
    for k in range(8):                                            # Rippen an den acht Kanten
        a = math.radians(22.5 + 45 * k)
        fuss = Vector((xm + 2.55 * math.cos(a), ym + 2.55 * math.sin(a), z_acht))
        netz.stab(fuss, fuss + (spitze - fuss) * 0.93, 0.16, 0.16, st["Elbenstein"])
    ap_h = 2.55 * math.cos(math.radians(22.5))
    for w in (0, 90, 180, 270):                                   # Wimperge mit Fenster
        M = wm((xm + ap_h * math.cos(math.radians(w)), ym + ap_h * math.sin(math.radians(w)), z_acht), w)
        bs.platte(netz, M, [(-0.8, 0.3), (0.8, 0.3), (0.0, 2.9)], -0.45, 0.5, st["Elbenstein"])
        elbenfenster(netz, st, M @ Matrix.Translation((0, -0.45, 0)), 0.0, 0.5, 0.5, 1.4, "Elbenglas", stein="Elbenstein", pfosten=0)


# ─────────────────────────────────────────────────────────────────────────────
#  Tretradkran auf dem Südturm
# ─────────────────────────────────────────────────────────────────────────────

def kran_bauen(haus, rad, st, kran=None, h_turm=None, haken_z=None):
    """Tretradkran: Kranhaus auf Pfosten, Ausleger nach +Y, zwei Treträder auf einer Welle entlang X. `kran` (Mitte des Kranhauses),
    `h_turm` (Mauerkrone darunter) und `haken_z` (Höhe des Hakens, sonst knapp unter der Mauerkrone) gelten ohne Angabe für den Südturm des
    Doms; St. Severin (koelner_kirchen.py) setzt eigene."""
    holz = st["Holz"]
    kran = KRAN if kran is None else kran
    h_turm = H_SUED if h_turm is None else h_turm
    kx, ky = kran.x, kran.y
    z0 = h_turm + 0.15
    hx, hy = 1.2, 1.2
    fuss = z0 + 1.55                                       # Boden des Kranhauses auf Pfosten
    for sx in (-1, 1):
        for sy in (-1, 1):
            haus.stab((kx + sx * hx, ky + sy * hy, z0), (kx + sx * hx, ky + sy * hy, fuss), 0.28, 0.28, st["Geruestholz"])
    haus.quader((kx, ky, fuss + 0.1), (2 * hx + 0.3, 2 * hy + 0.3, 0.2), holz, ohne=("-z",))
    h = 2.4
    for (cx, cy, br, tf) in ((kx, ky - hy, 2 * hx, 0.12), (kx, ky + hy, 2 * hx, 0.12), (kx - hx, ky, 0.12, 2 * hy), (kx + hx, ky, 0.12, 2 * hy)):
        haus.quader((cx, cy, fuss + 0.2 + h / 2), (br, tf, h), st["Bretter"], ohne=("-z", "+z"))
    for sx in (-1, 1):
        for sy in (-1, 1):
            haus.stab((kx + sx * hx, ky + sy * hy, fuss + 0.2), (kx + sx * hx, ky + sy * hy, fuss + 0.2 + h), 0.2, 0.2, st["Fachwerk"])
    drehung = Matrix.Rotation(math.radians(90), 4, "Z")              # First entlang Y (zum Ausleger)
    ob = fuss + 0.2 + h
    d = bs.satteldach(haus, st, ky - hy, ky + hy, -(kx + hx), -(kx - hx), ob, 38, traufe=0.3, ortgang=0.25, enden="Holz alt", matrix=drehung,
                      **bs.SCHINDEL)
    hoch = hx * d["tan"]
    for y, seite in ((ky - hy - 0.2, "vorne"), (ky + hy + 0.2, "hinten")):
        bs.platte(haus, bs.wandmatrix((kx, y, ob), seite), [(-hx, 0), (hx, 0), (0, hoch)], 0, 0.12, st["Bretter"])
    # Ausleger über das Langhaus: steigt vom Haus bis über die Turmkante, Strebe, Rolle, Seil, Haken, Steinlast
    a = Vector((kx, ky - hy + 0.4, ob + 0.4))
    spitze = Vector((kx, ky + hy + 3.4, ob + 1.9))
    haus.stab(a, spitze, 0.3, 0.26, st["Holz"], quer=(1, 0, 0))
    haus.stab(Vector((kx, ky + hy + 0.1, ob - 0.9)), a.lerp(spitze, 0.7), 0.2, 0.18, st["Holz"], quer=(1, 0, 0))
    for sx in (-1, 1):
        haus.stab(spitze + Vector((sx * 0.17, -0.55, -0.2)), spitze + Vector((sx * 0.17, 0.15, 0.1)), 0.24, 0.05, st["Holz alt"], quer=(1, 0, 0))
    haus.stab((kx - 0.14, spitze.y + 0.05, spitze.z - 0.05), (kx + 0.14, spitze.y + 0.05, spitze.z - 0.05), 0.3, 0.3, st["Eisen"], quer=(0, 1, 0))
    haken = Vector((kx, spitze.y + 0.2, h_turm - 0.2 if haken_z is None else haken_z))            # hängt über dem Langhausdach, nicht auf dem Turm
    haus.stab(spitze + Vector((0, 0.05, 0.1)), Vector((kx, ky + hy - 0.3, ob + 0.3)), 0.04, 0.04, st["Seil"])
    haus.stab(spitze + Vector((0, 0.22, -0.1)), haken + Vector((0, 0, 0.2)), 0.04, 0.04, st["Seil"])
    haus.quader(haken + Vector((0, 0, 0.1)), (0.1, 0.08, 0.2), st["Eisen"])
    for sx in (-1, 1):
        haus.stab(haken + Vector((sx * 0.03, 0, 0.0)), haken + Vector((sx * 0.45, 0, -0.75)), 0.03, 0.03, st["Seil"])
    haus.quader(haken + Vector((0, 0, -1.0)), (1.0, 0.75, 0.7), st["Haustein"])
    # Treträder links und rechts außen am Kranhaus, gemeinsame Achse
    achse_z = fuss + 1.3
    n, r, b = 12, 2.0, 0.4
    for mx in (kx - hx - 0.7, kx + hx + 0.7):
        for x in (mx - b, mx + b):
            for k in range(n):
                w0, w1 = 2 * math.pi * k / n, 2 * math.pi * (k + 1) / n
                wm_ = (w0 + w1) / 2
                dd = 0.05 + 0.003 * (k % 2)
                for rr, normale in ((r, 1), (r - 0.14, -1)):
                    p = [(x - dd, ky + rr * math.cos(w0), achse_z + rr * math.sin(w0)), (x + dd, ky + rr * math.cos(w0), achse_z + rr * math.sin(w0)),
                         (x + dd, ky + rr * math.cos(w1), achse_z + rr * math.sin(w1)), (x - dd, ky + rr * math.cos(w1), achse_z + rr * math.sin(w1))]
                    rad.vieleck_nach(p, (0, normale * math.cos(wm_), normale * math.sin(wm_)), st["Holz"])
                for sx in (-1, 1):
                    p = [(x + sx * dd, ky + r * math.cos(w0), achse_z + r * math.sin(w0)), (x + sx * dd, ky + r * math.cos(w1), achse_z + r * math.sin(w1)),
                         (x + sx * dd, ky + (r - 0.14) * math.cos(w1), achse_z + (r - 0.14) * math.sin(w1)),
                         (x + sx * dd, ky + (r - 0.14) * math.cos(w0), achse_z + (r - 0.14) * math.sin(w0))]
                    rad.vieleck_nach(p, (sx, 0, 0), st["Holz"])
            for k in range(4):                                       # Speichen
                w = 2 * math.pi * (k + 0.5) / 4
                rad.stab((x, ky, achse_z), (x, ky + (r - 0.1) * math.cos(w), achse_z + (r - 0.1) * math.sin(w)), 0.1, 0.1, st["Holz"], quer=(1, 0, 0))
        for k in range(n):                                           # Trittbretter
            w = 2 * math.pi * (k + 0.5) / n
            t = Matrix.Translation((mx, ky + (r - 0.165) * math.cos(w), achse_z + (r - 0.165) * math.sin(w))) @ Matrix.Rotation(w, 4, "X")
            rad.quader((0, 0, 0), (2 * b - 0.1, 0.05, 0.3), st["Bretter"], matrix=t)
    rad.stab((kx - hx - 1.3, ky, achse_z), (kx + hx + 1.3, ky, achse_z), 0.26, 0.26, holz, quer=(0, 1, 0))      # Welle durch das Haus


# ─────────────────────────────────────────────────────────────────────────────
#  Ausgabe
# ─────────────────────────────────────────────────────────────────────────────

def main():
    import bpy
    objekte = bauen()
    bs.bericht(NAME, [o for o in objekte if o.type != "EMPTY" or o.get("anker")] + [k for o in objekte for k in o.children_recursive])
    vorher, nachher = bs.farben_zusammenlegen(objekte)
    print(f"  Materialien: {vorher} → {nachher}")
    print("geschrieben:", bs.exportieren(NAME, objekte))
    if "--ohne-bilder" in sys.argv:
        return
    schnell = "--schnell" in sys.argv
    alles = lambda o: True                                                         # noqa: E731
    bs.vorschau_buehne(FLAECHE, ziel_hoehe=28.0, ausschnitt=80.0, schnell=schnell)
    if "--nur-nacht" in sys.argv:
        print(bs.nachtbild(NAME, alles, 3))
        return
    if "--nur-anker" in sys.argv:
        print(bs.anker_bild(NAME, alles))
        return
    print(bs.rendern(NAME, alles, 3))                                              # Nordwest (vorne links)
    print(bs.rendern(NAME + "-suedost", alles, 1))                                 # Südost (hinten rechts)
    # Einblick wie im Spiel: Dach (Chor, Langhausdach), Turm und die Wand zur Kamera aus
    ausgeblendet = ("Dach", "Turm")
    print(bs.rendern(NAME + "-innen", lambda o: not (o.parent and o.parent.name in ausgeblendet) and o.name != "Wand_rechts", 1))
    if "--nah" in sys.argv:
        szene = bpy.context.scene
        for o in szene.objects:
            if o.type == "MESH":
                o.hide_render = False
        for name, drehung, ziel, breite in (("-nah-chor", 1, (0, 9, 16), 34.0), ("-nah-west", 3, (0, -14, 30), 50.0),
                                            ("-nah-turm", 3, (-4, -14, 42), 38.0)):
            szene["_ziel"] = 0.0
            bs.blick(drehung)
            kam = szene.camera
            kam.location += Vector(ziel)
            kam.data.ortho_scale = breite
            print(bs.rendern_einzeln(NAME + name))
        for name, az, neig, ziel, breite in (("-ans-west", -90, 8, (0, -14, 29), 75.0), ("-ans-portal", -90, 6, (0, -16, 15), 30.0),
                                             ("-ans-sued", 0, 8, (0, -2, 29), 80.0), ("-ans-ost", 90, 8, (0, 9, 20), 70.0)):
            szene["_ziel"] = 0.0
            bs.blick(3)                                    # Sonne wie bei den Schrägansichten
            kam = szene.camera
            a, n = math.radians(az), math.radians(neig)
            kam.location = Vector(ziel) + 80 * Vector((math.cos(a) * math.cos(n), math.sin(a) * math.cos(n), math.sin(n)))
            kam.rotation_euler = (math.radians(90 - neig), 0, a + math.radians(90))
            kam.data.ortho_scale = breite
            print(bs.rendern_einzeln(NAME + name))
        szene["_ziel"] = 16.0
        return
    if not schnell:
        print(bs.rundum(NAME, alles))
    print(bs.anker_bild(NAME, alles))
    print(bs.nachtbild(NAME, alles, 3))


if __name__ == "__main__":
    main()
