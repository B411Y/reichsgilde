"""Form von Pferd B (Schritt a2): gezielte Verschiebungsfelder in der ausgerichteten Idle-Pose, zurueckgerechnet in die Rohecken (Bindepose), so dass Gelenke, Gewichte und
Clips unveraendert bleiben. Reines numpy (laeuft mit normalem Python): python -I tools/modelle/pferd2_form_b.py <aus.npz> [--ziel=1.6] [--rohr=1.15] [--tiefe=0.05] ...

Pro Eckenwolke: Welt-Ecken P (Idle, ausgerichtet) -> Feld D(P) (Meter) -> Rohverschiebung d_raw = L^-1 D mit L = Mn3 @ C @ M3 (M3 = gemischte 3x3 der Haut).
Jedes Feld wirkt auf ALLE Netze (Koerper, Maehne, Augen, Hufeisen) gleich, so bleiben die Teile zusammen.
"""
import json
import os
import sys

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import pferd2_b as B  # noqa: E402

ARGS = sys.argv[1:]
OPT = dict(a[2:].split("=", 1) if "=" in a else (a[2:], "1") for a in ARGS if a.startswith("--"))
POS = [a for a in ARGS if not a.startswith("--")]
C = np.array([[1, 0, 0], [0, 0, -1], [0, 1, 0.0]])          # glTF (x, y, z) -> Blender (x, -z, y)


def glatt(t):
    t = np.clip(t, 0.0, 1.0)
    return t * t * (3 - 2 * t)


def leg_zentren(Pk, y_mitte, zs, seite, vorn):
    """Mittelpunkt (x, y) des Beins je Hoehe aus den Koerperecken des Beins (Seite x>0 oder x<0, vorn y<y_mitte)."""
    sel = (np.sign(Pk[:, 0]) == seite) & ((Pk[:, 1] < y_mitte) == vorn)
    Q = Pk[sel]
    aus = []
    for z in zs:
        b = Q[np.abs(Q[:, 2] - z) < 0.03]
        # nur das Bein (nicht Fesselbehang-Nachbarn): Median
        aus.append((float(np.median(b[:, 0])), float(np.median(b[:, 1]))) if len(b) else (np.nan, np.nan))
    return np.array(aus)


