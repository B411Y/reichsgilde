"""Charakter Phase 4: Frisuren und Bärte bauen – Leitsträhnen (für TressFX) und Haarkarten (Rückfall) aus derselben
Quelle, dazu Kartenbild und Kopfhautkarten (eigener Code, MIT).

Aufruf:  tools/charakter/blender.sh -b --factory-startup -P tools/charakter/kopf4_haar.py -- <glb-ordner> \
           [backdaten=<npz>] [kopf=kopf4_mn.glb] [nur=mann_kurz,vollbart] [faktor=1.0] [vorschau=<ordner>]
         [nur_karten=1]   Karten (und Kopfhautkarten) aus den gespeicherten Strähnen neu bauen, ohne neu zu kämmen
         [nur_kopfhaut=1] nur die Kopfhautkarten neu (aus den gespeicherten Wurzeln)
         (oder tools/charakter/kopf4.sh)

Ergebnis in <glb-ordner>/haar4/ (nicht im Repository):
  stuetzen.json/.bin   Stützpunkte: Formnamen und Versätze je Form (Godot-Achsen), float32
  <stil>.json/.bin     je Stil und Teil: Strähnen (Grundlage = Mann, 25 Jahre), ihre Bindung an die Stützpunkte,
                       Zufall und Rang je Strähne; Karten (Ecken, Normalen, Tangenten, UV, UV2, Farbe, Bindung, Indizes)
  haar_karten.png      Kartenbild (siehe kopf4_karten.py)
  kopfhaut.png, kopfhaut_w.png, bart.png   Kopfhautkarten in der Spiel-UV
Ablauf und Gründe: docs/charakter/phase4.md.
"""
import json
import os
import sys
import time

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import kopf4_daten as kd  # noqa: E402
import kopf4_frisuren as kf  # noqa: E402
import kopf4_karten as kk  # noqa: E402

t0 = time.time()
arg = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
ORDNER = os.path.abspath(os.path.expanduser(arg[0] if arg else "~/werkzeuge/charakter/glb4"))
OPT = dict(a.split("=", 1) for a in arg[1:])
BACKDATEN = os.path.expanduser(OPT.get("backdaten", "~/werkzeuge/charakter/arbeit/phase4/backdaten.npz"))
GLB = os.path.join(ORDNER, OPT.get("kopf", "kopf4_mn.glb"))
NUR = set(OPT["nur"].split(",")) if "nur" in OPT else None
FAKTOR = float(OPT.get("faktor", "1.0"))
AUS = os.path.join(ORDNER, "haar4")
os.makedirs(AUS, exist_ok=True)
rng = np.random.default_rng(1480)


def zeit(text):
    print(f"[{time.time() - t0:6.1f} s] {text}", flush=True)


def bloecke_schreiben(pfad_ohne, kopf_json, felder):
    """felder: {name: numpy-Feld} -> <pfad>.bin (aneinander, float32/int32) und <pfad>.json (Kopf mit Lage je Block)."""
    lage = {}
    pos = 0
    with open(pfad_ohne + ".bin", "wb") as f:
        for name, feld in felder.items():
            feld = np.ascontiguousarray(feld)
            if feld.dtype.kind == "f":
                feld = feld.astype("<f4")
                art = "f32"
            else:
                feld = feld.astype("<i4")
                art = "i32"
            roh = feld.tobytes()
            f.write(roh)
            lage[name] = {"start": pos, "bytes": len(roh), "art": art, "form": list(feld.shape)}
            pos += len(roh)
    kopf_json["bloecke"] = lage
    with open(pfad_ohne + ".json", "w", encoding="utf-8") as f:
        json.dump(kopf_json, f, ensure_ascii=False, indent=1)


# ================================================================= 1. Kopf und Stützpunkte
kopf = kd.Kopf(BACKDATEN, GLB)
zeit(f"Kopf: {len(kopf.p0)} Ecken, {len(kopf.namen)} Formen, Zuordnung .glb max {kopf.zuordnung_fehler_mm:.4f} mm, "
     f"ohne Form {kopf.ohne_form}")
