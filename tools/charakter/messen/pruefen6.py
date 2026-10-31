#!/usr/bin/env python3
"""Sammelprüfung Charakter Phase 6 (M1): Ist / Ziel / Status als pruefen6.json und pruefen6.md.

Läuft ohne Godot und ohne Blender (nur Blenders Python für numpy und OpenImageIO):

    BPY=~/werkzeuge/blender/blender-5.2.2-linux-x64/5.2/python/bin/python3.13
    $BPY -I tools/charakter/messen/pruefen6.py --mappe=<Prüfmappe> [--gewichte=<gewichte.json>] [--glb=<kopf.glb>]
         [--alt5=<Bilder der Phase 5, gesicht30>] [--a0=<Prüfmappe A0 = Stand 5>] [--aus=<Ordner>]

Ab S2/S3 (Prüfmappe A1, Stand 6): Prüfkugeln (p10b_kugeln), Formnormalen A/B (s2_ab_mann30), dünne Formdaten
(duenn_vergleich.json, s2_duenn), Farbtest über die Hilfsfunktion, Iris und Lederhaut, Wange (Chroma, a*), Licht :
Schatten und R/G unter L-V und L-S, M2 gepaart (messung.jsonl mit Stand 5 und 6).

Die Prüfmappe enthält die gerenderten Bögen (je Bogen ein Ordner mit zz_ss.png und zz_ss.json), bruecke_pruefen.json,
gewichte*.json (Gewichteausdruck) und messung.jsonl (M2). Fehlt etwas, steht die Zeile auf „offen“.
"""
import glob
import json
import math
import os
import re
import subprocess
import sys
import time

import numpy as np

HIER = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.dirname(HIER))
from messen import bevoelkerung, bild as B, bild_werte as BW  # noqa: E402
from messen.glb import Glb  # noqa: E402
from messen.kamera import Kamera  # noqa: E402
from messen.netz_masse import Netzmesser  # noqa: E402

REPO = os.path.abspath(os.path.join(HIER, "..", "..", ".."))
ZEILEN = []


def zeile(bereich, groesse, ist, ziel, status, hinweis=""):
    ZEILEN.append({"bereich": bereich, "groesse": groesse, "ist": ist, "ziel": ziel, "status": status, "hinweis": hinweis})


def f(x, n=2):
    if x is None or (isinstance(x, float) and not math.isfinite(x)):
        return "–"
    return f"{x:.{n}f}".replace(".", ",")


def im_bereich(x, a, b):
    return x is not None and math.isfinite(x) and a <= x <= b


# ------------------------------------------------------------------ 1. Farben statisch
def farben_statisch():
    d = os.path.join(REPO, "game", "versuch", "charakter")
    farb = set()
    for p in glob.glob(os.path.join(d, "*.gdshader*")):
        for m in re.finditer(r"uniform\s+vec[34]\s+(\w+)\s*:[^;]*source_color", open(p, encoding="utf-8").read()):
            farb.add(m.group(1))
    treffer = []
    for p in sorted(glob.glob(os.path.join(d, "*.gd"))):
        for nr, z in enumerate(open(p, encoding="utf-8"), 1):
            for m in re.finditer(r'set_shader_parameter\(\s*"(\w+)"\s*,\s*Vector3\(', z):
                if m.group(1) in farb and "# stand5" not in z:
                    treffer.append(f"{os.path.basename(p)}:{nr} {m.group(1)}")
    zeile("Farben", "Farb-Uniforms (source_color) mit Vector3 übergeben", f"{len(treffer)} Stellen", "0 (ab S2)",
          "Befund" if treffer else "OK", "; ".join(treffer))
    return treffer


# ------------------------------------------------------------------ 2. Netzmaße
PHASE5 = {  # Werte der Bestandsaufnahme (Plan 1.3), andere Messwege
    "wangenwoelbung": {"m30": 1.5, "w30": 4.2, "grund": 5.9},
    "kinnmitte_zy": {"m30": 0.84},
    "gesichtsindex": {"w30": 0.79, "m30": 0.905},
    "zy": {"m30": 148.0, "w30": 147.5},
    "braue_pupille": {"m30": 15.0, "w30": 20.5},
    "mundlage": {"m30": 0.27, "w30": 0.27},
    "nasofrontalwinkel": {"w30": 148.0, "m30": 128.0},
}


