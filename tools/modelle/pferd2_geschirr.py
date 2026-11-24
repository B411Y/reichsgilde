"""Geschirr fuer Pferd B (Eigenbau): Kummet mit Hamen und Zughaken am Halsansatz, Bauchgurt. Haengt als eigenes Netz "Geschirr" an das Paket (npz) des Pferds,
mit eigenen Ecken, Gewichten und UV in einem Farbfeld; das Pferd-Netz bleibt unveraendert (Dreiecke werden getrennt gezaehlt).

    python -I tools/modelle/pferd2_geschirr.py <paket_in.npz> <Stehen_J.npz> <Ziehen_gehen_J.npz> <paket_out.npz> [--hoehe=1.40] [--info=<json>]

Ablauf: Das Geschirr wird in der Stehpose (Stehen Bild 0 = Ruhepose des GLB) in normierten Weltkoordinaten (Meter, z hoch, y nach hinten) gebaut: Kummet = elliptischer, gepolsterter
Ring senkrecht zur Halsachse (Knochen Hals 0 -> Hals 1), Hamen = zwei Holzstangen aussen am Ring, Zughaken = je ein Eisenstift in der Mitte der Hamen, Bauchgurt = Band um den Rumpf
hinter den Vorderbeinen. Die Lage des Rings entlang des Halses wird so gesucht, dass seine Mitte im Ziehen im Mittel auf `hoehe` (Standard 1,40 m, Hoehe der Deichsel am
Pferdekarren, karren.py) liegt: das ist zugleich der Zugpunkt (Anker_Geschirr). Gewichte und Knochen kopiert jede Ecke von der naechsten Ecke des Koerpers (das Geschirr verformt
sich wie die Haut darunter); Rohecken = inverse Hautmatrix der Ecke. Material "Geschirr": UV zeigen in vier Farbfelder (Leder, Holz, Eisen, Gurt), siehe pferd2_spiel.py.
"""
import json
import os
import sys

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import pferd2_b as B  # noqa: E402
import pferd2_gang as G  # noqa: E402

OPT = {a.split("=")[0][2:]: (a.split("=", 1)[1] if "=" in a else "1") for a in sys.argv[1:] if a.startswith("--")}
POS = [a for a in sys.argv[1:] if not a.startswith("--")]

# Farbfelder im UV des Netzes "Geschirr" (Quadranten, Mitte jeweils bei +-0,25 um 0,5)
FELD = {"leder": (0.25, 0.25), "holz": (0.75, 0.25), "eisen": (0.25, 0.75), "gurt": (0.75, 0.75)}


def rohr(punkte, radius, seiten, feld, geschlossen=False, kappen=True, breit=None):
    """Rohr (Dreiecke) entlang einer Linie von Punkten; radius: Zahl oder Liste je Punkt. Gibt (P, F, UV) zurueck."""
    punkte = np.asarray(punkte, float)
    n = len(punkte)
    rad = np.full(n, float(radius)) if np.isscalar(radius) else np.asarray(radius, float)
    t = np.zeros_like(punkte)
    for i in range(n):
        a, b = punkte[(i - 1) % n] if geschlossen else punkte[max(i - 1, 0)], punkte[(i + 1) % n] if geschlossen else punkte[min(i + 1, n - 1)]
        t[i] = (b - a) / max(np.linalg.norm(b - a), 1e-9)
    ref = np.array([0.0, 0.0, 1.0])
    P, F = [], []
    nrm0 = np.cross(t[0], ref)
    if np.linalg.norm(nrm0) < 1e-6:
        nrm0 = np.cross(t[0], [1.0, 0.0, 0.0])
    nrm = nrm0 / np.linalg.norm(nrm0)
    for i in range(n):
        nrm = nrm - np.dot(nrm, t[i]) * t[i]
        nrm /= np.linalg.norm(nrm)
        bin_ = np.cross(t[i], nrm)
        for s in range(seiten):
            w = 2 * np.pi * s / seiten
            o = rad[i] * (np.cos(w) * nrm + np.sin(w) * bin_)
            if breit is not None:                                      # Querschnitt gestreckt: Anteil von o in Richtung breit[0] mal breit[1]
                ax_ = np.asarray(breit[0], float)
                o = o + (breit[1] - 1.0) * np.dot(o, ax_) * ax_
            P.append(punkte[i] + o)
    for i in range(n - (0 if geschlossen else 1)):
        i2 = (i + 1) % n
        for s in range(seiten):
            s2 = (s + 1) % seiten
            a, b, c, d = i * seiten + s, i * seiten + s2, i2 * seiten + s2, i2 * seiten + s
            F += [[a, b, c], [a, c, d]]
    if kappen and not geschlossen:
        for ende, rev in ((0, False), (n - 1, True)):
            m = len(P)
            P.append(punkte[ende])
            for s in range(seiten):
                s2 = (s + 1) % seiten
                a, b = ende * seiten + s, ende * seiten + s2
                F.append([m, a, b] if rev else [m, b, a])
    P = np.array(P)
    uv = np.tile(np.array(FELD[feld]), (len(P), 1))
    return P, np.array(F), uv


