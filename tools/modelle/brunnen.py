"""Marktbrunnen – achteckiger Laufbrunnen mit gotischer Säule, Ritterfigur, acht Wappenschilden, vier Röhren und Viehtrog.

    blender -b --factory-startup --python tools/modelle/brunnen.py -- [--schnell] [--aus=<Ordner>]     (nur die Vorschaubilder)

Die Bauteile baut `bauen(st)` für `markt.py` (marktplatz.glb). Sie stehen in eigenen Netzen, damit das Spiel Wasser und Strahl mit eigenen Shadern
belegen und im Winter tauschen kann:

    Brunnen          Stufe, Becken aus Steinplatten, Säule, Fialen, Sockel mit Wappen, Ritter, Röhren   (Stoffe Sandstein, Haustein, Stadtrot, Fassung)
    Brunnen_Trog     Viehtrog mit Überlaufrinne                                                           (Stoff Haustein)
    Brunnen_Wasser   Wasserfläche in Becken und Trog                                                      (Stoff Brunnenwasser, Shader brunnenwasser.gdshader)
    Brunnen_Strahl   vier Strahlen aus den Röhren, Überlauf in den Trog                                   (Stoff Brunnenstrahl, Shader brunnenstrahl.gdshader)
    Brunnen_Eis      Winter: Eis in Becken und Trog, Eiszapfen, Schnee auf dem Rand (Stoffe Eis, Schnee); im Sommer unsichtbar

Maße (Meter): Becken 4,6 m Umkreis (8-Eck), Rand 0,95 m hoch, Stufe 0,17 m, Gesamthöhe etwa 5 m, Trog 1,8 × 0,7 m an der -Y-Seite (Spiel: +Z).
Der Brunnen steht auf Höhe 0. Alles selbst gebaut, nichts aus Fremdquellen, keine Wappen einer wirklichen Stadt.
"""

import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bausteine as bs  # noqa: E402
import fuellung_p2 as p2  # noqa: E402,F401  (bringt die Farben Schnee und Eis)
import natur as nt  # noqa: E402
from bausteine import Matrix, Vector  # noqa: E402

bs.FARBEN.update({
    "Stadtrot": (0.62, 0.12, 0.12),
    "Fassung": (0.74, 0.58, 0.24),                 # Gold der Wappen, Röhren und Krone
    "Brunnenwasser": (0.15, 0.21, 0.23),           # im Spiel: Shader brunnenwasser.gdshader
    "Brunnenstrahl": (0.62, 0.74, 0.78),           # im Spiel: Shader brunnenstrahl.gdshader
})
bs.RAUHEIT.update({"Fassung": 0.4, "Brunnenwasser": 0.06, "Brunnenstrahl": 0.05})

N = 8                    # Ecken des Beckens
R = 2.3                  # Umkreis des Beckens (innen die Wand dazu 0,3 m)
RAND = 0.95              # Höhe des Beckenrands
WASSER = 0.14            # Wasserspiegel unter dem Rand
TROG_MITTE = Vector((0.0, -3.35, 0.0))       # Blender; im Spiel +Z
AUFTREFFER = 1.5         # Abstand der Auftreffpunkte der Strahlen von der Mitte (der Shader kennt ihn: brunnenwasser.gdshader)


def lage(x, y, z, rz=0.0):
    return Matrix.Translation((x, y, z)) @ Matrix.Rotation(math.radians(rz), 4, "Z")


def ecke(n, r, k):
    w = 2 * math.pi * k / n + math.pi / n
    return Vector((r * math.cos(w), r * math.sin(w), 0.0))


def platten(netz, st, n, r, z0, z1, dicke, stoff, spalt=0.012):
    """Mantel eines n-Ecks aus einzelnen Steinplatten mit Gehrung (Fuge dazwischen); r = Umkreis außen, dicke radial nach innen."""
    r_in = r - dicke / math.cos(math.pi / n)
    for k in range(n):
        o0, o1 = ecke(n, r, k), ecke(n, r, k + 1)
        i0, i1 = ecke(n, r_in, k), ecke(n, r_in, k + 1)
        t = (o1 - o0).normalized() * (spalt / 2)
        pts = [o0 + t, o1 - t, i1 - t, i0 + t]
        profil = [(q.y, -q.x) for q in reversed(pts)]
        netz.prisma(profil, z0, z1, st[stoff], matrix=Matrix.Rotation(math.radians(-90), 4, "Y"))