def feld(P, opt, lm, ziel):
    """Verschiebung D (n,3) in Metern fuer Punkte P (ausgerichtete Idle-Pose). lm: Landmarken."""
    D = np.zeros_like(P)
    # 1. Roehrbeine kraeftiger: radial um die Beinachse, zwischen Fessel und Karpus/Sprunggelenk weich ein- und ausgeblendet. rohr_v / rohr_h (Faktor vorn / hinten)
    for (seite, vorn), (zs, ctr) in lm["beine"].items():
        k = float(opt.get("rohr_v" if vorn else "rohr_h", 1.0)) - 1.0
        if abs(k) < 1e-9:
            continue
        zlo, zhi = float(opt.get("rohr_zlo", lm["rohr_z"][0])), lm["rohr_z"][1]
        sel = (np.sign(P[:, 0]) == seite) & ((P[:, 1] < lm["y_mitte"]) == vorn) & (P[:, 2] > zlo - 0.05) & (P[:, 2] < zhi + 0.05)
        if not sel.any():
            continue
        z = P[sel, 2]
        cx, cy = np.interp(z, zs, ctr[:, 0]), np.interp(z, zs, ctr[:, 1])
        f = k * glatt((z - zlo) / 0.07) * glatt((zhi - z) / 0.07)
        D[sel, 0] += (P[sel, 0] - cx) * f
        D[sel, 1] += (P[sel, 1] - cy) * f
    # 2. Tiefere Brust: Bauchlinie nach unten, Ruecken fest, nach unten auslaufend (Beinansaetze), vorn und hinten ausklingend
    a = float(opt.get("tiefe", 0.0))
    if a:
        zt, zb = lm["z_back"], lm["z_belly"]
        yb, yg = lm["y_brust"], lm["y_gesaess"]
        inrumpf = glatt((P[:, 1] - (yb - 0.02)) / 0.22) * glatt((yb + 0.62 * (yg - yb) - P[:, 1]) / 0.35)
        hoch = glatt((zt - P[:, 2]) / (zt - zb))
        tief = np.where(P[:, 2] < zb, glatt(1.0 - (zb - P[:, 2]) / 0.22), 1.0)
        D[:, 2] -= a * hoch * tief * inrumpf * (np.abs(P[:, 0]) < 0.40)
    # 3. Breiter (x-Streckung um die Mittellinie): brust, rumpf, kruppe
    for name, yc, br in (("brust", lm["y_brust"] + 0.12, float(opt.get("brust_br", 0.25))), ("gurt", lm["y_widerrist"] + 0.12, 0.32), ("rumpf", 0.5 * (lm["y_brust"] + lm["y_gesaess"]), 0.40), ("kruppe", lm["y_gesaess"] - 0.25, float(opt.get("kruppe_br", 0.25)))):
        kk = float(opt.get(name, 0.0))
        if kk:
            fy = glatt(1.0 - np.abs(P[:, 1] - yc) / br)
            fz = glatt((P[:, 2] - lm["z_belly"] * 0.85) / 0.2) * glatt((lm["z_back"] + 0.06 - P[:, 2]) / 0.06)
            D[:, 0] += P[:, 0] * kk * fy * fz
    # 3b. Kruppe: oben flacher und runder, leichte Doppelkruppe (Mittellinie 1,5 cm tiefer, Seiten bleiben)
    kf = float(opt.get("kruppe_furche", 0.0))
    if kf:
        fy = glatt(1.0 - np.abs(P[:, 1] - (lm["y_gesaess"] - 0.25)) / 0.30)
        fz = glatt((P[:, 2] - (lm["z_back"] - 0.15)) / 0.10)
        D[:, 2] -= kf * (1.0 - glatt(np.abs(P[:, 0]) / 0.10)) * fy * fz
    # 3c. Hufe breiter (alle vier): radial um die Beinachse, am Boden am staerksten
    kh = float(opt.get("huf", 0.0))
    if kh:
        for (seite, vorn), (zs, ctr) in lm["beine"].items():
            sel = (np.sign(P[:, 0]) == seite) & ((P[:, 1] < lm["y_mitte"]) == vorn) & (P[:, 2] < 0.14)
            if sel.any():
                z = P[sel, 2]
                cx, cy = np.interp(z, zs, ctr[:, 0]), np.interp(z, zs, ctr[:, 1])
                f = kh * glatt((0.14 - z) / 0.09)
                D[sel, 0] += (P[sel, 0] - cx) * f
                D[sel, 1] += (P[sel, 1] - cy) * f
    # 4b. Kamm: obere Haelfte des Halses hoeher (nicht die Kehle): z-Streckung ueber der Halsachse
    kk2 = float(opt.get("kamm", 0.0))
    if kk2:
        hy, hz = lm["hals_y"], lm["hals_z"]
        c = np.interp(P[:, 1], hy, hz)
        fy = glatt((P[:, 1] - hy[0]) / 0.20) * glatt((hy[-1] - P[:, 1]) / 0.22)
        fz = glatt((P[:, 2] - c) / 0.10) * (P[:, 2] > 1.0)
        D[:, 2] += kk2 * np.maximum(P[:, 2] - c, 0.0) * fy * fz
    # 4. Hals dicker: radial um die Halsachse (x und z), nur im Halsbereich, an Kopf und Brust weich auslaufend; Laenge und Kopf bleiben
    kk = float(opt.get("hals", 0.0))
    if kk:
        hy, hz = lm["hals_y"], lm["hals_z"]
        c = np.interp(P[:, 1], hy, hz)
        fy = glatt((P[:, 1] - hy[0]) / 0.10) * glatt((hy[-1] - P[:, 1]) / 0.10)
        fz = glatt((P[:, 2] - 1.0) / 0.12)
        f = kk * fy * fz
        D[:, 0] += P[:, 0] * f
        D[:, 2] += (P[:, 2] - c) * f
    # 5. Runde 5: Halsunterseite/Halsansatz voller bei FESTER Oberlinie (z-Streckung nach unten von der Oberlinie aus, zur Schulter hin staerker), Hals breiter
    ku = float(opt.get("hals_unten", 0.0))
    kx = float(opt.get("hals_x", 0.0))
    if ku or kx:
        hy, hz, ht = lm["hals_y"], lm["hals_z"], lm["hals_top"]
        top = np.interp(P[:, 1], hy, ht)
        fy = glatt((P[:, 1] - hy[0]) / 0.20) * glatt((hy[-1] + 0.12 - P[:, 1]) / 0.15)
        fz = glatt((P[:, 2] - 1.12) / 0.15) * (P[:, 2] <= top + 0.02)
        basis = 1.0 + float(opt.get("hals_basis", 0.9)) * glatt((P[:, 1] - (hy[0] + 0.20)) / 0.30)
        D[:, 2] -= np.minimum(ku * np.maximum(top - P[:, 2], 0.0) * fy * fz * basis, float(opt.get("hals_cap", 0.09)))
        D[:, 0] += P[:, 0] * kx * fy * fz * basis
    # 6. Brust nach vorn (Seitenansicht), Schulter breiter
    bv = float(opt.get("brustvor", 0.0))
    if bv:
        fw = glatt(1.0 - np.abs(P[:, 1] - (lm["y_brust"] + 0.10)) / 0.25) * glatt((P[:, 2] - 0.75) / 0.15) * glatt((1.42 - P[:, 2]) / 0.15)
        D[:, 1] -= bv * fw
    ks = float(opt.get("schulter", 0.0))
    if ks:
        fy = glatt(1.0 - np.abs(P[:, 1] - (lm["y_brust"] + 0.32)) / 0.24)
        fz = glatt((P[:, 2] - 0.85) / 0.15) * glatt((1.45 - P[:, 2]) / 0.12)
        D[:, 0] += P[:, 0] * ks * fy * fz
    # 7. Unterarm und Hosen kraeftiger, Fesselkopf ausgestellt (nach hinten, Geometrie ohne Haarkarten)
    for (seite, vorn), (zs, ctr) in lm["beine"].items():
        k = float(opt.get("arm_v" if vorn else "arm_h", 0.0))
        fl = float(opt.get("fessel", 0.0))
        if not (k or fl):
            continue
        zlo, zhi = (0.46, 0.80) if vorn else (0.56, 0.90)
        sel = (np.sign(P[:, 0]) == seite) & ((P[:, 1] < lm["y_mitte"]) == vorn) & (P[:, 2] < 0.95)
        if not sel.any():
            continue
        z = P[sel, 2]
        cx, cy = np.interp(z, zs, ctr[:, 0]), np.interp(z, zs, ctr[:, 1])
        if k:
            f = k * glatt((z - zlo) / 0.08) * glatt((zhi - z) / 0.08)
            D[sel, 0] += (P[sel, 0] - cx) * f
            D[sel, 1] += (P[sel, 1] - cy) * f
        if fl:
            f = glatt((z - 0.05) / 0.05) * glatt((0.24 - z) / 0.07)
            s = (P[sel, 1] - cy) / (np.abs(P[sel, 1] - cy) + 0.02)
            D[sel, 1] += fl * f * (0.5 + 0.5 * s)
            D[sel, 0] += (P[sel, 0] - cx) * 0.25 * f
    return D


