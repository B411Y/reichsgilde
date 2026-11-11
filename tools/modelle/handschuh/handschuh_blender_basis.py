"""3D-Handschuh als Mauszeiger (Entwurf, Teil 1): Modell, Gerüst und Bewegungen, noch nicht im Spiel.

    blender -b --factory-startup --python tools/modelle/handschuh3d.py -- --aus=<Ordner fuer die .glb> --bilder=<Ordner>
        [--nur=bau|clips|alles] [--test=pose]

Quelle der Hand: MakeHuman/MPFB-Grundnetz (MPFB 2.0.18, Netz und Gewichte CC0) mit dem Fingerskelett „default“; die Hand wird
im Ruhezustand herausgelöst (Handwurzel, Handfläche, fünf Finger, kurzes Unterarmstück), mit der Handwurzel als Ursprung so gedreht,
dass die Finger nach +Y zeigen und der Handrücken nach +Z. Darüber der Handschuh als Netz: Leder um 3,5 mm aufgedickt, Falten an
den Fingergelenken (Verschiebung und Eckenfarbe), Stulpe mit Rollkante und dunklem Futter, schmaler Riemen mit Messingschnalle und
losem Ende, dazu ein Umriss-Netz (invertierte Hülle) und das Fragezeichen für den Hilfe-Zeiger. Leder gemalt per Skript (numpy):
Farbtextur und Normalkarte, 512 x 512, kachelbar.

Skelett (Auszug aus „default“): Wurzel, lowerarm01/02, wrist, metacarpal1–4, finger1–5 je 3 Glieder (Daumen = finger1).
Marke `Anker_Spitze` (Empty) an der Zeigefingerspitze: exakter Klickpunkt.
Bewegungen als Aktionen (24 Bilder/s): Ruhe, Zeigen, Tippen, Greifen und sechs Übergänge.

Nichts aus „Die Gilde“, kein Human Generator, kein KI-Netz. MPFB und Blender nur als Werkzeug; Ausgabe gehört uns.
"""

import math
import os
import sys
import importlib
import addon_utils

import bmesh
import bpy
import numpy as np
from mathutils import Matrix, Vector, Quaternion

HIER = os.path.dirname(os.path.abspath(__file__))
FPS = 24

# Maße in Metern (1 Einheit = 1 m); die Hand ist etwa 0,19 m lang
DICKE = 0.0035          # Aufdickung des Leders
UMRISS = 0.0036         # Abstand der Umrisshülle vom Leder
HAND_VERHAELTNIS = 0.42
SCHNITT = 0.060         # Unterarmstück bis so weit hinter dem Handgelenk
FINGER_KNOCHEN = {"daumen": 1, "zeige": 2, "mittel": 3, "ring": 4, "klein": 5}

BEHALTEN = ["lowerarm01.R", "lowerarm02.R", "wrist.R"] + [f"metacarpal{i}.R" for i in (1, 2, 3, 4)] + \
    [f"finger{k}-{g}.R" for k in range(1, 6) for g in (1, 2, 3)]


def args():
    a = {}
    if "--" in sys.argv:
        for s in sys.argv[sys.argv.index("--") + 1:]:
            if s.startswith("--"):
                k, _, v = s[2:].partition("=")
                a[k] = v if v else "1"
    return a


def holen(paket, name):
    for m in list(sys.modules):
        if m.endswith(paket):
            return getattr(importlib.import_module(m), name)
    raise RuntimeError("MPFB nicht gefunden: " + paket)


# ─────────────────────────────────────────────────────────────────────────────
#  Hand aus MPFB herauslösen, in Zeigerlage drehen
# ─────────────────────────────────────────────────────────────────────────────

def hand_holen():
    addon_utils.enable("bl_ext.user_default.mpfb", default_set=True, persistent=False)
    ts = holen("mpfb.services.targetservice", "TargetService")
    hs = holen("mpfb.services.humanservice", "HumanService")
    d = ts.get_default_macro_info_dict()
    d["gender"] = 1.0
    basis = hs.create_human(mask_helpers=False, detailed_helpers=True, extra_vertex_groups=False, macro_detail_dict=d)
    rig = hs.add_builtin_rig(basis, "default")
    bpy.context.view_layer.update()
    kn = rig.data.bones
    # Handachsen im Ruhezustand: f Finger, t Daumenseite, n Handrücken
    f = (kn["finger3-3.R"].tail_local - kn["finger3-1.R"].head_local).normalized()
    t = kn["finger1-1.R"].head_local - kn["finger5-1.R"].head_local
    t = (t - f * t.dot(f)).normalized()
    n = f.cross(t).normalized()
    arm = (kn["wrist.R"].head_local - kn["lowerarm02.R"].head_local).normalized()
    wk = Vector(kn["wrist.R"].head_local)
    y = f.copy()
    z = (n - y * n.dot(y)).normalized()
    x = y.cross(z).normalized()
    R = Matrix(((x.x, x.y, x.z, 0), (y.x, y.y, y.z, 0), (z.x, z.y, z.z, 0), (0, 0, 0, 1)))
    M = R @ Matrix.Translation(-wk)
    print("Basis: Welt", rig.matrix_world.to_translation(), "Eltern", basis.parent)

    # Netz: ausgewertetes Netz (Formziele eingerechnet) statt der Grundform, Formziele danach nicht mehr nötig
    dg = bpy.context.evaluated_depsgraph_get()
    me_ausgewertet = bpy.data.meshes.new_from_object(basis.evaluated_get(dg))
    alt = basis.data
    basis.data = me_ausgewertet
    bpy.data.meshes.remove(alt)
    me = basis.data
    gname = {g.index: g.name for g in basis.vertex_groups}
    gw = np.zeros(len(me.vertices))
    haut = np.zeros(len(me.vertices), dtype=bool)
    for v in me.vertices:
        for ge in v.groups:
            nm = gname.get(ge.group)
            if nm in BEHALTEN:
                gw[v.index] += ge.weight
            elif nm == "body":
                haut[v.index] = True
    co = np.array([(rig.matrix_world @ v.co)[:] for v in me.vertices])
    wk_w = np.array((rig.matrix_world @ wk)[:])
    along = (co - wk_w) @ np.array(arm[:])
    keep = (gw > 0.5) & haut & (along > -SCHNITT)
    bm = bmesh.new()
    bm.from_mesh(me)
    bm.verts.ensure_lookup_table()
    wegv = [v for v in bm.verts if not keep[v.index]]
    bmesh.ops.delete(bm, geom=wegv, context="VERTS")
    # größte zusammenhängende Insel behalten
    bm.verts.ensure_lookup_table()
    gesehen, inseln = set(), []
    for v in bm.verts:
        if v.index in gesehen:
            continue
        st, grp = [v], []
        gesehen.add(v.index)
        while st:
            c = st.pop()
            grp.append(c)
            for e in c.link_edges:
                o = e.other_vert(c)
                if o.index not in gesehen:
                    gesehen.add(o.index)
                    st.append(o)
        inseln.append(grp)
    inseln.sort(key=len, reverse=True)
    rest = [v for g in inseln[1:] for v in g]
    if rest:
        bmesh.ops.delete(bm, geom=rest, context="VERTS")
    bm.to_mesh(me)
    bm.free()
    # Netz in Zeigerlage (Armaturenraum -> Welt über matrix_world des Gerüsts, hier Einheitsmatrix erwartet)
    mw = rig.matrix_world.copy()
    me.transform(M @ mw)
    basis.matrix_world = Matrix.Identity(4)
    basis.parent = None
    # Vertexgruppen aufräumen
    for g in list(basis.vertex_groups):
        if g.name not in BEHALTEN:
            basis.vertex_groups.remove(g)
    for m in list(basis.modifiers):
        basis.modifiers.remove(m)
    # Gerüst
    rig.matrix_world = Matrix.Identity(4)
    bpy.context.view_layer.objects.active = rig
    bpy.ops.object.mode_set(mode="EDIT")
    eb = rig.data.edit_bones
    for b in list(eb):
        if b.name not in BEHALTEN:
            eb.remove(b)
    for b in eb:
        b.transform(M @ mw)
    eb["lowerarm01.R"].parent = None
    w = eb.new("Wurzel")
    w.head = (0, 0, 0)
    w.tail = (0, -0.04, 0)
    w.roll = 0
    eb["lowerarm01.R"].parent = w
    bpy.ops.object.mode_set(mode="OBJECT")
    # sparsam: Hand auf etwa die Hälfte der Flächen vereinfachen (Gewichte bleiben erhalten)
    dec = basis.modifiers.new("Vereinfachen", "DECIMATE")
    dec.ratio = HAND_VERHAELTNIS
    dg2 = bpy.context.evaluated_depsgraph_get()
    neu = bpy.data.meshes.new_from_object(basis.evaluated_get(dg2))
    alt2 = basis.data
    basis.modifiers.remove(dec)
    basis.data = neu
    bpy.data.meshes.remove(alt2)
    print("Hand vereinfacht auf", len(neu.polygons), "Flächen", len(neu.vertices), "Ecken")
    basis.name = "Hand_roh"
    rig.name = "Handschuh_Gerüst".replace("ü", "ue")
    return basis, rig, (f, t, n)