def ring_punkte(c, ex, eu, a, b, n, w0=0.0, w1=2 * np.pi):
    w = np.linspace(w0, w1, n, endpoint=False if w1 - w0 >= 2 * np.pi - 1e-9 else True)
    return c + np.cos(w)[:, None] * a * ex + np.sin(w)[:, None] * b * eu


def zusammen(teile):
    P, F, U, o = [], [], [], 0
    for p, f, u in teile:
        P.append(p)
        F.append(f + o)
        U.append(u)
        o += len(p)
    return np.vstack(P), np.vstack(F), np.vstack(U)


R_POLSTER = 0.045                                                    # Halbmesser des Polsters (9 cm dick)


def kontur_ring(c, e1, e2, r, versatz_n=None, breit=0.0, dick=0.012):
    """Band mit Rechteckquerschnitt entlang einer Kontur (Radius r[i] je Winkel in der Ebene e1/e2 um c); Breite `breit` quer zur Ebene (Richtung versatz_n), Dicke `dick` nach aussen."""
    n = len(r)
    P, F = [], []
    ang = 2 * np.pi * np.arange(n) / n
    d = np.cos(ang)[:, None] * e1 + np.sin(ang)[:, None] * e2
    innen = c + d * r[:, None]
    aussen = c + d * (r + dick)[:, None]
    for s_ in (-0.5, 0.5):
        P.append(innen + s_ * breit * versatz_n)
        P.append(aussen + s_ * breit * versatz_n)
    P = np.vstack(P)                                                    # Blocks: innen-, aussen- (Rand 1), innen+, aussen+ (Rand 2)
    A0, A1, B0, B1 = (np.arange(n) + k * n for k in range(4))
    for i in range(n):
        j = (i + 1) % n
        # aussen (Flaeche von aussen, Rand 1 -> Rand 2)
        F += [[A1[i], A1[j], B1[j]], [A1[i], B1[j], B1[i]]]
        # innen
        F += [[A0[i], B0[i], B0[j]], [A0[i], B0[j], A0[j]]]
        # Rand 1 und Rand 2 (Kanten)
        F += [[A0[i], A1[j], A1[i]], [A0[i], A0[j], A1[j]]]
        F += [[B0[i], B1[i], B1[j]], [B0[i], B1[j], B0[j]]]
    uv = np.tile(np.array(FELD["gurt"]), (len(P), 1))
    return P, np.array(F)[:, ::-1].copy(), uv                            # Umlaufsinn so, dass die Normalen nach aussen zeigen


