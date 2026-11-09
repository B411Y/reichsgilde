"""Neue Einrichtung (personal_einrichtung.py) im echten Raummodell: sucht an den Wänden des Werkraums eine freie Stelle für jedes Stück (Raster der belegten
Fläche aus dem gebauten Raum, Stufe 3 mit aller Einrichtung, Türen und Arbeitsplätze freigehalten), stellt es dorthin und rendert es aus dem Blickpunkt
des Raums, der es am besten zeigt (Spielkamera, Sichtfeld 50°, mit den Texturen der Material-Bibliothek).

    blender -b --factory-startup --python tools/modelle/personal_raumvorschau.py -- --raum=schmied --stuecke=Schleifstein,Ladentheke [--aus=Ordner] [--schnell]
        [--raumschluessel=Werk] [--wand=vorne|hinten|links|rechts] [--lehrling=Dieb]

Schreibt <raum>_<Stück>.png nach `--aus` und gibt je Stück Wand, örtliche Lage und Rahmen für das Raumskript aus (`rahmen(x, y, drehung)`).
Nur Vorschau, es wird nichts nach game/assets geschrieben.
"""

import importlib
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bausteine as bs  # noqa: E402
from bausteine import Matrix, Vector  # noqa: E402
import innenausbau as ia  # noqa: E402
import personal_einrichtung as pe  # noqa: E402

SCHNELL = "--schnell" in sys.argv
ZELLE = 0.05
RAND = 0.15       # Abstand zu allem anderen (seitlich und vorn)


def arg(name, vorgabe=None):
    for a in sys.argv:
        if a.startswith(f"--{name}="):
            return a.split("=", 1)[1]
    return vorgabe


TOLERANZ = float(arg("toleranz", "0.0"))
ABWAND = float(arg("abwand", "0.0"))
AUS = arg("aus", os.path.join(bs.WURZEL, "docs", "modelle", "personal_einrichtung"))
os.makedirs(AUS, exist_ok=True)
bs.VORSCHAU = AUS


class Raster:
    """Belegte Fläche eines Raums (Zellen von 5 cm)."""

    def __init__(self, x0, x1, y0, y1):
        self.x0, self.x1, self.y0, self.y1 = x0, x1, y0, y1
        self.nx, self.ny = int(math.ceil((x1 - x0) / ZELLE)), int(math.ceil((y1 - y0) / ZELLE))
        self.belegt = [bytearray(self.ny) for _ in range(self.nx)]
        self.quelle = {}                    # Zelle → Name des Objekts, das sie belegt (nur zur Prüfung)
        self.aktuell = ""

    def zelle(self, x, y):
        return int((x - self.x0) / ZELLE), int((y - self.y0) / ZELLE)

    def setzen(self, x, y):
        i, j = self.zelle(x, y)
        if 0 <= i < self.nx and 0 <= j < self.ny:
            self.belegt[i][j] = 1
            self.quelle.setdefault((i, j), self.aktuell)

    def rechteck(self, ecken, rand=0.0):
        """Alle Zellen im (gedrehten) Rechteck `ecken` (vier Punkte), um `rand` erweitert."""
        xs, ys = [p[0] for p in ecken], [p[1] for p in ecken]
        a, b, c = ecken[0], ecken[1], ecken[3]
        ux, uy = b[0] - a[0], b[1] - a[1]
        vx, vy = c[0] - a[0], c[1] - a[1]
        lu, lv = math.hypot(ux, uy), math.hypot(vx, vy)
        ux, uy, vx, vy = ux / lu, uy / lu, vx / lv, vy / lv
        i0, j0 = self.zelle(min(xs) - rand, min(ys) - rand)
        i1, j1 = self.zelle(max(xs) + rand, max(ys) + rand)
        for i in range(max(i0, 0), min(i1, self.nx - 1) + 1):
            for j in range(max(j0, 0), min(j1, self.ny - 1) + 1):
                px, py = self.x0 + (i + 0.5) * ZELLE - a[0], self.y0 + (j + 0.5) * ZELLE - a[1]
                u, v = px * ux + py * uy, px * vx + py * vy
                if -rand <= u <= lu + rand and -rand <= v <= lv + rand:
                    yield i, j

    def frei(self, ecken, rand=0.0):
        return all(not self.belegt[i][j] for i, j in self.rechteck(ecken, rand))

    def belegen(self, ecken, rand=0.0):
        for i, j in self.rechteck(ecken, rand):
            self.belegt[i][j] = 1

    def abstand(self, x, y, richtung, maximal=2.0):
        """Wie weit man von (x, y) in `richtung` frei gehen kann (Meter, höchstens `maximal`)."""
        d = 0.0
        while d < maximal:
            i, j = self.zelle(x + richtung[0] * d, y + richtung[1] * d)
            if not (0 <= i < self.nx and 0 <= j < self.ny) or self.belegt[i][j]:
                break
            d += ZELLE
        return d