def netzmasse(gew_datei, glb_pfad):
    if not gew_datei or not os.path.exists(gew_datei):
        zeile("Netz", "Netzmaße", "–", "–", "offen", "kein Gewichteausdruck")
        return None
    d = json.load(open(gew_datei, encoding="utf-8"))
    nm = Netzmesser(Glb(glb_pfad or d["kopf"]))
    pers = {p["name"]: p for p in d["personen"]}
    erg = {"grund": nm.messen({})}
    for n, p in pers.items():
        if n.startswith("mittel_"):
            erg[n[7:]] = nm.messen(p["szene"])
    # Wiedergabe der Phase-5-Werte
    for k, alt in PHASE5.items():
        for wer, w5 in alt.items():
            if wer not in erg:
                continue
            ist = erg[wer][k]
            rel = (ist - w5) / w5
            zeile("Netz (Wiedergabe Phase 5)", f"{k} {wer}", f(ist), f"{f(w5)} ±3 %", "OK" if abs(rel) <= 0.03 else "abweichend",
                  "Bestandsaufnahme mit anderem Messweg; Begründung in phase6.md" if abs(rel) > 0.03 else "")
    # Mittelgesicht, alle Altersstufen
    for wer in ("m20", "m35", "m50", "m70", "w20", "w35", "w50", "w70"):
        if wer in erg:
            m = erg[wer]
            zeile("Netz (Mittelgesicht)", wer, " · ".join(f"{k} {f(m[k])}" for k in ("zy", "n_me", "gesichtsindex", "kinnmitte_zy",
                  "mund_zy", "mundlage", "braue_pupille", "wangenwoelbung", "augentiefe", "kieferknick", "kieferknick_form", "kinn_hals_winkel")),
                  "S4-Ziele", "Stand", "")
    # S4-Ziele (Mann/Frau 35)
    ziele = [("gesichtsindex", "m35", 0.85, 0.90), ("gesichtsindex", "w35", 0.84, 0.90), ("kinnmitte_zy", "m35", 0.69, 0.76),
             ("kinnmitte_zy", "w35", 0.66, 0.72), ("mund_zy", "m35", 0.84, 0.89), ("mund_zy", "w35", 0.84, 0.90),
             ("wangenwoelbung", "m35", -99, 0.0), ("wangenwoelbung", "w35", -99, 2.0), ("mundlage", "m35", 0.31, 0.35),
             ("mundlage", "w35", 0.31, 0.35), ("braue_pupille", "m35", 9, 12), ("braue_pupille", "w35", 12, 14),
             ("nasofrontalwinkel", "w35", 134, 142), ("kinn_hals_winkel", "m35", 105, 125), ("kinn_hals_winkel", "w35", 105, 125),
             ("kieferknick", "m35", 25, 999)]
    for k, wer, a, b in ziele:
        if wer in erg:
            ist = erg[wer][k]
            zeile("Netz (Ziel S4)", f"{k} {wer}", f(ist), f"{f(a) if a > -99 else '≤'} … {f(b) if b < 999 else '∞'}",
                  "erfüllt" if im_bereich(ist, a, b) else "offen (S4)")
    zeile("Netz", "Spiegelabstand RMS Mann 35", f(erg.get("m35", {}).get("spiegel_rms"), 3) + " mm", "0,5–1,0 mm (S6)", "offen (S6)")
    return erg


# ------------------------------------------------------------------ 2b. Bevölkerung (Stichprobe aus dem Gewichteausdruck)
def bevoelkerung_pruefen(gew_datei, glb_pfad):
    if not gew_datei or not os.path.exists(gew_datei):
        return
    d = json.load(open(gew_datei, encoding="utf-8"))
    nm = Netzmesser(Glb(glb_pfad or d["kopf"]))
    for g, wer in ((1.0, "m"), (0.0, "w")):
        ms = [nm.messen(p["szene"]) for p in d["personen"] if p.get("saat") and abs(p["geschlecht"] - g) < 0.5]
        if len(ms) < 3:
            continue
        k = bevoelkerung.kennzahlen(ms, wer)
        teile = []
        for name in ("se_me", "zy", "mundbreite", "n_sn"):
            e = k["masse"].get(name)
            if e and "niosh_streuung" in e:
                teile.append(f"{name} σ {f(e['streuung'], 1)} = {f(e['anteil_streuung'])}·NIOSH")
        zeile("Bevölkerung (Stichprobe)", f"{k['anzahl']} Zufallspersonen {wer}, 35 Jahre (Saat über den Kern)",
              " · ".join(teile) + f" · K p95 {f(k.get('K', {}).get('p95'))} · Nachbar Median {f(k.get('nachbar', {}).get('median'))}",
              "S5: 0,65–0,85·NIOSH je Maß; S7: Nachbar Median ≥ 0,7", "Stand", "1000er-Prüfung ab S5")


# ------------------------------------------------------------------ 3. Gewichte: Ausdruck gegen Szene
def gewichte_vergleich(gew_datei, mappe):
    zellen = sorted(glob.glob(os.path.join(mappe, "m1_gewichte20", "*.json")))
    if not gew_datei or not os.path.exists(gew_datei) or not zellen:
        zeile("Gewichte", "Gewichteausdruck = Szene", "–", "< 1e-5, 20 Personen", "offen")
        return
    aus = {p.get("saat"): p for p in json.load(open(gew_datei, encoding="utf-8"))["personen"] if p.get("saat")}
    gr, n = 0.0, 0
    for z in zellen:
        d = json.load(open(z, encoding="utf-8"))
        s = d["zelle"].get("saat")
        if s not in aus:
            continue
        sz, ga = d["kopf"]["szene"], aus[s]["szene"]
        gr = max(gr, max(abs(sz[k] - ga.get(k, 0.0)) for k in sz))
        n += 1
    zeile("Gewichte", "Gewichteausdruck (headless) = Formgewichte in der Szene (Render)", f"{n} Personen, max. {gr:.1e}",
          "< 1e-5, 20 Personen", "OK" if n >= 20 and gr < 1e-5 else "ABWEICHUNG")


# ------------------------------------------------------------------ 4. Bildwerte
def pupillen(zelle_json, nm):
    d = json.load(open(zelle_json, encoding="utf-8"))
    k = Kamera(d)
    P = nm.netz.punkte(d["kopf"]["szene"])
    p, _ = k.projizieren(np.array([P["ps_r"], P["ps_l"]]))
    a, b = tuple(p[0]), tuple(p[1])
    return (a, b) if a[0] < b[0] else (b, a)


