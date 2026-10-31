"""Charakter Phase 2: Basiskopf als Büste mit Reglern und Mimik nach .glb (eigener Code, MIT).

Aufruf:  tools/charakter/blender.sh -b -P tools/charakter/kopf2_bauen.py -- <ausgabe.glb> [morph_normalen=0|1]
         (oder tools/charakter/kopf2.sh)

Gesicht, Mund, Zähne, Zunge, Augen und Mimik (53 ARKit-Formen, 2 Pupillenformen) kommen vom ICT-FaceKit (MIT);
Büste, Alter, Geschlecht, Kraft, Gewicht und Formregler von MakeHuman (Assets CC0), ausgeführt über MPFB 2 (GPL,
nur als Werkzeug in Blender). Brauen und Wimpern sind eigene Karten (Phase 3, kopf3_haare.py).
Ablauf und Gründe: docs/charakter/phase2.md. Ergebnis: <ausgabe>.glb und <ausgabe>.json (Regler, Makros, Maße).

Achsen hier: Blender (z oben, Gesicht nach -y, +x = linke Seite der Figur), Meter.
"""
import json
import math
import os
import sys
import time

import bmesh
import bpy
import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import kopf_gemeinsam as kg  # noqa: E402
import kopf2_passung as kp  # noqa: E402
import kopf2_quellen as kq  # noqa: E402
import kopf2_werkzeug as kw  # noqa: E402
import kopf3_haare as kh  # noqa: E402

HIER = os.path.dirname(os.path.abspath(__file__))
arg = kg.argumente()
AUSGABE = os.path.abspath(arg[0] if arg else os.path.expanduser("~/werkzeuge/charakter/glb/kopf2.glb"))
OPTIONEN = dict(a.split("=", 1) for a in arg[1:])
MORPH_NORMALEN = OPTIONEN.get("morph_normalen", "0") == "1"
BACKDATEN = OPTIONEN.get("backdaten")                   # Phase 3: Daten für kopf3_backen.py (npz)
GLAETTEN = int(OPTIONEN.get("glaetten", "12"))         # Glättungsschritte der übertragenen MPFB-Formen
t0 = time.time()
bericht = {"ausgabe": os.path.basename(AUSGABE), "morph_normalen": MORPH_NORMALEN}


def zeit(text):
    print(f"[{time.time() - t0:6.1f} s] {text}", flush=True)


for o in list(bpy.data.objects):
    bpy.data.objects.remove(o, do_unlink=True)

# ================================================================= 1. Quellen und Passung
ict = kq.ict_laden()
mp = kq.Mpfb()
BASIS = mp.makros(gender=1.0, age=0.5, muscle=0.5, weight=0.5)
P = mp.lage(BASIS)
pa = kp.anpassen(ict, mp, P, bericht)
S = pa["ausgerichtet"].copy()          # ICT im Raum der MPFB-Büste (Form des Spielkopfs)
F = pa["angepasst"]                    # an die MPFB-Haut angepasste Kopie (nur für die Bindung)
DREH = pa["s"] * pa["r"]               # ICT-Versätze in den MPFB-Raum
zeit("Passung: " + json.dumps(bericht["ausrichtung"]))

T = kq.ICT_TEILE
N_HAUT = T["zahnfleisch"][0]           # ICT 0..14061: Gesicht, Hinterkopf/Hals, Mund- und Augenhöhlen (ein Netz)
fl_alle = ict["flaechen"]
fl_haut = [f for f in fl_alle if max(f) < N_HAUT]
fl_haut_idx = [i for i, f in enumerate(fl_alle) if max(f) < N_HAUT]


def teil(name):
    a, b = T[name]
    return np.arange(a, b)


def rand_schleifen(flaechen):
    """Geordnete Randschleifen (Ecken) eines Flächenverbunds."""
    zaehler = {}
    for f in flaechen:
        for k in range(len(f)):
            e = (min(f[k], f[(k + 1) % len(f)]), max(f[k], f[(k + 1) % len(f)]))
            zaehler[e] = zaehler.get(e, 0) + 1
    nb = {}
    for (a, b), c in zaehler.items():
        if c == 1:
            nb.setdefault(a, []).append(b)
            nb.setdefault(b, []).append(a)
    gesehen, aus = set(), []
    for s in nb:
        if s in gesehen:
            continue
        schleife, vor, jetzt = [s], None, s
        gesehen.add(s)
        while True:
            weiter = [x for x in nb[jetzt] if x != vor and x not in gesehen]
            if not weiter:
                break
            vor, jetzt = jetzt, weiter[0]
            schleife.append(jetzt)
            gesehen.add(jetzt)
        aus.append(schleife)
    return aus


hals = [s for s in rand_schleifen(fl_haut) if len(s) > 20]
assert len(hals) == 1, f"ICT-Haut sollte genau einen Rand (Hals) haben, gefunden {len(hals)}"
hals = np.array(hals[0])


def naht(teil_a, teil_b):
    """Ecken von teil_a, die mit teil_b eine Fläche teilen (z. B. Lidrand = Gesicht an Augenhöhle)."""
    a0, a1 = T[teil_a]
    b0, b1 = T[teil_b]
    aus = set()
    for f in fl_haut:
        if any(b0 <= i < b1 for i in f):
            aus.update(i for i in f if a0 <= i < a1)
    return np.array(sorted(aus))


lippennaht = naht("gesicht", "mundhoehle")
lidnaht = {"l": naht("gesicht", "augenhoehle_l"), "r": naht("gesicht", "augenhoehle_r")}
augen = {s: pa["augen_ict"][s] for s in ("l", "r")}       # (Mitte, Halbmesser) im MPFB-Raum
zeit(f"Hals {len(hals)} Ecken, Lippennaht {len(lippennaht)}, Lidnaht {len(lidnaht['l'])}/{len(lidnaht['r'])}")

# ================================================================= 2. Hals an die Büste anpassen
flaeche_ganz = kw.Flaeche(P, kw.dreiecke(mp.koerper_flaechen))
lage_f, _, _, _ = flaeche_ganz.naechste(F[:N_HAUT], 0.05)
from mathutils.kdtree import KDTree  # noqa: E402
baum = KDTree(len(hals))
for i, j in enumerate(hals):
    baum.insert(S[j], i)
baum.balance()
abstand_hals = np.array([baum.find(S[i])[2] for i in range(N_HAUT)])
w_hals = 1.0 - kw.glatt_stufe(abstand_hals, 0.0, 0.05)
w_hals[T["mundhoehle"][0]:] = 0.0
S[:N_HAUT] += w_hals[:, None] * (lage_f - S[:N_HAUT])
bericht["hals_angleich_max_mm"] = round(float(np.linalg.norm(lage_f[hals] - pa["ausgerichtet"][hals], axis=1).max() * 1000), 1)

# ================================================================= 3. Büste aus MPFB schneiden
q = S[hals]
mitte_hals = q.mean(0)
_, _, vt = np.linalg.svd(q - mitte_hals)
n_schnitt = vt[2] if vt[2][2] > 0 else -vt[2]          # zeigt zum Kopf
SAUM = 0.012                                            # Breite des Nahtbands zwischen ICT-Hals und Büste
# Halsachse aus den MPFB-Gelenken (Halsansatz -> Kopfmitte), für Kragen und Kleidung
hals_fuss = P[mp.gruppen["joint-neck"]].mean(0)
hals_achse = P[mp.gruppen["joint-head"]].mean(0) - hals_fuss
hals_achse /= np.linalg.norm(hals_achse)


schluessel_z = (P[mp.gruppen["joint-l-clavicle"]].mean(0)[2] + P[mp.gruppen["joint-r-clavicle"]].mean(0)[2]) / 2.0
schulter_x = P[mp.gruppen["joint-l-shoulder"]].mean(0)[0]
UNTEN = schluessel_z - 0.20
SEITE = schulter_x + 0.08                                # mit dem Ansatz der Oberarme (Kinder im Bild)
# Vorzeichenbehafteter Abstand jeder MPFB-Ecke von der Halsschleife (die nach dem Angleich auf der MPFB-Haut
# liegt): Betrag = Abstand zur Schleife, Vorzeichen = oberhalb (Kopfseite) oder unterhalb. Geschnitten wird auf der
# Linie „SAUM unter der Schleife“ – eine Ebene ginge nicht, weil der ICT-Hals vorn tief bis zur Drosselgrube reicht.
schleife_dicht = np.vstack([q * (1 - a_) + np.roll(q, -1, 0) * a_ for a_ in np.linspace(0, 1, 6)[:-1]])
kd_schl = KDTree(len(schleife_dicht))
for i, p_ in enumerate(schleife_dicht):
    kd_schl.insert(p_, i)
kd_schl.balance()


def hoehe_zur_schleife(p_):
    co, i, d = kd_schl.find(p_)
    return d if (np.asarray(p_) - np.asarray(co)) @ n_schnitt > 0 else -d


bm = bmesh.new()
bv = [bm.verts.new(tuple(p)) for p in P]
for f in mp.koerper_flaechen:
    if all(P[i, 2] > UNTEN - 0.05 for i in f) and all(abs(P[i, 0]) < SEITE + 0.05 for i in f):
        bm.faces.new([bv[i] for i in f])
for v in [v for v in bm.verts if not v.link_faces]:
    bm.verts.remove(v)
for ebene_co, ebene_no in (((0, 0, UNTEN), (0, 0, -1)), ((SEITE, 0, 0), (1, 0, 0)), ((-SEITE, 0, 0), (-1, 0, 0))):
    bmesh.ops.bisect_plane(bm, geom=list(bm.verts) + list(bm.edges) + list(bm.faces), plane_co=ebene_co,
                           plane_no=ebene_no, clear_outer=True)
# Schnitt entlang der Linie hoehe = -SAUM: Kanten teilen, neue Punkte in den Flächen verbinden, oben löschen
wl = bm.verts.layers.float.new("hoehe")
for v in bm.verts:
    v[wl] = hoehe_zur_schleife(v.co)
neu_v = set()
for e in list(bm.edges):
    a_, b_ = e.verts
    wa, wb = a_[wl] + SAUM, b_[wl] + SAUM
    if wa * wb < 0:
        ne, nv = bmesh.utils.edge_split(e, a_, wa / (wa - wb))
        nv[wl] = -SAUM
        neu_v.add(nv)