def landmarken(Q, F, ziel):
    zh = 0.61 * ziel
    sz = B.schnitt(Q, F, 2, zh)
    sz = sz[np.abs(sz[:, 0]) > 0.03 * ziel]
    yb, yg = float(sz[:, 1].min()), float(sz[:, 1].max())
    yw = yb + 0.23 * (yg - yb)
    z_back = B.oben_bei(Q, F, yb + 0.45 * (yg - yb))
    s = B.schnitt(Q, F, 1, yb + 0.35 * (yg - yb))
    s = s[(np.abs(s[:, 0]) < 0.16) & (s[:, 2] > 0.5)]
    z_belly = float(s[:, 2].min())
    y_mitte = 0.5 * (yb + yg)
    zs = np.arange(0.03, 0.66, 0.03)
    beine = {}
    for seite in (1, -1):
        for vorn in (True, False):
            sel = (np.sign(Q[:, 0]) == seite) & ((Q[:, 1] < y_mitte) == vorn)
            R = Q[sel]
            ctr = []
            for z in zs:
                b = R[np.abs(R[:, 2] - z) < 0.035]
                ctr.append((float(np.median(b[:, 0])), float(np.median(b[:, 1]))) if len(b) else (np.nan, np.nan))
            ctr = np.array(ctr)
            ok = ~np.isnan(ctr[:, 0])
            beine[(seite, vorn)] = (zs[ok], ctr[ok])
    hy, hz = [], []
    for y in np.arange(-0.95, -0.42, 0.04):
        s = B.schnitt(Q, F, 1, float(y))
        s = s[(np.abs(s[:, 0]) < 0.25) & (s[:, 2] > 1.0)]
        if len(s):
            hy.append(float(y))
            hz.append(0.5 * (float(s[:, 2].max()) + float(s[:, 2].min())))
    ht = []
    for y in hy:
        s = B.schnitt(Q, F, 1, float(y))
        s = s[(np.abs(s[:, 0]) < 0.25) & (s[:, 2] > 1.0)]
        ht.append(float(s[:, 2].max()))
    return dict(hals_top=np.array(ht), hals_y=np.array(hy), hals_z=np.array(hz), y_brust=yb, y_gesaess=yg, y_widerrist=yw, z_back=z_back, z_belly=z_belly, y_mitte=y_mitte, beine=beine, rohr_z=(0.14, 0.45))