def kopf(netz, st, mitte, r, stoff, n=8):
    m = Vector(mitte)
    nt.ast(netz, st, m - Vector((0, 0, r)), m, r * 0.15, r * 0.95, stoff=stoff, n=n)
    nt.ast(netz, st, m, m + Vector((0, 0, r)), r * 0.95, r * 0.15, stoff=stoff, n=n)


# ─────────────────────────────────────────────────────────────────────────────
#  Wappen: neutrale heraldische Muster, nichts von einer wirklichen Stadt
# ─────────────────────────────────────────────────────────────────────────────

SCHILD = [(-1, 1), (1, 1), (1, 0), (0.6, -0.6), (0, -1), (-0.6, -0.6), (-1, 0)]
MUSTER = {
    "geteilt": [[(-1, 1), (1, 1), (1, 0), (-1, 0)]],
    "gespalten": [[(-1, 1), (0, 1), (0, -1), (-0.6, -0.6), (-1, 0)]],
    "balken": [[(-1, 0.35), (1, 0.35), (1, 0), (0.83, -0.15), (-0.83, -0.15), (-1, 0)]],
    "schraegbalken": [[(-1, 1), (-0.4, 1), (0.83, -0.15), (0.3, -0.55), (-1, 0.45)]],
    "loewe": [[(-0.2, 0.3), (0.3, 0.3), (0.35, -0.3), (-0.1, -0.45), (-0.35, -0.2)],                 # Rumpf
              [(-0.05, 0.8), (0.35, 0.8), (0.4, 0.45), (0.0, 0.35), (-0.1, 0.5)],                    # Kopf
              [(0.3, 0.25), (0.75, 0.55), (0.8, 0.4), (0.35, 0.05)], [(0.3, 0.0), (0.75, 0.2), (0.7, 0.05), (0.3, -0.18)],   # Vorderpranken
              [(-0.3, -0.25), (-0.5, -0.6), (-0.25, -0.62), (-0.05, -0.4)], [(0.0, -0.4), (0.1, -0.8), (0.35, -0.78), (0.3, -0.35)],   # Hinterbeine
              [(-0.35, -0.1), (-0.75, 0.0), (-0.8, 0.5), (-0.6, 0.55), (-0.62, 0.1), (-0.3, 0.05)]],     # Schweif
    "adler": [[(-0.15, 0.2), (0.15, 0.2), (0.2, -0.4), (0.0, -0.7), (-0.2, -0.4)],
              [(-0.15, 0.2), (-0.9, 0.7), (-0.85, 0.05), (-0.55, -0.15), (-0.15, -0.2)],
              [(0.15, 0.2), (0.15, -0.2), (0.55, -0.15), (0.85, 0.05), (0.9, 0.7)],
              [(-0.1, 0.2), (0.1, 0.2), (0.14, 0.55), (0.0, 0.72), (-0.14, 0.55)]],
}


def wappen(netz, st, mitte, richtung, hoehe, muster, feld="Stadtrot", bild="Fassung"):
    """Schild (Wappenform) an einer senkrechten Fläche: `mitte` auf der Fläche, `richtung` = Normale (waagerecht), `hoehe` = ganze Höhe."""
    v = Vector((richtung[0], richtung[1], 0)).normalized()
    seite = Vector((-v.y, v.x, 0))
    s = hoehe / 2

    def punkte(liste, vor):
        return [Vector(mitte) + seite * (a * s * 0.85) + Vector((0, 0, b * s)) + v * vor for a, b in liste]
    netz.vieleck_nach(punkte(SCHILD, 0.004), v, st[feld])
    for teil in MUSTER[muster]:
        netz.vieleck_nach(punkte(teil, 0.008), v, st[bild])


# ─────────────────────────────────────────────────────────────────────────────
#  Bauteile
# ─────────────────────────────────────────────────────────────────────────────