for f in list(bm.faces):
    vs = [v for v in f.verts if v in neu_v]
    if len(vs) == 2:
        bmesh.ops.connect_verts(bm, verts=vs)
weg = [f for f in bm.faces if sum(v[wl] for v in f.verts) / len(f.verts) > -SAUM
       or any(v.co.z < UNTEN - 1e-5 for v in f.verts) or any(abs(v.co.x) > SEITE + 1e-5 for v in f.verts)]
bmesh.ops.delete(bm, geom=weg, context="FACES")
for v in [v for v in bm.verts if not v.link_faces]:
    bm.verts.remove(v)
# nur das größte zusammenhängende Stück (Reste an Armen und Rücken weg)
bm.verts.ensure_lookup_table()
stueck = {}
for v in bm.verts:
    if v.index in stueck:
        continue
    stapel, nr = [v], len(set(stueck.values()))
    while stapel:
        x = stapel.pop()
        if x.index in stueck:
            continue
        stueck[x.index] = nr
        for e in x.link_edges:
            o = e.other_vert(x)
            if o.index not in stueck:
                stapel.append(o)
groesstes = max(set(stueck.values()), key=lambda k: sum(1 for s in stueck.values() if s == k))
bmesh.ops.delete(bm, geom=[v for v in bm.verts if stueck[v.index] != groesstes], context="VERTS")
bm.verts.ensure_lookup_table()
bm.verts.index_update()
B_ecken = np.array([tuple(v.co) for v in bm.verts])
B_flaechen = [[v.index for v in f.verts] for f in bm.faces]
B_hoehe = np.array([v[wl] for v in bm.verts])
bm.free()
hoehe_schnitt = B_hoehe
oben_rand = [s for s in rand_schleifen(B_flaechen) if np.all(np.abs(hoehe_schnitt[s] + SAUM) < 2e-4)]
if len(oben_rand) != 1:
    for s in rand_schleifen(B_flaechen):
        print("Büstenrand", len(s), "Höhe", hoehe_schnitt[s].min(), hoehe_schnitt[s].max(), "z", B_ecken[s, 2].min(), B_ecken[s, 2].max())
    print("Halsschleife z", S[hals, 2].min(), S[hals, 2].max(), "n", n_schnitt, "Schlüsselbein", schluessel_z)
    raise SystemExit("Oberer Rand der Büste nicht eindeutig")
oben_rand = np.array(oben_rand[0])
zeit(f"Büste: {len(B_ecken)} Ecken, {len(B_flaechen)} Flächen, oberer Rand {len(oben_rand)}")

# ================================================================= 4. Hautnetz: ICT-Haut + Naht + Büste
NB = len(B_ecken)
H_ecken = np.vstack([S[:N_HAUT], B_ecken])
H_flaechen = fl_haut + [[i + N_HAUT for i in f] for f in B_flaechen]
me = bpy.data.meshes.new("Haut")
me.from_pydata([tuple(p) for p in H_ecken], [], H_flaechen)
bm = bmesh.new()
bm.from_mesh(me)
bm.verts.ensure_lookup_table()
bm.edges.ensure_lookup_table()


def kanten_der_schleife(schleife):
    aus = []
    for k in range(len(schleife)):
        e = bm.edges.get((bm.verts[int(schleife[k])], bm.verts[int(schleife[(k + 1) % len(schleife)])]))
        assert e is not None
        aus.append(e)
    return aus


nahtflaechen = bmesh.ops.bridge_loops(bm, edges=kanten_der_schleife(hals) + kanten_der_schleife(oben_rand + N_HAUT))["faces"]
# Umlaufsinn der Naht wie bei den angrenzenden Flächen (sonst dunkle Linie durch falsche Normalen)
stimmt = 0
for f in nahtflaechen:
    for e in f.edges:
        for g in e.link_faces:
            if g not in nahtflaechen:
                stimmt += 1 if f.normal.dot(g.normal) > 0 else -1
if stimmt < 0:
    bmesh.ops.reverse_faces(bm, faces=nahtflaechen)
bericht["naht_umgedreht"] = bool(stimmt < 0)
anzahl_vor_naht = len(H_flaechen)
bm.to_mesh(me)
bm.free()
assert len(me.vertices) == len(H_ecken)
n_flaechen = len(me.polygons)
bericht["naht_flaechen"] = n_flaechen - anzahl_vor_naht
haut = bpy.data.objects.new("Haut", me)
bpy.context.collection.objects.link(haut)
me.polygons.foreach_set("use_smooth", [True] * n_flaechen)
zeit(f"Hautnetz {len(me.vertices)} Ecken, {n_flaechen} Flächen (Naht {bericht['naht_flaechen']})")

# Stoffe: 0 Haut, 1 Mund (Mundhöhle), 2 Kleidung (Büste unter der Kragenlinie)
mundh = T["mundhoehle"]
# Kragenlinie: Schnitt der Haut mit einer Ebene quer zur Halsachse, KRAGEN_HOEHE über dem MPFB-Halsgelenk (vorn
# tiefer, weil der Hals nach vorn geneigt ist). So liegt sie geschlossen um den Hals (Stehkragen).
KRAGEN_HOEHE = 0.008
kragen_mitte = hals_fuss + hals_achse * KRAGEN_HOEHE
e1_ = np.cross(hals_achse, [0.0, 1.0, 0.0])
e1_ /= np.linalg.norm(e1_)
e2_ = np.cross(hals_achse, e1_)


def isolinie(ecken, flaechen, wert, stufe=0.0):
    """Geschlossene Linien wert == stufe auf einem Netz (lineare Teilung der Kanten); die längste geschlossene."""
    tri = kw.dreiecke(flaechen)
    punkt, nachbar = {}, {}
    for a_, b_, c_ in tri:
        schnitte = []
        for u, v in ((a_, b_), (b_, c_), (c_, a_)):
            if (wert[u] - stufe) * (wert[v] - stufe) < 0:
                schl = (min(u, v), max(u, v))
                if schl not in punkt:
                    s_ = (stufe - wert[u]) / (wert[v] - wert[u])
                    punkt[schl] = ecken[u] * (1 - s_) + ecken[v] * s_
                schnitte.append(schl)
        if len(schnitte) == 2:
            nachbar.setdefault(schnitte[0], []).append(schnitte[1])
            nachbar.setdefault(schnitte[1], []).append(schnitte[0])
    ketten, gesehen = [], set()
    for s0 in nachbar:
        if s0 in gesehen:
            continue
        kette, vor, jetzt = [s0], None, s0
        gesehen.add(s0)
        while True:
            weiter = [x for x in nachbar[jetzt] if x != vor and x not in gesehen]
            if not weiter:
                break
            vor, jetzt = jetzt, weiter[0]
            kette.append(jetzt)
            gesehen.add(jetzt)
        if len(kette) > 2 and kette[0] in nachbar[kette[-1]]:
            ketten.append(np.array([punkt[k] for k in kette]))
    assert ketten, "Kragenlinie: keine geschlossene Linie"
    return max(ketten, key=len)


# Halt B2 (Option ausschnitt=<mm>): Kragenlinie vorn tiefer (offener Ausschnitt), damit das Kinn nicht auf dem Kragen
# sitzt (Abnahme: Kinn ≥ 30 mm über der Kragenoberkante; Stehkragen der Phase 5: 9,5 mm). Senkung längs der Halsachse
# D · max(0, cos φ)³, φ = Winkel um die Halsachse ab vorn (e2_ zeigt nach vorn, −y); das Band ist vorn um
# AUSSCHNITT_BAND niedriger. Ohne Option (0) genau wie bisher.
AUSSCHNITT = float(OPTIONEN.get("ausschnitt", "0")) / 1000.0
AUSSCHNITT_BAND = float(OPTIONEN.get("ausschnitt_band", "0.5"))


def vorn_anteil(p_):
    """max(0, cos φ)³ um die Halsachse (1 = vorn Mitte, 0 = Seite und hinten)."""
    r_ = np.asarray(p_) - kragen_mitte
    r_ = r_ - np.outer(r_ @ hals_achse, hals_achse) if r_.ndim == 2 else r_ - (r_ @ hals_achse) * hals_achse
    c_ = (r_ @ e2_) / np.maximum(np.linalg.norm(r_, axis=-1), 1e-9)
    return np.clip(c_, 0.0, 1.0) ** 3


def kragen_wert(p_):
    """Höhe über der Kragenlinie (Meter längs der Halsachse; < 0 = Kleidung)."""
    return (np.asarray(p_) - kragen_mitte) @ hals_achse + AUSSCHNITT * vorn_anteil(p_)


nah_fl = [list(p_.vertices) for p_ in me.polygons if not all(mundh[0] <= i < mundh[1] for i in p_.vertices)
          and np.linalg.norm(H_ecken[list(p_.vertices)] - kragen_mitte, axis=1).max() < 0.13 + AUSSCHNITT]
kragen_linie = isolinie(H_ecken, nah_fl, kragen_wert(H_ecken))
# gleichmäßig neu verteilen (etwa alle 6 mm)
seg = np.linalg.norm(np.roll(kragen_linie, -1, 0) - kragen_linie, axis=1)
umfang = np.concatenate([[0], np.cumsum(seg)])
n_kragen = int(umfang[-1] / 0.006)
geschl = np.vstack([kragen_linie, kragen_linie[:1]])
ziele = np.linspace(0, umfang[-1], n_kragen, endpoint=False)
kragen_linie = np.stack([np.interp(ziele, umfang, geschl[:, a_]) for a_ in range(3)], axis=1)


def ausserhalb_kragen(p_):
    """True, wo Kleidung ist: unter der Kragenlinie (mit Ausschnitt)."""
    return kragen_wert(p_) < 0


bericht["ausschnitt_mm"] = round(AUSSCHNITT * 1000, 1)


bericht["kragen_umfang_cm"] = round(float(umfang[-1] * 100), 1)


stoff_idx = np.zeros(n_flaechen, dtype=np.int64)
for k, p_ in enumerate(me.polygons):
    vs = list(p_.vertices)
    if all(mundh[0] <= i < mundh[1] for i in vs):
        stoff_idx[k] = 1
    else:
        # Kleidung, sobald eine Ecke außerhalb der Kragenlinie liegt (der Kragen deckt die gezackte Grenze)
        if ausserhalb_kragen(H_ecken[vs]).any():
            stoff_idx[k] = 2
