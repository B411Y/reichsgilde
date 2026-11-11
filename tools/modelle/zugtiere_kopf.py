"""Zugtiere Runde 9: neuer Pferdekopf (Eigenbau) statt des Keilkopfes der Quaternius-Vorlage.

Der Kopf ist ein Längsschnitt-Körper (Ringe von der Stirn zum Maul, je Ring ein abgerundetes Oval mit eigener Breite, Oberhöhe und Tiefe):
Backenknochen und Ganasche (Kieferwinkel) stehen heraus, der Nasenrücken ist schlank, das Maul rund mit Nüstern und Lippenfurche, über
den Augen liegt ein Augenbogen, die Augen sind eingesetzte Kugeln mit Lidring. Die Ohren sind eigene Blätter an den vorhandenen Ohrknochen.
Das alte Kopfnetz wird gelöscht (Flächen mit Gewicht im Kopf), die offene Kante liegt im hinteren Ring des neuen Kopfes verborgen.
Alle Maße in Metern, Skelett im Ruhezustand.
"""

import math

import bmesh
from mathutils import Vector

# Kopf-Längsschnitte: s (m vom Genick), halbe Breite, Höhe über der Mittellinie, Tiefe darunter
SCHNITTE = [
    (-0.12, 0.116, 0.105, 0.095),
    (-0.06, 0.118, 0.108, 0.125),
    (0.00, 0.118, 0.108, 0.165),
    (0.06, 0.116, 0.102, 0.205),
    (0.12, 0.112, 0.096, 0.232),
    (0.17, 0.102, 0.088, 0.215),
    (0.22, 0.088, 0.078, 0.150),
    (0.28, 0.074, 0.072, 0.098),
    (0.36, 0.063, 0.068, 0.078),
    (0.46, 0.057, 0.063, 0.066),
    (0.54, 0.056, 0.058, 0.062),
    (0.60, 0.060, 0.056, 0.060),
    (0.65, 0.072, 0.060, 0.068),
    (0.69, 0.077, 0.062, 0.067),
    (0.715, 0.073, 0.059, 0.061),
    (0.733, 0.058, 0.046, 0.048),
    (0.744, 0.024, 0.020, 0.020),
    (0.749, 0.006, 0.005, 0.005),
]
RINGZAHL_UM = 30
KOPF_LAENGE = 1.04
KOPF_QUER = 1.32
OHR_LAENGE = 0.23
KOPF_NEIGUNG = 50.0       # Grad unter der Waagerechten (die Vorlage hat 34)
HALS_ENDRADIUS = 0.098
HALS_ANFANG = 0.0


def _smooth01(x):
    x = max(0.0, min(1.0, x))
    return x * x * (3 - 2 * x)


def _tab(s, spalte):
    t = SCHNITTE
    if s <= t[0][0]:
        return t[0][spalte]
    for i in range(len(t) - 1):
        if s <= t[i + 1][0]:
            u = (s - t[i][0]) / (t[i + 1][0] - t[i][0])
            # Catmull-Rom über vier Stützstellen (weich, ohne Knick)
            p0 = t[max(i - 1, 0)][spalte]
            p1, p2 = t[i][spalte], t[i + 1][spalte]
            p3 = t[min(i + 2, len(t) - 1)][spalte]
            return 0.5 * ((2 * p1) + (-p0 + p2) * u + (2 * p0 - 5 * p1 + 4 * p2 - p3) * u * u + (-p0 + 3 * p1 - 3 * p2 + p3) * u ** 3)
    return t[-1][spalte]


def _sp(x, e):
    return math.copysign(abs(x) ** (2.0 / e), x)


def _gauss(x, m, sg):
    return math.exp(-0.5 * ((x - m) / sg) ** 2)


def _winkeldiff(a, b):
    d = (a - b + math.pi) % (2 * math.pi) - math.pi
    return d