def figur(netz, st, fuss, hoehe):
    """Ritter aus Stein: langes Gewand, Gürtel, Arme, Kopf mit Krone, Wappenschild (geteilt) vor der Brust; blickt nach -Y (zum Trog)."""
    x, y, z = fuss
    g = "Haustein"
    nt.ast(netz, st, (x, y, z), (x, y, z + hoehe * 0.55), hoehe * 0.15, hoehe * 0.1, stoff=g, n=8)
    nt.ast(netz, st, (x, y, z + hoehe * 0.55), (x, y, z + hoehe * 0.8), hoehe * 0.1, hoehe * 0.12, stoff=g, n=8)
    nt.ast(netz, st, (x, y, z + hoehe * 0.8), (x, y, z + hoehe * 0.86), hoehe * 0.12, hoehe * 0.05, stoff=g, n=8)
    kopf(netz, st, (x, y, z + hoehe * 0.93), hoehe * 0.07, g)
    nt.ast(netz, st, (x, y, z + hoehe * 0.99), (x, y, z + hoehe * 1.0), hoehe * 0.075, hoehe * 0.07, stoff="Fassung", n=8)
    nt.ast(netz, st, (x, y, z + hoehe * 1.0), (x, y, z + hoehe * 1.05), hoehe * 0.075, hoehe * 0.02, stoff="Fassung", n=8)
    for s in (-1, 1):
        a = Vector((x + s * hoehe * 0.12, y, z + hoehe * 0.75))
        nt.ast(netz, st, a, a + Vector((0, -hoehe * 0.05, -hoehe * 0.28)), hoehe * 0.04, hoehe * 0.03, stoff=g, n=5)
    wappen(netz, st, (x, y - hoehe * 0.14, z + hoehe * 0.62), (0, -1, 0), hoehe * 0.26, "geteilt")


def fiale(netz, st, x, y, z0, hoehe, r, stoff="Sandstein"):
    """Gotische Fiale: Schaft, Spitzhelm mit Krabben, Kreuzblume."""
    nt.ast(netz, st, (x, y, z0), (x, y, z0 + hoehe * 0.45), r, r * 0.85, stoff=stoff, n=8)
    nt.ast(netz, st, (x, y, z0 + hoehe * 0.45), (x, y, z0 + hoehe * 0.88), r * 1.1, r * 0.1, stoff=stoff, n=8)
    for k in range(4):
        w = k * math.pi / 2 + math.pi / 4
        netz.quader((0, 0, 0), (r * 0.45, r * 0.45, r * 0.45), st[stoff], lage(x + r * 0.95 * math.cos(w), y + r * 0.95 * math.sin(w), z0 + hoehe * (0.5 + 0.12 * k), 45))
    kopf(netz, st, (x, y, z0 + hoehe * 0.95), r * 0.35, stoff)


def roehre(netz, st, a, b, r=0.05):
    nt.ast(netz, st, a, b, r, r * 0.75, stoff="Fassung", n=6)
    nt.ast(netz, st, Vector(b), Vector(b) + (Vector(b) - Vector(a)).normalized() * 0.03, r * 0.85, r * 0.85, stoff="Fassung", n=6)


def strahl(netz, st, start, richtung, reichweite, z_wasser, dicke=0.04, schritte=7):
    """Wasserstrahl als Bogen aus kurzen Röhren (je vier Ecken); der Shader rollt Streifen entlang der Höhe. Am Ende ein paar Spritzer."""
    start = Vector(start)
    d = Vector((richtung[0], richtung[1], 0)).normalized()
    fall = start.z - z_wasser
    pts = [start + d * reichweite * (i / schritte) - Vector((0, 0, fall * (i / schritte) ** 2)) for i in range(schritte + 1)]
    for a, b in zip(pts, pts[1:]):
        nt.ast(netz, st, a, b, dicke, dicke * 0.9, stoff="Brunnenstrahl", n=4)
    ende = pts[-1]
    for k in range(5):
        w = k * 2 * math.pi / 5 + 0.3
        netz.quader((0, 0, 0), (0.03, 0.03, 0.06), st["Brunnenstrahl"], lage(ende.x + 0.11 * math.cos(w), ende.y + 0.11 * math.sin(w), z_wasser + 0.05 + 0.03 * (k % 2)))
    return ende


def zapfen(netz, st, x, y, z, laenge, r=0.025):
    nt.ast(netz, st, (x, y, z), (x, y, z - laenge), r, r * 0.12, stoff="Eis", n=5)