me.polygons.foreach_set("material_index", stoff_idx)

# UV: eine Karte. ICT-Kachel 1001 (Gesicht) -> [0, 0,6] × [0,4, 1]; Kachel 1002 (Hinterkopf, Hals, Höhlen)
# -> [0,6, 1] × [0,6, 1]; Büste und Naht (Smart-UV) -> [0, 1] × [0, 0,38]
uvl = me.uv_layers.new(name="UVMap")
uv_feld = np.zeros((len(me.loops), 2))
ict_uv = ict["uv"]
for k, p_ in enumerate(me.polygons):
    if k >= len(fl_haut):
        continue
    fuv = ict["flaechen_uv"][fl_haut_idx[k]]
    for j, li in enumerate(p_.loop_indices):
        u, v = ict_uv[fuv[j]]
        kachel = int(math.floor(u))
        u -= kachel
        if kachel == 0:
            uv_feld[li] = (0.6 * u, 0.4 + 0.6 * v)
        else:
            uv_feld[li] = (0.6 + 0.4 * u, 0.6 + 0.4 * v)
uvl.data.foreach_set("uv", uv_feld.ravel())
bpy.context.view_layer.objects.active = haut
haut.select_set(True)
bpy.ops.object.mode_set(mode="EDIT")
bme = bmesh.from_edit_mesh(me)
bme.faces.ensure_lookup_table()
for f in bme.faces:
    f.select_set(f.index >= len(fl_haut))
bmesh.update_edit_mesh(me)
bpy.ops.uv.smart_project(angle_limit=math.radians(70), island_margin=0.02, scale_to_bounds=False)
bpy.ops.object.mode_set(mode="OBJECT")
uv_feld = np.empty(len(me.loops) * 2)
uvl.data.foreach_get("uv", uv_feld)
uv_feld = uv_feld.reshape(-1, 2)
buesten_schleifen = np.array([li for k, p_ in enumerate(me.polygons) if k >= len(fl_haut) for li in p_.loop_indices])
b_uv = uv_feld[buesten_schleifen]
lo, hi = b_uv.min(0), b_uv.max(0)
massstab = min(0.98 / (hi[0] - lo[0]), 0.36 / (hi[1] - lo[1]))
uv_feld[buesten_schleifen] = (b_uv - lo) * massstab + np.array([0.01, 0.01])
uvl.data.foreach_set("uv", uv_feld.ravel())
zeit("UV gesetzt")

# ================================================================= 5. Bindung an die MPFB-Haut
# ICT: angepasste Lage, am Hals zur echten Lage hin (die dort auf der MPFB-Haut liegt, sonst entsteht an der Naht
# eine Stufe); Büste: liegt schon auf MPFB
herkunft_pos = np.vstack([F[:N_HAUT] + w_hals[:, None] * (S[:N_HAUT] - F[:N_HAUT]), B_ecken])
bindung = flaeche_ganz.binden(herkunft_pos, 0.2)
H_kanten, H_grad = kw.kanten([list(p_.vertices) for p_ in me.polygons], len(H_ecken))
BUESTE = np.arange(len(H_ecken)) >= N_HAUT
G_GLATT = np.zeros(len(H_ecken))
G_GLATT[:N_HAUT] = 1.0 - w_hals
if "pruefen" in OPTIONEN:
    # Probe: Büstenecken, die auf einer MPFB-Ecke liegen, müssen deren Versatz exakt bekommen
    kd_p = KDTree(len(P))
    for i, p_ in enumerate(P):
        kd_p.insert(p_, i)
    kd_p.balance()
    d_test = mp.lage(mp.makros(gender=1.0, age=0.0)) - P
    fehler_ = []
    for j in range(N_HAUT, len(H_ecken)):
        co, i, ab = kd_p.find(H_ecken[j])
        if ab < 1e-6:
            uebertr = (d_test[bindung[0][j]] * bindung[1][j][:, None]).sum(0)
            fehler_.append((np.linalg.norm(uebertr - d_test[i]), j, i, tuple(bindung[0][j])))
    fehler_.sort(reverse=True)
    print("PRUEFEN Büste", len(fehler_), "größte Abweichungen (m):", [(round(f[0], 4), f[2], f[3]) for f in fehler_[:8]])
bericht["bindung_abstand_mm"] = {"mittel": round(float(np.mean(bindung[3][:N_HAUT]) * 1000), 2),
                                 "max": round(float(np.max(bindung[3][:N_HAUT]) * 1000), 2)}

# Tiefe in den Höhlen (Abstand zur Naht), für Übergänge und die dunkle Mundhöhle
kd_lippe = KDTree(len(lippennaht))
for i, j in enumerate(lippennaht):
    kd_lippe.insert(S[j], i)
kd_lippe.balance()
mh = teil("mundhoehle")
tiefe_mund = np.array([kd_lippe.find(S[i])[2] for i in mh])
tiefe_auge = {}
for s in ("l", "r"):
    kd = KDTree(len(lidnaht[s]))
    for i, j in enumerate(lidnaht[s]):
        kd.insert(S[j], i)
    kd.balance()
    tiefe_auge[s] = np.array([kd.find(S[i])[2] for i in teil("augenhoehle_" + s)])

# Teile im ICT-Raum (ausgerichtet)
oz = np.arange(*kq.ICT_OBERE_ZAEHNE)
uz = np.arange(kq.ICT_OBERE_ZAEHNE[1], T["zaehne"][1])
z_biss = (S[oz, 2].min() + S[uz, 2].max()) / 2.0
zf = teil("zahnfleisch")


def unterkiefer_gewicht(z):
    return kw.glatt_stufe(z, z_biss + 0.004, z_biss - 0.004)


# ================================================================= 6. Formen aus MPFB: Makros und Gene
def makro_versatz(makros):
    d = mp.lage(makros) - P
    anker = d[mp.gruppen["joint-head"]].mean(0)       # Kopfmitte bleibt stehen (Bildausschnitt)
    return d - anker


def ziel_versatz(teile):
    d = np.zeros_like(P)
    for name, w in teile.items():
        d += float(w) * mp.versatz(name)
    return d


# Phase 6: eigene Reglerdatei (regler=<datei>, Standard kopf2_regler.json – Phase 5 bleibt so nachbaubar)
REGLER = OPTIONEN.get("regler", os.path.join(HIER, "kopf2_regler.json"))
regler = json.load(open(REGLER, encoding="utf-8"))["gene"]
formen_mpfb = {}                 # Name -> Versatz am MPFB-Netz
ALTER = [("saeugling", 0.0), ("kind", 0.1875), ("jung", 0.5), ("alt", 1.0)]
GESCHLECHT = [("w", 0.0), ("m", 1.0)]
mimikmass = {}
gesicht_pkt = np.concatenate([mp.gruppen["helper-l-eye"], mp.gruppen["helper-r-eye"], mp.gruppen["lips"]])


def gesichtsgroesse(p):
    q = p[gesicht_pkt]
    return float(np.sqrt(((q - q.mean(0)) ** 2).sum(1).mean()))


g0 = gesichtsgroesse(P)
for g, gw in GESCHLECHT:
    for a, aw in ALTER:
        name = f"makro_{g}_{a}"
        lage_ = mp.lage(mp.makros(gender=gw, age=aw))
        mimikmass[name] = round(gesichtsgroesse(lage_) / g0, 4)
        if (g, a) != ("m", "jung"):
            formen_mpfb[name] = makro_versatz(mp.makros(gender=gw, age=aw))
        if a == "saeugling":
            continue
        for art, schl in (("kraft", "muscle"), ("gewicht", "weight")):
            mitte_ = mp.lage(mp.makros(gender=gw, age=aw))
            for richt, wert in (("minus", 0.0), ("plus", 1.0)):
                d = mp.lage(mp.makros(gender=gw, age=aw, **{schl: wert})) - mitte_
                d -= d[mp.gruppen["joint-head"]].mean(0)
                formen_mpfb[f"makro_{g}_{a}_{art}_{richt}"] = d
zeit(f"Makroformen: {len(formen_mpfb)}")
gen_formen = {}
for kennung, eintrag in regler.items():
    if eintrag["art"] == "form":
        for richt in ("minus", "plus"):
            if richt in eintrag:
                formen_mpfb[f"gen_{kennung}_{richt}"] = ziel_versatz(eintrag[richt])
                gen_formen.setdefault(kennung, {})[richt] = f"gen_{kennung}_{richt}"
    elif eintrag["art"] == "eigen" and kennung == "augen_tiefe":
        maske = np.zeros(len(P))
        for s in ("l", "r"):
            m_ = np.linalg.norm(mp.versatz(f"eyes/{s}-eye-trans-out"), axis=1)
            maske = np.maximum(maske, m_ / m_.max())
        for richt, vz, schl in (("minus", -1.0, "vor_mm"), ("plus", 1.0, "zurueck_mm")):
            d = np.zeros_like(P)
            d[:, 1] = vz * maske * eintrag[richt][schl] / 1000.0
            formen_mpfb[f"gen_{kennung}_{richt}"] = d
            gen_formen.setdefault(kennung, {})[richt] = f"gen_{kennung}_{richt}"
# Phase 5: Altersformen (Reife des erwachsenen Gesichts, Greis) aus kopf2_regler.json „alter_formen“ und, zum
# Ausprobieren, Probeformen „probe_<name>“ aus einer JSON-Datei (Option proben=<datei>, {name: {ziel: gewicht}})
alter_formen = json.load(open(REGLER, encoding="utf-8")).get("alter_formen", {})
alter_eigen = {}                 # Phase 6: Altersform -> {eigene Form: Gewicht} (kopf6_eigen.py, nach der Übertragung)
for name, teile in alter_formen.items():
    if not name.startswith("_"):
        formen_mpfb[name] = ziel_versatz(teile.get("ziele", {}))
        if teile.get("eigen"):
            alter_eigen[name] = teile["eigen"]
