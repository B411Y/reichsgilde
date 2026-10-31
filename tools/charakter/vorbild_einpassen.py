"""Weg B des Probekopfs „Halt B2“: einen frei lizenzierten Kopfscan (Abguss, Lebend- oder Totenmaske, 3D-Scan) nicht-starr
auf unser Kopfnetz ziehen und den Unterschied zum B2-Mittelgesicht als Form „vorbild_<name>“ ausgeben (Charakter
Phase 6; eigener Code, MIT; numpy, scipy und trimesh (MIT) in Blenders Python, per PYTHONPATH aus einem eigenen
Zielordner, z. B. ~/werkzeuge/charakter/pylib_b2).

Ablauf
 1. Merkpunkte am Scan: Bildpunkte (Lid- und Mundwinkel, Nasenflügel) aus einem Frontbild mit Tiefenpuffer
    (<name>_front.npz; Drehung R0, Ausschnitt, Maßstab), dazu Nasenspitze, Subnasale und Pogonion automatisch aus dem
    Tiefenprofil der Mittellinie.
 2. Ähnlichkeitsabbildung (Umeyama: Drehung, Verschiebung, ein Maßstab) der Scan-Merkpunkte auf dieselben Merkpunkte des
    B2-Mittelgesichts (Geschlecht des Vorbilds, 35 Jahre). Der Maßstab nimmt die Kopfgröße heraus; übrig bleibt die Form.
 3. Nicht-starre Einpassung in sechs Runden: je Hautecke des Gesichts der nächste Punkt der Scanfläche (verworfen über
    10 mm oder bei Normalen > 70° auseinander), dann  min Σ w_i |S_i + D_i − c_i|² + α Σ_Kanten |D_i − D_j|² + β Merkpunkte,
    α (Steifigkeit) fällt von 60 auf 1. Die Augenregion (Lidring ± 7 mm) hat keinen Datenterm: Abgüsse haben
    geschlossene Augen, dort füllt die Glättung aus der Umgebung.
 4. Versatz D = Einpassung − B2-Mittelgesicht, leicht geglättet (I + 2 L), ausgeblendet am Rand des Gesichts (vor den Ohren, über der Stirn, unter
    dem Kinn) und an den Lidern (Lidring ± 5 → 11 mm; Augapfel und Lidrand bleiben zusammen).

Ausgabe <aus>/vorbild_<name>.npz: basis (Ecken von Haut/0 der .glb, Meter, Godot-Achsen), versatz (Meter), name;
Bericht <aus>/vorbild_<name>.json (Restabstand, RMS des Versatzes, Merkpunkte). Der Probebau nimmt die Datei mit
kopf2_bauen.py … vorbilder=<a.npz>,<b.npz> als eigene Form auf (Brauen, Wimpern, Lider, Kragen folgen der Haut).

Aufruf: PYTHONPATH=<pylib> $BPY -I tools/charakter/vorbild_einpassen.py <vorbild.json> --glb=<glb6p/kopf6p.glb>
        --gewichte=<gewichte.json> --rezept_s4=<rezept_s4_b2.json> --aus=<ordner>
<vorbild.json>: {"name", "geschlecht": "m"|"w", "scan": <Scan .npy-Präfix oder Datei>, "front": <name>_front.npz,
                 "merkpunkte_px": {"en_r": [x, y], …}, "lizenz": …, "quelle": …}
"""
import json
import os
import sys

import numpy as np

HIER = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HIER)
from messen.glb import Glb  # noqa: E402
from messen.merkpunkte import Kopfnetz  # noqa: E402

AUGENRING = {"r": list(range(36, 42)), "l": list(range(42, 48))}


def umeyama(a, b, w=None):
    """Ähnlichkeitsabbildung s·R·a + t ≈ b (gewichtete kleinste Quadrate)."""
    w = np.ones(len(a)) if w is None else np.asarray(w, float)
    w = w / w.sum()
    ma, mb = (w[:, None] * a).sum(0), (w[:, None] * b).sum(0)
    A, B = a - ma, b - mb
    C = (w[:, None] * B).T @ A
    U, S, Vt = np.linalg.svd(C)
    d = np.sign(np.linalg.det(U @ Vt))
    D = np.diag([1, 1, d])
    R = U @ D @ Vt
    s = (S * np.diag(D)).sum() / (w * (A ** 2).sum(1)).sum()
    return s, R, mb - s * R @ ma