def bildwerte(mappe, nm, alt5):
    erg = {}
    # Wiedergabe Phase 5: gesicht30 (altes Licht, Bogen boegen5/gesicht30.json) neu gerendert und das Bild der Phase 5
    for quelle, ordner in (("neu (Stand 5)", os.path.join(mappe, "gesicht30")), ("Phase 5", alt5)):
        if not ordner or not os.path.exists(os.path.join(ordner, "00_00.png")):
            continue
        for zz, wer in (("00_00", "m30"), ("01_00", "w30")):
            img = B.lesen(os.path.join(ordner, zz + ".png"))
            j = os.path.join(mappe, "gesicht30", zz + ".json")
            if os.path.exists(j):
                pl, pr = pupillen(j, nm)
            else:
                continue
            w = BW.werte_vorn(img, pl, pr)
            z = w["zonen"]["wange_licht"]
            erg[(quelle, wer)] = w
            ok = all(abs(a - b) <= 0.03 * abs(b) for a, b in ((z["L"], 83.0), (z["C"], 11.0)))
            zeile("Bild (Wiedergabe Phase 5)", f"Wange L*/a*/b*/C {wer}, {quelle}", f"{f(z['L'],1)}/{f(z['a'],1)}/{f(z['b'],1)}/{f(z['C'],1)}",
                  "83/4,7/9,8, C 11 (L*, C ±3 %)", "OK" if ok else "abweichend")
            zeile("Bild (Wiedergabe Phase 5)", f"Licht:Schatten, Feinstruktur Wange/Stirn, Oberlid, Iris {wer}, {quelle}",
                  f"{f(w['licht_schatten'])} · {f(w['feinstruktur']['wange_licht']['hp'])}/{f(w['feinstruktur']['stirn']['hp'])} · "
                  f"{f(w['oberlid_dL'],1)} · {f(w['augen']['links']['iris_Y'])}",
                  "1,25–1,3 · 0,25–0,41/0,2 · −10 · 0,30–0,37", "Stand")
    if ("neu (Stand 5)", "m30") in erg and os.path.exists(os.path.join(alt5 or "", "00_00.png")):
        d = BW.bildunterschied(B.lesen(os.path.join(mappe, "gesicht30", "00_00.png")), B.lesen(os.path.join(alt5, "00_00.png")))
        zeile("Bild (Wiedergabe Phase 5)", "Bildunterschied gesicht30 Mann neu gegen Phase 5", f"{f(d['mittel'])}/255 (p99 {f(d['p99'],0)})",
              "≤ 1/255 ohne TressFX (TressFX bewegt sich)", "OK" if d["mittel"] <= 1.0 else "Stand", "TressFX-Haar im Bild")
    # P1 (K1, L-V und L-S): Mann/Frau mittel (H2) 35 Jahre
    for licht in ("lv", "ls"):
        ordner = os.path.join(mappe, f"p01_alter_ton_k1_{licht}")
        for zz, wer in (("01_01", "Mann H2 35"), ("04_01", "Frau H2 35")):
            p = os.path.join(ordner, zz + ".png")
            if not os.path.exists(p):
                continue
            pl, pr = pupillen(os.path.join(ordner, zz + ".json"), nm)
            w = BW.werte_vorn(B.lesen(p), pl, pr)
            erg[(licht, wer)] = w
            z, zs = w["zonen"]["wange_licht"], w["zonen"]["wange_schatten"]
            zeile(f"Bild P1 L-{'V' if licht == 'lv' else 'S'}", f"{wer}: Wange L*/a*/b*/C · L:S · R/G licht/schatten · Oberlid ΔL*",
                  f"{f(z['L'],1)}/{f(z['a'],1)}/{f(z['b'],1)}/{f(z['C'],1)} · {f(w['licht_schatten'])} · {f(z['RG'])}/{f(zs['RG'])} · {f(w['oberlid_dL'],1)}",
                  "S2/S3: C ≥ 14 (S2), L:S 2–4, R/G ≥ 1,25/1,30 (S3)", "Stand")
    return erg