# Phase 6 (S6): Herkunftsachsen der MakeHuman-Makros (african, asian; Mann und Frau 25) – nur zum Bereinigen der
# Gesichtszüge (herkunft=1), sie kommen nicht in den fertigen Kopf
if OPTIONEN.get("herkunft") == "1":
    for g, gw in GESCHLECHT:
        mitte_h = makro_versatz(mp.makros(gender=gw, age=0.5))
        for rasse in ("african", "asian"):
            r_ = {"caucasian": 0.0, "african": 0.0, "asian": 0.0}
            r_[rasse] = 1.0
            formen_mpfb[f"herkunft_{rasse}_{g}"] = makro_versatz(mp.makros(gender=gw, age=0.5, race=r_)) - mitte_h
if "proben" in OPTIONEN:
    for name, teile in json.load(open(OPTIONEN["proben"], encoding="utf-8")).items():
        if not name.startswith("_"):
            formen_mpfb["probe_" + name] = ziel_versatz(teile)
zeit(f"Formen aus MPFB gesamt: {len(formen_mpfb)}")


def augen_aehnlichkeit(d_mpfb, s):
    """Verschiebung und Maßstab des MPFB-Hilfsauges -> Versatz-Funktion für den ICT-Augapfel."""
    idx = mp.gruppen[f"helper-{s}-eye"]
    x = P[idx]
    y = x + d_mpfb[idx]
    rx = np.sqrt(((x - x.mean(0)) ** 2).sum(1).mean())
    ry = np.sqrt(((y - y.mean(0)) ** 2).sum(1).mean())
    verschiebung = y.mean(0) - x.mean(0)
    mitte_ict = augen[s][0]
    return lambda p: verschiebung + (ry / rx - 1.0) * (p - mitte_ict)


def zahn_affin(d_mpfb, gruppe):
    idx = mp.gruppen[gruppe]
    a, b = kw.affin(P[idx], P[idx] + d_mpfb[idx])
    return lambda p: p @ a.T + b - p


# ================================================================= 7. Netze der übrigen Teile
def neues_netz(name, ecken, flaechen, stoff_je_flaeche, stoffe, uv_je_schleife=None):
    me_ = bpy.data.meshes.new(name)
    me_.from_pydata([tuple(p) for p in ecken], [], flaechen)
    me_.polygons.foreach_set("use_smooth", [True] * len(flaechen))
    for s_ in stoffe:
        me_.materials.append(s_)
    me_.polygons.foreach_set("material_index", stoff_je_flaeche)
    if uv_je_schleife is not None:
        u = me_.uv_layers.new(name="UVMap")
        u.data.foreach_set("uv", np.asarray(uv_je_schleife, dtype=np.float64).ravel())
    ob = bpy.data.objects.new(name, me_)
    bpy.context.collection.objects.link(ob)
    return ob


def stoff(name, farbe=(0.8, 0.8, 0.8, 1.0), bild=None, rauheit=0.5):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    bsdf = m.node_tree.nodes["Principled BSDF"]
    bsdf.inputs["Base Color"].default_value = farbe
    bsdf.inputs["Roughness"].default_value = rauheit
    if bild:
        tex = m.node_tree.nodes.new("ShaderNodeTexImage")
        tex.image = bpy.data.images.load(bild)
        tex.image.pack()
        m.node_tree.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
        m.node_tree.links.new(tex.outputs["Alpha"], bsdf.inputs["Alpha"])
    return m


def teilnetz(name, bereiche, stoffwahl, stoffe):
    """Netz aus ICT-Eckbereichen; stoffwahl(ict_stoffname) -> Index in stoffe."""
    idx = np.concatenate([np.arange(a, b) for a, b in bereiche])
    neu = -np.ones(len(S), dtype=np.int64)
    neu[idx] = np.arange(len(idx))
    fl, st, uvs = [], [], []
    for k, f in enumerate(fl_alle):
        if neu[f[0]] >= 0:
            fl.append([int(neu[i]) for i in f])
            st.append(stoffwahl(ict["stoffe"][k]))
            for t_ in ict["flaechen_uv"][k]:
                u, v = ict_uv[t_]
                uvs.append((u - math.floor(u), v))
    return neues_netz(name, S[idx], fl, st, stoffe, uvs), idx


st_haut, st_mund, st_kleid = stoff("Haut", (0.78, 0.6, 0.5, 1)), stoff("Mund", (0.45, 0.2, 0.2, 1)), stoff("Kleidung", (0.55, 0.5, 0.42, 1), rauheit=0.9)
for s_ in (st_haut, st_mund, st_kleid):
    me.materials.append(s_)
st_leder, st_iris, st_traene, st_lid = stoff("Augen_Lederhaut"), stoff("Augen_Iris"), stoff("Traene"), stoff("Lidschatten")
st_horn = stoff("Hornhaut")             # Phase 3: Hornhaut als eigene Fläche (Glanz), wird unten zugeteilt
augen_obj, augen_idx = teilnetz("Augen", [T["auge_l"], T["auge_r"], T["traene_l"], T["traene_r"], T["lidschatten_l"], T["lidschatten_r"]],
                                lambda s_: {"M_ScleraLeft": 0, "M_ScleraRight": 0, "M_IrisLeft": 1, "M_IrisRight": 1,
                                            "M_LacrimalFluid": 2}.get(s_, 3), [st_leder, st_iris, st_traene, st_lid, st_horn])
st_zahn, st_zfl = stoff("Zaehne", (0.85, 0.82, 0.75, 1)), stoff("Zahnfleisch", (0.6, 0.25, 0.25, 1))
zahn_obj, zahn_idx = teilnetz("Zaehne", [T["zahnfleisch"], T["zaehne"]], lambda s_: 0 if s_ == "M_Teeth" else 1, [st_zahn, st_zfl])

# Brauen und Wimpern (Phase 3): eigene Karten, das Bild entsteht Haar für Haar (kopf3_haare.py,
# kopf3_haartexturen.py); die MakeHuman-Karten der Phase 2 entfallen
haut_tri = kw.dreiecke([f for f in fl_haut if max(f) < T["mundhoehle"][0]])
flaeche_S = kw.Flaeche(S[:N_HAUT], haut_tri)
S_haut_nrm = kw.ecken_normalen(S[:N_HAUT], haut_tri)
merk68 = S[ict["merkpunkte68"]]
b_e, b_fl, b_uv, b_lage, b_seite = kh.brauen(merk68, flaeche_S)
# Phase 5: Wurzeln der Wimpern an der vorderen Lidkante = wo die ICT-Wimpernstreifen die Haut berühren
gesicht_idx = teil("gesicht")
_kd_gesicht = KDTree(len(gesicht_idx))
for i_, j_ in enumerate(gesicht_idx):
    _kd_gesicht.insert(S[j_], i_)
_kd_gesicht.balance()
wimper_wurzeln = {}
for s_ in ("l", "r"):
    q_ = S[teil("wimpern_" + s_)]
    d_ = np.array([_kd_gesicht.find(p_)[2] for p_ in q_])
    wimper_wurzeln[s_] = np.unique(np.round(q_[d_ < 0.0003], 6), axis=0)
bericht["wimpern_wurzeln"] = {s_: int(len(v_)) for s_, v_ in wimper_wurzeln.items()}
w_e, w_fl, w_uv, w_oben, w_seite, w_wurzel = kh.wimpern(wimper_wurzeln, {s_: S[lidnaht[s_]] for s_ in ("l", "r")}, augen,
                                                        flaeche_S)
st_braue = stoff("Brauen", (0.1, 0.07, 0.05, 1))
st_wimper = stoff("Wimpern", (0.05, 0.04, 0.03, 1))
haar_ecken = np.vstack([b_e, w_e]).tolist()
haar_flaechen = [list(f) for f in b_fl] + [[i + len(b_e) for i in f] for f in w_fl]
haar_stoff = [0] * len(b_fl) + [1] * len(w_fl)
haar_uv = [u for f_uv in b_uv for u in f_uv] + [u for f_uv in w_uv for u in f_uv]
haar_art = ["brauen"] * len(b_e) + ["wimpern"] * len(w_e)
bericht["haarkarten"] = {"brauen_ecken": int(len(b_e)), "wimpern_ecken": int(len(w_e))}
haare_obj = neues_netz("Haare", np.array(haar_ecken), haar_flaechen, haar_stoff, [st_braue, st_wimper], haar_uv)
haar_art = np.array(haar_art)
zeit(f"Teile: Augen {len(augen_idx)}, Zähne {len(zahn_idx)}, Haare {len(haar_ecken)} Ecken")

# Kragen: Band entlang der Kragenlinie, außen 2–6,5 mm, 2,6 cm hoch, schmiegt
# sich oben an den Hals
flaeche_H = kw.Flaeche(H_ecken, kw.dreiecke([list(p_.vertices) for p_ in me.polygons if stoff_idx[p_.index] != 1]))
ringe = []
band_faktor = 1.0 - AUSSCHNITT_BAND * vorn_anteil(kragen_linie) if AUSSCHNITT > 0 else np.ones(len(kragen_linie))
for hoch, aus_mm in ((0.0, 0.002), (0.012, 0.0055), (0.024, 0.0065), (0.026, 0.004), (0.022, 0.0015)):
    q = kragen_linie + np.outer(hoch * band_faktor, hals_achse)
    lage_, nrm, _, _ = flaeche_H.naechste(q, 0.05)
    radial = (lage_ - hals_fuss) - np.outer((lage_ - hals_fuss) @ hals_achse, hals_achse)
    nrm = np.where((nrm * radial).sum(1, keepdims=True) < 0, -nrm, nrm)   # nach außen
    ringe.append(lage_ + nrm * aus_mm)
kr_ecken = np.vstack(ringe)
kr_flaechen, kr_uv = [], []
for r_ in range(len(ringe) - 1):
    for k in range(n_kragen):
        a, b = r_ * n_kragen + k, r_ * n_kragen + (k + 1) % n_kragen
        c, d = b + n_kragen, a + n_kragen
        kr_flaechen.append([a, b, c, d])
        u0, u1, v0, v1 = k / n_kragen, (k + 1) / n_kragen, r_ / 4, (r_ + 1) / 4
        kr_uv.extend([(u0, v0), (u1, v0), (u1, v1), (u0, v1)])