def bauen(st):
    """Baut alle Netze des Brunnens und gibt sie als {Name: Objekt} zurück (Ursprung in der Mitte des Beckens am Boden)."""
    stein = bs.Netz("Brunnen")
    trog = bs.Netz("Brunnen_Trog")
    wasser = bs.Netz("Brunnen_Wasser")
    fluss = bs.Netz("Brunnen_Strahl")
    eis = bs.Netz("Brunnen_Eis")
    z_w = RAND - WASSER

    bs.achteck(stein, st, (0, 0, 0), R + 0.05 + 0.42, 0.0, 0.17, "Haustein", n=N)                    # eine Stufe
    platten(stein, st, N, R, 0.0, RAND - 0.1, 0.3, "Haustein")                                       # Beckenwand
    platten(stein, st, N, R + 0.05, RAND - 0.1, RAND, 0.42, "Haustein", spalt=0.014)               # Randplatten, breiter als die Wand
    stein.vieleck_nach([ecke(N, R - 0.3, k) + Vector((0, 0, 0.18)) for k in range(N)], (0, 0, 1), st["Haustein"])    # Beckenboden
    wasser.vieleck_nach([ecke(N, R - 0.29, k) + Vector((0, 0, z_w)) for k in range(N)], (0, 0, 1), st["Brunnenwasser"])
    # Säule: Sockel im Becken, Schaft, Ringe, Kapitell mit Brüstungsplatte, vier Fialen, Sockel mit Wappen, Ritter
    nt.ast(stein, st, (0, 0, 0.18), (0, 0, 0.7), 0.5, 0.4, stoff="Haustein", n=8)
    nt.ast(stein, st, (0, 0, 0.7), (0, 0, 3.0), 0.3, 0.24, stoff="Haustein", n=8)
    for z in (1.4, 2.2):
        nt.ast(stein, st, (0, 0, z), (0, 0, z + 0.1), 0.33, 0.33, stoff="Sandstein", n=8)
    nt.ast(stein, st, (0, 0, 2.9), (0, 0, 3.15), 0.34, 0.5, stoff="Sandstein", n=8)
    nt.ast(stein, st, (0, 0, 3.15), (0, 0, 3.3), 0.5, 0.5, stoff="Haustein", n=8)
    for k in range(4):                                                                               # vier Fialen über den Ecken der Brüstung
        w = k * math.pi / 2 + math.pi / 8
        fiale(stein, st, 0.4 * math.cos(w), 0.4 * math.sin(w), 3.3, 0.8, 0.055)
    nt.ast(stein, st, (0, 0, 3.3), (0, 0, 3.75), 0.27, 0.25, stoff="Sandstein", n=8)               # Sockel der Figur
    figur(stein, st, (0, 0, 3.75), 1.15)
    # acht Wappen auf den Seitenflächen (Mitten des Achtecks, 22,5° neben den Röhren): vier am Schaft, vier am Sockel
    ap = math.cos(math.pi / 8)
    for k, muster in enumerate(["balken", "schraegbalken", "gespalten", "geteilt"]):
        w = math.pi / 8 + k * math.pi / 2
        wappen(stein, st, (0.253 * ap * math.cos(w), 0.253 * ap * math.sin(w), 2.55), (math.cos(w), math.sin(w), 0), 0.34, muster)
    for k, muster in enumerate(["loewe", "adler", "loewe", "adler"]):
        w = 3 * math.pi / 8 + k * math.pi / 2
        wappen(stein, st, (0.26 * ap * math.cos(w), 0.26 * ap * math.sin(w), 3.52), (math.cos(w), math.sin(w), 0), 0.3, muster, feld="Fassung", bild="Stadtrot")
    # vier Röhren auf Höhe 1,9 m an den Seitenmitten (45° …); die Wappen am Schaft stehen dazwischen
    enden = []
    for k in range(4):
        w = k * math.pi / 2 + math.pi / 4
        v = Vector((math.cos(w), math.sin(w), 0))
        a = v * 0.25 + Vector((0, 0, 1.95))
        b = v * 0.62 + Vector((0, 0, 1.88))
        roehre(stein, st, a, b)
        enden.append(strahl(fluss, st, b, v, AUFTREFFER - 0.62, z_w))
    # Viehtrog mit Überlauf: Rinne über den Rand, Strahl in den Trog
    tx, ty = TROG_MITTE.x, TROG_MITTE.y
    stein.quader((0, 0, 0), (0.34, 0.8, 0.1), st["Haustein"], lage(0, -(R * math.cos(math.pi / N) + 0.05 - 0.05) - 0.3, RAND - 0.12))           # Speier auf dem Rand
    z_t = 0.52
    trog.quader((0, 0, 0.3), (1.9, 0.78, 0.6), st["Haustein"], lage(tx, ty, 0.0))
    trog.quader((0, 0, 0.0), (1.5, 0.4, 0.02), st["Haustein"], lage(tx, ty, 0.6))
    # Ausschnitt im Trog: Innenwände aus vier Platten über der Wasserlinie
    for dx, dy, bx, by in ((0, 0.31, 1.9, 0.16), (0, -0.31, 1.9, 0.16), (0.89, 0, 0.12, 0.78), (-0.89, 0, 0.12, 0.78)):
        trog.quader((0, 0, 0.0), (bx, by, 0.1), st["Haustein"], lage(tx + dx, ty + dy, 0.6))
    wasser.vieleck_nach([Vector((tx + sx * 0.8, ty + sy * 0.28, z_t)) for sx, sy in ((-1, -1), (1, -1), (1, 1), (-1, 1))], (0, 0, 1), st["Brunnenwasser"])
    spritz_von = Vector((0, -(R * math.cos(math.pi / N) + 0.05) - 0.65, RAND - 0.08))
    strahl(fluss, st, spritz_von, (0, -1), 0.38, z_t, 0.035, 6)
    # Winter: Eis statt Wasser, Eiszapfen an den Röhren und am Rand, Schnee auf Rand, Brüstung und Trog
    eis.vieleck_nach([ecke(N, R - 0.29, k) + Vector((0, 0, z_w + 0.01)) for k in range(N)], (0, 0, 1), st["Eis"])
    eis.vieleck_nach([Vector((tx + sx * 0.8, ty + sy * 0.28, z_t + 0.01)) for sx, sy in ((-1, -1), (1, -1), (1, 1), (-1, 1))], (0, 0, 1), st["Eis"])
    for k in range(4):
        w = k * math.pi / 2 + math.pi / 4
        v = Vector((math.cos(w), math.sin(w), 0))
        zapfen(eis, st, *(v * 0.6 + Vector((0, 0, 1.86))), 0.32)
        zapfen(eis, st, *(v * 0.64 + Vector((0.05 * v.y, -0.05 * v.x, 1.86))), 0.2, 0.02)
    for k in range(N):
        m = (ecke(N, R + 0.05, k) + ecke(N, R + 0.05, k + 1)) / 2
        for d in (-0.5, 0.0, 0.5):
            t = (ecke(N, R + 0.05, k + 1) - ecke(N, R + 0.05, k)).normalized() * d
            zapfen(eis, st, m.x + t.x, m.y + t.y, RAND - 0.06, 0.14 + 0.06 * ((k + int(d * 2)) % 3), 0.02)
    platten(eis, st, N, R + 0.05, RAND, RAND + 0.04, 0.42, "Schnee", spalt=0.014)
    nt.ast(eis, st, (0, 0, 3.3), (0, 0, 3.34), 0.5, 0.5, stoff="Schnee", n=8)
    eis.quader((0, 0, 0), (1.9, 0.78, 0.04), st["Schnee"], lage(tx, ty, 0.64))
    objekte = {}
    for netz in (stein, trog, wasser, fluss, eis):
        o = netz.objekt()
        objekte[netz.name] = o
    return objekte