def punkt(s, th):
    """Lage in der Kopfebene (x seitlich, z nach oben) für Längsmaß s und Umfangswinkel th (0 = seitlich, 90 Grad = oben)."""
    wx, top, bot = _tab(s, 1), _tab(s, 2), _tab(s, 3)
    c, sn = math.cos(th), math.sin(th)
    e = 2.35 if s < 0.60 else 2.6
    x = wx * _sp(c, e)
    z = (top if sn >= 0 else bot) * _sp(sn, 2.0 if sn < 0 else e)
    # Formen am Kopf: wirken radial (nach außen bzw. innen)
    th_ = abs(th) if abs(th) <= math.pi else 2 * math.pi - abs(th)
    # Seite spiegeln: Funktionen nur von |x|-Seite abhängig, daher th auf die rechte Hälfte spiegeln
    tr = math.atan2(sn, abs(c))                                   # -90..90 Grad
    zu = 0.0
    zu += 0.0150 * _gauss(s, 0.105, 0.045) * _gauss(tr, math.radians(52), 0.34)          # Augenbogen über dem Auge
    zu += -0.0040 * _gauss(s, 0.105, 0.030) * _gauss(tr, math.radians(24), 0.22)         # Augenhöhle (flache Mulde unter dem Bogen)
    zu += 0.0160 * _gauss(s, 0.140, 0.065) * _gauss(tr, math.radians(-42), 0.45)         # Backe, Ganasche
    zu += 0.0070 * _gauss(s, 0.255, 0.075) * _gauss(tr, math.radians(8), 0.26)           # Gesichtsleiste (Wangenkamm)
    zu += -0.0060 * _gauss(s, 0.30, 0.05) * _gauss(tr, math.radians(-22), 0.30)         # Mulde vor der Ganasche
    zu += 0.0050 * _gauss(s, 0.665, 0.030) * _gauss(tr, math.radians(40), 0.45)          # Nüsternwulst
    zu += -0.0110 * _gauss(s, 0.665, 0.060) * _gauss(tr, math.radians(-18), 0.22)        # Lippenfurche
    zu += 0.0040 * _gauss(s, 0.40, 0.14) * _gauss(tr, math.radians(90), 0.40)           # Nasenrücken leicht eingezogen (Seitenansicht)
    # Nüstern: tiefe Vertiefung an der Stirn des Mauls
    zu += -0.0140 * _gauss(s, 0.717, 0.011) * _gauss(tr, math.radians(48), 0.22)
    r = math.hypot(x, z)
    if r > 1e-6 and zu != 0.0:
        f = (r + zu) / r
        x *= f
        z *= f
    return x * (1.0 if c >= 0 else 1.0), z


def kopf_rahmen(ske):
    """Genickpunkt p0, Kopfachse a, seitlich lat, nach oben up (Meter, Ruhelage)."""
    kh = Vector(ske.data.bones["Head"].head_local)
    kt = Vector(ske.data.bones["Head"].tail_local)
    ph = math.radians(KOPF_NEIGUNG)
    a = Vector((0.0, -math.cos(ph), -math.sin(ph)))
    up = Vector((0.0, -math.sin(ph), math.cos(ph)))
    p0 = Vector((0.0, kh.y + 0.05, kh.z - 0.115))
    return p0, a, Vector((1, 0, 0)), up


def ohr_ansatz(ske, seite):
    p0, a, lat, up = kopf_rahmen(ske)
    s_e, tr = -0.01, math.radians(70)
    x, z = punkt(s_e, tr)
    basis = p0 + a * (s_e * KOPF_LAENGE) + (lat * (seite * x * 0.80) + up * (z * 0.90)) * KOPF_QUER
    richt = Vector((seite * 0.16, -0.20, 1.0)).normalized()
    return basis, basis + richt * OHR_LAENGE


def ohren_knochen(ske, ob):
    """Ohrknochen Ohr_L/Ohr_R an den Ohransatz des neuen Kopfes (statt aus dem alten Netz)."""
    import bpy
    bpy.context.view_layer.objects.active = ske
    for o in bpy.context.scene.objects:
        o.select_set(o is ske)
    bpy.ops.object.mode_set(mode="EDIT")
    for seite, name in ((1, "Ohr_L"), (-1, "Ohr_R")):
        b, t = ohr_ansatz(ske, seite)
        eb = ske.data.edit_bones.new(name)
        eb.head = b
        eb.tail = t
        eb.parent = ske.data.edit_bones["Head"]
    bpy.ops.object.mode_set(mode="OBJECT")
    for name in ("Ohr_L", "Ohr_R"):
        ob.vertex_groups.new(name=name)