# Umlaufsinn: die Außenseite soll nach außen zeigen
fn = np.cross(kr_ecken[kr_flaechen[0][1]] - kr_ecken[kr_flaechen[0][0]], kr_ecken[kr_flaechen[0][3]] - kr_ecken[kr_flaechen[0][0]])
if fn @ (kr_ecken[0] - hals_fuss - hals_achse * ((kr_ecken[0] - hals_fuss) @ hals_achse)) < 0:
    kr_flaechen = [f[::-1] for f in kr_flaechen]
    kr_uv = [u for k in range(0, len(kr_uv), 4) for u in kr_uv[k:k + 4][::-1]]
st_kragen = stoff("Kragen", (0.62, 0.56, 0.46, 1), rauheit=0.9)
kragen_obj = neues_netz("Kragen", kr_ecken, kr_flaechen, [0] * len(kr_flaechen), [st_kragen], kr_uv)
zeit(f"Kragen {len(kr_ecken)} Ecken")

# ================================================================= 8. Formen auf alle Teile
haut_idx_ict = np.arange(N_HAUT)
ah = {s: teil("augenhoehle_" + s) for s in ("l", "r")}
w_auge = {s: kw.glatt_stufe(tiefe_auge[s], 0.001, 0.006) for s in ("l", "r")}
w_mund = kw.glatt_stufe(tiefe_mund, 0.003, 0.015)
uk_mund = unterkiefer_gewicht(S[mh, 2])
uk_zf = unterkiefer_gewicht(S[zf, 2])
# Nachbarn für Teile, die der Haut folgen
lid_teile = np.concatenate([teil("traene_l"), teil("traene_r"), teil("lidschatten_l"), teil("lidschatten_r")])
haut_und_hoehle = np.concatenate([np.arange(T["mundhoehle"][0]), ah["l"], ah["r"]])
nb_lid = kw.k_naechste(S[haut_und_hoehle], S[lid_teile], k=4)
gesicht_bereich = np.arange(T["mundhoehle"][0])
nb_braue = kw.k_naechste(S[gesicht_bereich], np.array(haar_ecken)[haar_art == "brauen"], k=4)
nb_wimper = {}
haar_pos = np.array(haar_ecken)
haar_wurzel = np.vstack([np.zeros((len(b_e), 3)), w_wurzel])     # Phase 5: Wurzel je Wimperecke
for s in ("l", "r"):
    sel = np.where((haar_art == "wimpern") & ((haar_pos[:, 0] > 0) == (s == "l")))[0]
    # an die Haut der Lidkante an der eigenen Wurzel gebunden (nicht an die nächste Haut der Ecke: die Spitze einer
    # 10 mm langen Wimper läge sonst näher am Lid darüber)
    nb_wimper[s] = (sel, kw.k_naechste(S[gesicht_idx], haar_wurzel[sel], k=3))
nb_kragen = kw.k_naechste(H_ecken, kr_ecken, k=4)


def knn(feld, nb):
    idx, gew = nb
    return (feld[idx] * gew[..., None]).sum(1)


# Lidbereich: die mittlere Bewegung des Lidrands wird an die Verschiebung des Augapfels angeglichen (die Form des
# Lids bleibt, nur der Mittelwert wird gezogen); voll bis 1,3 Augenhalbmesser um die Augenmitte, aus bei 2,6
w_lid = {}
for s in ("l", "r"):
    d_ = np.linalg.norm(H_ecken[:N_HAUT] - augen[s][0], axis=1)
    w_lid[s] = 1.0 - kw.glatt_stufe(d_, 1.3 * augen[s][1], 2.6 * augen[s][1])
lid_abweichung = {}


def formen_aus_mpfb(d_mpfb, name=""):
    """Versatz am MPFB-Netz -> Versätze für Haut, Augen, Zähne, Haare, Kragen."""
    dh = kw.uebertragen(bindung, d_mpfb)
    # Die Bindung ist stückweise linear auf dem groben MPFB-Netz (Kanten etwa 1 cm): ohne Glätten zeichnen sich bei
    # großen Formen (Alter) dessen Kanten als Grate im feinen ICT-Netz ab. Geglättet wird nur der Rest nach Abzug
    # einer affinen Abbildung (Mitteln über Nachbarn ist für Maßstab und Verschiebung auf unregelmäßigen Netzen nicht
    # neutral und erzeugt Dellen) und nur auf dem ICT-Teil; die Büste behält den genauen MPFB-Versatz.
    a_, b_ = kw.affin(H_ecken[:N_HAUT], H_ecken[:N_HAUT] + dh[:N_HAUT])
    linear = H_ecken @ a_.T + b_ - H_ecken
    rest = kw.glaetten(dh - linear, H_kanten, H_grad, GLAETTEN, fest=BUESTE)
    # Am Hals (bis 5 cm über der Naht) gilt der genaue Versatz, sonst springt die Naht zur Büste
    dh = (1 - G_GLATT)[:, None] * dh + G_GLATT[:, None] * (linear + rest)
    voll = np.zeros_like(S)            # ICT-Ecken
    voll[:N_HAUT] = dh[:N_HAUT]
    for s in ("l", "r"):
        f_auge = augen_aehnlichkeit(d_mpfb, s)
        korr = f_auge(augen[s][0][None])[0] - voll[lidnaht[s]].mean(0)
        lid_abweichung.setdefault(name, []).append(float(np.linalg.norm(korr) * 1000))
        voll[:N_HAUT] += w_lid[s][:, None] * korr
        idx = teil("auge_" + s)
        voll[idx] = f_auge(S[idx])
        idx = ah[s]
        voll[idx] = (1 - w_auge[s])[:, None] * voll[idx] + w_auge[s][:, None] * f_auge(S[idx])
    f_oben = zahn_affin(d_mpfb, "helper-upper-teeth")
    f_unten = zahn_affin(d_mpfb, "helper-lower-teeth")
    voll[oz] = f_oben(S[oz])
    voll[uz] = f_unten(S[uz])
    voll[zf] = (1 - uk_zf)[:, None] * f_oben(S[zf]) + uk_zf[:, None] * f_unten(S[zf])
    tief = (1 - uk_mund)[:, None] * f_oben(S[mh]) + uk_mund[:, None] * f_unten(S[mh])
    voll[mh] = (1 - w_mund)[:, None] * voll[mh] + w_mund[:, None] * tief
    dh[:N_HAUT] = voll[:N_HAUT]
    voll[lid_teile] = knn(voll[haut_und_hoehle], nb_lid)
    return dh, voll


def formen_aus_ict(d_ict):
    voll = d_ict @ DREH.T
    dh = np.zeros((len(H_ecken), 3))
    dh[:N_HAUT] = voll[:N_HAUT]
    return dh, voll


def drehung_zwischen(a, b):
    """Je Zeile die kleinste Drehung (Matrix), die die Richtung a auf die Richtung b bringt (Rodrigues)."""
    a = a / np.maximum(np.linalg.norm(a, axis=1, keepdims=True), 1e-12)
    b = b / np.maximum(np.linalg.norm(b, axis=1, keepdims=True), 1e-12)
    k = np.cross(a, b)
    s = np.linalg.norm(k, axis=1)
    c = (a * b).sum(1)
    k = k / np.maximum(s, 1e-12)[:, None]
    kx = np.zeros((len(a), 3, 3))
    kx[:, 0, 1], kx[:, 0, 2], kx[:, 1, 2] = -k[:, 2], k[:, 1], -k[:, 0]
    kx[:, 1, 0], kx[:, 2, 0], kx[:, 2, 1] = k[:, 2], -k[:, 1], k[:, 0]
    eins = np.broadcast_to(np.eye(3), kx.shape)
    return eins + s[:, None, None] * kx + (1 - c)[:, None, None] * (kx @ kx)


def haare_und_kragen(dh, voll, mimik=False):
    dhaar = np.zeros_like(haar_pos)
    dhaar[haar_art == "brauen"] = knn(voll[gesicht_bereich], nb_braue)
    for s in ("l", "r"):
        sel, nb = nb_wimper[s]
        if not mimik:
            dhaar[sel] = knn(voll[gesicht_idx], nb)
            continue
        # Mimik (Blinzeln): Die Wimper dreht mit dem Lidrand um die Augenmitte (der Lidrand gleitet über den
        # Augapfel), statt nur verschoben zu werden – sonst stehen die Wimpern beim geschlossenen Auge nach oben
        c = augen[s][0]
        wurzel = haar_wurzel[sel]
        neu = wurzel + knn(voll[gesicht_idx], nb)
        r = drehung_zwischen(wurzel - c, neu - c)
        v = haar_pos[sel]
        dhaar[sel] = neu + np.einsum("nij,nj->ni", r, v - wurzel) - v
    return dhaar, knn(dh, nb_kragen)


# Phase 5: Adamsapfel für „alter_reife_m“ (eigen; das MakeHuman-Netz ist am Hals zu grob dafür): Buckel 3 mm nach vorn,
# 5,2 cm unter dem Kinn auf der Mittellinie, quer 8 mm, längs 11 mm (Gauß), nur auf der Vorderseite des Halses
kinn_p = S[ict["merkpunkte68"][8]]
_hals_vorn = np.where((np.abs(H_ecken[:, 0]) < 0.006) & (np.abs(H_ecken[:, 2] - (kinn_p[2] - 0.052)) < 0.004))[0]
adams_p = H_ecken[_hals_vorn[np.argmin(H_ecken[_hals_vorn, 1])]]
_rel = H_ecken - adams_p
adams_buckel = np.zeros_like(H_ecken)
adams_buckel[:, 1] = -0.003 * np.exp(-0.5 * ((_rel[:, 0] / 0.008) ** 2 + (_rel[:, 2] / 0.011) ** 2)) * (_rel[:, 1] < 0.012)
bericht["adamsapfel_mm"] = [round(float(v) * 1000, 1) for v in adams_p]

# Phase 6 (S4): eigene Formen direkt auf dem ICT-Netz (kopf6_eigen.py), dazu der Adamsapfel als eigene Form
eigen_haut = {}
if OPTIONEN.get("eigen") == "1":
    import kopf6_eigen as ke
    eigen_haut, eigen_bericht = ke.formen(H_ecken, N_HAUT, H_flaechen, S[ict["merkpunkte68"]], augen,
                                          np.arange(T["mundhoehle"][0], N_HAUT), abstand_hals)
    eigen_haut["eigen_adamsapfel"] = adams_buckel.copy()
    bericht["eigen_formen"] = eigen_bericht
    zeit("Eigene Formen: " + json.dumps(eigen_bericht, ensure_ascii=False))