def baue(c, ex, eu, rr, gurt, mit_gurt=True, nrm=None):
    """Geschirr-Teile: Kummet = geschlossenes Polster entlang der Kontur rr (Halbmesser der Ringmitte je Winkel, Birnenform, in der Ebene ex/eu um c), Hamen (Holzbuegel) und Zughaken daran, Bauchgurt."""
    teile = []
    r_p = R_POLSTER
    n = len(rr)
    pts = np.array([c + np.cos(2 * np.pi * i / n) * rr[i] * ex + np.sin(2 * np.pi * i / n) * rr[i] * eu for i in range(n)])
    teile.append(rohr(pts, r_p, 8, "leder", geschlossen=True, breit=(np.cross(ex, eu), 1.7)))          # Polster 9 cm im Ring und 15 cm in Laufrichtung dick
    # Hamen: zwei Holzbuegel aussen am Polster (von unten nach oben an der Seite, ein Stueck ueber dem Polster endend), Zughaken in der Mitte
    for s in (+1, -1):
        w = np.linspace(-0.72, 0.80, 8)                        # Hamen enden seitlich am Polster (unten nicht ueber die Brustmitte hinaus, kein Stab unter dem Polster)
        hp = []
        for x in w:
            i = int(round((((0.0 if s > 0 else np.pi) + x) % (2 * np.pi)) / (2 * np.pi) * n)) % n
            ang = 2 * np.pi * i / n
            d = np.cos(ang) * ex + np.sin(ang) * eu
            hp.append(c + d * (rr[i] + r_p * 0.9 + 0.02))
        hp = np.array(hp)
        if hp[0] @ eu > hp[-1] @ eu:
            hp = hp[::-1]
        hp[-1] += 0.025 * eu
        teile.append(rohr(hp, 0.026, 6, "holz"))
        # Zughaken: Eisenhaken in der Seitenmitte (Winkel 0 bzw. 180 Grad), nach aussen und oben gekruemmt
        i0 = 0 if s > 0 else n // 2
        ang = 2 * np.pi * i0 / n
        d = np.cos(ang) * ex + np.sin(ang) * eu
        h0 = c + d * (rr[i0] + r_p * 0.9 + 0.02)
        teile.append(rohr([h0, h0 + d * 0.06, h0 + d * 0.085 + 0.03 * eu], 0.022, 6, "eisen"))
    if mit_gurt:
        cg, rg, ey, ez = gurt
        P, F, U = kontur_ring(cg, ey, ez, rg, versatz_n=np.array([0.0, 1.0, 0.0]), breit=0.09, dick=0.012)
        teile.append((P, F, U))
    return zusammen(teile)