def kopf_pferd(bm, gw, ske, ob, platz_fell=0, platz_dunkel=1, platz_maul=2, platz_tief=5):
    """Baut den Pferdekopf neu. Gibt eine kurze Beschreibung zurück."""
    p0, a, lat, up = kopf_rahmen(ske)
    ohr = [n for n in ("Ohr_L", "Ohr_R") if n in gw.idx]
    kopfgr = ["Head"] + ohr
    alt = {v for v in bm.verts if gw.summe(v, kopfgr) > 0.5}
    loeschen = [fl for fl in bm.faces if any(v in alt for v in fl.verts)]
    rand = set()
    for fl in loeschen:
        for v in fl.verts:
            if v not in alt:
                rand.add(v)
    bmesh.ops.delete(bm, geom=loeschen, context="FACES")
    lose = [v for v in bm.verts if not v.link_faces]
    bmesh.ops.delete(bm, geom=lose, context="VERTS")
    rand = [v for v in rand if v.is_valid]
    # Hals zum Kopf hin verjuengen (die Vorlage hat einen sehr dicken Hals bis zum Kopf): am Rand soll er unter das hintere Oval des Kopfes passen
    na, nb = Vector(ske.data.bones["Neck"].head_local), Vector(ske.data.bones["Neck"].tail_local)
    nax = (nb - na)
    nl = nax.length
    nax.normalize()
    if rand:
        rr = max(((v.co - na) - nax * (v.co - na).dot(nax)).length for v in rand)
        zielr = HALS_ENDRADIUS
        f_end = min(1.0, zielr / max(rr, 1e-6))
        print("Hals: Randradius", round(rr, 3), "Faktor am Rand", round(f_end, 3), "Randpunkte", len(rand))
        for v in bm.verts:
            w = gw.summe(v, ["Neck"])
            if w <= 0.0:
                continue
            t = (v.co - na).dot(nax) / nl
            k = _smooth01((t - HALS_ANFANG) / (1.0 - HALS_ANFANG + 0.12))
            f = 1.0 - (1.0 - f_end) * k * min(1.0, w * 1.5)
            ax_pt = na + nax * (v.co - na).dot(nax)
            v.co = ax_pt + (v.co - ax_pt) * f
    # offene Kante des Halses schliessen (bleibt unter dem Kopf)
    if rand:
        rs = set(rand)
        kanten = [e for e in bm.edges if e.is_boundary and e.verts[0] in rs and e.verts[1] in rs]
        if kanten:
            bmesh.ops.holes_fill(bm, edges=kanten, sides=40)
            for fl in bm.faces:
                if fl.material_index == 0 and not fl.smooth:
                    fl.smooth = True
    ringe = []
    s_liste = [t[0] for t in SCHNITTE]
    # feinere Teilung zwischen den Stützstellen
    fein = []
    for i in range(len(s_liste) - 1):
        n = 2 if s_liste[i + 1] - s_liste[i] > 0.045 else 1
        for k in range(n):
            fein.append(s_liste[i] + (s_liste[i + 1] - s_liste[i]) * k / n)
    fein.append(s_liste[-1])
    n_um = RINGZAHL_UM
    for s in fein:
        ring = []
        for k in range(n_um):
            th = 2 * math.pi * k / n_um
            x, z = punkt(s, th)
            ring.append(bm.verts.new(p0 + (a * (s * KOPF_LAENGE) + (lat * x + up * z) * KOPF_QUER)))
        ringe.append((s, ring))
    gewichte = {}
    for s, ring in ringe:
        w_hals = max(0.0, min(1.0, (0.02 - s) / 0.14)) ** 1.5     # hinten am Hals mitgehen
        for v in ring:
            v[gw.dl][gw.idx["Head"]] = 1.0 - w_hals
            if w_hals > 0:
                v[gw.dl][gw.idx["Neck"]] = w_hals
    nuest = []
    for i in range(len(ringe) - 1):
        s0, r0 = ringe[i]
        s1, r1 = ringe[i + 1]
        for k in range(n_um):
            vs = (r0[k], r0[(k + 1) % n_um], r1[(k + 1) % n_um], r1[k])
            try:
                fl = bm.faces.new(vs)
            except ValueError:
                continue
            fl.smooth = True
            sm = 0.5 * (s0 + s1)
            thm = 2 * math.pi * (k + 0.5) / n_um
            trm = math.atan2(math.sin(thm), abs(math.cos(thm)))
            pl = platz_fell
            if sm > 0.725 and trm < math.radians(30):
                pl = platz_maul                                    # Maul: helleres, kurzes Haar
            nu_d = math.hypot(_winkeldiff(trm, math.radians(48)) * 0.9, (sm - 0.717) * 1.5)
            fl.material_index = pl
    # Auge: Kugel im Kopf, Rand (Lid) = Wulst der Form
    for seite in (1, -1):
        s_e, tr_e = 0.115, math.radians(40)
        x, z = punkt(s_e, tr_e if seite > 0 else math.pi - tr_e)
        c = p0 + (a * (s_e * KOPF_LAENGE) + (lat * x + up * z) * KOPF_QUER)
        nach_aussen = (lat * x + up * z).normalized()
        ctr = c - nach_aussen * 0.004
        lid = bmesh.ops.create_uvsphere(bm, u_segments=12, v_segments=8, radius=0.035)
        for v in lid["verts"]:
            v.co = c - nach_aussen * 0.012 + v.co * Vector((0.95, 1.1, 0.85))
            v[gw.dl][gw.idx["Head"]] = 1.0
        for fl in {f for v in lid["verts"] for f in v.link_faces}:
            fl.material_index = platz_fell
            fl.smooth = True
        geo = bmesh.ops.create_uvsphere(bm, u_segments=12, v_segments=8, radius=0.028)
        vs = geo["verts"]
        # in die Länge ziehen (Mandelform) und zum Kopf ausrichten
        for v in vs:
            v.co = ctr + (v.co * Vector((0.80, 1.18, 1.0)))
            v[gw.dl][gw.idx["Head"]] = 1.0
        for fl in {f for v in vs for f in v.link_faces}:
            fl.material_index = platz_tief
            fl.smooth = True
    for seite in (1, -1):
        s_n, tr_n = 0.712, math.radians(46)
        x, z = punkt(s_n, tr_n if seite > 0 else math.pi - tr_n)
        c = p0 + (a * (s_n * KOPF_LAENGE)) + (lat * x + up * z) * KOPF_QUER
        nach_aussen = (a * 0.7 + (lat * x + up * z).normalized() * 0.7).normalized()
        geo = bmesh.ops.create_uvsphere(bm, u_segments=10, v_segments=6, radius=0.019)
        for v in geo["verts"]:
            q = v.co * Vector((0.55, 0.8, 1.15))
            v.co = c - nach_aussen * 0.006 + q
            v[gw.dl][gw.idx["Head"]] = 1.0
        for fl in {f for v in geo["verts"] for f in v.link_faces}:
            fl.material_index = platz_tief
            fl.smooth = True
    # Ohren: Blätter an den vorhandenen Ohrknochen
    for name in ohr:
        seite = 1 if name == "Ohr_L" else -1
        start, _spitze = ohr_ansatz(ske, seite)
        laenge = OHR_LAENGE
        wurz = 0.056
        punkte = []
        for k in range(8):
            u = k / 7.0
            dirv = (Vector((seite * 0.20, -0.16, 1.0)) + Vector((-seite * 0.10 * u, -0.06 * u * u, 0.0)))   # fast gerade, Spitze leicht nach vorn
            dirv.normalize()
            punkte.append(start + dirv * (laenge * u) + Vector((0, 0, -0.01)))
        radien_br, radien_di = [], []
        for k in range(8):
            u = k / 7.0
            br = wurz * (1.0 - 0.88 * (u ** 1.4)) * (1.0 if u > 0.04 else 0.8)
            br *= 1.0 + 0.40 * math.sin(math.pi * min(u * 1.25, 1.0))      # Bauch in der Mitte
            di = 0.027 * (1.0 - 0.40 * u)
            radien_br.append(br)
            radien_di.append(di)
        ringe_o = []
        n_o = 8
        for k, p in enumerate(punkte):
            tt = (punkte[min(k + 1, 7)] - punkte[max(k - 1, 0)]).normalized()
            vor = Vector((0, -1, 0))
            u_vec = vor - tt * vor.dot(tt)
            u_vec.normalize()
            br_vec = tt.cross(u_vec).normalized()
            ring = []
            for q in range(n_o):
                w = 2 * math.pi * q / n_o
                ring.append(bm.verts.new(p + br_vec * math.cos(w) * radien_br[k] + u_vec * math.sin(w) * radien_di[k]))
            ringe_o.append(ring)
        for k in range(7):
            u = (k + 0.5) / 7.0
            for q in range(n_o):
                fl = bm.faces.new((ringe_o[k][q], ringe_o[k][(q + 1) % n_o], ringe_o[k + 1][(q + 1) % n_o], ringe_o[k + 1][q]))
                fl.smooth = True
                fl.material_index = platz_dunkel if u > 0.86 else platz_fell
        fl = bm.faces.new(ringe_o[-1])
        fl.material_index = platz_dunkel
        fl.smooth = True
        for k, ring in enumerate(ringe_o):
            u = k / 7.0
            w = u * u * (3 - 2 * u)
            for v in ring:
                v[gw.dl][gw.idx[name]] = max(w, 0.02)
                v[gw.dl][gw.idx["Head"]] = 1.0 - max(w, 0.02)
    return f"Kopf neu: {len(ringe)} Ringe, alt entfernt {len(loeschen)} Flaechen, Genick {tuple(round(c, 3) for c in p0)}"
