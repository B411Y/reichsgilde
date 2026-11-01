"""Grundkopf E3: absolute Gesichtsform eines FaceBuilder-Kopfs als Form „vorbild_grund_<g>“ (Charakter Phase 6,
01.11.2026; eigener Code, MIT; numpy, scipy aus pylib_b2; nur Werkzeug).

Anders als fb_auf_gitter.py kopf (Versatz Person − FB-Grundgitter, auf unser Mittelgesicht gelegt) trägt diese Form den
ganzen Abstand unseres Mittelgesichts zur FB-Person: Vorher lief „fb_auf_gitter.py grund“ mit der FB-Person statt des
Grundgitters (--fb=<person>/fb_kopf.npz --bereich=kopf), unser Netz liegt dort nicht-starr auf der Personenfläche (X).
Versatz = X − S, danach wie fb_auf_gitter.py kopf:
  1. Restbewegung (Ähnlichkeit über das Gesicht) heraus – Größe und Lage regeln die Gene;
  2. Lider und Augapfel folgen dem mittleren Versatz ihres Umfelds (8–16 mm vom Lidrand);
  3. Bereich: nur das Gesicht. Der Schädel bleibt (die FB-Köpfe raten ihn unter dem Haar: Mann 176 mm lang statt
     ≈ 190–195 nach DIN 33402), Ohren und Hals ebenso. Ausblenden vor dem Ohr wie Weg B, an der Stirn aber weich über
     g + 20 … g + 80 mm (Weg B: g + 28 … g + 42 – die Stirn der Frau liegt 8–10 mm zurück, das gäbe dort eine Stufe),
     unter dem Kinn wie Weg B.
Ausgabe <aus>/vorbild_grund_<g>.npz (basis, versatz [m], name, augen_versatz) – Format wie vorbild_einpassen.py, also
direkt für kopf2_bauen.py … vorbilder=<npz>; dazu .json.

Aufruf (Blender-Python, PYTHONPATH=~/werkzeuge/charakter/pylib_b2):
  $BPY -I grundkopf_fb.py --abbildung=<abs_g/abbildung_g.npz> --geschlecht=m|w --glb=<kopf6p.glb> --rezept_s4=<json>
       --aus=<ordner> [--fb_werkzeug=~/werkzeuge/charakter/facebuilder] [--stirn=20,80] [--staerke=1.0]
"""
import json
import os
import sys

import numpy as np

HIER = os.path.dirname(os.path.abspath(__file__))