def main():
    sk = G.Skelett()
    d = np.load(POS[0], allow_pickle=True)
    n = int(d["n"])
    prims = [dict(mat=str(d[f"mat{i}"]), pos=d[f"pos{i}"], uv=d[f"uv{i}"], jo=d[f"jo{i}"], w=d[f"w{i}"], idx=d[f"idx{i}"]) for i in range(n)]
    J0 = np.load(POS[1])["J"][0].astype(float)
    Jz = np.load(POS[2])["J"].astype(float)
    horse = next(p for p in prims if p["mat"] == "Horse")
    wk, _ = sk.H.haut(horse, J0)
    K = B.anwenden(sk.mn, B.blender_koord(wk))
    a0 = sk.norm(sk.global_()[sk.id["BN_Neck_00_06_06"]][:3, 3])
    a1 = sk.norm(sk.global_()[sk.id["BN_Neck_01_07_07"]][:3, 3])
    ax = (a1 - a0) / np.linalg.norm(a1 - a0)
    ex = np.array([1.0, 0.0, 0.0])
    phi = np.radians(float(OPT.get("neigung", 28.0)))                  # Ringebene: Normale zeigt nach vorn und um phi nach oben (ein Kummet steht fast senkrecht, nicht senkrecht zum Hals)
    nrm = np.array([0.0, -np.cos(phi), np.sin(phi)])
    eu = np.cross(nrm, ex)
    eu /= np.linalg.norm(eu)
    mit_gurt = "ohne-gurt" not in OPT
    ex = np.array([1.0, 0.0, 0.0])

    def kontur(c, e1, e2, nrm_, dicke, luft, n=40, rmax=0.5, zmin=0.9):
        """Aeussere Huelle des Koerpers in der Scheibe |(v-c).nrm_| < dicke, polar um c in der Ebene e1/e2 (Radius je Winkel, geglaettet)."""
        rel = K - c
        sel = (np.abs(rel @ nrm_) < dicke) & (K[:, 2] > zmin) & (np.linalg.norm(rel - np.outer(rel @ nrm_, nrm_), axis=1) < rmax)
        u, v = rel[sel] @ e1, rel[sel] @ e2
        rad, ang = np.hypot(u, v), np.arctan2(v, u) % (2 * np.pi)
        r = np.zeros(n)
        for i in range(n):
            a = 2 * np.pi * i / n
            dw = np.abs((ang - a + np.pi) % (2 * np.pi) - np.pi)
            m = dw < np.radians(7.5)
            r[i] = rad[m].max() if m.any() else np.nan
        ok = ~np.isnan(r)
        r = np.interp(np.arange(n), np.arange(n)[ok], r[ok], period=n)
        for _ in range(3):                                            # glaetten (Mittel mit den Nachbarn), Huelle nie unterschreiten
            r = np.maximum(r, (np.roll(r, 1) + 2 * r + np.roll(r, -1)) / 4)
        return r + luft

    # Bauchgurt: Rumpfscheibe bei y = -0.12 (hinter den Ellbogen), Band um die Huelle
    y0 = float(OPT.get("gurt_y", -0.12))
    ey, ez = np.array([1.0, 0.0, 0.0]), np.array([0.0, 0.0, 1.0])
    sg = K[(abs(K[:, 1] - y0) < 0.05) & (K[:, 2] > 0.7)]
    cg = np.array([(sg[:, 0].min() + sg[:, 0].max()) / 2, y0, (sg[:, 2].min() + sg[:, 2].max()) / 2])
    rg = kontur(cg, ey, ez, np.array([0.0, 1.0, 0.0]), 0.05, 0.002, n=40, rmax=0.6, zmin=0.7)
    gurt = (cg, rg, ey, ez)

    # Kummet: Ebene durch Widerristvorderkante (oben) und Bugspitze (unten), um phi aus der Senkrechten; Polster liegt in der Huelle des Halses und der Brust (Birnenform)
    phi = np.radians(float(OPT.get("neigung", 25.0)))
    nrm = np.array([0.0, -np.cos(phi), np.sin(phi)])
    eu = np.cross(nrm, ex)
    eu /= np.linalg.norm(eu)
    c = np.array([float(OPT.get("cx", -0.049)), float(OPT.get("cy", -0.64)), float(OPT.get("cz", 1.37))])
    rr = kontur(c, ex, eu, nrm, 0.04, R_POLSTER * 0.8, n=28, rmax=0.5, zmin=0.95)

    def netz_q():
        return baue(c, ex, eu, rr, gurt, mit_gurt) + (c,)

    def posiere(P, F, U, J):
        """Gewichte von der naechsten Ecke des Koerpers kopieren, Rohecken berechnen; gibt (pos_roh, jo, w, Mtot) zurueck."""
        baum = None
        try:
            from scipy.spatial import cKDTree
            baum = cKDTree(K)
            nn = baum.query(P)[1]
        except ImportError:
            nn = np.array([int(np.argmin(((K - p) ** 2).sum(1))) for p in P])
        jo, w = horse["jo"][nn], horse["w"][nn]
        # normierte Welt -> glTF-Welt
        b = (P - sk.mn[:3, 3]) @ np.linalg.inv(sk.mn[:3, :3]).T
        wg = np.stack([b[:, 0], b[:, 2], -b[:, 1]], axis=1)
        raw = np.zeros((len(P), 3))
        Mt = np.zeros((len(P), 4, 4))
        for st in range(4):
            Mt += w[:, st, None, None] * J[jo[:, st]]
        for i in range(len(P)):
            raw[i] = np.linalg.solve(Mt[i], np.append(wg[i], 1.0))[:3]
        return raw, jo, w

    P, F, U, c = netz_q()
    raw, jo, w = posiere(P, F, U, J0)
    # Ringmitte im Ziehen (Mittel ueber alle Bilder): der Zugpunkt; der Ring selbst wippt um diese Lage
    # nur der Ring (erstes Teilnetz) bestimmt die Mitte: seine Ecken sind die ersten 22*8 (beim geschlossenen Rohr gibt es keine Kappen)
    n_ring = 28 * 8
    ring = []
    for f in range(len(Jz) - 1):
        wz, _ = sk.H.haut(dict(pos=raw[:n_ring], jo=jo[:n_ring], w=w[:n_ring]), Jz[f])
        ring.append(B.anwenden(sk.mn, B.blender_koord(wz)).mean(0))
    ring = np.array(ring)
    zug = ring.mean(0)
    wz0, _ = sk.H.haut(dict(pos=raw[:n_ring], jo=jo[:n_ring], w=w[:n_ring]), J0)
    ruhe = B.anwenden(sk.mn, B.blender_koord(wz0)).mean(0)
    # Durchdringung: Ringecken gegen die Haut in der Stehpose und in allen Bildern des Ziehens (vorzeichenbehafteter Abstand der Rohrmitte, Soll >= Polsterdicke minus Verformungsluft)
    frei_min = 9.0
    for J in [J0] + [Jz[f] for f in range(len(Jz) - 1)]:
        wk2, _ = sk.H.haut(horse, J)
        K2 = B.anwenden(sk.mn, B.blender_koord(wk2))
        N2 = B.weiche_normalen(K2, horse["idx"])
        wr, _ = sk.H.haut(dict(pos=raw[:n_ring], jo=jo[:n_ring], w=w[:n_ring]), J)
        # Mittellinie des Rings (22 Ringpunkte x 8 Rohrpunkte: Mittelpunkt je Ringpunkt)
        Pm = B.anwenden(sk.mn, B.blender_koord(wr)).reshape(28, 8, 3).mean(1)
        nn = np.array([int(np.argmin(((K2 - q) ** 2).sum(1))) for q in Pm])
        frei_min = min(frei_min, float((((Pm - K2[nn]) * N2[nn]).sum(1)).min()))
    ring_d = np.linalg.norm(ex)
    info = {"ring_frei_min_m": round(frei_min, 4), "ring_halbmesser_min_max": [round(float(rr.min()), 4), round(float(rr.max()), 4)], "haken_x_von_ringmitte": round(float(rr[0] + R_POLSTER * 0.9 + 0.02 + 0.085), 4), "ebene_neigung_grad": round(float(np.degrees(phi)), 1), "dreiecke": int(len(F)), "ring_mitte_stehen": [round(float(x), 4) for x in ruhe],
            "zugpunkt_ziehen_normiert": [round(float(x), 4) for x in zug],
            "ring_schwankung_mm": [round(float((ring.max(0) - ring.min(0))[k] * 1000), 1) for k in range(3)],
            "anker_ziehen_godot": [round(float(zug[0]), 4), round(float(zug[2]), 4), round(float(-zug[1]), 4)]}
    aus = {k: d[k] for k in d.files}
    aus["n"] = n + 1
    aus[f"mat{n}"], aus[f"pos{n}"], aus[f"uv{n}"], aus[f"jo{n}"], aus[f"w{n}"], aus[f"idx{n}"] = "Geschirr", raw.astype(np.float32), U.astype(np.float32), jo.astype(np.int32), w.astype(np.float32), F.astype(np.int32)
    np.savez_compressed(POS[3], **aus)
    if "info" in OPT:
        json.dump(info, open(OPT["info"], "w"), indent=1)
    print("GESCHIRR", json.dumps(info))


if __name__ == "__main__":
    main()