def scan_merkpunkte(front, px):
    """Bildpunkte -> 3D (Koordinaten nach R0); Tiefe = vorderste Fläche im 5 × 5-Fenster."""
    tief, crop, s, gr = front["tief"], front["crop"], float(front["s"]), int(front["gr"])

    def punkt(x, y):
        x, y = int(round(x)), int(round(y))
        fen = tief[max(y - 2, 0):y + 3, max(x - 2, 0):x + 3]
        z = fen[np.isfinite(fen)].max()
        return np.array([crop[0] + x / (gr - 1) * s, crop[2] + (gr - 1 - y) / (gr - 1) * s, z])
    P = {k: punkt(*v) for k, v in px.items()}
    # Mittellinie: Tiefenprofil zwischen Augen und Kinn
    xm = int(round(np.mean([px[k][0] for k in ("en_r", "en_l", "ch_r", "ch_l")])))
    y_auge = int(round(np.mean([px[k][1] for k in ("en_r", "en_l")])))
    y_mund = int(round(np.mean([px[k][1] for k in ("ch_r", "ch_l")])))
    spalte = np.where(np.isfinite(tief[:, xm - 2:xm + 3]), tief[:, xm - 2:xm + 3], -np.inf).max(1)
    ok = np.isfinite(spalte)
    spalte = np.interp(np.arange(len(spalte)), np.nonzero(ok)[0], spalte[ok])     # Löcher im Tiefenpuffer füllen
    h = y_mund - y_auge
    r_prn = y_auge + 15 + int(np.argmax(spalte[y_auge + 15:y_mund - 10]))
    # Subnasale: tiefster Punkt unter der Sehne Nasenspitze–Oberlippe (die Oberlippe liegt bei manchen Köpfen hinter
    # der Subnasale, dann fände ein bloßes Minimum den Mundspalt)
    r_ls = r_prn + int(0.1 * h) + int(np.argmax(spalte[r_prn + int(0.1 * h):y_mund]))
    zeilen = np.arange(r_prn, r_ls + 1)
    sehne = spalte[r_prn] + (spalte[r_ls] - spalte[r_prn]) * (zeilen - r_prn) / max(r_ls - r_prn, 1)
    r_sn = r_prn + int(np.argmin(spalte[zeilen] - sehne))
    # Pogonion: unterhalb der Lippenfalte (die Unterlippe steht oft weiter vor als das Kinn), über dem Hals
    r_pg = y_mund + int(0.3 * h) + int(np.argmax(spalte[y_mund + int(0.3 * h):y_mund + int(0.75 * h)]))
    for k, r in (("prn", r_prn), ("sn", r_sn), ("pg", r_pg)):
        P[k] = punkt(xm, r)
        px[k] = [xm, r]
    return P


def laplace(n, dreiecke):
    from scipy import sparse
    e = np.concatenate([dreiecke[:, [0, 1]], dreiecke[:, [1, 2]], dreiecke[:, [2, 0]]])
    e = np.unique(np.sort(e, 1), axis=0)
    A = sparse.coo_matrix((np.ones(len(e)), (e[:, 0], e[:, 1])), shape=(n, n))
    A = ((A + A.T) > 0).astype(float)
    return sparse.diags(np.asarray(A.sum(1)).ravel()) - A


def glatt(x, a, b):
    t = np.clip((x - a) / (b - a), 0.0, 1.0)
    return t * t * (3 - 2 * t)