stuetz = kd.stuetzpunkte(kopf)
# nur Formen, die die Stützpunkte merklich bewegen
namen = [n for n in kopf.namen if np.abs(kopf.formen[n][stuetz]).max() > 5e-5]
versatz = np.stack([kd.nach_godot(kopf.formen[n][stuetz]) for n in namen]).astype(np.float32)
bloecke_schreiben(os.path.join(AUS, "stuetzen"),
                  {"anzahl": int(len(stuetz)), "formen": namen, "hautecken": [int(i) for i in stuetz[:0]],
                   "lage": "Grundlage Mann, 25 Jahre; Godot-Achsen (x, y oben, z vorn)"},
                  {"lage": kd.nach_godot(kopf.p0[stuetz]), "versatz": versatz})
zeit(f"Stützpunkte: {len(stuetz)}, Formen mit Wirkung {len(namen)}")

HUELLEN = {}
GEWICHTE = {"m": {}, "w": {"makro_w_jung": 1.0}}


def huelle(g):
    if g not in HUELLEN:
        lage = kopf.lage(GEWICHTE[g])
        h = kd.Huelle(kopf, lage)
        h.bindung = kd.Bindung(lage[stuetz])
        h.stuetz_versatz = kopf.versatz(GEWICHTE[g])[stuetz]
        HUELLEN[g] = h
        zeit(f"Hülle {g}: Scheitel z {h.scheitel[2]:.3f}, Augen z {h.auge_z:.3f}, Maß {kopf.mass:.3f}")
    return HUELLEN[g]


def zur_grundlage(h, punkte):
    """Punkte (n,3) der Hülle -> Bindung und Lage in der Grundlage (Godot-Achsen)."""
    idx, gew = h.bindung(punkte)
    zur = punkte - (h.stuetz_versatz[idx] * gew[..., None]).sum(1)
    return kd.nach_godot(zur), idx, gew


def aus_bin(h, pfad_ohne):
    """Gespeicherte Strähnen eines Teils lesen und in die Lage der Hülle zurückrechnen (für nur_karten)."""
    kopf_j = json.load(open(pfad_ohne + ".json", encoding="utf-8"))
    roh = open(pfad_ohne + ".bin", "rb").read()
    felder = {}
    for name, b in kopf_j["bloecke"].items():
        art = "<f4" if b["art"] == "f32" else "<i4"
        felder[name] = np.frombuffer(roh[b["start"]:b["start"] + b["bytes"]], art).reshape(b["form"])
    S, P = kopf_j["straehnen"], kopf_j["punkte"]
    pos = kd.von_godot(felder["straehnen_pos"].reshape(-1, 3).astype(np.float64))
    idx = felder["straehnen_idx"].reshape(-1, 4)
    gew = felder["straehnen_gew"].reshape(-1, 4).astype(np.float64)
    st = (pos + (h.stuetz_versatz[idx] * gew[..., None]).sum(1)).reshape(S, P, 3)
    return {k: v for k, v in felder.items() if k.startswith("straehnen")}, st