def raster_bauen(raum, schluessel, rect, objekte, z0=0.0, ohne_pe=False):
    """Belegte Fläche der Höhen 0,1 … 2,0 m aus den Netzen des Raums (Stufe 3, mit Einrichtung); Wände (näher als 12 cm an der Raumgrenze) zählen nicht."""
    import bpy
    x0, x1, y0, y1 = rect
    r = Raster(x0 - 0.6, x1 + 0.6, y0 - 0.6, y1 + 0.6)
    rand = 0.12
    tueren = []
    for o in bpy.context.scene.objects:
        if o.type != "MESH" or o.name.startswith(("_", "Decke_", "Himmel", "Kranbalken")) or not ia.sichtbar(o, 3, True, True, 2, {schluessel}):
            continue
        if ohne_pe and o.name.startswith("Einrichtung_") and o.name[12:].split(".")[0] in pe.ALLE:
            continue
        # Türen: Fläche davor freihalten
        tuer = o.name.startswith(("Tuer_", "Holztuer", "Bedienung_Karren_")) or o.name in ("Einrichtung_Eisentuer",)
        r.aktuell = o.name
        w = o.matrix_world
        me = o.data
        vor_ = sum(sum(c) for c in r.belegt)
        for p in me.polygons:
            pts = [w @ me.vertices[i].co for i in p.vertices]
            zs = [q.z for q in pts]
            if max(zs) < z0 + 0.1 or min(zs) > z0 + 2.0:
                continue
            if all(abs(q.x - x0) < rand or abs(q.x - x1) < rand or abs(q.y - y0) < rand or abs(q.y - y1) < rand for q in pts) and not tuer:
                continue
            for k in range(1, len(pts) - 1):
                a, b, c = pts[0], pts[k], pts[k + 1]
                lang = max((a - b).length, (b - c).length, (a - c).length)
                n = max(2, int(lang / ZELLE) + 1)
                for s in range(n + 1):
                    for t in range(n + 1 - s):
                        q = a + (b - a) * (s / n) + (c - a) * (t / n)
                        r.setzen(q.x, q.y)
                        if tuer:
                            tueren.append((q.x, q.y))
        if "--objekte" in sys.argv:
            print("OBJEKT", o.name, sum(sum(c) for c in r.belegt) - vor_)
    # Arbeitsplätze (Figur davor): Fläche 0,7 m um den Anker und 0,9 m in Blickrichtung
    import bpy as _b
    for o in _b.context.scene.objects:
        if o.type == "EMPTY" and o.name.startswith("Arbeit_") and ia.raum_von(o) == schluessel:
            p = o.matrix_world.translation
            vor = o.matrix_world.to_3x3() @ Vector((0, 1, 0))
            for d in (-0.25, 0.0, 0.25, 0.5, 0.75, 1.0):
                r.belegen([(p.x + vor.x * d - 0.45, p.y + vor.y * d - 0.45), (p.x + vor.x * d + 0.45, p.y + vor.y * d - 0.45),
                           (p.x + vor.x * d + 0.45, p.y + vor.y * d + 0.45), (p.x + vor.x * d - 0.45, p.y + vor.y * d + 0.45)])
    for x, y in tueren:
        for dx in (-0.5, 0, 0.5):
            for dy in (-0.5, 0, 0.5):
                r.setzen(x + dx, y + dy)
    return r


def waende(rect):
    x0, x1, y0, y1 = rect
    # Name: (Ursprung der Wand, Richtung der Wand [örtlich +x], Drehung des Stücks, Länge)
    return {
        "vorne": ((x0, y0), (1, 0), 0, x1 - x0),
        "hinten": ((x1, y1), (-1, 0), 180, x1 - x0),
        "links": ((x0, y1), (0, -1), -90, y1 - y0),
        "rechts": ((x1, y0), (0, 1), 90, y1 - y0),
    }


def rahmen(x, y, drehung, z=0.0):
    return Matrix.Translation((x, y, z)) @ Matrix.Rotation(math.radians(drehung), 4, "Z")


def bbox_lokal(netz):
    xs = [p.x for p in netz.ecken]
    ys = [p.y for p in netz.ecken]
    zs = [p.z for p in netz.ecken]
    return min(xs), max(xs), min(ys), max(ys), max(zs)