def main():
    opt = {"fb_werkzeug": "~/werkzeuge/charakter/facebuilder", "stirn": "20,80", "staerke": "1.0"}
    opt.update(dict(a[2:].split("=", 1) for a in sys.argv[1:] if a.startswith("--") and "=" in a))
    for k in ("abbildung", "glb", "rezept_s4", "aus", "fb_werkzeug"):
        opt[k] = os.path.abspath(os.path.expanduser(opt[k]))
    sys.path.insert(0, HIER)
    sys.path.insert(0, opt["fb_werkzeug"])
    import fb_auf_gitter as fa
    import vorbild_einpassen as ve
    g = opt["geschlecht"]
    fa_opt = dict(fa.VORGABE)
    fa_opt.update({"glb": opt["glb"], "rezept_s4": opt["rezept_s4"], "werkzeug": HIER})
    haut, P = fa.mittelgesicht(fa_opt, g)
    A = np.load(opt["abbildung"])
    S, X, tri = A["S"], A["X"], A["tri"]
    S2 = P["_haut"]
    if S2.shape != S.shape or np.abs(S2 - S).max() > 0.01:
        sys.exit(f"Mittelgesicht passt nicht zur Abbildung (max {np.abs(S2 - S).max():.3f} mm): andere glb/rezept_s4?")
    N_S = haut.flaechennormalen(S / 1000.0)
    rand_w, abst_je, daten = fa.bereiche(ve, S, P, N_S, "gesicht")
    # Bereich neu: vor dem Ohr und unter dem Kinn wie Weg B, Stirn weich
    s0, s1 = (float(x) for x in opt["stirn"].split(","))
    z_ohr = P["_z_ohr"]
    unten = ve.glatt(S[:, 1], P["me"][1] - 18, P["me"][1] - 6)
    rand = ve.glatt(S[:, 2], z_ohr + 4, z_ohr + 30) * unten * (1.0 - ve.glatt(S[:, 1], P["g"][1] + s0, P["g"][1] + s1))

    D = X - S
    # Nasenlöcher: ohne Datenterm eingepasst (innen verschoben sich Ecken bis 11 mm) – dort harmonisch aus dem Umfeld
    from scipy import sparse
    from scipy.sparse.linalg import spsolve
    nase_abst = np.minimum.reduce([np.linalg.norm(S - P[k], axis=1) for k in ("al_r", "al_l", "sn")])
    # Runde 1 (Godot): Nasenlöcher der Frau schwarz umrandet (Innenlagen stachen durch) – der ganze Nasenflügel- und
    # Lochbereich um al und sn folgt harmonisch seinem Rand, die Spitze bleibt FB
    # Runde 2: an der Flügelfurche der Frau klappte die Haut um (schwarze Bögen) – Bereich auf 20 mm erweitert
    frei_n = ((nase_abst < 14.0) & (daten < 0.5)) | (nase_abst < float(opt.get("nase_mm", "20")))
    # Nasenhöhle (innen, hinter der Flügelbasis): dem Umfeld folgen, sonst wandert der Steg innen bis 9 mm seitlich
    al_y, al_z = 0.5 * (P["al_r"][1] + P["al_l"][1]), 0.5 * (P["al_r"][2] + P["al_l"][2])
    al_x = 0.5 * (abs(P["al_r"][0]) + abs(P["al_l"][0]))
    frei_n |= (np.abs(S[:, 0]) < al_x - 2.0) & (S[:, 1] > al_y - 6.0) & (S[:, 1] < P["prn"][1] + 10.0) & \
        (S[:, 2] < al_z + 2.0) & (S[:, 2] > al_z - 40.0)
    L = ve.laplace(len(S), tri).tocsr()
    # Ausreißer (z. B. Nasensteg auf die FB-Nasenflügel gezogen): Abstand zur geglätteten Form > 2,5 mm, um einen
    # Ring erweitert, ebenfalls aus dem Umfeld
    from scipy.sparse.linalg import factorized
    glaett = factorized((sparse.identity(len(S)) + 5.0 * L).tocsc())
    Dg = np.stack([glaett(D[:, k]) for k in range(3)], 1)
    aus_ = np.linalg.norm(D - Dg, axis=1) > 2.5
    nachbar = (abs(L) > 0).astype(np.float64)
    aus_ = (nachbar @ aus_.astype(np.float64)) > 0
    ausreisser = int(aus_.sum())
    frei_n = frei_n | aus_
    if frei_n.any():
        fi, ke = np.nonzero(frei_n)[0], np.nonzero(~frei_n)[0]
        Lff, Lfk = L[fi][:, fi], L[fi][:, ke]
        for k in range(3):
            D[fi, k] = spsolve(Lff.tocsc(), -Lfk @ D[ke, k])
        # Runde 2: innen wanderte der Steg mit den Wangen 5 mm seitlich und stach durch den Flügel – seitlicher Anteil
        # zur Mitte hin auf 0 (Steg bleibt in der Mitte, die Flügel behalten ihn)
        D[fi, 0] *= ve.glatt(np.abs(S[fi, 0]), 0.0, al_x + 4.0)
    gew_r = rand * daten
    sel = gew_r > 0.3
    s_r, R_r, t_r = ve.umeyama(S[sel], (S + D)[sel], gew_r[sel])
    D = ((R_r.T @ ((S + D) - t_r).T).T / s_r) - S
    rest = {"massstab": round(float(s_r), 4),
            "drehung_grad": round(float(np.degrees(np.arccos(np.clip((np.trace(R_r) - 1) / 2, -1, 1)))), 2),
            "verschiebung_mm": [round(float(x), 2) for x in t_r]}
    augen_t = {}
    hoehle = {}
    for s in "rl":
        ring_ = (abst_je[s] > 8.0) & (abst_je[s] < 16.0) & (rand > 0.5)
        augen_t[s] = D[ring_].mean(0) if ring_.any() else np.zeros(3)
        # Augenhöhle hinter dem Lid: die Einpassung zog diese verdeckten Ecken bis 25 mm vor auf die geschlossenen
        # FB-Lider. Ecken innerhalb des Lidrings (in x, y) und mehr als 6 mm hinter seiner hintersten Ecke folgen ganz
        # dem Umfeld.
        rp = np.array([P[f"lm{i}"] for i in ve.AUGENRING[s]])
        c = rp.mean(0)
        r_xy = np.linalg.norm(rp[:, :2] - c[:2], axis=1).max()
        hoehle[s] = (np.linalg.norm(S[:, :2] - c[:2], axis=1) < r_xy + 2.0) & (S[:, 2] < rp[:, 2].min() - 6.0)
        # dazu alle von vorn verdeckten Ecken bis 30 mm um die Lidmitte (Lidfalte innen, Lidnaht): Strahl von der Ecke
        # nach vorn (+z) trifft ein Dreieck, das die Ecke nicht enthält, mehr als 0,5 mm vor ihr
        nahe = np.nonzero(np.linalg.norm(S - c, axis=1) < 30.0)[0]
        verdeckt = np.zeros(len(S), bool)
        pk = S[nahe]
        tri_n = tri[np.isin(tri, nahe).any(1)]
        for t in tri_n:
            A_, B_, C_ = S[t[0]], S[t[1]], S[t[2]]
            lo, hi = np.minimum.reduce([A_, B_, C_])[:2], np.maximum.reduce([A_, B_, C_])[:2]
            k_ = np.nonzero((pk[:, 0] >= lo[0]) & (pk[:, 0] <= hi[0]) & (pk[:, 1] >= lo[1]) & (pk[:, 1] <= hi[1]))[0]
            if not len(k_):
                continue
            v0, v1 = B_[:2] - A_[:2], C_[:2] - A_[:2]
            den = v0[0] * v1[1] - v1[0] * v0[1]
            if abs(den) < 1e-9:
                continue
            w = pk[k_, :2] - A_[:2]
            u = (w[:, 0] * v1[1] - v1[0] * w[:, 1]) / den
            v = (v0[0] * w[:, 1] - w[:, 0] * v0[1]) / den
            innen = (u >= 0) & (v >= 0) & (u + v <= 1)
            z_t = A_[2] + u * (B_[2] - A_[2]) + v * (C_[2] - A_[2])
            treffer = innen & (z_t > pk[k_, 2] + 0.5) & ~np.isin(nahe[k_], t)
            verdeckt[nahe[k_[treffer]]] = True
        hoehle[s] |= verdeckt
    # Augenpartie harmonisch (Laplace auf dem Netz): fest sind der Lidrand (≤ 2,5 mm, folgt dem Augapfel = augen_t) und
    # alles Sichtbare ab 15 mm vom Lidrand (Versatz D); frei der Rest bis 15 mm und alle verdeckten Ecken. So bewegen sich
    # Außenhaut und verdeckte Innenlagen (Lidfalte, Lidnaht) über die Netzverbindung gemeinsam – ein Umschalten je Ecke
    # (sichtbar/verdeckt) riss im ersten Versuch Falten in Lid und Nasenwurzel.
    seite_r = abst_je["r"] <= abst_je["l"]
    abst = np.minimum(abst_je["r"], abst_je["l"])
    verd = hoehle["r"] | hoehle["l"]
    # Runde 1 (Godot): Augapfel 5 mm vor, Oberlid harmonisch zur 8 mm zurückweichenden Braue – das Weiß stach über dem
    # Lid durch. Jetzt: Augapfel höchstens AUGE_VOR_MAX mm vor (FB rät die Augen an geschlossenen Lidern), das ganze Lid
    # bis 6 mm vom Rand folgt dem Augapfel, harmonisch erst 6–20 mm
    for s_ in "rl":
        augen_t[s_] = augen_t[s_].copy()
        augen_t[s_][2] = min(augen_t[s_][2], float(opt.get("auge_vor_max", "2.0")))
    rand_lid = abst <= 6.0
    frei_a = ((abst < 20.0) | verd) & ~rand_lid
    ziel_a = D.copy()
    ziel_a[rand_lid & seite_r] = augen_t["r"]
    ziel_a[rand_lid & ~seite_r] = augen_t["l"]
    fi, ke = np.nonzero(frei_a)[0], np.nonzero(~frei_a)[0]
    Lff, Lfk = L[fi][:, fi].tocsc(), L[fi][:, ke]
    for k in range(3):
        ziel_a[fi, k] = spsolve(Lff, -Lfk @ ziel_a[ke, k])
    D = ziel_a
    # Grundkopf symmetrisch (Asymmetrie bringen die Gene): Mittel aus Versatz und gespiegeltem Versatz der Spiegelecke
    from scipy.spatial import cKDTree
    _, spiegel = cKDTree(S * np.array([-1.0, 1.0, 1.0])).query(S)
    D = 0.5 * (D + D[spiegel] * np.array([-1.0, 1.0, 1.0]))
    rand = 0.5 * (rand + rand[spiegel])
    at = 0.5 * (augen_t["r"] + augen_t["l"] * np.array([-1.0, 1.0, 1.0]))
    augen_t = {"r": at, "l": at * np.array([-1.0, 1.0, 1.0])} if P["lm36"][0] * P["lm42"][0] < 0 else augen_t
    k = float(opt["staerke"])
    versatz = D * rand[:, None] * k
    gesicht = rand > 0.5
    betrag = np.linalg.norm(versatz, axis=1)
    bericht = {"name": f"grund_{g}", "abbildung": opt["abbildung"], "stirn_mm": [s0, s1], "staerke": k,
               "restbewegung": rest, "rms_versatz_mm": round(float(np.sqrt((versatz[gesicht] ** 2).sum(1).mean())), 2),
               "max_versatz_mm": round(float(betrag.max()), 2),
               "hoehle_ecken": {s: int(hoehle[s].sum()) for s in "rl"}, "nase_frei_ecken": int(frei_n.sum()), "ausreisser_ecken": ausreisser,
               "augen_versatz_mm": {s: [round(float(x) * k, 2) for x in augen_t[s]] for s in "rl"}}
    print(json.dumps(bericht, ensure_ascii=False), flush=True)
    os.makedirs(opt["aus"], exist_ok=True)
    aus = os.path.join(opt["aus"], f"vorbild_grund_{g}")
    np.savez_compressed(aus + ".npz", basis=A["haut_grund"], versatz=(versatz / 1000.0).astype(np.float32),
                        name=f"grund_{g}",
                        augen_versatz=(np.array([augen_t["r"], augen_t["l"]]) * k / 1000.0).astype(np.float32))
    np.savez_compressed(aus + "_pruef.npz", netz=S.astype(np.float32), eingepasst=(S + versatz).astype(np.float32),
                        dreiecke=tri, rand=rand.astype(np.float32))
    json.dump(bericht, open(aus + ".json", "w", encoding="utf-8"), indent=1, ensure_ascii=False)


if __name__ == "__main__":
    main()