def anker():
    """Der Trog als Anker (das Spiel macht daraus Hindernisse für die Passanten)."""
    return [bs.anker("Anker_Trog", (TROG_MITTE.x, TROG_MITTE.y, 0.0), 0.0)]


# ─────────────────────────────────────────────────────────────────────────────
#  Vorschau
# ─────────────────────────────────────────────────────────────────────────────

def vorschau(aus, schnell):
    import bpy
    import fuellung_vorschau as fv
    bs.VORSCHAU = aus
    bs.FARBEN["Sandstein"] = (0.62, 0.54, 0.41)      # nur fürs Bild gedämpft (unter der Vorschausonne sonst überstrahlt)
    bs.neue_szene()
    st = bs.Stoffe()
    teile = bauen(st)
    bs.vorschau_buehne(None, ziel_hoehe=2.0, ausschnitt=11.0, schnell=schnell)
    bpy.data.objects["Sonne"].data.energy = 2.4
    s = bpy.context.scene
    s.render.resolution_x, s.render.resolution_y = 1000, 750
    s.render.resolution_percentage = 50 if schnell else 70
    s.cycles.samples = 24 if schnell else 56
    fv.texturieren()
    for jahreszeit, sichtbar in (("sommer", {"Brunnen", "Brunnen_Trog", "Brunnen_Wasser", "Brunnen_Strahl"}), ("winter", {"Brunnen", "Brunnen_Trog", "Brunnen_Eis"})):
        auswahl = {teile[n] for n in sichtbar}
        s.camera.data.ortho_scale = 11.0
        s["_ziel"] = 2.0
        print(bs.rendern(f"marktbrunnen_{jahreszeit}", lambda o: o in auswahl))
        s.camera.data.ortho_scale = 6.4
        s["_ziel"] = 1.5
        print(bs.rendern(f"marktbrunnen_{jahreszeit}_nah", lambda o: o in auswahl))


def main():
    aus = [a.split("=", 1)[1] for a in sys.argv if a.startswith("--aus=")]
    vorschau(aus[0] if aus else r"C:\Reichsgilde_Austausch\marktbrunnen", "--schnell" in sys.argv)


if __name__ == "__main__":
    main()