def suche(raster, rect, bb, erlaubt):
    """Beste freie Stelle: Liste (Punktzahl, wand, u, M) – u = Lage des Stückursprungs entlang der Wand."""
    xmin, xmax, ymin, ymax, _ = bb
    treffer = []
    for wand, (ursprung, richtung, drehung, laenge) in waende(rect).items():
        if erlaubt and wand not in erlaubt:
            continue
        u = 0.4 - xmin
        while u + xmax < laenge - 0.3:
            M = rahmen(ursprung[0] + richtung[0] * u, ursprung[1] + richtung[1] * u, drehung) @ Matrix.Translation((0, ABWAND, 0))
            ecken = [M @ Vector(p) for p in ((xmin - RAND, 0.01, 0), (xmax + RAND, 0.01, 0), (xmax + RAND, ymax + RAND, 0), (xmin - RAND, ymax + RAND, 0))]
            e2 = [(p.x, p.y) for p in ecken]
            zellen = list(raster.rechteck(e2))
            belegt = sum(1 for i, j in zellen if raster.belegt[i][j])
            if belegt <= TOLERANZ * len(zellen):
                # Platz davor: wie weit frei in Blickrichtung des Stücks (+y örtlich)
                mitte = M @ Vector(((xmin + xmax) / 2, ymax + 0.05, 0))
                vorn = M.to_3x3() @ Vector((0, 1, 0))
                platz = raster.abstand(mitte.x, mitte.y, (vorn.x, vorn.y), 2.0)
                seite = min(raster.abstand(mitte.x, mitte.y, (-vorn.y, vorn.x), 1.0), raster.abstand(mitte.x, mitte.y, (vorn.y, -vorn.x), 1.0))
                treffer.append((platz + 0.5 * seite - 40.0 * belegt / len(zellen), wand, u, M))
            u += 0.1
    treffer.sort(key=lambda t: -t[0])
    return treffer


def blick_waehlen(blicke, mitte):
    """Blickpunkt (Name, Auge, Ziel), der die Stelle `mitte` am besten zeigt."""
    beste = None
    for name, (auge, ziel) in blicke.items():
        a, z = Vector(auge), Vector(ziel)
        vor = (z - a).normalized()
        zum = mitte - a
        d = zum.length
        if d < 1.2:
            continue
        v2, z2 = vor.xy.normalized(), zum.xy.normalized()
        az = math.degrees(math.atan2(v2.x * z2.y - v2.y * z2.x, v2.dot(z2)))
        el = math.degrees(math.asin(max(-1, min(1, zum.normalized().z)))) - math.degrees(math.asin(max(-1, min(1, vor.z))))
        if abs(az) > 34 or abs(el) > 22:
            continue
        punkt = abs(az) + 0.5 * abs(el) + d * 1.5
        if beste is None or punkt < beste[0]:
            beste = (punkt, name, auge, ziel)
    return beste