# ─────────────────────────────────────────────────────────────────────────────
#  Leder (gemalt per Skript): Farbtextur und Normalkarte, kachelbar
# ─────────────────────────────────────────────────────────────────────────────

def rauschen(n, zellen, rng):
    """Kachelbares Wertrauschen (bikubisch geglättet) der Größe n x n."""
    g = rng.random((zellen, zellen))
    xs = np.linspace(0, zellen, n, endpoint=False)
    i0 = np.floor(xs).astype(int)
    fr = xs - i0
    fr = fr * fr * (3 - 2 * fr)
    i1 = (i0 + 1) % zellen
    a = g[np.ix_(i0, i0)] * (1 - fr)[None, :] * (1 - fr)[:, None] + g[np.ix_(i0, i1)] * fr[None, :] * (1 - fr)[:, None]
    b = g[np.ix_(i1, i0)] * (1 - fr)[None, :] * fr[:, None] + g[np.ix_(i1, i1)] * fr[None, :] * fr[:, None]
    return a + b


def fbm(n, rng, grund=4, stufen=5, gew=0.55):
    s = np.zeros((n, n))
    amp, tot = 1.0, 0.0
    z = grund
    for _ in range(stufen):
        s += rauschen(n, z, rng) * amp
        tot += amp
        amp *= gew
        z *= 2
    return s / tot


