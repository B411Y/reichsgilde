"""Netzbau fuer den Neubau der Gebaeude: Flaechen mit Trim-Sheet-UV (Style-Guide 3.3 und 4), als Blender-Objekte.

Jede Flaeche bekommt ihre UV direkt beim Bauen: Sie liegt auf einem Band des Trim-Sheets (Bandgrenzen aus trimsheet.py),
laeuft entlang der Maserung ueber UV.x (darf ueber 1 hinaus, die Baender kacheln waagerecht) und fuellt quer dazu das Band.
Koordinaten wie in Blender: X rechts, Y nach hinten (Gasse bei -Y), Z oben. UV wie in Godot: v = 0 ist die oberste Bildzeile;
beim Schreiben ins Netz wird v gespiegelt (Blender zaehlt von unten).
"""
import math
import random

import bpy
from mathutils import Vector

ROT = (1.0, 0.0, 0.0, 1.0)       # Eckenfarbe der Akzente (Fensterlaeden), Style-Guide 3.3
SCHWARZ = (0.0, 0.0, 0.0, 1.0)


class Teil:
    """Ein spaeteres Blender-Objekt: sammelt Flaechen (Ecken, UV, Material, Eckenfarbe)."""

    def __init__(self, name, baender, saat=1):
        self.name = name
        self.baender = baender
        self.flaechen = []
        self.zufall = random.Random(saat)

    def dreiecke(self):
        return sum(len(f[0]) - 2 for f in self.flaechen)

    def dazu(self, anderes):
        """Uebernimmt alle Flaechen eines anderen Teils (z. B. die Scheiben ins Atlas-Netz)."""
        self.flaechen.extend(anderes.flaechen)

    # ── Grundformen ────────────────────────────────────────────────────────────────────────────────────────────────

    def vieleck(self, stoff, punkte, uvs, weg_von=None, zu=None, farbe=SCHWARZ, glatt=False):
        """Flaeche aus 3 oder 4 Punkten. `weg_von`/`zu`: Punkt, von dem die Sichtseite weg- bzw. zu dem sie hinzeigt."""
        punkte = [Vector(p) for p in punkte]
        n = (punkte[1] - punkte[0]).cross(punkte[2] - punkte[0])
        mitte = sum(punkte, Vector()) / len(punkte)
        drehen = False
        if weg_von is not None: drehen = n.dot(mitte - Vector(weg_von)) < 0
        if zu is not None: drehen = n.dot(Vector(zu) - mitte) < 0
        if drehen:
            punkte = punkte[::-1]; uvs = uvs[::-1]
        self.flaechen.append((punkte, list(uvs), stoff, farbe, glatt))

    def rechteck(self, stoff, o, eu, ev, b, h, band, laengs="u", u0=None, anteil=(0.0, 1.0), u_bereich=None, **kw):
        """Rechteck o + s*eu + t*ev (s bis b, t bis h) auf dem Band `band`.

        laengs="u": die Textur laeuft entlang eu, quer (t) fuellt sie den Anteil des Bandes (t = 0 ist der untere Bandrand).
        laengs="v": die Textur laeuft entlang ev (stehende Balken), quer ist s."""
        o, eu, ev = Vector(o), Vector(eu).normalized(), Vector(ev).normalized()
        v0, v1, m = self.baender[band]
        if u0 is None: u0 = self.zufall.random()
        unten = v1 - anteil[0] * (v1 - v0); oben = v1 - anteil[1] * (v1 - v0)
        pts = [o, o + eu * b, o + eu * b + ev * h, o + ev * h]
        if u_bereich:                      # festes Farbfeld (Kleinteile): die Flaeche bleibt ganz in diesem Ausschnitt
            ua, ub = u_bereich
            uvs = [(ua, unten), (ub, unten), (ub, oben), (ua, oben)]
            return self.vieleck(stoff, pts, uvs, **kw)
        if laengs == "u":
            uvs = [(u0, unten), (u0 + b / m, unten), (u0 + b / m, oben), (u0, oben)]
        else:
            uvs = [(u0, unten), (u0, oben), (u0 + h / m, oben), (u0 + h / m, unten)]
        self.vieleck(stoff, pts, uvs, **kw)

    def welt(self, stoff, punkte, band, o, eu, zbasis, hoehe, m=None, **kw):
        """Vieleck in einer senkrechten Wand: UV.x aus der Lage entlang eu, UV.y aus der Hoehe ueber `zbasis` (Putz, Lehmwand)."""
        v0, v1, bm = self.baender[band]
        m = m or bm
        o, eu = Vector(o), Vector(eu).normalized()
        uvs = []
        for p in punkte:
            p = Vector(p)
            t = min(max((p.z - zbasis) / hoehe, 0.0), 1.0)
            uvs.append(((p - o).dot(eu) / m, v1 - t * (v1 - v0)))
        self.vieleck(stoff, punkte, uvs, **kw)

    def kasten(self, stoff, mn, mx, band, ohne=(), laengs=None, farbe=SCHWARZ, baender=None):
        """Achsparalleler Kasten. `ohne`: Seiten, die fehlen ("-x", "+x", "-y", "+y", "-z", "+z").
        Die Textur laeuft je Seite entlang der laengeren Kante (oder entlang der Achse `laengs`: "x", "y", "z").
        `baender`: abweichendes Band fuer einzelne Seiten, z. B. {"+z": "bohlen"}."""
        mn, mx = Vector(mn), Vector(mx)
        mitte = (mn + mx) / 2
        achsen = {"x": Vector((1, 0, 0)), "y": Vector((0, 1, 0)), "z": Vector((0, 0, 1))}
        for achse, (a, b) in (("x", ("y", "z")), ("y", ("x", "z")), ("z", ("x", "y"))):
            for seite, wert in (("-", mn), ("+", mx)):
                if seite + achse in ohne: continue
                o = Vector(mn); setattr(o, achse, getattr(wert, achse))
                la, lb = getattr(mx, a) - getattr(mn, a), getattr(mx, b) - getattr(mn, b)
                richtung = "u" if (laengs == a if laengs in (a, b) else la >= lb) else "v"
                self.rechteck(stoff, o, achsen[a], achsen[b], la, lb, (baender or {}).get(seite + achse, band), laengs=richtung, weg_von=mitte, farbe=farbe)

    def stab(self, stoff, a, b, dicke, band, breite=None, oben=None, kappen=False, u_bereich=None):
        """Kantholz von a nach b (vier Laengsseiten, Textur entlang der Achse). `oben`: ungefaehre Richtung der Breitseite."""
        a, b = Vector(a), Vector(b)
        achse = (b - a); laenge = achse.length; achse.normalize()
        hilfe = Vector(oben) if oben is not None else (Vector((0, 0, 1)) if abs(achse.z) < 0.9 else Vector((0, 1, 0)))
        e1 = achse.cross(hilfe).normalized(); e2 = e1.cross(achse).normalized()
        w = (breite or dicke) / 2; d = dicke / 2
        mitte = (a + b) / 2
        u0 = self.zufall.random()
        for o, ev, h in ((a - e1 * w - e2 * d, e1, 2 * w), (a - e1 * w + e2 * d, e1, 2 * w), (a - e1 * w - e2 * d, e2, 2 * d), (a + e1 * w - e2 * d, e2, 2 * d)):
            self.rechteck(stoff, o, achse, ev, laenge, h, band, u0=u0, weg_von=mitte, u_bereich=u_bereich)
        if kappen:
            for p in (a, b):
                self.rechteck(stoff, p - e1 * w - e2 * d, e1, e2, 2 * w, 2 * d, band, weg_von=mitte, u_bereich=u_bereich)

    def brett(self, stoff, a, b, breite, n, band, tiefe=0.05, seiten=True, farbe=SCHWARZ, laengs="u"):
        """Balken auf einer Wand: von a nach b (Mittellinie in der Wandebene), `breite` quer, steht um `tiefe` entlang n vor.
        Sichtseite und (mit `seiten`) die zwei Laengsseiten; fuer Fachwerk, Streben, Rahmen."""
        a, b, n = Vector(a), Vector(b), Vector(n).normalized()
        achse = (b - a); laenge = achse.length; achse.normalize()
        quer = n.cross(achse).normalized()
        u0 = self.zufall.random()
        vorn = a - quer * breite / 2 + n * tiefe
        innen = (a + b) / 2 - n
        self.rechteck(stoff, vorn, achse, quer, laenge, breite, band, u0=u0, weg_von=innen, farbe=farbe)   # Maserung immer entlang des Balkens
        if seiten:
            for kante in (a - quer * breite / 2, a + quer * breite / 2):
                self.rechteck(stoff, kante, achse, n, laenge, tiefe, band, u0=u0, weg_von=(a + b) / 2, anteil=(0.0, 0.3), farbe=farbe)

    def wand(self, stoff, o, eu, breite, z0, z1, band, loecher=(), zbasis=None, hoehe=2.2, laibung=None, laibung_tiefe=0.0, u_start=0.0, **kw):
        """Senkrechte Wand mit rechteckigen Oeffnungen: o ist die untere Ecke (z wird durch z0 ersetzt), eu die Richtung der Breite.
        `loecher`: (s0, s1, za, zb) in Wandkoordinaten. `laibung`: (Stoff, Band) fuer die vier Flaechen in der Oeffnung, die um
        `laibung_tiefe` hinter die Wand reichen (entgegen der Sichtseite)."""
        o, eu = Vector(o), Vector(eu).normalized()
        o = Vector((o.x, o.y, 0.0))
        ez = Vector((0, 0, 1))
        zbasis = z0 if zbasis is None else zbasis
        ganz = list(loecher)                                   # die Laibung nimmt die ganze Oeffnung, die Wand nur ihren Anteil
        loecher = [(a, b, max(c, z0), min(d, z1)) for a, b, c, d in loecher if min(d, z1) > max(c, z0)]
        ss = sorted({0.0, breite} | {x for l in loecher for x in l[:2]})
        zs = sorted({z0, z1} | {z for l in loecher for z in l[2:]})
        urs = o - eu * u_start
        for i in range(len(ss) - 1):
            for j in range(len(zs) - 1):
                sm, zm = (ss[i] + ss[i + 1]) / 2, (zs[j] + zs[j + 1]) / 2
                if any(l[0] < sm < l[1] and l[2] < zm < l[3] for l in loecher): continue
                pts = [o + eu * ss[i] + ez * zs[j], o + eu * ss[i + 1] + ez * zs[j], o + eu * ss[i + 1] + ez * zs[j + 1], o + eu * ss[i] + ez * zs[j + 1]]
                self.welt(stoff, pts, band, urs, eu, zbasis, hoehe, **kw)
        if laibung:
            # Sichtseite der Wand bestimmen (wie in vieleck), die Laibung geht nach hinten
            n = eu.cross(ez)
            probe = o + eu * breite / 2 + ez * (z0 + z1) / 2
            if "weg_von" in kw and n.dot(probe - Vector(kw["weg_von"])) < 0: n = -n
            if "zu" in kw and n.dot(Vector(kw["zu"]) - probe) < 0: n = -n
            ls, lb = laibung
            for (s0, s1, za, zb) in ganz:
                mitte = o + eu * (s0 + s1) / 2 + ez * (za + zb) / 2 - n * laibung_tiefe / 2
                for p, richtung, laenge in ((o + eu * s0 + ez * za, eu, s1 - s0), (o + eu * s0 + ez * zb, eu, s1 - s0), (o + eu * s0 + ez * za, ez, zb - za), (o + eu * s1 + ez * za, ez, zb - za)):
                    self.rechteck(ls, p, richtung, -n, laenge, laibung_tiefe, lb, zu=mitte, anteil=(0.0, 0.6))

    def dreh(self, stoff, mitte, profil, band, seiten=6, deckel=True, u_bereich=None, anteil=(0.0, 1.0), gedreht=False, glatt=True):
        """Drehkoerper um die Z-Achse durch `mitte`: profil = [(radius, hoehe), ...] von unten nach oben. Die Flaechen werden weich
        schattiert (`glatt`), so wirkt ein Krug mit wenigen Seiten rund. `gedreht`: die Textur laeuft entlang der Hoehe statt um den
        Koerper (Fassdauben: jede Seite fuellt quer das ganze Band)."""
        mitte = Vector(mitte)
        v0, v1, m = self.baender[band]
        ua, ub = u_bereich if u_bereich else (0.0, 1.0)
        hges = max(profil[-1][1] - profil[0][1], 1e-4)
        vu = v1 - anteil[0] * (v1 - v0); vo = v1 - anteil[1] * (v1 - v0)
        ring = lambda r, z: [mitte + Vector((r * math.cos(2 * math.pi * k / seiten), r * math.sin(2 * math.pi * k / seiten), z)) for k in range(seiten)]
        for (r0, z0), (r1, z1) in zip(profil[:-1], profil[1:]):
            a, b = ring(r0, z0), ring(r1, z1)
            t0 = vu + (vo - vu) * (z0 - profil[0][1]) / hges; t1 = vu + (vo - vu) * (z1 - profil[0][1]) / hges
            for k in range(seiten):
                k2 = (k + 1) % seiten
                x0 = ua + (ub - ua) * k / seiten; x1 = ua + (ub - ua) * (k + 1) / seiten
                pts = [a[k], a[k2], b[k2], b[k]]; uvs = [(x0, t0), (x1, t0), (x1, t1), (x0, t1)]
                if gedreht:
                    h0, h1 = ua + (z0 - profil[0][1]) / m, ua + (z1 - profil[0][1]) / m
                    uvs = [(h0, vu), (h0, vo), (h1, vo), (h1, vu)]
                if r1 < 1e-5: pts, uvs = pts[:3], uvs[:3]
                elif r0 < 1e-5: pts, uvs = [a[k], b[k2], b[k]], [uvs[0], uvs[2], uvs[3]]
                self.vieleck(stoff, pts, uvs, weg_von=mitte + Vector((0, 0, (z0 + z1) / 2)), glatt=glatt)
        if deckel and profil[-1][0] > 1e-5:
            r, z = profil[-1]
            a = ring(r, z)
            um, vm = (ua + ub) / 2, (vu + vo) / 2
            for k in range(1, seiten - 1):
                self.vieleck(stoff, [a[0], a[k], a[k + 1]], [(um, vm)] * 3, weg_von=mitte)

    # ── Blender-Objekt ─────────────────────────────────────────────────────────────────────────────────────────────

    def objekt(self, stoffe, sammlung=None, mit_farbe=True):
        """Baut das Objekt: Ecken je Flaeche, UV-Kanal `UVMap`, Eckenfarbe `Col`. Gleiche Ecken werden danach verschmolzen."""
        ecken, flaechen, uvs, farben, mats = [], [], [], [], []
        namen = []
        glatt = []
        for pts, uv, stoff, farbe, weich in self.flaechen:
            glatt.append(weich)
            start = len(ecken)
            ecken.extend(tuple(p) for p in pts)
            flaechen.append(tuple(range(start, start + len(pts))))
            uvs.extend(uv); farben.extend([farbe] * len(pts))
            if stoff not in namen: namen.append(stoff)
            mats.append(namen.index(stoff))
        netz = bpy.data.meshes.new(self.name)
        netz.from_pydata(ecken, [], flaechen)
        for n in namen: netz.materials.append(stoffe[n])
        netz.polygons.foreach_set("material_index", mats)
        netz.polygons.foreach_set("use_smooth", glatt)
        kanal = netz.uv_layers.new(name="UVMap")
        for i, (u, v) in enumerate(uvs): kanal.data[i].uv = (u, 1.0 - v)
        if mit_farbe:
            col = netz.color_attributes.new(name="Col", type="BYTE_COLOR", domain="CORNER")
            for i, f in enumerate(farben): col.data[i].color = f
        netz.update()
        obj = bpy.data.objects.new(self.name, netz)
        (sammlung or bpy.context.scene.collection).objects.link(obj)
        return obj
