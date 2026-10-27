# Plan B: MPFB-Kopf an eine TRELLIS-Form anpassen. Läuft innerhalb von exportieren_proto.py (--trellis=<glb>).
#  1. TRELLIS-Netz laden, Farbe je Punkt aus der Textur, Haut/Haar/Iris unterscheiden
#  2. Ähnlichkeitstransformation (Augen + Nasenspitze), danach ICP auf die Hautflächen
#  3. Gesichtsbereich des MPFB-Netzes auf die TRELLIS-Haut ziehen (nächster Punkt), Rest per Relaxation ausfüllen,
#     dieselbe Verschiebung auf alle Shape Keys, Augen/Zähne starr mitführen, Wimpern per Nachbarpunkt
#  4. Farbe der TRELLIS-Textur auf die MPFB-UVs backen (Cycles, Auswahl auf Aktiv)
#  5. Haar und Bart: nicht-hautfarbene Flächen der TRELLIS-Form als feste Haarschale (verdünnt, eigene UV, gebacken)
import sys, os, math
import numpy as np
import bpy, bmesh
from mathutils import Vector, Matrix, kdtree
from mathutils.bvhtree import BVHTree

E = sys.modules["__main__"]


def log(*a):
    print("PLANB", *a, flush=True)


def netz_arrays(o):
    me = o.data
    n = len(me.vertices)
    v = np.empty(n * 3); me.vertices.foreach_get("co", v); v = v.reshape(-1, 3)
    mw = np.array(o.matrix_world)
    return v @ mw[:3, :3].T + mw[:3, 3]


def flaechen(me):
    me.calc_loop_triangles()
    t = np.empty(len(me.loop_triangles) * 3, np.int64)
    me.loop_triangles.foreach_get("vertices", t)
    return t.reshape(-1, 3)


def punkt_farben(o):
    """Farbe (sRGB, 0..1) je Punkt aus der Basisfarben-Textur über die UVs."""
    me = o.data
    img = None
    nt = o.data.materials[0].node_tree
    for n in nt.nodes:
        if n.type == "BSDF_PRINCIPLED" and n.inputs["Base Color"].links:
            f = n.inputs["Base Color"].links[0].from_node
            if f.type == "TEX_IMAGE":
                img = f.image
    if img is None:
        img = [n.image for n in nt.nodes if n.type == "TEX_IMAGE"][0]
    w, h = img.size
    px = np.empty(w * h * 4, np.float32); img.pixels.foreach_get(px); px = px.reshape(h, w, 4)[:, :, :3]
    uvl = me.uv_layers.active
    uv = np.empty(len(me.loops) * 2); uvl.data.foreach_get("uv", uv); uv = uv.reshape(-1, 2)
    vi = np.empty(len(me.loops), np.int64); me.loops.foreach_get("vertex_index", vi)
    x = np.clip((uv[:, 0] % 1.0) * (w - 1), 0, w - 1).astype(int)
    y = np.clip((uv[:, 1] % 1.0) * (h - 1), 0, h - 1).astype(int)
    col = np.zeros((len(me.vertices), 3), np.float32)
    col[vi] = px[y, x]
    pix = np.zeros((len(me.vertices), 2), np.int64)
    pix[vi] = np.stack([x, y], 1)
    punkt_farben.pix = pix
    return np.clip(col, 0, 1), img


def klassen(c):
    """0 sonstiges (Kleidung), 1 Haut, 2 Haar/Bart (dunkelbraun), 3 Iris (olivgrün)."""
    r, g, b = c[:, 0], c[:, 1], c[:, 2]
    v = c.max(axis=1)
    k = np.zeros(len(c), np.int8)
    k[(v > 0.42) & (r > g * 1.04) & (r > b * 1.12)] = 1
    k[(v <= 0.42) & (r >= g * 0.95) & (r >= b)] = 2
    k[(g > r * 1.0) & (b < g * 0.55) & (v > 0.25)] = 3
    return k


def umeyama(src, dst):
    """Ähnlichkeitstransformation dst ≈ s * R @ src + t."""
    ms, md = src.mean(0), dst.mean(0)
    s0, d0 = src - ms, dst - md
    cov = d0.T @ s0 / len(src)
    U, D, Vt = np.linalg.svd(cov)
    S = np.eye(3)
    if np.linalg.det(U) * np.linalg.det(Vt) < 0:
        S[2, 2] = -1
    R = U @ S @ Vt
    var = (s0 ** 2).sum() / len(src)
    s = np.trace(np.diag(D) @ S) / var
    t = md - s * R @ ms
    return s, R, t