# Halt B2, Weg B (Option vorbilder=<a.npz>,<b.npz>): Formen aus eingepassten Kopfscans (vorbild_einpassen.py) als
# „vorbild_<name>“. Die Datei trägt die Ecken von Haut/0 der .glb (Godot-Achsen: x, y oben, z vorn; Meter) und ihren
# Versatz; je Hautecke die gleich liegende Ecke (≤ 0,2 mm), sonst 0. Brauen, Wimpern, Lider und Kragen folgen wie bei
# den eigenen Formen; Augapfel und Zähne bleiben fest.
vorbild_augen = {}                 # Halt B2: Augapfel folgt dem Vorbild (Versatz je Seite, Blender-Achsen)
if OPTIONEN.get("vorbilder"):
    vb_bericht = {}
    for vb_pfad in OPTIONEN["vorbilder"].split(","):
        vb = np.load(vb_pfad)
        bg, vg = vb["basis"].astype(np.float64), vb["versatz"].astype(np.float64)
        b_bl = np.stack([bg[:, 0], -bg[:, 2], bg[:, 1]], 1)
        v_bl = np.stack([vg[:, 0], -vg[:, 2], vg[:, 1]], 1)
        vb_idx, _ = kw.k_naechste(b_bl, H_ecken[:N_HAUT], k=1)
        abst_ = np.linalg.norm(b_bl[vb_idx[:, 0]] - H_ecken[:N_HAUT], axis=1)
        dh = np.zeros_like(H_ecken)
        dh[:N_HAUT] = np.where(abst_[:, None] < 2e-4, v_bl[vb_idx[:, 0]], 0.0)
        vb_name = "vorbild_" + str(vb["name"])
        eigen_haut[vb_name] = dh
        if "augen_versatz" in vb:
            av = vb["augen_versatz"].astype(np.float64)
            vorbild_augen[vb_name] = np.stack([av[:, 0], -av[:, 2], av[:, 1]], 1)   # [rechts (−x), links (+x)]
        vb_bericht[vb_name] = {"ecken_getroffen": int((abst_ < 2e-4).sum()), "max_mm": round(float(np.abs(dh).max() * 1000), 2)}
    bericht["vorbilder"] = vb_bericht
    zeit("Vorbilder: " + json.dumps(vb_bericht, ensure_ascii=False))

alle_formen = {}                   # Name -> {objektname: versatz}
for name, d in formen_mpfb.items():
    dh, voll = formen_aus_mpfb(d, name)
    if name == "alter_reife_m":
        dh = dh + adams_buckel
        voll[:N_HAUT] += adams_buckel[:N_HAUT]
    if name in alter_eigen:
        for en, ew in alter_eigen[name].items():
            dh = dh + float(ew) * eigen_haut[en]
            voll[:N_HAUT] += float(ew) * eigen_haut[en][:N_HAUT]
        voll[lid_teile] = knn(voll[haut_und_hoehle], nb_lid)
    dhaar, dkr = haare_und_kragen(dh, voll)
    alle_formen[name] = {"Haut": dh, "Augen": voll[augen_idx], "Zaehne": voll[zahn_idx], "Haare": dhaar, "Kragen": dkr}
zeit("MPFB-Formen übertragen")
if "pruefen" in OPTIONEN:
    ob_r = oben_rand + N_HAUT
    for name in ("makro_w_alt", "makro_m_saeugling", "gen_kopf_plus"):
        d = alle_formen[name]["Haut"]
        roh = kw.uebertragen(bindung, formen_mpfb[name])
        sprung = []
        for j in hals:
            k = ob_r[np.argmin(np.linalg.norm(H_ecken[ob_r] - H_ecken[j], axis=1))]
            sprung.append(np.linalg.norm(d[j] - d[k]))
        aend = np.linalg.norm(d[hals] - roh[hals], axis=1)
        # Ecken 2–6 cm über der Schleife
        print("PRUEFEN", name, "Sprung Halsschleife->Büste mm: max", round(max(sprung) * 1000, 2), "mittel",
              round(float(np.mean(sprung)) * 1000, 2), "| Glätten ändert die Schleife um mm max", round(float(aend.max()) * 1000, 2))
bericht["lidrand_angleich_mm"] = {k: round(max(v), 2) for k, v in sorted(lid_abweichung.items(), key=lambda x: -max(x[1]))[:8]}
for name, d in ict["formen"].items():
    dh, voll = formen_aus_ict(d)
    dhaar, dkr = haare_und_kragen(dh, voll, mimik=True)
    alle_formen[name] = {"Haut": dh, "Augen": voll[augen_idx], "Zaehne": voll[zahn_idx], "Haare": dhaar, "Kragen": dkr}
# Phase 6 (S4): eigene Formen – Augen und Zähne fest, Lidteile, Brauen, Wimpern und Kragen folgen der Haut
for name, dh in eigen_haut.items():
    voll = np.zeros_like(S)
    voll[:N_HAUT] = dh[:N_HAUT]
    if name in vorbild_augen:
        for t_ in ("auge_l", "auge_r"):
            ix_ = teil(t_)
            voll[ix_] = vorbild_augen[name][0 if S[ix_, 0].mean() < 0 else 1]
    voll[lid_teile] = knn(voll[haut_und_hoehle], nb_lid)
    dhaar, dkr = haare_und_kragen(dh, voll)
    alle_formen[name] = {"Haut": dh, "Augen": voll[augen_idx], "Zaehne": voll[zahn_idx], "Haare": dhaar, "Kragen": dkr}
# Phase 6 (S6): ICT-Identitätskomponenten (ict_ident=0-8,10-15,9,20 …) wie die Mimik übertragen, am Hals zur Büstennaht
# ausgeblendet (1 − w_hals); Rohformen für die Gesichtszüge und die Asymmetrie, sie kommen nicht in den fertigen Kopf
if OPTIONEN.get("ict_ident"):
    nummern = []
    for teil_ in OPTIONEN["ict_ident"].split(","):
        a_, _, b_ = teil_.partition("-")
        nummern += list(range(int(a_), int(b_ or a_) + 1))
    for name, d in kq.ict_identitaeten(nummern).items():
        dh, voll = formen_aus_ict(d)
        aus_ = (1.0 - w_hals)[:, None]
        dh[:N_HAUT] *= aus_
        voll[:N_HAUT] *= aus_
        dhaar, dkr = haare_und_kragen(dh, voll)
        alle_formen[name] = {"Haut": dh, "Augen": voll[augen_idx], "Zaehne": voll[zahn_idx], "Haare": dhaar, "Kragen": dkr}
zeit(f"Formen gesamt: {len(alle_formen)}")

# Seitenprobe: „…Left“ bewegt Ecken mit +x (linke Seite der Figur)
seiten = {}
for name, d in ict["formen"].items():
    if (name.endswith("Left") or name.endswith("Right")) and not name.startswith(("jaw", "mouthLeft", "mouthRight")):
        m_ = np.linalg.norm(d, axis=1)
        seiten[name] = round(float((ict["ecken"][:, 0] * m_).sum() / m_.sum() * 100), 2)
bericht["seiten_x_cm"] = {k: v for k, v in seiten.items() if k.startswith(("eyeBlink", "mouthSmile", "browOuter"))}
for paar in ("jawLeft", "jawRight", "mouthLeft", "mouthRight"):
    d = ict["formen"][paar]
    m_ = np.linalg.norm(d, axis=1) > 1e-4
    bericht["seiten_x_cm"][paar + "_richtung_x"] = round(float(d[m_, 0].mean() * 100), 3)
assert all((v > 0) == k.endswith("Left") for k, v in seiten.items()), "Seitenzuordnung der ICT-Formen stimmt nicht"

# ================================================================= 9. Eckfarben: Hautmasken, Mundtiefe, Iris
lm = ict["merkpunkte68"]
al, ar = augen["l"][0], augen["r"][0]
aa = float(np.linalg.norm(al - ar))
mund_p = S[lippennaht].mean(0)
mund_p[1] = S[lippennaht, 1].min()
haut_p = S[:T["mundhoehle"][0]]
nahe = np.where((np.abs(haut_p[:, 0]) < 0.12 * aa) & (np.abs(haut_p[:, 2] - al[2] - 0.75 * aa) < 0.08 * aa))[0]
punkte = {"auge_l": al, "auge_r": ar, "nase": S[lm[30]], "mund": mund_p, "stirn": haut_p[nahe[np.argmin(haut_p[nahe, 1])]],
          "kinn": S[lm[8]]}
ohr = np.zeros(len(H_ecken))
band = np.where((haut_p[:, 2] < al[2] + 0.3 * aa) & (haut_p[:, 2] > mund_p[2]))[0]
for sx in (1.0, -1.0):
    rand_ = band[np.argmax(sx * haut_p[band, 0])]
    ohr = np.maximum(ohr, np.exp(-((H_ecken - S[rand_]) ** 2).sum(1) / (2 * (0.45 * aa) ** 2)))
lippe = np.zeros(len(H_ecken))
d_l = np.array([kd_lippe.find(p)[2] for p in H_ecken[:T["mundhoehle"][0]]])
lippe[:T["mundhoehle"][0]] = (1 - kw.glatt_stufe(d_l, 0.005, 0.011)) * (np.abs(H_ecken[:T["mundhoehle"][0], 0]) < 0.6 * aa)
kg.masken_setzen(haut, punkte, aa, ohren=ohr.astype(np.float32), lippen=lippe.astype(np.float32))
attr = me.color_attributes["Hautmaske"]
farbe = np.empty(len(H_ecken) * 4, dtype=np.float32)
attr.data.foreach_get("color", farbe)
farbe = farbe.reshape(-1, 4)
farbe[mh, 2] = np.clip(tiefe_mund / 0.03, 0.0, 1.0)            # Mundhöhle: B = Tiefe ab der Lippe (0..3 cm)
farbe[mh, 0] = 0.0
farbe[mh, 1] = 0.0
attr.data.foreach_set("color", farbe.ravel())