def main():
    import bpy
    raum = arg("raum", "schmied")
    schluessel = arg("raumschluessel", "Werk")
    stuecke = (arg("stuecke") or "").split(",")
    erlaubt = arg("wand")
    erlaubt = erlaubt.split(",") if erlaubt else None
    mod = importlib.import_module(f"{raum}_innen")
    objekte = mod.bauen()
    if arg("rect"):
        rect = tuple(getattr(mod, n) for n in arg("rect").split(","))
    else:
        rect = (mod.WX0, mod.WX1, mod.WY0, mod.WY1)
    st = bs.Stoffe()
    raum_obj = bpy.data.objects[f"Raum_{schluessel}"]
    z0 = float(getattr(mod, arg("z0")) if arg("z0") else 0.0)
    raster = raster_bauen(raum, schluessel, rect, objekte, z0, ohne_pe=True)
    plan = []
    if "--karte" in sys.argv:                                   # Karte (10 cm) des Raums: oben = hinten, # belegt
        for j in range(raster.zelle(0, rect[3])[1], raster.zelle(0, rect[2])[1] - 1, -2):
            print("KARTE %+5.1f " % (raster.y0 + (j + 0.5) * ZELLE) + "".join(
                "#" if any(raster.belegt[i + di][j - dj] for di in range(2) for dj in range(2)) else "." for i in range(raster.zelle(rect[0], 0)[0], raster.zelle(rect[1], 0)[0], 2)))
    for name in stuecke:
        funktion, _ = pe.ALLE[name]
        probe = bs.Netz("_Probe", alle=True)
        if name == "Ladentheke":
            funktion(probe, st, Matrix.Identity(4), arg("ware", "Schmied"), float(arg("breite", "2.3")))
        elif name == "Lehrlingsstube":
            funktion(probe, st, Matrix.Identity(4), arg("lehrling", "Werkstatt"), arg("bett", "1") == "1")
        elif name == "Gewoelbekeller":
            funktion(probe, st, Matrix.Identity(4), False)
        else:
            funktion(probe, st, Matrix.Identity(4))
        bb = bbox_lokal(probe)
        treffer = suche(raster, rect, bb, erlaubt)
        if not treffer:
            print(f"KEINE STELLE für {name} (Breite {bb[1] - bb[0]:.2f}, Tiefe {bb[3]:.2f})")
            continue
        punkt, wand, u, M = treffer[int(arg("rang", "0"))]
        M = Matrix.Translation((0, 0, z0)) @ M
        netz = bs.Netz("Einrichtung_" + name, alle=True)
        if name == "Ladentheke":
            funktion(netz, st, M, arg("ware", "Schmied"), float(arg("breite", "2.3")))
        elif name == "Lehrlingsstube":
            funktion(netz, st, M, arg("lehrling", "Werkstatt"), arg("bett", "1") == "1")
        elif name == "Gewoelbekeller":
            funktion(netz, st, M, False)
        else:
            funktion(netz, st, M)
        o = netz.objekt()
        o.parent = raum_obj
        o.matrix_parent_inverse = Matrix.Identity(4)
        xmin, xmax, ymin, ymax, zmax = bb
        raster.belegen([(p.x, p.y) for p in [M @ Vector(c) for c in ((xmin - RAND, 0.01, 0), (xmax + RAND, 0.01, 0), (xmax + RAND, ymax + RAND, 0), (xmin - RAND, ymax + RAND, 0))]])
        t = M.translation
        drehung = {"vorne": 0, "hinten": 180, "links": -90, "rechts": 90}[wand]
        print(f"STELLE {raum} {name}: Wand {wand}, rahmen({t.x:.2f}, {t.y:.2f}, {drehung}), Breite {xmax - xmin:.2f} ({xmin:.2f} … {xmax:.2f}), Tiefe {ymax:.2f}, Höhe {zmax:.2f}, Platz davor {punkt:.2f}")
        plan.append((name, M, bb))

    if "--ohne-bild" in sys.argv:
        return
    # Rendern
    szene, kam = ia.buehne(SCHNELL, glut=260, fuell=[((rect[0] + rect[1]) / 2, (rect[2] + rect[3]) / 2, 3.2, 140, 1)])
    szene.render.resolution_x, szene.render.resolution_y = 960, 540
    szene.cycles.samples = 24 if SCHNELL else 64
    ia.zeigen(szene, 3, True, spaene=2, raeume={schluessel})
    try:
        import fuellung_vorschau as fv
        fv.texturieren()
    except Exception as fehler:  # noqa: BLE001
        print("keine Texturen:", fehler)
    szene.view_settings.exposure = -0.4
    blicke = {k: v for k, v in mod.BLICKE.items() if k.startswith(f"Blick_{schluessel}_")}
    for name, M, bb in plan:
        mitte = M @ Vector(((bb[0] + bb[1]) / 2, bb[3] / 2, bb[4] / 2))
        wahl = blick_waehlen(blicke, mitte)
        if wahl is None:
            vorn = M.to_3x3() @ Vector((0, 1, 0))
            auge = Vector((mitte.x + vorn.x * 4.2, mitte.y + vorn.y * 4.2, z0 + 2.4))
            auge.x = min(max(auge.x, rect[0] + 0.3), rect[1] - 0.3)      # im Raum bleiben
            auge.y = min(max(auge.y, rect[2] + 0.3), rect[3] - 0.3)
            ziel = Vector((mitte.x, mitte.y, z0 + 0.9))
            bezeichnung = "freier Blick"
        else:
            _, bname, auge, ziel = wahl
            bezeichnung = bname
        a = Vector(auge)
        vor = (Vector(ziel) - a).normalized() if wahl else (mitte - a).normalized()
        if wahl:
            # Blick auf das Stück ausrichten, Auge bleibt (die Kamera des Spiels schwenkt zu festen Punkten; hier zeigt der Blick das Stück)
            vor = (Vector(ziel) - a).normalized()
        rechts = vor.cross(Vector((0, 0, 1))).normalized()
        oben = rechts.cross(vor)
        m = Matrix((rechts, oben, -vor)).transposed().to_4x4()
        m.translation = a
        kam.matrix_world = m
        bild = bs.rendern_einzeln(f"{raum}_{name}", f"{raum}: {name} ({bezeichnung})")
        print("BILD", bild)


if __name__ == "__main__":
    main()