def bvh_aus(verts, tris):
    return BVHTree.FromPolygons([Vector(p) for p in verts], [tuple(int(i) for i in t) for t in tris])


def weiter(b, a, dna, M, S, LM):
    import json
    ordner = os.path.dirname(a["aus"])
    # ---- 1. TRELLIS laden -------------------------------------------------------------
    vorher = set(bpy.data.objects)
    bpy.ops.import_scene.gltf(filepath=a["trellis"])
    T = [o for o in bpy.data.objects if o not in vorher and o.type == "MESH"][0]
    T.name = "TRELLIS"
    tv = netz_arrays(T)
    tt = flaechen(T.data)
    farbe, timg = punkt_farben(T)
    kl = klassen(farbe)
    log("TRELLIS", len(tv), "Punkte", len(tt), "Dreiecke; Haut", int((kl == 1).sum()), "Haar", int((kl == 2).sum()), "Iris", int((kl == 3).sum()))
    # ---- MPFB-Landmarken ----------------------------------------------------------------
    kopf = [o for o in M.objekte(b) if o.name == "Human"][0]
    augen = [o for o in M.objekte(b) if "low-poly" in o.name][0]
    mv = netz_arrays(kopf)
    ev = netz_arrays(augen)
    mx = np.median(ev[:, 0])
    def iris(seite):
        p = ev[(ev[:, 0] > mx) if seite > 0 else (ev[:, 0] < mx)]
        mitte = p.mean(0)
        vorn = p[p[:, 1] < p[:, 1].min() + 0.003]
        return vorn.mean(0), mitte
    elL, mL = iris(+1); elR, mR = iris(-1)
    zentral = mv[(np.abs(mv[:, 0]) < 0.004) & (mv[:, 2] > 1.4)]
    nase = zentral[np.argmin(zentral[:, 1])]
    log("MPFB Augen", elL, elR, "Nase", nase)
    # ---- TRELLIS-Landmarken -------------------------------------------------------------
    # Augenmitten werden von Hand (Frontansicht, x,z in TRELLIS-Einheiten) angegeben: --tl=x,z (Bildseite rechts, +x) --tr=x,z
    def oberflaeche(xz):
        d = np.hypot(tv[:, 0] - xz[0], tv[:, 2] - xz[1])
        m = np.where(d < 0.012)[0]
        j = m[np.argmin(tv[m, 1])]
        return tv[j]
    tL = oberflaeche([float(x) for x in a["tl"].split(",")])
    tR = oberflaeche([float(x) for x in a["tr"].split(",")])
    dE = np.linalg.norm(tL - tR)
    zE = (tL[2] + tR[2]) / 2
    xm = (tL[0] + tR[0]) / 2
    haut = np.where(kl == 1)[0]
    cand = haut[(np.abs(tv[haut, 0] - xm) < 0.15 * dE) & (tv[haut, 2] < zE - 0.1 * dE) & (tv[haut, 2] > zE - 0.9 * dE)]
    tN = tv[cand[np.argmin(tv[cand, 1])]]
    log("TRELLIS Augen", tL, tR, "Nase", tN, "Augenabstand", dE)
    # Vorzeichen: MPFB links = +x; TRELLIS-Seiten passend zuordnen (beide -Y vorn)
    s, R, t = umeyama(np.array([tL, tR, tN]), np.array([elL, elR, nase]))
    log("Start-Ähnlichkeit s", s)
    tv2 = (s * (R @ tv.T)).T + t
    # ---- ICP auf Hautflächen im Gesichtsbereich ---------------------------------------------
    zchin = nase[2] - 0.085
    zbrow = nase[2] + 0.075
    def gesichtsmaske(p):
        return (p[:, 2] > zchin) & (p[:, 2] < zbrow + 0.03) & (np.abs(p[:, 0]) < 0.07) & (p[:, 1] < nase[1] + 0.09)
    hm = np.where(gesichtsmaske(mv))[0]
    mt = kopf.data; mtri = flaechen(mt)
    mbvh = bvh_aus(mv, mtri)
    hsel = haut[gesichtsmaske(tv2[haut])]
    rng = np.random.default_rng(1)
    pr = hsel[rng.choice(len(hsel), min(15000, len(hsel)), replace=False)]
    for it in range(15):
        p = tv2[pr]
        q = np.array([mbvh.find_nearest(Vector(x))[0] for x in p])
        d = np.linalg.norm(p - q, axis=1)
        gut = d < np.percentile(d, 85)
        s2, R2, t2 = umeyama(p[gut], q[gut])
        tv2 = (s2 * (R2 @ tv2.T)).T + t2
        s *= s2
        if it % 5 == 4:
            log("ICP", it, "mittlerer Abstand", float(d[gut].mean()))
    tlm = (s * 0 + 1) * 0
    # Position der TRELLIS-Augen nach der Anpassung (Abweichung zu den MPFB-Augen ist ein Maß für die Passung)
    def trafo_punkt(pk):
        j = np.argmin(np.linalg.norm(tv - pk, axis=1)); return tv2[j]
    log('Augenabweichung links', np.round(trafo_punkt(tL) - elL, 4), 'rechts', np.round(trafo_punkt(tR) - elR, 4), 'Nase', np.round(trafo_punkt(tN) - nase, 4))
    # ---- 3. Gesicht auf die TRELLIS-Haut ziehen ------------------------------------------------
    hautf = np.all(kl[tt] == 1, axis=1)
    hbvh = bvh_aus(tv2, tt[hautf])
    D = np.zeros_like(mv)
    fest = np.zeros(len(mv), bool)
    zfuss = zchin - 0.10
    BX = float(a.get("mask_x", 0.09)); BY = float(a.get("mask_y", 0.12))
    for i, p in enumerate(mv):
        if p[2] < zfuss:
            fest[i] = True
            continue
        if not (zchin - 0.02 < p[2] < zbrow + 0.045 and abs(p[0]) < BX and p[1] < nase[1] + BY):
            continue
        hit = hbvh.find_nearest(Vector(p), 0.05 if abs(p[0]) > float(a.get("ohr_x", 9)) else 0.012)
        if hit[0] is not None:
            D[i] = np.array(hit[0]) - p
            fest[i] = True
    log("gezogene Punkte", int((fest & (mv[:, 2] >= zfuss)).sum()), "von", len(mv))
    # Rest per Relaxation (harmonisches Auffüllen)
    nb = [[] for _ in range(len(mv))]
    for e in mt.edges:
        nb[e.vertices[0]].append(e.vertices[1]); nb[e.vertices[1]].append(e.vertices[0])
    frei = np.where(~fest)[0]
    maxn = max(len(x) for x in nb)
    idx = np.full((len(mv), maxn), -1); 
    for i, x in enumerate(nb):
        idx[i, :len(x)] = x
    gueltig = idx >= 0
    idx0 = np.where(gueltig, idx, 0)
    for it in range(600):
        nachbarn = D[idx0] * gueltig[:, :, None]
        mitte = nachbarn.sum(1) / np.maximum(gueltig.sum(1), 1)[:, None]
        D[frei] = mitte[frei]
    # leichte Glättung der Verschiebung im gezogenen Bereich (Rauschen der TRELLIS-Haut)
    for it in range(4):
        nachbarn = D[idx0] * gueltig[:, :, None]
        mitte = nachbarn.sum(1) / np.maximum(gueltig.sum(1), 1)[:, None]
        D = 0.5 * D + 0.5 * mitte
        D[mv[:, 2] < zfuss] = 0
    log("max Verschiebung", float(np.linalg.norm(D, axis=1).max()))
    E.G._verschieben(kopf, D)
    # Wimpern und Brauen: Nachbarverschiebung; Augen und Zähne starr
    kd = kdtree.KDTree(len(mv))
    for i, p in enumerate(mv):
        kd.insert(Vector(p), i)
    kd.balance()
    for o in list(M.objekte(b)):
        if o.type != "MESH" or o is kopf:
            continue
        pv = netz_arrays(o)
        if "eyebrow" in o.name:
            bpy.data.objects.remove(o, do_unlink=True)     # Brauen sind in der Textur gemalt
            continue
        if "eyelash" in o.name:
            dl = np.array([D[kd.find(Vector(p))[1]] for p in pv])
        elif "low-poly" in o.name:
            seite = pv[:, 0] > mx
            dl = np.zeros_like(pv)
            for sd, c in ((True, elL), (False, elR)):
                m = np.linalg.norm(mv - c, axis=1) < 0.03
                zen = pv[seite == sd].mean(0)
                dl[seite == sd] = D[m].mean(0) + np.array([0, 0.004, 0]) - (pv[seite == sd] - zen) * 0.08
        elif "teeth" in o.name:
            m = np.linalg.norm(mv - np.array([0, nase[1] + 0.017, nase[2] - 0.026]), axis=1) < 0.03
            dl = np.tile(D[m].mean(0), (len(pv), 1))
        else:
            continue
        E.G._verschieben(o, dl)
    # ---- gemalte Augen der TRELLIS-Textur mit Hautfarbe übermalen (die Augen kommen vom MPFB-Augapfel) ----
    px_img = np.empty(timg.size[0] * timg.size[1] * 4, np.float32); timg.pixels.foreach_get("pixels" if False else "pixels", px_img) if False else timg.pixels.foreach_get(px_img)
    px_img = px_img.reshape(timg.size[1], timg.size[0], 4)
    pix = punkt_farben.pix
    for c in ((tL, tR) if a.get("augen_malen") else ()):
        d = np.linalg.norm(tv - c, axis=1)
        innen = np.where(d < 0.2 * dE)[0]
        rundum = np.where((d > 0.2 * dE) & (d < 0.4 * dE) & (kl == 1))[0]
        hautfarbe = farbe[rundum].mean(0) if len(rundum) else np.array([0.6, 0.45, 0.38])
        for v in innen:
            x0, y0 = pix[v]
            px_img[max(y0 - 3, 0):y0 + 4, max(x0 - 3, 0):x0 + 4, :3] = hautfarbe
    timg.pixels.foreach_set(px_img.ravel())
    timg.update()
    # ---- Hautfarbe backen --------------------------------------------------------------------
    sc = bpy.context.scene
    sc.render.engine = "CYCLES"
    try:
        prefs = bpy.context.preferences.addons["cycles"].preferences
        prefs.compute_device_type = "CUDA"; prefs.get_devices()
        for d in prefs.devices:
            d.use = True
        sc.cycles.device = "GPU"
    except Exception as ex:
        log("GPU nicht verfügbar", ex)
    sc.cycles.samples = 4
    # TRELLIS ins MPFB-Koordinatensystem bringen (Punkte setzen)
    T.data.vertices.foreach_set("co", (tv2 @ np.array(T.matrix_world.inverted())[:3, :3].T + np.array(T.matrix_world.inverted())[:3, 3]).ravel())
    T.data.update()
    bake_groesse = int(a.get("backen", 2048))
    def backen(ziel, name, ext=0.03, ray=0.06):
        img = bpy.data.images.new(name, bake_groesse, bake_groesse)
        mat = bpy.data.materials.new(name + "_mat"); mat.use_nodes = True
        tex = mat.node_tree.nodes.new("ShaderNodeTexImage"); tex.image = img
        mat.node_tree.nodes.active = tex
        pb = mat.node_tree.nodes["Principled BSDF"]
        mat.node_tree.links.new(tex.outputs["Color"], pb.inputs["Base Color"])
        ziel.data.materials.clear(); ziel.data.materials.append(mat)
        for x in bpy.context.selected_objects: x.select_set(False)
        T.select_set(True); ziel.select_set(True)
        bpy.context.view_layer.objects.active = ziel
        bpy.ops.object.bake(type="DIFFUSE", pass_filter={"COLOR"}, use_selected_to_active=True, cage_extrusion=ext,
                            max_ray_distance=ray, margin=10, use_clear=True)
        return img, mat
    himg, hmat = backen(kopf, "haut_gemalt")
    himg.filepath_raw = os.path.join(ordner, "haut_gemalt.png"); himg.file_format = "PNG"; himg.save()
    log("Haut gebacken", himg.filepath_raw)
    # ---- 5. Haarschale: Flächen des MPFB-Kopfes, die unter Haar/Bart liegen, nach außen auf die TRELLIS-Haaroberfläche ----
    ganz = bvh_aus(tv2, tt)
    mv2 = netz_arrays(kopf)                       # Kopf nach dem Anpassen
    kopf.data.calc_loop_triangles()
    nor = np.empty(len(mv2) * 3); kopf.data.vertices.foreach_get("normal", nor); nor = nor.reshape(-1, 3)
    haarig = np.zeros(len(mv2), bool)
    stirn = tv2[haut[(np.abs(tv2[haut, 0]) < 0.04) & (tv2[haut, 1] < nase[1] + 0.07) & (tv2[haut, 2] > nase[2] + 0.06)]]
    zstirn = float(np.percentile(stirn[:, 2], 97)) - 0.004 if len(stirn) else nase[2] + 0.11
    log('Haaransatz z', zstirn)
    dicke = np.zeros(len(mv2))
    for i, p in enumerate(mv2):
        if p[2] < zchin - float(a.get("haar_tief", 0.06)) or p[2] > nase[2] + 0.2 or p[1] > nase[1] + 0.2 or abs(p[0]) > float(a.get("haar_x", 0.13)):
            continue
        n = Vector(nor[i])
        hit = ganz.ray_cast(Vector(p) - n * 0.004, n, 0.07)
        if hit[0] is None:
            continue
        if np.mean(kl[tt[hit[2]]] == 2) > 0.5 or p[2] > zstirn:
            haarig[i] = True
            dicke[i] = max((Vector(hit[0]) - Vector(p)).length, 0.003)
    # Lippenbereich frei lassen (Mund muss sauber lesbar bleiben)
    mund = np.array([0, nase[1] + 0.017, nase[2] - 0.026])
    lip = ((np.abs(mv2[:, 0] - mund[0]) / 0.026) ** 2 + ((mv2[:, 2] - mund[2]) / 0.016) ** 2) < 1.0
    haarig[lip & (mv2[:, 1] < nase[1] + 0.05)] = False
    # Augenbereich ausschließen (Brauen werden mit der Haut gemalt)
    for c in (elL, elR):
        haarig[np.linalg.norm(mv2 - c, axis=1) < 0.03] = False
    log("haarige Punkte", int(haarig.sum()))
    bm = bmesh.new(); bm.from_mesh(kopf.data)
    bm.verts.ensure_lookup_table(); bm.faces.ensure_lookup_table()
    weg = [f for f in bm.faces if not all(haarig[v.index] for v in f.verts)]
    bmesh.ops.delete(bm, geom=weg, context="FACES_ONLY")
    bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context="VERTS")
    # Inseln zählen, kleine entfernen
    bm.faces.ensure_lookup_table()
    seen = set(); inseln = []
    for f in bm.faces:
        if f.index in seen: continue
        st = [f]; seen.add(f.index); gr = []
        while st:
            c = st.pop(); gr.append(c)
            for e in c.edges:
                for nf in e.link_faces:
                    if nf.index not in seen:
                        seen.add(nf.index); st.append(nf)
        inseln.append(gr)
    gross = max(len(g) for g in inseln)
    bmesh.ops.delete(bm, geom=[f for g in inseln if len(g) < 0.08 * gross for f in g], context="FACES")
    bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context="VERTS")
    # Schalenpunkte nach außen: Originalposition und Normale sind in "kopf"; Schalen-Netz hat eigene Punktreihenfolge,
    # daher Zuordnung über die Lage (die Punkte sind unverändert, nur Teilmenge)
    shell_pos = np.array([v.co[:] for v in bm.verts])
    shell_me = bpy.data.meshes.new("haar_schale")
    bm.to_mesh(shell_me); bm.free()
    H = bpy.data.objects.new("haar_schale", shell_me)
    sc.collection.objects.link(H)
    kdk = kdtree.KDTree(len(mv2))
    for i, p in enumerate(mv2):
        kdk.insert(Vector(p), i)
    kdk.balance()
    zuordnung = np.array([kdk.find(Vector(p))[1] for p in shell_pos])
    neu_pos = shell_pos + nor[zuordnung] * dicke[zuordnung, None]
    shell_me.vertices.foreach_set("co", neu_pos.ravel()); shell_me.update()
    # Schale glätten (zwei Durchgänge Mittelung entlang der Kanten), damit die Oberfläche ruhig wird
    nbs = [[] for _ in range(len(shell_me.vertices))]
    for e in shell_me.edges:
        nbs[e.vertices[0]].append(e.vertices[1]); nbs[e.vertices[1]].append(e.vertices[0])
    pos = neu_pos.copy()
    for it in range(3):
        pos = np.array([0.5 * pos[i] + 0.5 * pos[nbs[i]].mean(0) if nbs[i] else pos[i] for i in range(len(pos))])
    shell_me.vertices.foreach_set("co", pos.ravel()); shell_me.update()
    sub = H.modifiers.new("s", "SUBSURF"); sub.levels = 1; sub.render_levels = 1
    for x in bpy.context.selected_objects: x.select_set(False)
    H.select_set(True); bpy.context.view_layer.objects.active = H
    bpy.ops.object.modifier_apply(modifier="s")
    log("Haarschale", len(shell_me.polygons), "Flächen")
    himg2, hmat2 = backen(H, "haar_gemalt", 0.012, 0.03)
    himg2.filepath_raw = os.path.join(ordner, "haar_gemalt.png"); himg2.file_format = "PNG"; himg2.save()
    # Schale folgt dem Kopf (Kiefer, Mund): Shape Keys von der Basis übertragen
    basis, deltas = E.keys_lesen(kopf)
    H.parent = kopf
    E.keys_uebertragen(H, basis, deltas)
    for p in H.data.polygons:
        p.use_smooth = True
    bpy.data.objects.remove(T, do_unlink=True)
    log("Schale Dreiecke", sum(len(p.vertices) - 2 for p in H.data.polygons))
    E.weiter2(b, a, dna)