# Zähne und Zahnfleisch: R = Tiefe hinter den Lippen (für die dunkle Mundhöhle)
zahn_me = zahn_obj.data
za = zahn_me.color_attributes.new("Tiefe", "BYTE_COLOR", "POINT")
zahn_me.color_attributes.active_color = za
vorn_y = S[zahn_idx, 1].min()
tz = np.array([kd_lippe.find(S[i])[2] for i in zahn_idx])
zfarbe = np.zeros((len(zahn_idx), 4), dtype=np.float32)
zfarbe[:, 0] = np.clip(tz / 0.03, 0.0, 1.0)
zfarbe[:, 3] = 1.0
za.data.foreach_set("color", zfarbe.ravel())

# Augen: Irisdaten als Eckfarbe (wie Phase 0): innere Kugel R = Abstand von der Blickachse / (2·Irishalbmesser),
# G/B = Winkel um die Achse; äußere Kugel R = 1 im Bereich der Hornhaut
R_IRIS = 0.00585 * pa["s"]
aug_me = augen_obj.data
af = aug_me.color_attributes.new("Augendaten", "BYTE_COLOR", "POINT")
aug_me.color_attributes.active_color = af
afarbe = np.zeros((len(augen_idx), 4), dtype=np.float32)
afarbe[:, 3] = 1.0
pos_in_aug = {int(i): k for k, i in enumerate(augen_idx)}
augen_uv_neu = {}
for s in ("l", "r"):
    lo_s, hi_s = kq.ICT_LEDERHAUT[s]
    hi_i = kq.ICT_IRIS[s][1]
    mitte_ = S[lo_s:hi_s].mean(axis=0)
    iris = S[hi_s:hi_i]
    achse = iris.mean(axis=0) - mitte_
    achse /= np.linalg.norm(achse)

    def radial(p, iris=iris, achse=achse):
        d = p - iris.mean(axis=0)
        return np.linalg.norm(d - np.outer(d @ achse, achse), axis=1)
    vorn_i = ((iris - mitte_) @ achse) > 0
    # Phase 3: Die ICT-Iris hat die Pupille als Öffnung (Tunnel nach hinten), nicht ganz auf der Achse. Mitte und Größe
    # der Öffnung per Strahl von vorn: Wo der erste Treffer eine hintere Fläche ist, liegt die Öffnung.
    from mathutils.bvhtree import BVHTree as _Bvh
    from mathutils import Vector as _V
    fl_iris = [f for f in fl_alle if hi_s <= f[0] < hi_i]
    bvh_i = _Bvh.FromPolygons([_V(p_) for p_ in S[hi_s:hi_i]], [[i_ - hi_s for i_ in f] for f in fl_iris])
    vorn_f = [all(vorn_i[i_ - hi_s] for i_ in f) for f in fl_iris]
    ea = np.cross(achse, [0.0, 0.0, 1.0])
    ea /= np.linalg.norm(ea)
    eb = np.cross(achse, ea)
    zelle_ = R_IRIS / 40.0
    loch = []
    for gx in np.arange(-R_IRIS, R_IRIS, zelle_):
        for gy in np.arange(-R_IRIS, R_IRIS, zelle_):
            if gx * gx + gy * gy > R_IRIS * R_IRIS:
                continue
            q_ = iris.mean(axis=0) + ea * gx + eb * gy + achse * 0.03
            treffer = bvh_i.ray_cast(_V(q_), _V(-achse), 0.06)
            if treffer[0] is not None and not vorn_f[treffer[2]]:
                loch.append(iris.mean(axis=0) + ea * gx + eb * gy)
    loch_mitte = np.mean(loch, axis=0) if loch else iris.mean(axis=0)
    loch_r = math.sqrt(len(loch) * zelle_ * zelle_ / math.pi)
    bericht.setdefault("pupillenoeffnung", {})[s] = {"versatz_mm": round(float(np.linalg.norm(loch_mitte - iris.mean(axis=0))) * 1000, 2),
                                                     "halbmesser_limbus": round(loch_r / R_IRIS, 3)}
    beiwerk_pupille = round(loch_r / R_IRIS, 3)

    def radial(p, achse=achse, mitte=loch_mitte):
        d = p - mitte
        return np.linalg.norm(d - np.outer(d @ achse, achse), axis=1)
    k_iris = np.array([pos_in_aug[i] for i in range(hi_s, hi_i)])
    afarbe[k_iris, 0] = np.where(vorn_i, np.clip(radial(iris) / (2.0 * R_IRIS), 0.0, 1.0), 1.0)
    e1 = np.cross(achse, [0.0, 0.0, 1.0])
    e1 /= np.linalg.norm(e1)
    e2 = np.cross(achse, e1)
    d = iris - iris.mean(axis=0)
    w = np.arctan2(d @ e2, d @ e1)
    afarbe[k_iris, 1] = 0.5 + 0.5 * np.cos(w)
    afarbe[k_iris, 2] = 0.5 + 0.5 * np.sin(w)
    sk = S[lo_s:hi_s]
    vorn = ((sk - mitte_) @ achse) > 0
    k_sk = np.array([pos_in_aug[i] for i in range(lo_s, hi_s)])
    afarbe[k_sk, 0] = np.where(vorn & (radial(sk) < R_IRIS * 1.08), 1.0, 0.0)
    # Phase 3: ebene Abbildung entlang der Blickachse als UV (Iris: Limbus = Rand des Bildes, Lederhaut: Augapfel =
    # Rand des Bildes); oben im Bild = oben am Auge. Das rechte Auge ist um 140° gedreht, damit die Iris nicht
    # spiegelgleich aussieht.
    _, r_auge = kw.kugel(sk)
    e1v = np.cross(achse, [0.0, 0.0, 1.0])
    e1v /= np.linalg.norm(e1v)
    e2v = np.cross(e1v, achse)
    e2v *= np.sign(e2v[2])
    dreh = 0.0 if s == "l" else math.radians(140)
    # Mitte der ebenen Abbildung: Mitte der Pupillenöffnung (vordere Iris-Ecken nahe der Achse); die ICT-Pupille sitzt
    # nicht genau auf der Achse Lederhaut -> Iris, sonst liegen gemalte Pupille und Öffnung versetzt
    mitte_pupille = loch_mitte
    for bereich, mass in (((lo_s, hi_s), r_auge), ((hi_s, hi_i), R_IRIS)):
        d = S[bereich[0]:bereich[1]] - mitte_pupille
        x_, y_ = d @ e1v, d @ e2v
        x_, y_ = x_ * math.cos(dreh) - y_ * math.sin(dreh), x_ * math.sin(dreh) + y_ * math.cos(dreh)
        for k_, i_ in enumerate(range(*bereich)):
            augen_uv_neu[pos_in_aug[i_]] = (0.5 + 0.5 * x_[k_] / mass, 0.5 + 0.5 * y_[k_] / mass)
    bericht.setdefault("auge_mm", {})[s] = {"augapfel_halbmesser": round(r_auge * 1000, 2), "iris_halbmesser": round(R_IRIS * 1000, 2)}
# Lidschatten-Netz (ICT „EyeOcclusion“): R = Dunkelheit nach der Nähe zur Lidnaht, oben kräftiger
for s in ("l", "r"):
    idx_ = teil("lidschatten_" + s)
    kd = KDTree(len(lidnaht[s]))
    for i, j in enumerate(lidnaht[s]):
        kd.insert(S[j], i)
    kd.balance()
    d_ = np.array([kd.find(S[i])[2] for i in idx_])
    oben_ = S[idx_, 2] > augen[s][0][2]
    a_ = (1.0 - kw.glatt_stufe(d_, 0.0004, 0.005)) * np.where(oben_, 1.0, 0.7)
    afarbe[[pos_in_aug[int(i)] for i in idx_], 0] = a_
af.data.foreach_set("color", afarbe.ravel())
uvl_aug = aug_me.uv_layers.active
uv_alt = np.empty(len(aug_me.loops) * 2)
uvl_aug.data.foreach_get("uv", uv_alt)
uv_alt = uv_alt.reshape(-1, 2)
for li, lp in enumerate(aug_me.loops):
    if lp.vertex_index in augen_uv_neu:
        uv_alt[li] = augen_uv_neu[lp.vertex_index]
uvl_aug.data.foreach_set("uv", uv_alt.ravel())
# Hornhaut: Flächen der äußeren Kugel, deren Ecken alle im Hornhautbereich liegen (R = 1), bekommen den Stoff 4
mi_aug = np.empty(len(aug_me.polygons), dtype=np.int64)
aug_me.polygons.foreach_get("material_index", mi_aug)
for k_, p_ in enumerate(aug_me.polygons):
    if mi_aug[k_] == 0 and all(afarbe[v_, 0] > 0.99 for v_ in p_.vertices):
        mi_aug[k_] = 4
aug_me.polygons.foreach_set("material_index", mi_aug)
bericht["hornhaut_flaechen"] = int((mi_aug == 4).sum())
zeit("Eckfarben gesetzt")