# ------------------------------------------------------------------ 5. Kalibrierung (P10)
def kalibrierung(mappe):
    laeufe = sorted(glob.glob(os.path.join(mappe, "p10_kalibrier*", "00_00.png")))
    werte = []
    for p in laeufe:
        a = B.lesen(p)
        h, w = a.shape[:2]
        werte.append(float(a[h // 2 - 20:h // 2 + 20, w // 2 - 20:w // 2 + 20].mean() * 255))
    if werte:
        streu = (max(werte) - min(werte)) / np.mean(werte)
        zeile("Kalibrierung", f"Graukarte sRGB (L-V), {len(werte)} Läufe", " / ".join(f(x, 1) for x in werte), "118 ± 4, Läufe ±1 %",
              "OK" if all(114 <= x <= 122 for x in werte) and streu <= 0.01 and len(werte) >= 3 else "offen/abweichend")
    p = sorted(glob.glob(os.path.join(mappe, "p10_kalibrier*", "00_02.png")))
    if p:
        a = B.lesen(p[0])
        k = Kamera(json.load(open(p[0][:-4] + ".json", encoding="utf-8")))
        d = json.load(open(p[0][:-4] + ".json", encoding="utf-8"))
        am = d["kopf"]["augen_mitte"]
        lin = []
        for dx in (-0.0735, 0.0, 0.0735):
            q, _ = k.projizieren(np.array([[dx * 1000, (am[1] - 0.02) * 1000, am[2] * 1000]]))
            x, y = int(q[0, 0]), int(q[0, 1])
            lin.append(float(B.linear(a[y - 8:y + 8, x - 8:x + 8].reshape(-1, 3).mean(axis=0)).mean()))
        ok = abs(lin[0] - 0.5) < 0.01 and abs(lin[1] - 0.214) < 0.01 and abs(lin[2] - 0.214) < 0.01
        zeile("Kalibrierung", "Farbtest linear: Vector3(0,5) · Color(0,5) · StandardMaterial3D", " · ".join(f(x, 3) for x in lin),
              "0,5 · 0,214 · 0,214", "OK" if ok else "ABWEICHUNG", "Godot rechnet nur den Color von sRGB nach linear um")


# ------------------------------------------------------------------ 5b. S2/S3 (Stand 6)
def _kugel_masken(zz_json, form, n=3, abstand=2.3, r=0.036):
    """Scheiben der drei Prüfkugeln (Bühne: _kugeln6, Mitte = Augenmitte − 2 cm, Abstand 2,3 r) im Bild."""
    d = json.load(open(zz_json, encoding="utf-8"))
    k = Kamera(d)
    am = d["kopf"]["augen_mitte"]
    h, w = form
    yy, xx = np.mgrid[0:h, 0:w]
    aus = []
    for i in range(n):
        m = np.array([(i - 1) * r * abstand, am[1] - 0.02, am[2]]) * 1000
        q, _ = k.projizieren(np.array([m, m + [r * 1000, 0, 0]]))
        rp = abs(q[1, 0] - q[0, 0])
        aus.append(np.hypot(xx + 0.5 - q[0, 0], yy + 0.5 - q[0, 1]) <= 0.9 * rp)
    return aus


def kugeln(mappe):
    o = os.path.join(mappe, "p10b_kugeln")
    if not os.path.exists(os.path.join(o, "00_00.png")):
        zeile("S2 Prüfkugel", "Glanz und Streulicht (p10b_kugeln)", "–", "–", "offen")
        return
    winkel = (0, 30, 60, 80, 150)
    glanz, streu, streif = {}, {}, {}
    for si, wk in enumerate(winkel):
        for zi, ziel in ((0, glanz), (1, streu), (2, streif)):
            p = os.path.join(o, f"{zi:02d}_{si:02d}.png")
            if not os.path.exists(p):
                continue
            lin = B.linear(B.lesen(p)).mean(axis=2)
            m = _kugel_masken(p[:-4] + ".json", lin.shape)
            if zi == 0:
                ziel[wk] = [float(np.sort(lin[mm])[-25:].mean()) for mm in m]   # Glanzspitze: Mittel der 25 hellsten
            elif zi == 1:
                ziel[wk] = [float(lin[mm].mean()) for mm in m]
            else:
                ziel[wk] = [float(lin[mm].sum()) for mm in m]                    # Glanz über die ganze Kugel
    for wk in winkel[:4]:
        if wk not in glanz:
            continue
        h6, st, h3 = glanz[wk]
        q = h6 / max(st, 1e-6)
        tol = 0.10 if wk <= 60 else 0.20
        zeile("S2 Prüfkugel", f"Glanzspitze haut6 : StandardMaterial3D bei {wk}° (Wrap 0, eine Keule)",
              f"{f(q, 3)} ({f(h6, 3)} / {f(st, 3)})", f"1 ± {int(tol * 100)} %", "OK" if abs(q - 1) <= tol else "ABWEICHUNG")
    for wk in (80, 150):
        if wk in streif:
            h6, st, h3 = streif[wk]
            q = h6 / max(h3, 1e-6)
            zeile("S2 Prüfkugel", f"Streiflichtglanz {wk}° (Summe über die Kugel) haut6 : haut3 (Phase 5); Standard : haut3",
                  f"{f(q, 2)}; {f(st / max(h3, 1e-6), 2)}", "≥ 2,5 (gewertet bei 150°)" if wk == 150 else "Stand",
                  ("OK" if q >= 2.5 else "ABWEICHUNG") if wk == 150 else "Stand")
    for wk in winkel[:4]:
        if wk not in streu:
            continue
        h6, st, _ = streu[wk]
        q = h6 / max(st, 1e-6)
        zeile("S2 Prüfkugel", f"Streulicht haut6 : Lambert (StandardMaterial3D) bei {wk}°", f"{f(q, 3)} ({f(h6, 3)} / {f(st, 3)})",
              "1 ± 3 %", "OK" if abs(q - 1) <= 0.03 else "ABWEICHUNG")


def formnormalen_ab(mappe, nm):
    o = os.path.join(mappe, "s2_ab_mann30")
    if not os.path.exists(os.path.join(o, "00_01.png")):
        zeile("S2 Formnormalen", "A/B Mann 30 (s2_ab_mann30)", "–", "–", "offen")
        return
    wv = {}
    for zz, art in (("00_00", "ohne"), ("00_01", "mit")):
        pl, pr = pupillen(os.path.join(o, zz + ".json"), nm)
        w = BW.werte_vorn(B.lesen(os.path.join(o, zz + ".png")), pl, pr)
        wl = w["zonen"]["wange_licht"]["Y"]
        wv[art] = {"kiefer": w["zonen"]["kiefer_licht"]["Y"] / wl, "kinn": w["zonen"]["kinn"]["Y"] / wl}
    for teil in ("kiefer", "kinn"):
        d = (wv["ohne"][teil] - wv["mit"][teil]) * 100
        zeile("S2 Formnormalen", f"A/B Mann 30, L-V: {teil.capitalize()} gegen obere Wange (Y), ohne → mit Normalen",
              f"{f(wv['ohne'][teil] * 100, 1)} % → {f(wv['mit'][teil] * 100, 1)} % (−{f(d, 1)} Pp.)", "≥ 8 Prozentpunkte dunkler",
              "OK" if d >= 8 else "ABWEICHUNG")


def duenn(mappe):
    p = os.path.join(mappe, "duenn_vergleich.json")
    if os.path.exists(p):
        d = json.load(open(p, encoding="utf-8"))
        zeile("S2 dünne Formdaten", "Normalen und Lagen dicht gegen dünn (größte Abweichung)",
              f"Normale {d['normale_max']:.1e}, Lage {d['lage_max']:.1e} m ({d['normalen_formen']} Formen mit Normalen, {d['duenne_accessoren']} dünne Accessoren)",
              "Normalen < 1e-3", "OK" if d["normale_max"] < 1e-3 else "ABWEICHUNG")
        if "kopf5_mn_mb" in d:
            zeile("S2 dünne Formdaten", "Größe kopf6.glb gegen kopf5_mn.glb", f"{f(d['groesse_mb'][1], 1)} MB / {f(d['kopf5_mn_mb'], 1)} MB",
                  "nicht größer", "OK" if d["groesse_mb"][1] <= d["kopf5_mn_mb"] else "ABWEICHUNG")
    o = os.path.join(mappe, "s2_duenn")
    if os.path.exists(os.path.join(o, "00_01.png")):
        werte = []
        for zi in range(4):
            a, b = os.path.join(o, f"{zi:02d}_00.png"), os.path.join(o, f"{zi:02d}_01.png")
            if os.path.exists(a) and os.path.exists(b):
                werte.append(BW.bildunterschied(B.lesen(a), B.lesen(b))["mittel"])
        zeile("S2 dünne Formdaten", "Bild dünn gegen dicht (s2_duenn, gleiche Normalen)", " / ".join(f(x) for x in werte) + " /255",
              "mittlere Abweichung ≤ 1/255", "OK" if werte and max(werte) <= 1.0 else "ABWEICHUNG")


def _lstern(Y):
    t = Y ** (1 / 3) if Y > 0.008856 else 7.787 * Y + 16 / 116
    return 116 * t - 16


def stand6_bild(mappe, nm, a0):
    """Abnahme S2/S3 an P1 (K1, Mann H2 35, L-V und L-S) und am Farbtest (P10, Zeile Stand 6)."""
    w = {}
    for licht in ("lv", "ls"):
        for mappe_x, wer in ((mappe, "A1"), (a0, "A0")):
            o = os.path.join(mappe_x or "", f"p01_alter_ton_k1_{licht}")
            p = os.path.join(o, "01_01.png")
            if not mappe_x or not os.path.exists(p):
                continue
            pl, pr = pupillen(p[:-4] + ".json", nm)
            w[(wer, licht)] = BW.werte_vorn(B.lesen(p), pl, pr)
    if ("A1", "lv") in w:
        x = w[("A1", "lv")]
        z, zs = x["zonen"]["wange_licht"], x["zonen"]["wange_schatten"]
        zeile("S2 Farbe", "Wange Mann 35 (H2), L-V: Chroma · a*", f"{f(z['C'], 1)} · {f(z['a'], 1)}", "C ≥ 14 (Phase 5: 11) · a* ≥ 8 (4,7)",
              "OK" if z["C"] >= 14 and z["a"] >= 8 else "ABWEICHUNG")
        iris = np.mean([x["augen"][s]["iris_Y"] for s in ("links", "rechts")])
        leder = np.mean([x["augen"][s]["lederhaut_Y"] for s in ("links", "rechts")])
        if ("A0", "lv") in w:
            i5 = np.mean([w[("A0", "lv")]["augen"][s]["iris_Y"] for s in ("links", "rechts")])
            zeile("S2 Augen", "Iris-Leuchtdichte Mann 35, L-V: Stand 6 : Stand 5", f"{f(iris, 3)} / {f(i5, 3)} = {f(iris / i5, 2)}",
                  "≤ ⅓", "OK" if iris / i5 <= 1 / 3 else "ABWEICHUNG")
        dl = _lstern(leder) - z["L"]
        zeile("S2 Augen", "Lederhaut gegen beleuchtete Wange (L*), L-V", f"{f(_lstern(leder), 1)} − {f(z['L'], 1)} = {f(dl, 1)}",
              "≤ +5 L*", "OK" if dl <= 5 else "ABWEICHUNG")
        zeile("S3 Licht", "L-V: Licht : Schatten der Wangen (linear) · R/G licht · R/G Schatten",
              f"{f(x['licht_schatten'])} · {f(z['RG'])} · {f(zs['RG'])}", "2,0–4,0 · ≥ 1,25 · ≥ 1,30",
              "OK" if 2.0 <= x["licht_schatten"] <= 4.0 and z["RG"] >= 1.25 and zs["RG"] >= 1.30 else "ABWEICHUNG")
        if ("A1", "ls") in w:
            y = w[("A1", "ls")]
            yl, ys = y["zonen"]["wange_licht"], y["zonen"]["wange_schatten"]
            q = [y["licht_schatten"] / x["licht_schatten"], yl["RG"] / z["RG"], ys["RG"] / zs["RG"]]
            zeile("S3 Licht", "L-S: Licht : Schatten · R/G licht · R/G Schatten (Verhältnis zu L-V)",
                  f"{f(y['licht_schatten'])} · {f(yl['RG'])} · {f(ys['RG'])} ({' / '.join(f(v) for v in q)})", "je ±20 % von L-V",
                  "OK" if all(0.8 <= v <= 1.2 for v in q) else "ABWEICHUNG")
            zeile("S3 Licht", "L-S: Wange L*/a*/b*/C (Mann 35, H2)", f"{f(yl['L'], 1)}/{f(yl['a'], 1)}/{f(yl['b'], 1)}/{f(yl['C'], 1)}",
                  "Chroma ≥ 17 sonst E5 (AgX-Feinwerte/LUT)", "OK" if yl["C"] >= 17 else "Stand (E5)")
    # Farbtest über die Hilfsfunktion (P10, Zeile Stand 6, Spalte 2): Mitte gegen StandardMaterial3D in 8 Bit
    p = os.path.join(mappe, "p10_kalibrier", "01_02.png")
    if os.path.exists(p):
        a = B.lesen(p)
        k = Kamera(json.load(open(p[:-4] + ".json", encoding="utf-8")))
        am = json.load(open(p[:-4] + ".json", encoding="utf-8"))["kopf"]["augen_mitte"]
        v = []
        for dx in (0.0, 0.0735):
            q, _ = k.projizieren(np.array([[dx * 1000, (am[1] - 0.02) * 1000, am[2] * 1000]]))
            x0, y0 = int(q[0, 0]), int(q[0, 1])
            v.append(a[y0 - 8:y0 + 8, x0 - 8:x0 + 8].reshape(-1, 3).mean(axis=0) * 255)
        d = float(np.abs(v[0] - v[1]).max())
        zeile("S2 Farbe", "Farbtest: Hilfsfunktion (Stand 6) gegen StandardMaterial3D, sRGB", f"{f(float(v[0].mean()), 1)} / {f(float(v[1].mean()), 1)} (Δ {f(d, 1)})",
              "±2/255", "OK" if d <= 2 else "ABWEICHUNG")


def m2_gepaart(mappe):
    p = os.path.join(mappe, "messung.jsonl")
    if not os.path.exists(p):
        return
    paar = {}
    for z in open(p, encoding="utf-8"):
        d = json.loads(z)
        paar.setdefault((d["name"], d.get("licht", "spiel"), d.get("wiederholung", 0)), {})[d.get("stand", 5)] = d["gpu_ms"]
    je = {}
    for (name, licht, wdh), st in paar.items():
        if 5 in st and 6 in st:
            je.setdefault((name, licht), []).append(st[6] - st[5])
    grenzen = {"ohne_haar": 0.3, "haar_tressfx_ruhe": 1.2, "x8_ohne_haar": 3.0, "leer": None}
    for (name, licht), ds in sorted(je.items()):
        g = grenzen.get(name)
        med = float(np.median(ds))
        ziel = f"Δ ≤ +{f(g, 1)} ms" if g is not None else "–"
        if name == "ohne_haar" and licht == "spiel":
            ziel = "Δ ≤ +0,3 ms (S2) bzw. +0,5 ms mit weichen Schatten (S3)"
            g = 0.5
        zeile("Leistung M2 gepaart", f"{name}, Licht {licht}: Stand 6 − Stand 5 (Median aus {len(ds)})", f"{'+' if med >= 0 else ''}{f(med)} ms ({', '.join(f(x) for x in ds)})",
              ziel, ("OK" if med <= g else "ABWEICHUNG") if g is not None else "Stand")


# ------------------------------------------------------------------ 6. Bild gegen Netz (M0)
def profil_punkte(ys, zs, P):
    """Punkte eines Profilumrisses (Höhe y, Vorsprung z in mm; Bild oder Netz) mit denselben Regeln; die Suchfenster
    kommen aus den Netz-Merkpunkten (nur Höhen)."""
    ok = np.isfinite(zs)
    ys, zs = ys[ok], zs[ok]
    o = np.argsort(-ys)
    ys, zs = ys[o], zs[o]

    def extrem(a, b, art):
        i = np.nonzero((ys >= min(a, b)) & (ys <= max(a, b)))[0]
        j = i[np.argmax(zs[i])] if art == "vorn" else i[np.argmin(zs[i])]
        return j
    braue = 0.5 * (P["lm21"][1] + P["lm22"][1])
    g = extrem(braue - 8, braue + 15, "vorn")
    n = extrem(P["lm28"][1], ys[g], "hinten")
    prn = extrem(P["lm33"][1], P["lm28"][1], "vorn")
    sm = extrem(P["lm08"][1] + 5, P["li"][1] - 3, "hinten")
    pg = extrem(P["lm08"][1] - 8, ys[sm], "vorn")
    j = pg
    while j + 1 < len(zs) and zs[j + 1] >= zs[pg] - 10.0 and ys[j] - ys[j + 1] < 2.0:
        j += 1
    return {k: (float(ys[i]), float(zs[i])) for k, i in (("g", g), ("n", n), ("prn", prn), ("sm", sm), ("pg", pg), ("me", j))}


def netz_ecken(nm, gewichte, P):
    """Alle sichtbaren Teile (Haut mit Ohren, Mund, Kleid, Kragen, Augen, Brauen) in mm."""
    teile = [P["_haut"]] + [t.lage(gewichte) * 1000 for t in nm.glb.teile("Haut")[1:]]
    teile += [nm.glb.teil("Kragen", 0).lage(gewichte) * 1000]
    return np.concatenate(teile)


def bild_gegen_netz(mappe, nm):
    ordner = os.path.join(mappe, "m0_bild_netz")
    if not os.path.exists(os.path.join(ordner, "00_00.png")):
        zeile("Bild gegen Netz", "6 Verhältnisse", "–", "±3 %", "offen")
        return
    for zi, wer in ((0, "Mann 30"), (1, "Frau 30")):
        bild, netz = {}, {}
        for si, art in ((0, "ortho"), (1, "persp"), (2, "ortho_p"), (3, "persp_p")):
            d = json.load(open(os.path.join(ordner, f"{zi:02d}_{si:02d}.json"), encoding="utf-8"))
            a = B.lesen(os.path.join(ordner, f"{zi:02d}_{si:02d}.png"))
            k = Kamera(d)
            w = d["kopf"]["szene"]
            P = nm.netz.punkte(w)
            vg = BW.vordergrund(a)
            if art in ("ortho", "persp"):
                q, _ = k.projizieren(np.array([P["ps_r"], P["ps_l"], P["me"]]))
                ipd0 = abs(q[1, 0] - q[0, 0])
                pl, pr = BW.pupille_finden(a, tuple(q[0]), ipd0), BW.pupille_finden(a, tuple(q[1]), ipd0)
                e = {"ipd": math.dist(pl, pr), "pupille_dx": max(abs(pl[0] - q[0, 0]), abs(pr[0] - q[1, 0])),
                     "pupille_dy": max(abs(pl[1] - q[0, 1]), abs(pr[1] - q[1, 1])),
                     "ps_me": abs(q[2, 1] - 0.5 * (q[0, 1] + q[1, 1])) / ipd0}
                y_km = 0.5 * (P["sto"][1] + P["me"][1])
                for name, y in (("augen", 0.5 * (P["ps_r"][1] + P["ps_l"][1])), ("mund", P["sto"][1]), ("kinnmitte", y_km)):
                    yy, _ = k.projizieren(np.array([[0.0, y, P["sto"][2]]]))
                    e[name] = BW.umriss_breite(vg, yy[0, 1] - 0.5)
                bild[art] = e
                if art == "ortho":
                    A = netz_ecken(nm, w, P)
                    netz["ipd"] = float(P["ps_l"][0] - P["ps_r"][0])
                    netz["ps_me"] = (0.5 * (P["ps_r"][1] + P["ps_l"][1]) - P["me"][1]) / netz["ipd"]
                    for name, y in (("augen", 0.5 * (P["ps_r"][1] + P["ps_l"][1])), ("mund", P["sto"][1]), ("kinnmitte", y_km)):
                        sl = A[np.abs(A[:, 1] - y) < 0.6]
                        netz[name] = float(sl[:, 0].max() - sl[:, 0].min())
                    bild[art]["mm_je_px"] = k.size / k.h * 1000
            else:
                prof = BW.profil_vorn(vg, blick_rechts=False)
                if art == "ortho_p":
                    s = k.size / k.h * 1000
                    ys = k.o[1] * 1000 + (k.h / 2 - (np.arange(k.h) + 0.5)) * s
                    zs = -(prof - k.w / 2) * s
                    bild[art] = profil_punkte(ys, zs, P)
                    A = netz_ecken(nm, w, P)
                    A = A[np.abs(A[:, 0]) < 60]
                    yb = np.arange(np.floor(A[:, 1].min()), np.ceil(A[:, 1].max()), 0.5)
                    zb = np.full(len(yb), np.nan)
                    idx = np.clip(((A[:, 1] - yb[0]) / 0.5).astype(int), 0, len(yb) - 1)
                    np.fmax.at(zb, idx, A[:, 2])
                    # obere Hülle über ±1,5 mm (wie das Profil in merkpunkte.py): Fächer mit nur inneren Ecken zählen nicht
                    ok = np.isfinite(zb)
                    zb = np.interp(np.arange(len(zb)), np.nonzero(ok)[0], zb[ok])
                    zb = np.array([zb[max(0, i - 3):i + 4].max() for i in range(len(zb))])
                    netz["profil"] = profil_punkte(yb + 0.25, zb, P)
                else:
                    bild[art] = None
        o, pp, bp, np_ = bild["ortho"], bild["persp"], bild["ortho_p"], netz["profil"]
        s = o["mm_je_px"]
        hoehe_b = bp["n"][0] - bp["me"][0]
        hoehe_n = np_["n"][0] - np_["me"][0]
        verh = [
            ("Umriss Augenhöhe / Umriss Mundhöhe", o["augen"] / o["mund"], netz["augen"] / netz["mund"], pp["augen"] / pp["mund"]),
            ("Umriss Kinnmitte / Umriss Mundhöhe", o["kinnmitte"] / o["mund"], netz["kinnmitte"] / netz["mund"], pp["kinnmitte"] / pp["mund"]),
            ("IPD (Pupillen) / Umriss Mundhöhe", o["ipd"] / o["mund"], netz["ipd"] / netz["mund"], pp["ipd"] / pp["mund"]),
            ("n–me (Profil) / Umriss Mundhöhe", hoehe_b / (o["mund"] * s), hoehe_n / netz["mund"], None),
            ("prn–pg (Nase vor dem Kinn) / n–me", (bp["prn"][1] - bp["pg"][1]) / hoehe_b, (np_["prn"][1] - np_["pg"][1]) / hoehe_n, None),
            ("prn–n (Vorsprung) / n–me", (bp["prn"][1] - bp["n"][1]) / hoehe_b, (np_["prn"][1] - np_["n"][1]) / hoehe_n, None),
        ]
        for name, b_o, n_v, b_p in verh:
            rel = (b_o - n_v) / n_v
            zeile("Bild gegen Netz", f"{wer}: {name}", f"Bild ortho {f(b_o,3)} · Netz {f(n_v,3)} · Abw. {f(100*rel,1)} %"
                  + (f" · perspektivisch {f(b_p,3)} ({f(100*(b_p-n_v)/n_v,1)} %)" if b_p else ""), "±3 %",
                  "OK" if abs(rel) <= 0.03 else "ABWEICHUNG")
        zeile("Bild gegen Netz", f"{wer}: Pupillen im Bild (dunkelster Schwerpunkt) gegen projizierte Netzpupillen (Mitte der Öffnung)",
              f"waagerecht ortho {f(o['pupille_dx'],1)} px, perspektivisch {f(pp['pupille_dx'],1)} px; senkrecht {f(o['pupille_dy'],1)} px",
              "waagerecht ≤ 2 px", "OK" if o["pupille_dx"] <= 2 else "ABWEICHUNG",
              "senkrecht: das Oberlid deckt den oberen Irisrand (Ruhelage), der dunkle Schwerpunkt rückt nach unten")
        zeile("Bild gegen Netz", f"{wer}: Erklärung „Bild kürzer als Netz“: Pupillenlinie–me / IPD",
              f"Netz {f(netz['ps_me'],3)} · ortho {f(o['ps_me'],3)} · perspektivisch {f(pp['ps_me'],3)} ({f(100*(pp['ps_me']-netz['ps_me'])/netz['ps_me'],1)} %)",
              "Erklärung", "Stand", "Raumabstand n–me gegen Höhe: " + f(100 * (1 - (P['n'][1] - P['me'][1]) / np.linalg.norm(P['n'] - P['me'])), 1) + " %")


# ------------------------------------------------------------------ 6b. Fotovorlagen (Merkpunkte von Hand)
def vorlagen():
    p = os.path.join(HIER, "vorlagen_merkpunkte.json")
    if not os.path.exists(p):
        return
    for vid, v in json.load(open(p, encoding="utf-8"))["vorlagen"].items():
        q = v.get("vorn", {}).get("punkte", {})
        if not all(k in q for k in ("ps_r", "ps_l", "sn", "sto", "me", "zy_r", "zy_l", "br_r", "br_l")):
            continue
        ipd = math.dist(q["ps_r"], q["ps_l"])
        py = 0.5 * (q["ps_r"][1] + q["ps_l"][1])
        zy = abs(q["zy_l"][0] - q["zy_r"][0])
        werte = {"mundlage": (q["sto"][1] - q["sn"][1]) / (q["me"][1] - q["sn"][1]), "zy_ipd": zy / ipd,
                 "ps_me_zy": (q["me"][1] - py) / zy, "braue_ipd": (py - 0.5 * (q["br_r"][1] + q["br_l"][1])) / ipd,
                 "sn_sto_ipd": (q["sto"][1] - q["sn"][1]) / ipd}
        zeile("Vorlagen (Merkpunkte)", f"{vid} ({v.get('alter', '?')}, {v.get('geschlecht', '?')})",
              " · ".join(f"{k} {f(x, 3)}" for k, x in werte.items()), "Maßstab", "Stand", v.get("satz", ""))


# ------------------------------------------------------------------ 7. Brücke, Manifest, M2
def bruecke(mappe):
    p = os.path.join(mappe, "bruecke_pruefen.json")
    if not os.path.exists(p):
        zeile("Erbgut-Brücke", "Abnahme S0", "–", "–", "offen")
        return
    d = json.load(open(p, encoding="utf-8"))
    zeile("Erbgut-Brücke", f"bestehende Gene mit/ohne Vorschlag ({d['anzahl']} Startwerte × {d['voelker']} Völker, Würfeln und Vererben)",
          f"{d['abweichungen_wuerfeln']} / {d['abweichungen_vererben']} Abweichungen", "0", "OK" if d["abweichungen_wuerfeln"] == 0 and d["abweichungen_vererben"] == 0 else "ABWEICHUNG")
    zeile("Erbgut-Brücke", "derselbe Startwert zweimal", f"{d['gleiche_bytes_ungleich']} ungleich", "0", "OK" if d["gleiche_bytes_ungleich"] == 0 else "ABWEICHUNG")
    zeile("Erbgut-Brücke", "30 Gensätze (Galerie, Taste Z)", f"{d['galerie_30_verschieden']} verschieden", "30", "OK" if d["galerie_30_verschieden"] == 30 else "ABWEICHUNG")


def manifest():
    r = subprocess.run([sys.executable, "-I", os.path.join(HIER, "..", "quellen6_pruefen.py")], capture_output=True, text=True)
    letzte = r.stdout.strip().splitlines()[-1] if r.stdout.strip() else r.stderr[-200:]
    zeile("Quellen", "Manifest quellen6.json", letzte, "grün", "OK" if r.returncode == 0 else "ABWEICHUNG")


def m2(mappe):
    p = os.path.join(mappe, "messung.jsonl")
    if not os.path.exists(p):
        zeile("Leistung M2", "GPU-Zeit", "–", "–", "offen")
        return
    laeufe = {}
    for z in open(p, encoding="utf-8"):
        d = json.loads(z)
        laeufe.setdefault((d["name"], d.get("stand", 5)), []).append(d)
    for (name, stand), ds in sorted(laeufe.items()):
        g = [d["gpu_ms"] for d in ds]
        zeile("Leistung M2", f"{name}, Stand {stand} ({ds[0].get('aufloesung', '')})",
              f"Median {f(float(np.median(g)))} ms aus {len(g)} ({', '.join(f(x) for x in g)}), Last {', '.join(f(d.get('last', 0), 1) for d in ds)}",
              "Grundlage für die gepaarten Messungen ab S2", "Stand")


# ------------------------------------------------------------------ Ausgabe
def schreiben(aus, meta):
    os.makedirs(aus, exist_ok=True)
    json.dump({"meta": meta, "zeilen": ZEILEN}, open(os.path.join(aus, "pruefen6.json"), "w", encoding="utf-8"), indent=1, ensure_ascii=False)
    L = [f"# Prüfung Charakter Phase 6 ({meta['stand']})", "", f"- Commit: `{meta['commit']}` · Datum {meta['datum']} · Dauer {meta['dauer_s']} s",
         f"- Prüfmappe: `{meta['mappe']}`", "", "| Bereich | Größe | Ist | Ziel | Status | Hinweis |", "|---|---|---|---|---|---|"]
    for z in ZEILEN:
        L.append("| " + " | ".join(str(z[k]).replace("|", "/") for k in ("bereich", "groesse", "ist", "ziel", "status", "hinweis")) + " |")
    open(os.path.join(aus, "pruefen6.md"), "w", encoding="utf-8").write("\n".join(L) + "\n")


def main():
    t0 = time.time()
    opt = dict(a[2:].split("=", 1) for a in sys.argv[1:] if a.startswith("--") and "=" in a)
    mappe = os.path.expanduser(opt.get("mappe", "~/werkzeuge/charakter/bilder/phase6/A0"))
    gew = opt.get("gewichte") or next(iter(sorted(glob.glob(os.path.join(mappe, "gewichte*.json")))), None)
    glb_pfad = opt.get("glb", "~/werkzeuge/charakter/glb5/kopf5.glb")
    nm = Netzmesser(Glb(glb_pfad))
    farben_statisch()
    netzmasse(gew, glb_pfad)
    bevoelkerung_pruefen(gew, glb_pfad)
    gewichte_vergleich(gew, mappe)
    bildwerte(mappe, nm, opt.get("alt5"))
    kalibrierung(mappe)
    bild_gegen_netz(mappe, nm)
    vorlagen()
    bruecke(mappe)
    manifest()
    m2(mappe)
    kugeln(mappe)
    formnormalen_ab(mappe, nm)
    duenn(mappe)
    stand6_bild(mappe, nm, os.path.expanduser(opt["a0"]) if "a0" in opt else None)
    m2_gepaart(mappe)
    try:
        commit = subprocess.run(["git", "-C", REPO, "rev-parse", "--short", "HEAD"], capture_output=True, text=True).stdout.strip()
    except OSError:
        commit = "?"
    dauer = round(time.time() - t0, 1)
    zeile("Prüfung", "Laufzeit pruefen6.py (ohne Godot und Blender)", f"{f(dauer,1)} s", "≤ 120 s", "OK" if dauer <= 120 else "ABWEICHUNG")
    meta = {"stand": opt.get("titel", "Prüfmappe"), "commit": commit, "datum": time.strftime("%d.%m.%Y %H:%M"), "dauer_s": dauer,
            "mappe": mappe, "glb": glb_pfad, "gewichte": gew}
    schreiben(os.path.expanduser(opt.get("aus", mappe)), meta)
    print(f"pruefen6: {len(ZEILEN)} Zeilen, {dauer} s -> {opt.get('aus', mappe)}")


if __name__ == "__main__":
    main()