def main():
    ziel = float(OPT.get("ziel", 1.6))
    H = B.Haut()
    k = next(i for i, p in enumerate(H.prims) if p["mat"] == "Horse")
    J0 = H.gelenkmatrizen("Skeleton|1 Ilde", 0.0)
    normpfad = OPT.get("norm", "C:/Reichsgilde_Austausch/pferd2/schritt_a2/roh/henry_norm.json")
    if os.path.exists(normpfad):
        mn = B.lade_norm(normpfad)
    else:
        w, _ = H.haut(H.prims[k], J0)
        mn = B.ausrichtung(H, B.blender_koord(w), H.prims[k]["idx"], ziel)
        B.speichere_norm(normpfad, mn)
    w, _ = H.haut(H.prims[k], J0)
    Qk = B.anwenden(mn, B.blender_koord(w))
    lm = landmarken(Qk, H.prims[k]["idx"], ziel)
    print("LANDMARKEN", {a: (round(b, 3) if isinstance(b, float) else b) for a, b in lm.items() if a not in ("beine", "hals_y", "hals_z", "hals_top")})
    wb, _ = H.haut(H.prims[k], J0)
    Pb = B.anwenden(mn, B.blender_koord(wb))
    Pb_neu = Pb + feld(Pb, OPT, lm, ziel)
    out = {"n": len(H.prims)}
    for i, p in enumerate(H.prims):
        w, m3 = H.haut(p, J0)
        P = B.anwenden(mn, B.blender_koord(w))
        if p["mat"] == "Horseshoe" and float(OPT.get("eisen", 0.0)):
            D = np.zeros_like(P)
            for (seite, vorn), (zs, ctr) in lm["beine"].items():
                sel = (np.sign(P[:, 0]) == seite) & ((P[:, 1] < lm["y_mitte"]) == vorn)
                cx, cy = np.interp(P[sel, 2], zs, ctr[:, 0]), np.interp(P[sel, 2], zs, ctr[:, 1])
                D[sel, 0] += (P[sel, 0] - cx) * float(OPT["eisen"])
                D[sel, 1] += (P[sel, 1] - cy) * float(OPT["eisen"])
        elif p["mat"] == "Hair":
            # Maehne/Schweif: nicht mit der Halshoehe strecken, sondern am Kamm verankern (nur um die Anhebung des Kamms verschieben)
            oh = {k: v for k, v in OPT.items() if k not in ("kamm", "hals", "hals_unten", "hals_x")}
            D = feld(P, oh, lm, ziel)
            if float(OPT.get("einbett", 0.0)):
                # Wurzeln der Haarkarten ein Stueck in den Hals versenken (obere Kartenkante wird vom Fell verdeckt, kein "Buerstenrand")
                Z = P + D
                d_min = np.full(len(Z), 9.0)
                nn = np.zeros(len(Z), int)
                for a in range(0, len(Z), 500):
                    dd = np.linalg.norm(Z[a:a + 500, None, :] - Pb_neu[None, :, :], axis=2)
                    nn[a:a + 500] = dd.argmin(1)
                    d_min[a:a + 500] = dd.min(1)
                u = (Pb_neu[nn] - Z) / np.maximum(d_min, 1e-6)[:, None]
                wgt = glatt(1.0 - d_min / float(OPT.get("einbett_r", 0.05))) * glatt((Z[:, 1] + 0.95) / 0.06) * glatt((-0.40 - Z[:, 1]) / 0.08)      # nur am Hals (Kamm), nicht an Stirn/Ohr/Stirnlocke
                D += u * ((d_min + float(OPT["einbett"])) * wgt)[:, None]
            kk2 = float(OPT.get("kamm", 0.0))
            if kk2:
                hy, hz, ht = lm["hals_y"], lm["hals_z"], lm["hals_top"]
                c = np.interp(P[:, 1], hy, hz)
                fy = glatt((P[:, 1] - hy[0]) / 0.20) * glatt((hy[-1] - P[:, 1]) / 0.22)
                D[:, 2] += kk2 * np.maximum(np.interp(P[:, 1], hy, ht) - c, 0.0) * fy * (P[:, 2] > 1.0)
        else:
            D = feld(P, OPT, lm, ziel)
        L = mn[:3, :3] @ C @ m3                              # (n,3,3) via Broadcast
        L = np.einsum("ij,njk->nik", mn[:3, :3] @ C, m3)
        d_raw = np.linalg.solve(L, D[:, :, None])[:, :, 0]
        out[f"mat{i}"], out[f"pos{i}"], out[f"uv{i}"], out[f"jo{i}"], out[f"w{i}"], out[f"idx{i}"] = p["mat"], p["pos"] + d_raw, p["uv"], p["jo"], p["w"], p["idx"]
        print("FELD", p["mat"], "max |D| (mm)", round(float(np.abs(D).max()) * 1000, 1))
    np.savez_compressed(POS[0], **out)
    print("GESCHRIEBEN", POS[0])


if __name__ == "__main__":
    main()