# ================================================================= 9b. Daten für das Backen der Haut (Phase 3)
if BACKDATEN:
    # Hautnetz als Dreiecke mit Spiel-UV je Ecke, Stoff je Dreieck
    uv_spiel = np.empty(len(me.loops) * 2)
    me.uv_layers["UVMap"].data.foreach_get("uv", uv_spiel)
    uv_spiel = uv_spiel.reshape(-1, 2)
    h_tri, h_tri_uv, h_tri_stoff = [], [], []
    for p_ in me.polygons:
        vs, ls = list(p_.vertices), list(p_.loop_indices)
        for k in range(1, len(vs) - 1):
            h_tri.append((vs[0], vs[k], vs[k + 1]))
            h_tri_uv.append((uv_spiel[ls[0]], uv_spiel[ls[k]], uv_spiel[ls[k + 1]]))
            h_tri_stoff.append(stoff_idx[p_.index])
    # MakeHuman-UV je Dreieck der MPFB-Haut (gleiche Fächerteilung wie kw.dreiecke(mp.koerper_flaechen))
    mh_uv_lage = mp.roh.data.uv_layers.active
    koerper = set(mp.gruppen["body"].tolist())
    mh_tri_uv = []
    for p_ in mp.roh.data.polygons:
        vs = list(p_.vertices)
        if not all(i in koerper for i in vs):
            continue
        ls = list(p_.loop_indices)
        for k in range(1, len(vs) - 1):
            mh_tri_uv.append([tuple(mh_uv_lage.data[ls[j]].uv) for j in (0, k, k + 1)])
    mh_tri = kw.dreiecke(mp.koerper_flaechen)
    assert len(mh_tri) == len(mh_tri_uv), "MakeHuman-UV passt nicht zu den Dreiecken"
    maske_h = np.empty(len(H_ecken) * 4, dtype=np.float32)
    me.color_attributes["Hautmaske"].data.foreach_get("color", maske_h)
    np.savez_compressed(
        BACKDATEN, h_ecken=H_ecken, h_tri=np.array(h_tri), h_tri_uv=np.array(h_tri_uv), h_tri_stoff=np.array(h_tri_stoff),
        herkunft=herkunft_pos, mpfb=P, mh_tri=mh_tri, mh_tri_uv=np.array(mh_tri_uv), maske=maske_h.reshape(-1, 4),
        n_haut=N_HAUT, mundhoehle=np.array(T["mundhoehle"]), augenhoehlen=np.array([T["augenhoehle_l"], T["augenhoehle_r"]]),
        merk68=merk68, auge_l=np.append(augen["l"][0], augen["l"][1]), auge_r=np.append(augen["r"][0], augen["r"][1]),
        lidnaht_l=S[lidnaht["l"]], lidnaht_r=S[lidnaht["r"]], lippennaht=S[lippennaht], halsschleife=S[hals],
        hals_achse=hals_achse, kragen_mitte=kragen_mitte, brauen_ecken=b_e, brauen_flaechen=np.array(b_fl),
        brauen_uv=np.array(b_uv), brauen_lage=b_lage)
    zeit("Backdaten " + BACKDATEN)

# ================================================================= 10. Formschlüssel und Export
objekte = {"Haut": haut, "Augen": augen_obj, "Zaehne": zahn_obj, "Haare": haare_obj, "Kragen": kragen_obj}
formen_je_objekt = {}
for oname, ob in objekte.items():
    ob.shape_key_add(name="Basis", from_mix=False)
    basis_ = kg.ecken_als_feld(ob).astype(np.float64)
    liste = []
    for name in alle_formen:
        d = alle_formen[name][oname]
        if np.abs(d).max() < 1.5e-5:
            continue
        d = np.where(np.linalg.norm(d, axis=1, keepdims=True) < 1.5e-5, 0.0, d)   # unter 0,015 mm: 0 (Datei kleiner)
        kg.schluessel_setzen(ob, name, basis_ + d)
        liste.append(name)
    formen_je_objekt[oname] = liste
zeit("Formschlüssel gesetzt")

# Maße für die Kamera (Godot-Achsen: x, z, -y) und ihre Versätze je Form
def godot(p):
    return [round(float(p[0]), 5), round(float(p[2]), 5), round(float(-p[1]), 5)]


merk_idx = {"augen": np.concatenate([teil("auge_l"), teil("auge_r")]), "mund": lippennaht,
            "kinn": np.array([lm[8]]), "scheitel": np.array([int(np.argmax(S[:T["mundhoehle"][0], 2]))])}
merkpunkte = {k: godot(S[v].mean(0)) for k, v in merk_idx.items()}
merk_formen = {}
for name, d in alle_formen.items():
    if not (name.startswith(("makro_", "gen_", "alter_", "probe_", "eigen_", "ictid_", "herkunft_", "vorbild_"))):
        continue
    voll = np.zeros_like(S)
    voll[:N_HAUT] = d["Haut"][:N_HAUT]
    voll[augen_idx] = d["Augen"]
    merk_formen[name] = {k: godot(voll[v].mean(0)) for k, v in merk_idx.items()}

if "vorschau" in OPTIONEN:
    import kopf2_vorschau as kv
    farben = {"Haut": (0.8, 0.62, 0.52, 1), "Mund": (0.35, 0.08, 0.08, 1), "Kleidung": (0.35, 0.42, 0.6, 1),
              "Augen_Lederhaut": (0.92, 0.9, 0.88, 1), "Augen_Iris": (0.25, 0.4, 0.6, 1), "Traene": (0.9, 0.9, 1, 1),
              "Lidschatten": (0.5, 0.4, 0.4, 1), "Zaehne": (0.95, 0.93, 0.85, 1), "Zahnfleisch": (0.7, 0.3, 0.3, 1),
              "Brauen": (0.15, 0.1, 0.05, 1), "Wimpern": (0.05, 0.03, 0.02, 1), "Kragen": (0.55, 0.5, 0.4, 1)}
    for m in bpy.data.materials:
        if m.name in farben:
            m.diffuse_color = farben[m.name]
    sichtbar = [haut, augen_obj, zahn_obj, haare_obj, kragen_obj]
    lidschatten_aus = st_lid.diffuse_color
    kv.vorschau(sichtbar, OPTIONEN["vorschau"], "kopf",
                formen=[{}, {"jawOpen": 1.0}, {"mouthSmileLeft": 1.0, "mouthSmileRight": 1.0}, {"eyeBlinkLeft": 1.0, "eyeBlinkRight": 1.0},
                        {"makro_m_saeugling": 0.6, "makro_m_kind": 0.4}, {"makro_w_alt": 0.7, "makro_w_jung": 0.3}],
                ansichten=((0, 0.85, -0.03), (35, 0.85, -0.03), (90, 0.85, -0.03)), ziel=tuple(augen["l"][0] * [0, 1, 1]))
    kv.vorschau(sichtbar, OPTIONEN["vorschau"], "nah",
                formen=[{}, {"jawOpen": 0.7}, {"mouthFunnel": 1.0}, {"eyeBlinkLeft": 1.0}, {"gen_augen_abstand_plus": 1.0},
                        {"gen_augen_groesse_plus": 1.0}, {"makro_w_saeugling": 0.6, "makro_w_kind": 0.4, "jawOpen": 0.7}],
                ansichten=((15, 0.32, -0.035),), ziel=tuple(augen["l"][0] * [0, 1, 1]), groesse=(500, 400))
    kragen_obj.hide_render = False
    kv.vorschau([haut, augen_obj, zahn_obj, haare_obj], OPTIONEN["vorschau"], "hals_ohne_kragen",
                ansichten=((20, 0.55, -0.12), (160, 0.55, -0.12), (270, 0.55, -0.12)), ziel=tuple(augen["l"][0] * [0, 1, 1]))
    kv.vorschau(sichtbar, OPTIONEN["vorschau"], "hals", formen=[{}, {"makro_w_alt": 1.0, "makro_w_alt_gewicht_plus": 1.0}],
                ansichten=((0, 0.6, -0.12), (180, 0.6, -0.12), (300, 0.6, -0.12)), ziel=tuple(augen["l"][0] * [0, 1, 1]))
    zeit("Vorschau")

objektliste = list(objekte.values())
for ob in objektliste:
    for m in list(ob.modifiers):
        ob.modifiers.remove(m)
haut.data.color_attributes.active_color = haut.data.color_attributes["Hautmaske"]
# In Dreiecke teilen (der Schnitt der Büste hinterlässt Vielecke; ohne Dreiecke rechnet der Export keine Tangenten,
# die der Hautshader für die Poren braucht). Formschlüssel, UV und Eckfarben bleiben.
for ob in objektliste:
    n_formen = len(ob.data.shape_keys.key_blocks)
    bm = bmesh.new()
    bm.from_mesh(ob.data)
    bmesh.ops.triangulate(bm, faces=bm.faces[:], quad_method="BEAUTY", ngon_method="BEAUTY")
    bm.to_mesh(ob.data)
    bm.free()
    assert len(ob.data.shape_keys.key_blocks) == n_formen
kg.gltf_export(objektliste, AUSGABE, morph_normalen=MORPH_NORMALEN, eckfarben=True, tangenten=True)
zeit("Export")

dreiecke = {n: sum(len(p_.vertices) - 2 for p_ in o.data.polygons) for n, o in objekte.items()}
bericht.update({
    "ecken": {n: len(o.data.vertices) for n, o in objekte.items()},
    "dreiecke": dreiecke, "dreiecke_gesamt": sum(dreiecke.values()),
    "formen_je_objekt": {n: len(v) for n, v in formen_je_objekt.items()},
    "glb_mb": round(os.path.getsize(AUSGABE) / 1e6, 2), "sekunden": round(time.time() - t0, 1),
})
seitenlage = {
    "regel": "„…Left“ = linke Seite der Figur (aus ihrer Sicht); im Bild von vorn erscheint sie rechts. "
             "Blender +x, Godot +x, ICT „_L“, MakeHuman „l-“ meinen dieselbe Seite.",
    "probe_x_cm": bericht["seiten_x_cm"],
}
beiwerk = {
    "version": 1,
    "beschreibung": "Basiskopf Phase 2 (Charakter-Editor): ICT-Gesicht (MIT) auf MakeHuman-Büste (CC0). "
                    "Formen: makro_* (Alter × Geschlecht, Kraft und Gewicht je Alter), gen_* (Gestalt-Gene, minus/plus), ARKit-Mimik.",
    "geschlecht": [g for g, _ in GESCHLECHT], "alter_stuetzen": [{"name": a, "makro": w} for a, w in ALTER],
    "basis": "makro_m_jung (Mann, 25 Jahre, Kraft und Gewicht mittel) ist das Grundnetz",
    "mimikmass": mimikmass,
    "gene": {k: {"art": v["art"], "formen": gen_formen.get(k, {}), "wirkung": v.get("wirkung", "")} for k, v in regler.items()},
    "alter_formen": {k: {kk: vv for kk, vv in v.items() if kk != "ziele"} for k, v in alter_formen.items() if not k.startswith("_")},
    "arkit": sorted(ict["formen"]),
    "merkpunkte": merkpunkte, "merkpunkt_formen": merk_formen,
    "pupillenoeffnung": beiwerk_pupille,
    "seiten": seitenlage,
    "formen_je_objekt": formen_je_objekt,
    "bericht": bericht,
}
json.dump(beiwerk, open(AUSGABE[:-4] + ".json", "w", encoding="utf-8"), indent=1, ensure_ascii=False)
print("FERTIG", json.dumps(bericht, ensure_ascii=False))