def karten_neu(h, felder, st, karten_art):
    rg = felder["straehnen_info"][:, 1]
    E, N, T, UV, UV2, F, I, MS = kk.karten(st, st[:, 0], h, rng, **karten_art)
    for m, o, n in MS:
        F[o:o + n, 0] = rg[m]
    kpos, kidx, kgew = zur_grundlage(h, E)
    felder = dict(felder)
    felder.update({"karten_pos": kpos, "karten_nrm": kd.nach_godot(N), "karten_tan": np.concatenate([kd.nach_godot(T[:, :3]), T[:, 3:]], 1),
                   "karten_uv": UV, "karten_uv2": UV2, "karten_farbe": F, "karten_idx": kidx.astype(np.float32),
                   "karten_gew": kgew, "karten_index": I})
    S, P, _ = st.shape
    return felder, {"straehnen": int(S), "punkte": int(P), "karten_ecken": int(len(kpos)), "karten_dreiecke": int(len(I) // 3)}


def teil_daten(h, st, wurzeln_, buschel, karten_art, rang=True):
    S, P, _ = st.shape
    pos, idx, gew = zur_grundlage(h, st.reshape(-1, 3))
    zufall = rng.random(S)
    rauschen = rng.normal(0, 0.04, S)
    rg = h.rang(wurzeln_, rauschen) if rang else np.ones(S)
    bz = rng.random(buschel.max() + 1)[buschel] if len(buschel) else np.zeros(S)
    info = np.stack([zufall, rg, bz, np.zeros(S)], 1)
    felder = {"straehnen_pos": pos.reshape(S, P, 3), "straehnen_idx": idx.reshape(S, P, 4),
              "straehnen_gew": gew.reshape(S, P, 4), "straehnen_info": info}
    if karten_art is not None:
        E, N, T, UV, UV2, F, I, MS = kk.karten(st, wurzeln_, h, rng, **karten_art)
        for m, o, n in MS:
            F[o:o + n, 0] = rg[m]
        kpos, kidx, kgew = zur_grundlage(h, E)
        felder.update({"karten_pos": kpos, "karten_nrm": kd.nach_godot(N), "karten_tan": np.concatenate([kd.nach_godot(T[:, :3]), T[:, 3:]], 1),
                       "karten_uv": UV, "karten_uv2": UV2, "karten_farbe": F, "karten_idx": kidx.astype(np.float32),
                       "karten_gew": kgew, "karten_index": I})
    return felder, {"straehnen": int(S), "punkte": int(P), "karten_ecken": int(len(felder.get("karten_pos", []))),
                    "karten_dreiecke": int(len(felder.get("karten_index", [])) // 3)}


KARTEN_HAAR = {}
KARTEN_KURZ = {"lagen": ({"je": 10, "anteil": 1.0, "lage": 0, "spalten": (0, 1, 2, 3), "breite": (0.005, 0.012), "faktor": 1.3},
                        {"je": 1, "anteil": 0.45, "lage": 1, "spalten": (4, 5), "breite": (0.0025, 0.004), "faktor": 1.0})}
KARTEN_BART = {"bart": True, "lagen": ({"je": 8, "anteil": 1.0, "lage": 0, "spalten": (6, 7), "breite": (0.004, 0.01), "faktor": 1.2},
                                       {"je": 1, "anteil": 0.4, "lage": 1, "spalten": (6, 7), "breite": (0.002, 0.0035), "faktor": 1.0})}
KARTEN_ZOPF = {"lagen": ({"je": 6, "anteil": 1.0, "lage": 1, "spalten": (0, 2), "breite": (0.004, 0.012), "faktor": 1.0},)}
KARTEN_KNOTEN = {"lagen": ({"je": 6, "anteil": 1.0, "lage": 1, "spalten": (0, 1, 2), "breite": (0.004, 0.012), "faktor": 1.0},)}

uebersicht = {}
wurzeln_je_stil = {}
alle_stile = [s for g in ("m", "w") for s in kf.FRISUREN[g]] + kf.BAERTE
vorschau_teile = {}
for Klasse in alle_stile:
    name = Klasse.name
    if NUR and name not in NUR:
        continue
    h = huelle(Klasse.geschlecht)
    stil = Klasse(h, rng)
    stil.anzahl = int(stil.anzahl * FAKTOR)
    art = KARTEN_BART if stil.teil == "bart" else (KARTEN_KURZ if name == "mann_kurz" else KARTEN_HAAR)
    if "nur_karten" in OPT or "nur_kopfhaut" in OPT:
        teile = []
        vorschau_teile[name] = []
        for teil, endung, ka in (("haupt", "", art), ("zusatz", "_zusatz", KARTEN_ZOPF if name == "frau_zoepfe" else KARTEN_KNOTEN)):
            pf = os.path.join(AUS, name + endung)
            if not os.path.exists(pf + ".bin"):
                continue
            felder, st = aus_bin(h, pf)
            if teil == "haupt":
                wurzeln_je_stil[name] = (Klasse.geschlecht, st[:, 0].copy())
            if "nur_kopfhaut" in OPT:
                continue
            felder, zahlen = karten_neu(h, felder, st, ka)
            bloecke_schreiben(pf, {"name": name, "art": stil.teil if teil == "haupt" else "zusatz", "geschlecht": stil.geschlecht,
                                   "teil": teil, **zahlen}, felder)
            teile.append({"name": teil, **zahlen})
            vorschau_teile[name].append(st)
        if teile:
            uebersicht[name] = {"art": stil.teil, "geschlecht": stil.geschlecht, "teile": teile}
            zeit(f"{name}: Karten neu {teile}")
        continue
    st, ww, nn, buschel = kf.erzeugen(stil, rng, log=lambda t: zeit(t))
    wurzeln_je_stil[name] = (Klasse.geschlecht, ww)
    felder, zahlen = teil_daten(h, st, ww, buschel, art, rang=stil.teil != "bart")
    teile = [{"name": "haupt", **zahlen}]
    vorschau_teile[name] = [st]
    bloecke_schreiben(os.path.join(AUS, name), {"name": name, "art": stil.teil, "geschlecht": stil.geschlecht,
                                                "teil": "haupt", **zahlen}, felder)
    # Zusatzteile: Knoten bzw. Zöpfe
    if name == "frau_knoten":
        z = kf.knoten_straehnen(stil, rng, anzahl=int(700 * FAKTOR))
    elif name == "frau_zoepfe":
        z = np.concatenate([kf.zopf_straehnen(h, kf.zopf_bahn(h, stil.anker[s], s, h.k.mass), rng, h.k.mass,
                                              anzahl=int(360 * FAKTOR)) for s in ("l", "r")])
    else:
        z = None
    if z is not None:
        zw = z[:, 0]
        zb = np.arange(len(z)) // 6
        felder, zahlen = teil_daten(h, z, zw, zb, KARTEN_ZOPF if name == "frau_zoepfe" else KARTEN_KNOTEN, rang=False)
        bloecke_schreiben(os.path.join(AUS, name + "_zusatz"), {"name": name, "art": "zusatz", "geschlecht": stil.geschlecht,
                                                                "teil": "zusatz", **zahlen}, felder)
        teile.append({"name": "zusatz", **zahlen})
        vorschau_teile[name].append(z)
    uebersicht[name] = {"art": stil.teil, "geschlecht": stil.geschlecht, "teile": teile}
    zeit(f"{name}: {teile}")

# Übersicht (Reihenfolge der Frisuren je Geschlecht und der Bärte für die Gene „frisur“ und „bartform“)
alt = {}
pfad_ue = os.path.join(AUS, "haar4.json")
if os.path.exists(pfad_ue):
    alt = json.load(open(pfad_ue, encoding="utf-8")).get("stile", {})
alt.update(uebersicht)
json.dump({"stile": alt, "frisuren": {"m": [k.name for k in kf.FRISUREN["m"]], "w": [k.name for k in kf.FRISUREN["w"]]},
           "baerte": [k.name for k in kf.BAERTE], "stuetzen": int(len(stuetz)),
           "kopfhaut": {"kopfhaut.png": ["mann_kurz", "mann_pagen", "mann_zurueck", "rang"],
                        "kopfhaut_w.png": ["frau_knoten", "frau_offen", "frau_zoepfe", ""],
                        "bart.png": ["schnurrbart", "kinnbart", "vollbart", "langbart"]}},
          open(pfad_ue, "w", encoding="utf-8"), ensure_ascii=False, indent=1)

# ================================================================= 3. Kartenbild
if "ohne_bilder" not in OPT:
    if not os.path.exists(os.path.join(AUS, "haar_karten.png")) or "bilder" in OPT:
        kk.kartenbild(os.path.join(AUS, "haar_karten.png"), np.random.default_rng(7))
        zeit("Kartenbild")
    # ============================================================= 4. Kopfhautkarten (aus den Wurzeln, Rang aus der Lage)
    if not NUR or "karten" in OPT:
        G = 1024
        J, I, T_, W = kk.uv_rastern(kopf, G)
        tri = kopf.tri

        def je_bp(feld):
            return feld[tri[T_, 0]] * W[:, 0:1] + feld[tri[T_, 1]] * W[:, 1:2] + feld[tri[T_, 2]] * W[:, 2:3]

        v0 = tri[T_, 0]
        kopfteil = ~kopf.ist_bueste[v0] & ~kopf.ist_hoehle[v0]
        kanaele = {}
        for g in ("m", "w"):
            hp = huelle(g).p
            pb = je_bp(hp)
            for name, (gg, ww) in wurzeln_je_stil.items():
                if gg != g:
                    continue
                sel = np.nonzero(kopfteil)[0]
                d = np.zeros(len(pb))
                # Breite 3,5 mm: deckt auch den Scheitel und die Lücken zwischen den Karten (die Haut darunter wird
                # haarfarben), ohne den Haaransatz hart zu machen
                d[sel] = kk.dichte_karte(pb[sel], ww, 0.0035 * kopf.mass, 0.0032 * kopf.mass)
                kanaele[name] = d
        rang = np.ones(len(T_))
        hm = huelle("m")
        sel = np.nonzero(kopfteil)[0]
        rang[sel] = hm.rang(je_bp(hm.p)[sel])
        stile_m = [k.name for k in kf.FRISUREN["m"]]
        stile_w = [k.name for k in kf.FRISUREN["w"]]
        baerte = [k.name for k in kf.BAERTE]
        kk.karte_speichern(os.path.join(AUS, "kopfhaut.png"), G, J, I, [kanaele.get(n) for n in stile_m] + [rang])
        kk.karte_speichern(os.path.join(AUS, "kopfhaut_w.png"), G, J, I, [kanaele.get(n) for n in stile_w] + [None])
        kk.karte_speichern(os.path.join(AUS, "bart.png"), G, J, I, [kanaele.get(n) for n in baerte])
        zeit("Kopfhautkarten")

# ================================================================= 5. Vorschau (Workbench)
if "vorschau" in OPT:
    import bpy
    import kopf2_vorschau as kv
    for o in list(bpy.data.objects):
        bpy.data.objects.remove(o, do_unlink=True)

    def stoff(name, farbe):
        m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
        m.diffuse_color = farbe
        return m

    for name, teile in vorschau_teile.items():
        g = uebersicht[name]["geschlecht"]
        h = huelle(g)
        me = bpy.data.meshes.new("haut")
        ok = ~kopf.ist_hoehle[kopf.tri].any(1)
        me.from_pydata(h.p.tolist(), [], kopf.tri[ok].tolist())
        me.materials.append(stoff("Haut", (0.8, 0.62, 0.52, 1)))
        for poly in me.polygons:
            poly.use_smooth = True
        haut = bpy.data.objects.new("haut", me)
        bpy.context.scene.collection.objects.link(haut)
        objekte = [haut]
        for st in teile:
            cu = bpy.data.curves.new("haar", "CURVE")
            cu.dimensions = "3D"
            cu.bevel_depth = 0.00035
            cu.bevel_resolution = 0
            for s in st[::2]:
                sp = cu.splines.new("POLY")
                sp.points.add(len(s) - 1)
                for j, p in enumerate(s):
                    sp.points[j].co = (p[0], p[1], p[2], 1.0)
            cu.materials.append(stoff("Haar", (0.25, 0.15, 0.08, 1)))
            ob = bpy.data.objects.new("haar", cu)
            bpy.context.scene.collection.objects.link(ob)
            objekte.append(ob)
        ziel = (0, h.auge_y + 0.07, h.auge_z - 0.02)
        kv.vorschau(objekte, OPT["vorschau"], name, ansichten=((0, 0.75, 0.0), (60, 0.75, 0.0), (150, 0.75, 0.04)),
                    groesse=(420, 520), ziel=ziel)
        for o in objekte:
            bpy.data.objects.remove(o, do_unlink=True)
    zeit("Vorschau")
zeit("FERTIG")