def korn(n, rng, zellen=96):
    """Lederporen: Zellenrauschen (Abstand zum nächsten Punkt), kachelbar. Gibt (Abstand, Rand) zurück."""
    pts = rng.random((zellen * zellen, 2)) * n
    # Gitter aus Zellen mit je einem Punkt (Jitter), Suche in 3x3 Nachbarn mit Umbruch
    k = zellen
    gx, gy = np.meshgrid(np.arange(k), np.arange(k))
    px = (gx + rng.random((k, k)) * 0.9 + 0.05) * (n / k)
    py = (gy + rng.random((k, k)) * 0.9 + 0.05) * (n / k)
    X, Y = np.meshgrid(np.arange(n), np.arange(n))
    ci = (X * k // n)
    cj = (Y * k // n)
    d1 = np.full((n, n), 1e9)
    d2 = np.full((n, n), 1e9)
    for dx in (-1, 0, 1):
        for dy in (-1, 0, 1):
            ii = (ci + dx) % k
            jj = (cj + dy) % k
            qx = px[jj, ii] + ((ci + dx) - ii) * (n / k)
            qy = py[jj, ii] + ((cj + dy) - jj) * (n / k)
            d = np.hypot(X - qx, Y - qy)
            m = d < d1
            d2 = np.where(m, d1, np.where(d < d2, d, d2))
            d1 = np.where(m, d, d1)
    return d1 / (n / k), (d2 - d1) / (n / k)


def png_schreiben(pfad, rgb):
    """Schreibt ein 8-Bit-RGB-PNG ohne Zusatzpakete (Zeilen von oben nach unten)."""
    import zlib
    import struct
    h, w, _ = rgb.shape
    roh = b"".join(bytes([0]) + rgb[y].tobytes() for y in range(h))

    def chunk(t, d):
        c = struct.pack(">I", len(d)) + t + d
        return c + struct.pack(">I", zlib.crc32(t + d) & 0xFFFFFFFF)
    with open(pfad, "wb") as f:
        f.write(bytes([137]) + b"PNG" + bytes([13, 10, 26, 10]) + chunk(b"IHDR", struct.pack(">IIBBBBB", w, h, 8, 2, 0, 0, 0)) +
                chunk(b"IDAT", zlib.compress(roh, 9)) + chunk(b"IEND", b""))


def leder_texturen(aus, seed=7, n=512):
    rng = np.random.default_rng(seed)
    grob = fbm(n, rng, 3, 5)
    mittel = fbm(n, rng, 8, 4)
    fein = fbm(n, rng, 32, 3, 0.6)
    d1, rand = korn(n, rng)
    pore = np.clip(1.0 - rand * 3.2, 0, 1)                       # 1 in den Fugen zwischen den Poren
    kratz = np.zeros((n, n))
    for _ in range(26):                                           # Kratzer: kurze helle Striche
        x0, y0 = rng.random(2) * n
        ang = rng.random() * math.pi
        ln = 20 + rng.random() * 70
        for t in np.linspace(0, ln, int(ln * 2)):
            xx = int(x0 + math.cos(ang) * t) % n
            yy = int(y0 + math.sin(ang) * t) % n
            kratz[yy, xx] = 1.0
    kratz = np.clip(kratz + np.roll(kratz, 1, 0) * 0.5, 0, 1)
    # Farbe (sRGB 0..1): abgetragenes mittleres Braun, nach Vorlage; Abnutzung heller und röter, Flecken dunkler
    dunkel = np.array((0.33, 0.18, 0.09))
    mitte = np.array((0.49, 0.29, 0.15))
    hell = np.array((0.68, 0.44, 0.24))
    t1 = np.clip((grob - 0.30) * 1.6 + 0.25, 0, 1)[..., None]
    farbe = dunkel + (mitte - dunkel) * t1
    abrieb = np.clip((mittel - 0.58) * 2.4, 0, 1)[..., None] * 0.40 + kratz[..., None] * 0.35
    farbe = farbe + (hell - farbe) * abrieb
    farbe *= (0.88 + 0.22 * fein)[..., None]
    farbe *= (1.0 - 0.16 * pore)[..., None]
    fleck = np.clip((fbm(n, rng, 6, 3) - 0.62) * 3.0, 0, 1)[..., None]
    farbe *= (1.0 - 0.38 * fleck)
    farbe = np.clip(farbe, 0, 1)
    # Höhe: Poren und feines Rauschen, dazu langsames Wellen des Leders
    hoehe = 0.30 * (1.0 - pore) + 0.30 * fein + 0.30 * mittel - 0.4 * kratz
    gy, gx = np.gradient(hoehe)
    st = 3.0
    nx, ny, nz = -gx * st, -gy * st, np.ones_like(hoehe)
    ln = np.sqrt(nx * nx + ny * ny + nz * nz)
    nm = np.stack(((nx / ln) * 0.5 + 0.5, (ny / ln) * 0.5 + 0.5, (nz / ln) * 0.5 + 0.5), axis=-1)
    os.makedirs(aus, exist_ok=True)

    def speichern(arr, name, linear):
        pfad = os.path.join(aus, name + ".png")
        png_schreiben(pfad, np.clip(arr * 255.0 + 0.5, 0, 255).astype(np.uint8))
        img = bpy.data.images.load(pfad)
        img.colorspace_settings.name = "Non-Color" if linear else "sRGB"
        img.pack()
        return img
    print("TEXTUR Farbe Mittel", farbe.reshape(-1, 3).mean(axis=0), "Normal Mittel", nm.reshape(-1, 3).mean(axis=0))
    return speichern(farbe, "leder_farbe", False), speichern(nm, "leder_normal", True)


def stoff_bauen(name, farbe_img, normal_img, tint=(1, 1, 1), rau=0.62, metall=0.0, vcol=True, normal_st=1.0, einfarbig=None):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    nt = m.node_tree
    for nd in list(nt.nodes):
        nt.nodes.remove(nd)
    ausg = nt.nodes.new("ShaderNodeOutputMaterial")
    bsdf = nt.nodes.new("ShaderNodeBsdfPrincipled")
    nt.links.new(bsdf.outputs["BSDF"], ausg.inputs["Surface"])
    bsdf.inputs["Roughness"].default_value = rau
    bsdf.inputs["Metallic"].default_value = metall
    if einfarbig is not None:
        bsdf.inputs["Base Color"].default_value = (*einfarbig, 1)
        return m
    tx = nt.nodes.new("ShaderNodeTexImage")
    tx.image = farbe_img
    mix = nt.nodes.new("ShaderNodeMix")
    mix.data_type = "RGBA"
    mix.blend_type = "MULTIPLY"
    mix.inputs[0].default_value = 1.0
    nt.links.new(tx.outputs["Color"], mix.inputs[6])
    mix.inputs[7].default_value = (*tint, 1)
    if vcol:
        vc = nt.nodes.new("ShaderNodeVertexColor")
        vc.layer_name = "Falten"
        mix2 = nt.nodes.new("ShaderNodeMix")
        mix2.data_type = "RGBA"
        mix2.blend_type = "MULTIPLY"
        mix2.inputs[0].default_value = 1.0
        nt.links.new(mix.outputs[2], mix2.inputs[6])
        nt.links.new(vc.outputs["Color"], mix2.inputs[7])
        nt.links.new(mix2.outputs[2], bsdf.inputs["Base Color"])
    else:
        nt.links.new(mix.outputs[2], bsdf.inputs["Base Color"])
    tn = nt.nodes.new("ShaderNodeTexImage")
    tn.image = normal_img
    nmap = nt.nodes.new("ShaderNodeNormalMap")
    nmap.inputs["Strength"].default_value = normal_st
    nt.links.new(tn.outputs["Color"], nmap.inputs["Color"])
    nt.links.new(nmap.outputs["Normal"], bsdf.inputs["Normal"])
    return m


# ─────────────────────────────────────────────────────────────────────────────
#  Netze: Hand verdicken und faltig machen, Stulpe, Riemen, Schnalle
# ─────────────────────────────────────────────────────────────────────────────

def vertexgruppe_setzen(ob, name, ids, w=1.0):
    g = ob.vertex_groups.get(name) or ob.vertex_groups.new(name=name)
    g.add(list(ids), w, "REPLACE")


def hand_dicken(hand, rig):
    """Leder: Netz entlang der Normalen aufdicken, glätten, Falten an Fingergelenken und Handrücken, Eckenfarbe."""
    me = hand.data
    bpy.context.view_layer.objects.active = hand
    me.update()
    n = len(me.vertices)
    P = np.array([v.co[:] for v in me.vertices])
    N = np.array([v.normal[:] for v in me.vertices])
    kn = rig.data.bones
    # je Ecke: Finger mit größtem Gewicht
    gnamen = {g.index: g.name for g in hand.vertex_groups}
    fing = np.zeros((n, 6))
    gew = {}
    for v in me.vertices:
        for ge in v.groups:
            nm = gnamen[ge.group]
            gew[(v.index, nm)] = ge.weight
            if nm.startswith("finger"):
                fing[v.index, int(nm[6])] += ge.weight
    teil = np.where(fing.max(axis=1) > 0.5, fing.argmax(axis=1), 0)
    P2 = P + N * DICKE
    faerb = np.ones((n, 3))
    # Fingergelenke: Ringfalte quer zur Fingerachse, auf dem Rücken (Normale·Z > 0) tief, innen Querfalte (Beugefalte) schwächer
    for k in range(1, 6):
        for g in (2, 3):
            b = kn[f"finger{k}-{g}.R"]
            h = np.array(b.head_local[:])
            achse = np.array((b.tail_local - b.head_local).normalized()[:])
            sel = np.where(teil == k)[0]
            if not len(sel):
                continue
            s = (P[sel] - h) @ achse
            rad = P[sel] - h - np.outer(s, achse)
            nah = np.exp(-(s / 0.0075) ** 2)
            rueck = np.clip(N[sel][:, 2] * 1.4 + 0.15, 0, 1)
            # zwei Falten je Gelenk (leichte Wellenform)
            welle = 0.5 + 0.5 * np.cos((s / 0.0065) * math.pi)
            tief = -0.0026 * nah * (0.45 + 0.55 * rueck) * (0.5 + 0.5 * welle)
            P2[sel] += N[sel] * tief[:, None]
            faerb[sel] *= (1.0 - 0.26 * nah * (0.4 + 0.6 * rueck))[:, None]
        # Knöchel (Grundgelenk): Wulst oben, dunkler Saum davor
        b = kn[f"finger{k}-1.R"]
        h = np.array(b.head_local[:])
        achse = np.array((b.tail_local - b.head_local).normalized()[:])
        sel = np.where((teil == k) | (teil == 0))[0]
        s = (P[sel] - h) @ achse
        nah = np.exp(-(((s - 0.001) / 0.012) ** 2)) * np.clip(N[sel][:, 2], 0, 1)
        P2[sel] += N[sel] * (0.0020 * nah)[:, None]
        faerb[sel] *= (1.0 - 0.18 * np.exp(-(((s + 0.012) / 0.009) ** 2)) * np.clip(N[sel][:, 2], 0, 1))[:, None]
    # Fingerspitzen etwas heller (Abrieb), Fingerzwischenräume dunkler (Ecken zwischen zwei Fingern: Gewichte beider)
    for v in range(n):
        ws = [fing[v, k] for k in range(1, 6)]
        top = sorted(ws, reverse=True)
        if top[1] > 0.25:
            faerb[v] *= 0.72 + 0.28 * (1 - min(1, top[1] * 1.5))
    for i, v in enumerate(me.vertices):
        v.co = Vector(P2[i])
    # Eckenfarbe
    ca = me.color_attributes.new("Falten", "FLOAT_COLOR", "POINT")
    for i in range(n):
        ca.data[i].color = (*faerb[i], 1.0)
    return teil


def woelbung_faerben(ob):
    """Gemalter Eindruck: gewölbte Kanten (Fingerspitzen, Knöchel, Saum) heller, Mulden dunkler, in die Eckenfarbe „Falten“."""
    me = ob.data
    bm = bmesh.new()
    bm.from_mesh(me)
    bm.verts.ensure_lookup_table()
    ca = me.color_attributes["Falten"]
    neu = []
    for v in bm.verts:
        nb = [e.other_vert(v) for e in v.link_edges]
        if not nb:
            neu.append(1.0)
            continue
        if v.is_boundary:
            neu.append(1.0)
            continue
        mitte = sum((o.co for o in nb), Vector()) / len(nb)
        mlen = sum((o.co - v.co).length for o in nb) / len(nb)
        konvex = -((mitte - v.co).dot(v.normal)) / max(mlen, 1e-6)
        neu.append(min(1.32, max(0.62, 1.0 + 1.5 * konvex)))
    bm.free()
    for i in range(len(me.vertices)):
        c = ca.data[i].color
        ca.data[i].color = (min(1.4, c[0] * neu[i]), min(1.4, c[1] * neu[i]), min(1.4, c[2] * neu[i]), 1.0)



def uv_kasten(ob, dichte=0.26):
    """Kachelkarte: je Fläche entlang der größten Normalenachse projiziert (Leder ist kachelbar)."""
    me = ob.data
    uvl = me.uv_layers.get("UVMap") or me.uv_layers.new(name="UVMap")
    me.update()
    for p in me.polygons:
        nm = p.normal
        ax = max(range(3), key=lambda i: abs(nm[i]))
        a, b = [(1, 2), (0, 2), (0, 1)][ax]
        for li in p.loop_indices:
            c = me.vertices[me.loops[li].vertex_index].co
            uvl.data[li].uv = (c[a] / dichte, c[b] / dichte)


def stulpe_bauen(hand, rig):
    """Stulpe als Rotationsfläche um die Armachse (−Y), weit und gestülpt; Futter innen. Rückgabe: (stulpe, futter, mass)"""
    P = np.array([v.co[:] for v in hand.data.vertices])
    print("Hand-Box", P.min(axis=0), P.max(axis=0), len(P))
    sel = P[(P[:, 1] > -0.04) & (P[:, 1] < 0.0)]
    cx = float((sel[:, 0].min() + sel[:, 0].max()) / 2)
    cz = float((sel[:, 2].min() + sel[:, 2].max()) / 2)
    ax = float((sel[:, 0].max() - sel[:, 0].min()) / 2)
    az = float((sel[:, 2].max() - sel[:, 2].min()) / 2)
    print("Handgelenk: Mitte", cx, cz, "Halbachsen", ax, az)
    seg, ring = 28, 13
    y0, y1 = 0.022, -0.078                        # Hand-Ende und Öffnung
    def profil(s):                                # s 0 (Hand-Ende) .. 1 (Öffnung)
        faktor = 1.08 + 0.50 * s ** 1.5
        return faktor
    def ellipse(y, extra, d):
        s = (y0 - y) / (y0 - y1)
        fk = profil(min(1.0, s))
        a = (ax * 1.0 + 0.006) * fk + extra
        c = (az * 1.0 + 0.006) * fk * 0.92 + extra
        return a, c
    bm = bmesh.new()
    gitter = []
    ys = [y0 - (y0 - y1) * (i / (ring - 1)) for i in range(ring)]
    for i, y in enumerate(ys):
        zeile = []
        a, c = ellipse(y, 0.0, 0)
        for j in range(seg):
            t = 2 * math.pi * j / seg
            # leichte Unregelmäßigkeit und Falten des weichen Leders
            w = 1.0 + 0.035 * math.sin(3 * t + i * 0.9) + 0.02 * math.sin(7 * t - i * 0.5)
            zeile.append(bm.verts.new((cx + math.cos(t) * a * w, y, cz + math.sin(t) * c * w)))
        gitter.append(zeile)
    # Rollkante: zwei Ringe, die nach außen und etwas zurück gehen, dann wieder nach innen (gestülpter Saum)
    y_rand = y1
    a, c = ellipse(y_rand, 0.0, 0)
    rolle = []
    for (dy, du) in ((-0.004, 0.0045), (-0.0075, 0.0035), (-0.0085, 0.0005), (-0.0065, -0.0025)):
        zeile = []
        for j in range(seg):
            t = 2 * math.pi * j / seg
            w = 1.0 + 0.035 * math.sin(3 * t + (ring - 1) * 0.9) + 0.02 * math.sin(7 * t - (ring - 1) * 0.5)
            zeile.append(bm.verts.new((cx + math.cos(t) * (a + du) * w, y_rand + dy, cz + math.sin(t) * (c + du) * w)))
        rolle.append(zeile)
    alle = gitter + rolle
    for i in range(len(alle) - 1):
        for j in range(seg):
            bm.faces.new((alle[i][j], alle[i][(j + 1) % seg], alle[i + 1][(j + 1) % seg], alle[i + 1][j]))
    bm.normal_update()
    me = bpy.data.meshes.new("Stulpe")
    bm.to_mesh(me)
    bm.free()
    ob = bpy.data.objects.new("Stulpe", me)
    bpy.context.scene.collection.objects.link(ob)
    # Normalen nach außen
    bm = bmesh.new()
    bm.from_mesh(me)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bm.to_mesh(me)
    bm.free()
    # Futter: kleinere, nach innen gewendete Kopie des Stulpenrings (ohne Rolle) bis an die Öffnung
    bm = bmesh.new()
    gi = []
    for i, y in enumerate(ys):
        a, c = ellipse(y, -0.004, 0)
        zeile = []
        for j in range(seg):
            t = 2 * math.pi * j / seg
            w = 1.0 + 0.035 * math.sin(3 * t + i * 0.9) + 0.02 * math.sin(7 * t - i * 0.5)
            zeile.append(bm.verts.new((cx + math.cos(t) * a * w, y, cz + math.sin(t) * c * w)))
        gi.append(zeile)
    for i in range(len(gi) - 1):
        for j in range(seg):
            bm.faces.new((gi[i][j], gi[i][(j + 1) % seg], gi[i + 1][(j + 1) % seg], gi[i + 1][j]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bmesh.ops.reverse_faces(bm, faces=bm.faces)
    me2 = bpy.data.meshes.new("Futter")
    bm.to_mesh(me2)
    bm.free()
    fu = bpy.data.objects.new("Futter", me2)
    bpy.context.scene.collection.objects.link(fu)
    mass = dict(cx=cx, cz=cz, ax=ax, az=az, ellipse=ellipse, y0=y0, y1=y1)
    return ob, fu, mass


def riemen_bauen(mass):
    """Schmaler Lederriemen um die Stulpe nahe der Hand, Messingschnalle auf dem Rücken, loses Ende. Rückgabe (riemen, schnalle)."""
    cx, cz, ellipse = mass["cx"], mass["cz"], mass["ellipse"]
    y_m = -0.034
    breite = 0.0125
    dick = 0.0032
    seg = 36
    bm = bmesh.new()
    a, c = ellipse(y_m, 0.0, 0)
    ringe = []
    for (dy, dr) in ((-breite / 2, 0), (-breite / 2, dick), (breite / 2, dick), (breite / 2, 0)):
        pass
    def p(j, dy, dr, a=a, c=c):
        t = 2 * math.pi * j / seg + 0.0
        return (cx + math.cos(t) * (a + dr + 0.0015), y_m + dy, cz + math.sin(t) * (c + dr + 0.0015))
    quer = [(-breite / 2, 0.0), (-breite / 2, dick), (breite / 2, dick), (breite / 2, 0.0)]
    rg = [[bm.verts.new(p(j, dy, dr)) for j in range(seg)] for (dy, dr) in quer]
    for i in range(4):
        i2 = (i + 1) % 4
        for j in range(seg):
            bm.faces.new((rg[i][j], rg[i][(j + 1) % seg], rg[i2][(j + 1) % seg], rg[i2][j]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    # loses Ende: Streifen, der von der Schnalle schräg nach unten (Richtung Daumenseite) hängt
    t0 = math.pi / 2 - 0.02                          # Rücken (+Z)
    start = Vector((cx + math.cos(t0) * (a + dick + 0.0015), y_m, cz + math.sin(t0) * (c + dick + 0.0015)))
    ende = []
    for k in range(5):
        u = k / 4
        tt = t0 + 0.14 + u * 0.55
        r = a + dick + 0.0035 + 0.0015 * u
        r2 = c + dick + 0.0035 + 0.0015 * u
        ende.append((Vector((cx + math.cos(tt) * r, y_m - 0.001 - 0.018 * u * u, cz + math.sin(tt) * r2)), tt))
    vs = []
    for (pt, tt) in ende:
        nrm = Vector((math.cos(tt), 0, math.sin(tt)))
        vs.append((bm.verts.new(pt - Vector((0, 0.0042, 0))), bm.verts.new(pt + Vector((0, 0.0042, 0))),
                   bm.verts.new(pt - Vector((0, 0.0042, 0)) + nrm * 0.0022), bm.verts.new(pt + Vector((0, 0.0042, 0)) + nrm * 0.0022)))
    for k in range(4):
        a0, a1 = vs[k], vs[k + 1]
        bm.faces.new((a0[2], a0[3], a1[3], a1[2]))                  # Außenseite
        bm.faces.new((a0[0], a1[0], a1[1], a0[1]))                  # Innenseite
        bm.faces.new((a0[0], a0[2], a1[2], a1[0]))
        bm.faces.new((a0[1], a1[1], a1[3], a0[3]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    me = bpy.data.meshes.new("Riemen")
    bm.to_mesh(me)
    bm.free()
    riemen = bpy.data.objects.new("Riemen", me)
    bpy.context.scene.collection.objects.link(riemen)
    # Schnalle: Rahmen (vier Stäbe) mit Dorn, flach auf dem Rücken
    bm = bmesh.new()
    mitte = Vector((cx + math.cos(t0) * (a + dick + 0.003), y_m, cz + math.sin(t0) * (c + dick + 0.003)))
    nrm = Vector((math.cos(t0), 0, math.sin(t0))).normalized()
    quer_v = Vector((0, 1, 0))
    seit = nrm.cross(quer_v).normalized()
    def quader(cm, ex, ey, ez):
        # Mittelpunkt, Halbmaße entlang seit, quer_v, nrm
        pts = []
        for sx in (-1, 1):
            for sy in (-1, 1):
                for sz in (-1, 1):
                    pts.append(bm.verts.new(cm + seit * ex * sx + quer_v * ey * sy + nrm * ez * sz))
        idx = lambda sx, sy, sz: ((sx > 0) * 4 + (sy > 0) * 2 + (sz > 0))
        for q in ((0, 1, 3, 2), (4, 6, 7, 5), (0, 4, 5, 1), (2, 3, 7, 6), (0, 2, 6, 4), (1, 5, 7, 3)):
            bm.faces.new([pts[i] for i in q])
    bw, bh = 0.0085, 0.0090                            # Halbbreite (entlang Umfang) und Halbhöhe (Achse) des Rahmens
    st = 0.0013
    quader(mitte, st, bh, 0.0014)
    quader(mitte + seit * (2 * bw - st), st, bh, 0.0014)
    quader(mitte + seit * (bw - st), bw, st, 0.0014) if False else None
    quader(mitte + seit * (bw - 0.0), bw, st, 0.0014) if False else None
    quader(mitte + quer_v * (bh - st) + seit * (bw - st), bw, st, 0.0014)
    quader(mitte - quer_v * (bh - st) + seit * (bw - st), bw, st, 0.0014)
    quader(mitte + seit * (bw - st), bw * 0.85, 0.0009, 0.0018)    # Dorn
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    me = bpy.data.meshes.new("Schnalle")
    bm.to_mesh(me)
    bm.free()
    sn = bpy.data.objects.new("Schnalle", me)
    bpy.context.scene.collection.objects.link(sn)
    sn.location = -seit * (bw - st)                      # Rahmen mittig auf den Riemen
    return riemen, sn


def umriss_bauen(quelle, name="Umriss", abstand=UMRISS, ohne=()):
    """Invertierte Hülle: Kopie entlang der Eckennormalen nach außen, Flächen gewendet (nur Rückseiten sichtbar)."""
    me = quelle.data.copy()
    me.name = name
    for v in me.vertices:
        pass
    me.update()
    nm = [v.normal.copy() for v in me.vertices]
    # Doppelte Ecken an harten Kanten: Verschiebung über Position gemittelt, damit die Hülle geschlossen bleibt
    pos = {}
    for v, n_ in zip(me.vertices, nm):
        k = (round(v.co.x, 5), round(v.co.y, 5), round(v.co.z, 5))
        pos.setdefault(k, Vector((0, 0, 0)))
        pos[k] += n_
    for v in me.vertices:
        k = (round(v.co.x, 5), round(v.co.y, 5), round(v.co.z, 5))
        d = pos[k].normalized() if pos[k].length > 1e-9 else Vector((0, 0, 0))
        v.co = v.co + d * abstand
    bm = bmesh.new()
    bm.from_mesh(me)
    weg = [f for f in bm.faces if me.materials[f.material_index].name in ohne]
    if weg:
        bmesh.ops.delete(bm, geom=weg, context="FACES")
    bmesh.ops.reverse_faces(bm, faces=bm.faces)
    bm.to_mesh(me)
    bm.free()
    ob = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(ob)
    return ob


def marke_bauen():
    """Fragezeichen für den Hilfe-Zeiger: gewölbte Plakette aus Messing mit erhabenem „?“; als Netz."""
    cu = bpy.data.curves.new("Fragezeichen", "FONT")
    cu.body = "?"
    cu.size = 0.085
    cu.extrude = 0.006
    cu.bevel_depth = 0.0026
    cu.bevel_resolution = 1
    cu.resolution_u = 3
    cu.align_x = "CENTER"
    cu.align_y = "CENTER"
    ob = bpy.data.objects.new("Hilfe_Marke", cu)
    bpy.context.scene.collection.objects.link(ob)
    for o in bpy.context.view_layer.objects:
        o.select_set(False)
    bpy.context.view_layer.objects.active = ob
    ob.select_set(True)
    bpy.ops.object.convert(target="MESH")
    ob.select_set(False)
    return ob


# ─────────────────────────────────────────────────────────────────────────────
#  Pose und Bewegungen
# ─────────────────────────────────────────────────────────────────────────────

AX_X = Vector((1, 0, 0))
AX_Y = Vector((0, 1, 0))
AX_Z = Vector((0, 0, 1))
# Beugen = Drehung der Fingerspitzen zur Handfläche (−Z): Drehung um −X
BEUGE = -AX_X


def drehung(rig, knochen, achse, grad):
    r = rig.data.bones[knochen].matrix_local.to_3x3()
    lokal = (r.inverted() @ achse).normalized()
    return Quaternion(lokal, math.radians(grad))


def pose_dict(zeige=(0, 0, 0), mittel=(0, 0, 0), ring=(0, 0, 0), klein=(0, 0, 0), daumen_beugen=(0, 0, 0), daumen_quer=0.0,
              daumen_heben=0.0, spreizen=(0, 0, 0, 0), hand=(0, 0, 0), wurzel=(0, 0, 0)):
    """Pose als {Knochen: [(Achse, Grad), ...]}. Winkel in Grad. spreizen = Zeige, Mittel, Ring, Klein (Drehung um Z, positiv
    = zur Kleinfingerseite +X). hand = Handgelenk (Beugen nach Handfläche, Drehen um Z, Rollen um Y). wurzel = Wurzel (wie hand)."""
    d = {}
    for k, werte, sp in ((2, zeige, spreizen[0]), (3, mittel, spreizen[1]), (4, ring, spreizen[2]), (5, klein, spreizen[3])):
        d[f"finger{k}-1.R"] = [(AX_Z, -sp), (BEUGE, werte[0])]
        d[f"finger{k}-2.R"] = [(BEUGE, werte[1])]
        d[f"finger{k}-3.R"] = [(BEUGE, werte[2])]
    d["finger1-1.R"] = [(AX_Z, daumen_quer), (AX_X, daumen_heben), (BEUGE, daumen_beugen[0])]
    d["finger1-2.R"] = [(BEUGE, daumen_beugen[1])]
    d["finger1-3.R"] = [(BEUGE, daumen_beugen[2])]
    d["wrist.R"] = [(BEUGE, hand[0]), (AX_Z, hand[1]), (AX_Y, hand[2])]
    d["Wurzel"] = [(BEUGE, wurzel[0]), (AX_Z, wurzel[1]), (AX_Y, wurzel[2])]
    return d


def mischen(a, b, t):
    """Pose zwischen a und b (Winkel linear)."""
    out = {}
    for k in a:
        la, lb = a[k], b[k]
        out[k] = [(ax, wa + (wb - wa) * t) for (ax, wa), (_, wb) in zip(la, lb)]
    return out


def pose_anwenden(rig, d):
    for pb in rig.pose.bones:
        pb.rotation_mode = "QUATERNION"
        pb.rotation_quaternion = (1, 0, 0, 0)
    for k, liste in d.items():
        q = Quaternion((1, 0, 0, 0))
        for (ax, w) in liste:
            q = drehung(rig, k, ax, w) @ q
        rig.pose.bones[k].rotation_quaternion = q


def pose_keyen(rig, d, frame):
    pose_anwenden(rig, d)
    for k in d:
        rig.pose.bones[k].keyframe_insert("rotation_quaternion", frame=frame, group=k)


POSEN = {}


def posen_festlegen():
    # entspannt offen (leicht gekrümmte Finger wie auf der Vorlage), Daumen locker abgespreizt
    POSEN["ruhe"] = pose_dict(zeige=(10, 12, 6), mittel=(14, 18, 8), ring=(18, 22, 10), klein=(22, 24, 12),
                              daumen_beugen=(4, 8, 6), spreizen=(-9, 0, 6, 14))
    # Zeigefinger gestreckt, die anderen eingerollt, Daumen quer davor
    POSEN["zeigen"] = pose_dict(zeige=(2, 1, 1), mittel=(78, 92, 52), ring=(82, 98, 54), klein=(86, 98, 52),
                                daumen_beugen=(18, 24, 18), daumen_quer=-10, daumen_heben=-14, spreizen=(2, 0, 0, 0),
                                hand=(3, 0, 0))
    POSEN["faust"] = pose_dict(zeige=(80, 98, 58), mittel=(84, 100, 58), ring=(86, 100, 56), klein=(88, 98, 54),
                               daumen_beugen=(24, 28, 22), daumen_quer=-12, daumen_heben=-18, hand=(6, 0, 0))


def aktion_neu(rig, name):
    a = bpy.data.actions.new(name)
    a.use_fake_user = True
    rig.animation_data_create()
    rig.animation_data.action = a
    return a


def glatt(action):
    for fc in action.fcurves if hasattr(action, "fcurves") else []:
        for kp in fc.keyframe_points:
            kp.interpolation = "BEZIER"
            kp.handle_left_type = "AUTO_CLAMPED"
            kp.handle_right_type = "AUTO_CLAMPED"


def clips_bauen(rig):
    posen_festlegen()
    P = POSEN
    clips = {}
    rig.animation_data_create()
    nullpose = pose_dict()
    for k in nullpose:
        pass

    def start(name, laenge, schleife=False):
        a = aktion_neu(rig, name)
        a.frame_range = (0, laenge)
        a.use_frame_range = True
        a.use_cyclic = schleife
        clips[name] = (a, laenge, schleife)
        return a

    def mit_atmen(basis, t, amp=1.0, tippen=0.0):
        """Ruhe-Wiegen: Handgelenk und Wurzel schwingen leicht, Finger atmen (Phase t 0..1 auf einem Zyklus)."""
        w = 2 * math.pi * t
        d = {k: [(ax, wk) for (ax, wk) in v] for k, v in basis.items()}
        d["Wurzel"] = [(BEUGE, 1.6 * amp * math.sin(w)), (AX_Z, 2.2 * amp * math.sin(w + 1.2)), (AX_Y, 1.4 * amp * math.sin(w + 2.4))]
        wr = d["wrist.R"]
        d["wrist.R"] = [(wr[0][0], wr[0][1] + 1.5 * amp * math.sin(w + 0.8)), wr[1], wr[2]]
        for k in range(2, 6):
            f1 = d[f"finger{k}-1.R"]
            d[f"finger{k}-1.R"] = [f1[0], (f1[1][0], f1[1][1] + 2.0 * amp * math.sin(w + 0.35 * k))]
            f2 = d[f"finger{k}-2.R"]
            d[f"finger{k}-2.R"] = [(f2[0][0], f2[0][1] + 2.5 * amp * math.sin(w + 0.35 * k + 0.6))]
        return d

    # Ruhe: 2 s, nahtlos (Bild 48 = Bild 0)
    a = start("Ruhe", 48, True)
    for f in range(0, 49, 4):
        pose_keyen(rig, mit_atmen(P["ruhe"], f / 48.0), f)
    glatt(a)
    # Zeigen: 2 s Schleife, kaum Bewegung (Wurzel atmet schwach)
    a = start("Zeigen", 48, True)
    for f in range(0, 49, 4):
        pose_keyen(rig, mit_atmen(P["zeigen"], f / 48.0, 0.45), f)
    glatt(a)
    # Greifen: Faust, leichtes Pressen (Schleife 1,5 s)
    a = start("Greifen", 36, True)
    for f in range(0, 37, 3):
        t = f / 36.0
        d = mit_atmen(P["faust"], t, 0.3)
        d = mischen(d, mischen(P["faust"], P["faust"], 0), 0)
        pr = 0.5 + 0.5 * math.sin(2 * math.pi * t)
        for k in range(2, 6):
            d[f"finger{k}-1.R"] = [d[f"finger{k}-1.R"][0], (BEUGE, P["faust"][f"finger{k}-1.R"][1][1] + 3 * pr)]
        pose_keyen(rig, d, f)
    glatt(a)
    # Tippen: 1 s, Zeigefinger nickt (Kontakt bei Bild 7), Hand sinkt leicht mit
    a = start("Tippen", 24, False)
    z = P["zeigen"]
    kontakt = {k: [(ax, w) for (ax, w) in v] for k, v in z.items()}
    kontakt["finger2-1.R"] = [(AX_Z, -2), (BEUGE, 14)]
    kontakt["finger2-2.R"] = [(BEUGE, 8)]
    kontakt["finger2-3.R"] = [(BEUGE, 6)]
    kontakt["wrist.R"] = [(BEUGE, 13), (AX_Z, 0), (AX_Y, 0)]
    ausholen = {k: [(ax, w) for (ax, w) in v] for k, v in z.items()}
    ausholen["finger2-1.R"] = [(AX_Z, -2), (BEUGE, -10)]
    ausholen["finger2-2.R"] = [(BEUGE, -4)]
    ausholen["wrist.R"] = [(BEUGE, -5), (AX_Z, 0), (AX_Y, 0)]
    pose_keyen(rig, z, 0)
    pose_keyen(rig, ausholen, 4)
    pose_keyen(rig, kontakt, 7)
    pose_keyen(rig, mischen(kontakt, z, 0.35), 10)
    pose_keyen(rig, mischen(z, ausholen, 0.35), 14)
    pose_keyen(rig, z, 24)
    glatt(a)

    # Übergänge, Längen in Bildern (24/s)
    def uebergang(name, von, nach, laenge):
        a = start(name, laenge, False)
        n = max(2, laenge)
        for f in range(0, laenge + 1):
            t = f / laenge
            t = t * t * (3 - 2 * t)
            pose_keyen(rig, mischen(von, nach, t), f)
        glatt(a)

    uebergang("Uebergang_Ruhe_Zeigen", P["ruhe"], P["zeigen"], 6)
    uebergang("Uebergang_Zeigen_Ruhe", P["zeigen"], P["ruhe"], 6)
    uebergang("Uebergang_Ruhe_Greifen", P["ruhe"], P["faust"], 8)
    uebergang("Uebergang_Greifen_Ruhe", P["faust"], P["ruhe"], 8)
    uebergang("Uebergang_Zeigen_Greifen", P["zeigen"], P["faust"], 6)
    uebergang("Uebergang_Greifen_Zeigen", P["faust"], P["zeigen"], 6)
    rig.animation_data.action = clips["Ruhe"][0]
    return clips


# ─────────────────────────────────────────────────────────────────────────────
#  Szene bauen
# ─────────────────────────────────────────────────────────────────────────────

def modell_bauen(aus):
    for o in list(bpy.data.objects):
        bpy.data.objects.remove(o)
    hand, rig, achsen = hand_holen()
    teil = hand_dicken(hand, rig)
    farbe_img, normal_img = leder_texturen(os.path.join(aus, "texturen"))
    m_leder = stoff_bauen("Leder", farbe_img, normal_img, (1.0, 1.0, 1.0), 0.62, 0.0, True, 1.0)
    m_riemen = stoff_bauen("Riemen", farbe_img, normal_img, (0.62, 0.52, 0.45), 0.55, 0.0, False, 0.8)
    m_futter = stoff_bauen("Futter", None, None, einfarbig=(0.09, 0.045, 0.025), rau=0.9)
    m_messing = stoff_bauen("Messing", None, None, einfarbig=(0.78, 0.52, 0.17), rau=0.38, metall=1.0)
    m_umriss = stoff_bauen("Umriss", None, None, einfarbig=(0.02, 0.012, 0.008), rau=1.0)
    m_umriss.use_backface_culling = True
    stulpe, futter, mass = stulpe_bauen(hand, rig)
    riemen, schnalle = riemen_bauen(mass)
    # Eckenfarbe auch für Stulpe: Saum dunkler (Kante), Grundton 1
    teile = []
    for ob, mat in ((hand, m_leder), (stulpe, m_leder), (futter, m_futter), (riemen, m_riemen), (schnalle, m_messing)):
        ob.data.materials.clear()
        ob.data.materials.append(mat)
        uv_kasten(ob)
        if mat is m_leder or mat is m_riemen:
            if "Falten" not in ob.data.color_attributes:
                ca = ob.data.color_attributes.new("Falten", "FLOAT_COLOR", "POINT")
                for i, v in enumerate(ob.data.vertices):
                    c = 1.0
                    if ob is stulpe:
                        s = (mass["y0"] - v.co.y) / (mass["y0"] - mass["y1"])
                        c = 1.0 - 0.18 * max(0.0, (s - 0.7) / 0.3) - 0.10 * math.sin(3 * i)
                        if v.co.y < mass["y1"] - 0.003:
                            c *= 0.82
                    ca.data[i].color = (c, c, c, 1)
        teile.append(ob)
    # Gewichte: Stulpe, Futter, Riemen, Schnalle starr am Handgelenk
    for ob in (stulpe, futter, riemen, schnalle):
        vertexgruppe_setzen(ob, "wrist.R", range(len(ob.data.vertices)), 1.0)
    # Schnalle liegt mit Ursprung versetzt: Netz anwenden (Position einbacken)
    schnalle.data.transform(schnalle.matrix_world)
    schnalle.matrix_world = Matrix.Identity(4)
    # zusammenfügen
    for o in bpy.context.selected_objects:
        o.select_set(False)
    for ob in teile:
        ob.select_set(True)
    bpy.context.view_layer.objects.active = hand
    bpy.ops.object.join()
    hand.name = "Handschuh"
    hand.data.name = "Handschuh"
    # Stulpe/Riemen bekommen Eckenfarbe 1 beim Futter (Attribut nicht überall vorhanden) -> auffüllen
    ca = hand.data.color_attributes.get("Falten")
    if ca is None:
        ca = hand.data.color_attributes.new("Falten", "FLOAT_COLOR", "POINT")
    hand.data.color_attributes.active_color = ca
    # Weiche Normalen
    bpy.ops.object.shade_smooth()
    woelbung_faerben(hand)
    mod = hand.modifiers.new("Armature", "ARMATURE")
    mod.object = rig
    hand.parent = rig
    um = umriss_bauen(hand, "Handschuh_Umriss", UMRISS, ohne=("Futter", "Messing"))
    um.data.materials.clear()
    um.data.materials.append(m_umriss)
    um.parent = rig
    um.visible_shadow = False
    mod2 = um.modifiers.new("Armature", "ARMATURE")
    mod2.object = rig
    # Marke
    marke = marke_bauen()
    marke.data.materials.clear()
    marke.data.materials.append(m_messing)
    # Anker an der Zeigefingerspitze
    P = np.array([v.co[:] for v in hand.data.vertices])
    gn = {g.index: g.name for g in hand.vertex_groups}
    zeige_ids = [v.index for v in hand.data.vertices if any(gn[g.group].startswith("finger2-") and g.weight > 0.6 for g in v.groups)]
    spitze = P[zeige_ids][np.argmax(P[zeige_ids][:, 1])]
    print("Zeigefingerspitze (Ruhe):", spitze)
    anker = bpy.data.objects.new("Anker_Spitze", None)
    anker.empty_display_type = "PLAIN_AXES"
    anker.empty_display_size = 0.01
    bpy.context.scene.collection.objects.link(anker)
    anker.parent = rig
    anker.parent_type = "BONE"
    anker.parent_bone = "finger2-3.R"
    # Spitze: leicht vor dem äußersten Eckpunkt (Mitte der Fingerkuppe)
    anker.matrix_world = Matrix.Translation((spitze[0], spitze[1] - 0.0045, spitze[2] - 0.003))
    marke.parent = rig
    marke.parent_type = "BONE"
    marke.parent_bone = "Wurzel"
    marke.matrix_world = Matrix.Translation((0.105, 0.02, 0.04)) @ Matrix.Rotation(math.radians(-40), 4, "Z")
    mum = umriss_bauen(marke, "Hilfe_Marke_Umriss", 0.0016)
    mum.data.materials.clear()
    mum.data.materials.append(m_umriss)
    mum.parent = rig
    mum.visible_shadow = False
    mum.parent_type = "BONE"
    mum.parent_bone = "Wurzel"
    mum.matrix_world = marke.matrix_world.copy()
    # Dreiecke zählen
    def dreiecke(ob):
        return sum(len(p.vertices) - 2 for p in ob.data.polygons)
    print("DREIECKE Handschuh:", dreiecke(hand), "Umriss:", dreiecke(um), "Marke:", dreiecke(marke), "Marke-Umriss:", dreiecke(mum))
    return dict(hand=hand, umriss=um, rig=rig, marke=marke, marke_umriss=mum, anker=anker, tri=dict(
        hand=dreiecke(hand), umriss=dreiecke(um), marke=dreiecke(marke), marke_umriss=dreiecke(mum)))


def ansicht_testen(m, aus, name="pose_test"):
    pass


# ─────────────────────────────────────────────────────────────────────────────
#  Bilder (Blender Eevee)
# ─────────────────────────────────────────────────────────────────────────────

def buehne(m, px=384, ortho=0.44, tilt=16.0, roll=40.0):
    sc = bpy.context.scene
    sc.render.engine = "BLENDER_EEVEE"
    sc.render.resolution_x = sc.render.resolution_y = px
    sc.render.film_transparent = True
    sc.render.image_settings.file_format = "PNG"
    sc.render.image_settings.color_mode = "RGBA"
    sc.view_settings.view_transform = "Standard"
    w = bpy.data.worlds.new("W")
    w.use_nodes = True
    bg = w.node_tree.nodes["Background"]
    bg.inputs["Color"].default_value = (0.62, 0.60, 0.58, 1)
    bg.inputs["Strength"].default_value = 0.85
    sc.world = w
    pivot = bpy.data.objects.new("Kamerakopf", None)
    bpy.context.scene.collection.objects.link(pivot)
    pivot.location = (0, 0.045, 0)
    pivot.rotation_euler = (0, 0, math.radians(roll))
    cam = bpy.data.cameras.new("Kamera")
    cam.type = "ORTHO"
    cam.ortho_scale = ortho
    cam.clip_start, cam.clip_end = 0.01, 3.0
    co = bpy.data.objects.new("Kamera", cam)
    bpy.context.scene.collection.objects.link(co)
    co.parent = pivot
    d = 0.8
    co.location = (0, -d * math.sin(math.radians(tilt)), d * math.cos(math.radians(tilt)))
    co.rotation_euler = (math.radians(tilt), 0, 0)
    sc.camera = co
    for (nm, rot, energie, farbe) in (("Haupt", (math.radians(40), math.radians(-35), math.radians(35)), 4.0, (1.0, 0.93, 0.82)),
                                      ("Auf", (math.radians(55), math.radians(30), math.radians(-150)), 1.9, (0.82, 0.88, 1.0))):
        ld = bpy.data.lights.new(nm, "SUN")
        ld.energy = energie
        ld.color = farbe
        ld.angle = math.radians(12)
        lo = bpy.data.objects.new(nm, ld)
        bpy.context.scene.collection.objects.link(lo)
        lo.parent = pivot
        lo.rotation_euler = rot
    return pivot


def render_bild(pfad, mit_umriss, mit_marke=False, m=None):
    m["umriss"].hide_render = not mit_umriss
    m["marke"].hide_render = not mit_marke
    m["marke_umriss"].hide_render = not (mit_marke and mit_umriss)
    bpy.context.scene.render.filepath = pfad
    bpy.ops.render.render(write_still=True)


ANSICHTEN = {
    "zeiger": (0.0, 0.0, 40.0),          # wie ein Mauszeiger: Finger schräg nach links oben, Kamera leicht von vorn oben
    "seite": (-82.0, 0.0, 90.0),
    "innen": (180.0, 0.0, -40.0),
    "drauf": (0.0, 0.0, 0.0),
}


def ansicht(pivot, name):
    x, y, z = ANSICHTEN[name]
    pivot.rotation_euler = (math.radians(x), math.radians(y), math.radians(z))


def glb_schreiben(m, pfad):
    for o in bpy.context.view_layer.objects:
        o.select_set(False)
    for k in ("hand", "umriss", "rig", "marke", "marke_umriss", "anker"):
        m[k].select_set(True)
        m[k].hide_render = False
    bpy.context.view_layer.objects.active = m["rig"]
    bpy.ops.export_scene.gltf(filepath=pfad, export_format="GLB", use_selection=True, export_animations=True,
                              export_animation_mode="ACTIONS", export_skins=True, export_yup=True, export_apply=False,
                              export_image_format="AUTO", export_force_sampling=True, export_frame_range=False,
                              export_optimize_animation_size=True, export_vertex_color="ACTIVE")
    print("GLB:", pfad, os.path.getsize(pfad))


def vorschau(m, pivot, bilder, aus):
    import json
    rig, anker = m["rig"], m["anker"]
    clips = clips_bauen(rig)
    rig.animation_data.action = clips["Ruhe"][0]
    bpy.context.scene.frame_set(0)
    glb_schreiben(m, os.path.join(aus, "handschuh_zeiger.glb"))
    sc = bpy.context.scene
    bericht = {"dreiecke": m["tri"], "clips": {n: dict(bilder=l, schleife=sc_) for n, (_, l, sc_) in clips.items()}, "anker": {}}

    def clip_rendern(name, schritt, ansichten=("zeiger",), umriss=True, marke=False):
        a, laenge, _ = clips[name]
        rig.animation_data.action = a
        frames = list(range(0, laenge + 1, schritt))
        for av in ansichten:
            ansicht(pivot, av)
            for f in frames:
                sc.frame_set(f)
                bpy.context.view_layer.update()
                if av == "zeiger":
                    t = anker.matrix_world.translation
                    bericht["anker"].setdefault(name, {})[f] = [round(t.x, 4), round(t.y, 4), round(t.z, 4)]
                d = os.path.join(bilder, "clips", name)
                os.makedirs(d, exist_ok=True)
                render_bild(os.path.join(d, f"{av}_{'u' if umriss else 'o'}{'_m' if marke else ''}_{f:02d}.png"), umriss, marke, m)
        return frames

    clip_rendern("Ruhe", 4, ("zeiger",))
    clip_rendern("Ruhe", 4, ("zeiger",), umriss=False)
    clip_rendern("Zeigen", 4, ("zeiger",))
    clip_rendern("Zeigen", 4, ("zeiger",), umriss=False)
    clip_rendern("Greifen", 3, ("zeiger",))
    clip_rendern("Greifen", 3, ("zeiger",), umriss=False)
    clip_rendern("Tippen", 2, ("zeiger", "seite"))
    for n in ("Uebergang_Ruhe_Zeigen", "Uebergang_Zeigen_Ruhe", "Uebergang_Ruhe_Greifen", "Uebergang_Greifen_Ruhe",
              "Uebergang_Zeigen_Greifen", "Uebergang_Greifen_Zeigen"):
        clip_rendern(n, 1, ("zeiger",))
    # Tooltip-Variante (Marke) in Ruhe und Zeigen
    for n in ("Ruhe", "Zeigen"):
        rig.animation_data.action = clips[n][0]
        sc.frame_set(0)
        ansicht(pivot, "zeiger")
        d = os.path.join(bilder, "clips", n)
        render_bild(os.path.join(d, "hilfe_u_00.png"), True, True, m)
        render_bild(os.path.join(d, "hilfe_o_00.png"), False, True, m)
    # Ansichten (Ruhe, Bild 0) für den Vergleich mit der Vorlage
    rig.animation_data.action = clips["Ruhe"][0]
    sc.frame_set(0)
    os.makedirs(os.path.join(bilder, "ansichten"), exist_ok=True)
    for av in ("drauf", "innen", "seite", "zeiger"):
        ansicht(pivot, av)
        render_bild(os.path.join(bilder, "ansichten", f"ruhe_{av}.png"), True, False, m)
        render_bild(os.path.join(bilder, "ansichten", f"ruhe_{av}_ohne.png"), False, False, m)
    # große Bilder (1024) von Ruhe und Zeigen
    sc.render.resolution_x = sc.render.resolution_y = 1024
    for n in ("Ruhe", "Zeigen", "Greifen"):
        rig.animation_data.action = clips[n][0]
        sc.frame_set(0)
        ansicht(pivot, "zeiger")
        render_bild(os.path.join(bilder, "ansichten", f"gross_{n.lower()}.png"), True, False, m)
    with open(os.path.join(bilder, "bericht.json"), "w", encoding="utf-8") as f:
        json.dump(bericht, f, indent=1)
    print("BERICHT geschrieben")



def marke_ausrichten(m, pivot):
    """Das Fragezeichen steht aufrecht zur Zeigeransicht: Drehung der Kamera in dieser Ansicht, Ort bleibt (Ansicht 'zeiger')."""
    ansicht(pivot, "zeiger")
    bpy.context.view_layer.update()
    cam = bpy.context.scene.camera
    drehung_ = cam.matrix_world.to_3x3().to_4x4()
    ort = Matrix.Translation(m["marke"].matrix_world.to_translation())
    for k in ("marke", "marke_umriss"):
        m[k].matrix_world = ort @ drehung_


def main():
    a = args()
    aus = a.get("aus", os.path.join(os.getcwd(), "handschuh3d_aus"))
    bilder = a.get("bilder", os.path.join(os.getcwd(), "handschuh3d_bilder"))
    os.makedirs(aus, exist_ok=True)
    os.makedirs(bilder, exist_ok=True)
    m = modell_bauen(aus)
    pivot = buehne(m)
    marke_ausrichten(m, pivot)
    if a.get("test") == "pose":
        posen_festlegen()
        for nm in ("ruhe", "zeigen", "faust"):
            pose_anwenden(m["rig"], POSEN[nm])
            bpy.context.view_layer.update()
            bpy.context.evaluated_depsgraph_get().update()
            render_bild(os.path.join(bilder, f"test_{nm}.png"), True, False, m)
        # Seitenansicht
        pivot.rotation_euler = (math.radians(-82), 0, math.radians(90))
        pose_anwenden(m["rig"], POSEN["zeigen"])
        bpy.context.view_layer.update()
        dg = bpy.context.evaluated_depsgraph_get()
        ev = m["hand"].evaluated_get(dg)
        arr = np.array([(ev.matrix_world @ v.co)[:] for v in ev.data.vertices])
        print("ZEIGEN-BOX", arr.min(axis=0), arr.max(axis=0))
        for pb in m["rig"].pose.bones:
            if "finger3" in pb.name or pb.name == "Wurzel":
                print(pb.name, pb.rotation_quaternion[:], pb.parent.name if pb.parent else None)
        render_bild(os.path.join(bilder, "test_zeigen_seite.png"), True, False, m)
        render_bild(os.path.join(bilder, "test_zeigen_seite_ohne.png"), False, False, m)
        um = m["umriss"].evaluated_get(dg)
        arr = np.array([(um.matrix_world @ v.co)[:] for v in um.data.vertices])
        print("UMRISS-BOX", arr.min(axis=0), arr.max(axis=0), [md.name for md in m["hand"].modifiers], m["hand"].parent.name)
        return
    if a.get("nur", "alles") in ("clips", "alles"):
        vorschau(m, pivot, bilder, aus)


if __name__ == "__main__":
    main()