def main():
    import trimesh
    from scipy import sparse
    from scipy.sparse.linalg import factorized
    opt = dict(a[2:].split("=", 1) for a in sys.argv[2:] if a.startswith("--") and "=" in a)
    vb = json.load(open(sys.argv[1], encoding="utf-8"))
    name, g = vb["name"], vb["geschlecht"]
    aus = opt.get("aus", ".")
    os.makedirs(aus, exist_ok=True)
    basis_ordner = os.path.dirname(os.path.abspath(sys.argv[1]))

    # ---------------------------------------------------------------- B2-Mittelgesicht (Quelle)
    glb = Glb(os.path.expanduser(opt["glb"]))
    netz = Kopfnetz(glb)
    haut = netz.haut
    pers = {p["name"]: p for p in json.load(open(opt["gewichte"], encoding="utf-8"))["personen"]}
    w = {k: v for k, v in pers[f"mittel_{g}35"]["szene"].items()
         if not k.startswith(("gen_", "alter_")) and abs(v) > 1e-9}
    r4 = json.load(open(opt["rezept_s4"], encoding="utf-8"))
    for teil, f in ((f"alter_reife_{g}_oben", 1.0), (f"alter_reife_{g}_unten", 1.0), (f"alter_mitte_{g}", 0.55)):
        for k, v in r4[teil].items():
            w[k] = w.get(k, 0.0) + f * v
    P = netz.punkte(w)
    S = P["_haut"]                                     # mm, Godot-Achsen
    n = len(S)
    tri = haut.dreiecke
    N_S = haut.flaechennormalen(S / 1000.0)

    # ---------------------------------------------------------------- Scan, Merkpunkte, Ausrichtung
    front = np.load(os.path.join(basis_ordner, vb["front"]))
    R0 = front["R0"]
    praefix = os.path.join(basis_ordner, vb["scan"])
    V = np.load(praefix + "_v.npy") @ R0.T
    F = np.load(praefix + "_f.npy")
    px = {k: list(v) for k, v in vb["merkpunkte_px"].items()}
    Q = scan_merkpunkte(front, px)
    # Subnasale nicht: in der Frontansicht verdeckt (unter der Nasenspitze sieht man gleich die Oberlippe)
    namen = ["en_r", "en_l", "ex_r", "ex_l", "al_r", "al_l", "ch_r", "ch_l", "prn", "pg"]
    gew = np.array([0.2 if k.startswith(("en", "ex")) else 1.0 for k in namen])
    a = np.array([Q[k] for k in namen])
    b = np.array([P[k] for k in namen])
    s_, R_, t_ = umeyama(a, b, gew)
    V = (s_ * (R_ @ V.T)).T + t_
    Qt = {k: s_ * R_ @ Q[k] + t_ for k in namen}
    rest_umeyama = {k: round(float(np.linalg.norm(Qt[k] - P[k])), 2) for k in namen}
    print(name, "Maßstab", round(float(s_), 4), "Rest nach Umeyama (mm)", rest_umeyama)

    # ---------------------------------------------------------------- Gesichtsbereich, Augen
    z_ohr = P["_z_ohr"]
    rand = glatt(S[:, 2], z_ohr + 4, z_ohr + 30) * glatt(S[:, 1], P["me"][1] - 18, P["me"][1] - 6) \
        * (1.0 - glatt(S[:, 1], P["g"][1] + 28, P["g"][1] + 42))
    auge_abst = np.full(n, np.inf)
    for s in "rl":
        ring = np.array([P[f"lm{i}"] for i in AUGENRING[s]] + [P[f"lm{AUGENRING[s][0]}"]])
        for p0, p1 in zip(ring[:-1], ring[1:]):
            ab = p1 - p0
            tt = np.clip(((S - p0) @ ab) / (ab @ ab), 0, 1)
            auge_abst = np.minimum(auge_abst, np.linalg.norm(S - (p0 + np.outer(tt, ab)), axis=1))
    daten_auge = glatt(auge_abst, 7.0, 13.0)
    # Nasenlöcher: Scans haben dort Löcher oder Höhlen, der nächste Punkt springt hinein (Buckel am Nasenflügel)
    nase_abst = np.minimum.reduce([np.linalg.norm(S - P[k], axis=1) for k in ("al_r", "al_l", "sn")])
    unten = N_S[:, 1] < -0.35
    daten_auge = daten_auge * np.where(unten & (nase_abst < 9.0), 0.0, 1.0) * glatt(nase_abst, 2.0, 5.0)
    lid_aus = glatt(auge_abst, 5.0, 11.0)
    frei = np.nonzero(rand > 0.01)[0]
    # Scan auf den Gesichtsbereich beschneiden (schneller, keine Fremdteile wie Sockel oder Tuch)
    lo, hi = S[frei].min(0) - 15, S[frei].max(0) + 15
    keep = np.all((V > lo) & (V < hi), axis=1)
    fk = keep[F].all(1)
    scan = trimesh.Trimesh(V, F[fk], process=False)
    scan.remove_unreferenced_vertices()
    print(name, "Scan im Gesichtsbereich:", len(scan.vertices), "Ecken,", len(scan.faces), "Flächen")
    # Starre Verfeinerung (ICP, Maßstab aus den Merkpunkten bleibt): je Gesichtsecke der nächste Scanpunkt, Drehung und
    # Verschiebung des Scans so, dass die Fläche passt – grob abgelesene Merkpunkte (geschlossene Lider) zählen so weniger
    w_icp = rand * daten_auge
    sel = np.nonzero(w_icp > 0.5)[0]
    scan_vor, Qt_vor = scan.vertices.copy(), dict(Qt)
    for _ in range(10):
        c, dist, tid = trimesh.proximity.closest_point(scan, S[sel])
        ok = dist < np.percentile(dist, 85)
        _, R2, t2 = umeyama(c[ok], S[sel][ok])
        scan.vertices = (R2 @ scan.vertices.T).T + t2
        Qt = {k: R2 @ v + t2 for k, v in Qt.items()}
    print(name, "nach ICP: mittlerer Abstand", round(float(dist[ok].mean()), 2), "mm")
    rest_icp = {k: round(float(np.linalg.norm(Qt[k] - P[k])), 2) for k in namen}
    # ICP kann bei sehr anderen Gesichtern in ein falsches Minimum rutschen: dann gilt die Merkpunkt-Ausrichtung
    if np.mean(list(rest_icp.values())) > np.mean(list(rest_umeyama.values())) + 3.0:
        print(name, "ICP verworfen (Merkpunkte danach im Mittel", round(float(np.mean(list(rest_icp.values()))), 1), "mm daneben)")
        scan.vertices, Qt = scan_vor, Qt_vor
        rest_icp = {"verworfen": True}

    # ---------------------------------------------------------------- nicht-starre Einpassung
    L = laplace(n, tri).tocsr()
    idx_lm = {k: int(np.argmin(np.linalg.norm(S - P[k], axis=1))) for k in namen}
    D = np.zeros_like(S)
    rest = {}
    for runde, (alpha, beta) in enumerate([(60, 8), (25, 4), (10, 2), (4, 1), (2, 0.5), (1, 0.2)]):
        X = S + D
        c, dist, tid = trimesh.proximity.closest_point(scan, X[frei])
        nscan = scan.face_normals[tid]
        nq = (N_S[frei] * nscan).sum(1)
        wd = rand[frei] * daten_auge[frei] * (dist < 10.0) * (nq > 0.34)
        W = np.zeros(n)
        W[frei] = wd
        ziel = np.zeros_like(S)
        ziel[frei] = c - S[frei]
        Bl = np.zeros(n)
        zl = np.zeros_like(S)
        for k, i in idx_lm.items():
            bk = beta * (0.1 if k.startswith(("en", "ex")) else 1.0)
            Bl[i] += bk
            zl[i] += bk * (Qt[k] - S[i])
        # Ecken außerhalb des Bereichs festhalten (D = 0): großes Gewicht auf 0
        fest = np.where(rand > 0.01, 0.0, 1e4)
        M = (alpha * L + sparse.diags(W + Bl + fest)).tocsc()
        loese = factorized(M)
        rhs = W[:, None] * ziel + zl
        D = np.stack([loese(rhs[:, k]) for k in range(3)], 1)
        gut = wd > 0
        rest[runde] = {"alpha": alpha, "mittel_mm": round(float(dist[gut].mean()), 2), "p90_mm": round(float(np.percentile(dist[gut], 90)), 2),
                       "anteil_daten": round(float(gut.mean()), 3)}
        print(name, "Runde", runde, rest[runde])
    X = S + D
    c, dist, tid = trimesh.proximity.closest_point(scan, X[frei])
    gut = (rand[frei] * daten_auge[frei] > 0.5)
    rest["ende"] = {"mittel_mm": round(float(dist[gut].mean()), 2), "p90_mm": round(float(np.percentile(dist[gut], 90)), 2)}
    # leicht glätten (nur die große Form; kleine Buckel aus Scanfehlern weg): (I + 2 L) D_glatt = D
    loese_g = factorized((sparse.identity(n) + 2.0 * L).tocsc())
    D = np.stack([loese_g(D[:, k]) for k in range(3)], 1)
    versatz = D * (rand * lid_aus)[:, None]
    gesicht = rand > 0.5
    rms = float(np.sqrt((versatz[gesicht] ** 2).sum(1).mean()))
    bericht = {"name": name, "geschlecht": g, "quelle": vb.get("quelle"), "lizenz": vb.get("lizenz"),
               "massstab": float(s_), "rest_umeyama_mm": rest_umeyama, "rest_nach_icp_mm": rest_icp, "runden": rest, "rms_versatz_mm": round(rms, 2),
               "max_versatz_mm": round(float(np.linalg.norm(versatz, axis=1).max()), 2),
               "merkpunkte_px": px}
    print(name, "RMS Versatz", round(rms, 2), "mm, max", bericht["max_versatz_mm"], "mm, Rest", rest["ende"])
    np.savez_compressed(os.path.join(aus, f"vorbild_{name}.npz"), basis=haut.grund.astype(np.float32),
                        versatz=(versatz / 1000.0).astype(np.float32), name=name)
    # Zur Sichtprüfung: Scan nach der Ausrichtung (mm) und das eingepasste Netz
    np.savez_compressed(os.path.join(aus, f"vorbild_{name}_pruef.npz"), scan_v=scan.vertices.astype(np.float32),
                        scan_f=scan.faces, netz=S.astype(np.float32), eingepasst=(S + versatz).astype(np.float32),
                        dreiecke=tri)
    json.dump(bericht, open(os.path.join(aus, f"vorbild_{name}.json"), "w", encoding="utf-8"), indent=1, ensure_ascii=False)


if __name__ == "__main__":
    main()
