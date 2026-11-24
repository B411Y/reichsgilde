"""Hufhalter fuer uebernommene Clips des Autors (Idle, Eat, ...): Zehenkorrektur + Hufe mit ebenem IK am Boden festhalten, ohne die Bewegung des Rumpfs zu aendern.

    python -I tools/modelle/pferd2_hufhalter.py <Ausgabeordner> --clip="Skeleton|1 Ilde" --name=Stehen [--fps=24] [--ueberblend=0.4]

Ablauf je Bild: Clip abtasten (24 fps), Zehenknochen um die Gegenneigung drehen, je Bein den Bodenkontakt aus der Sohlenlage ableiten (Kontaktlauf = Sohle < tiefste Lage + 15 mm);
im Kontaktlauf wird die Sohle auf ihre mittlere Lage im Lauf und auf den Boden (z = 0) gesetzt (weich ein- und ausgeblendet), die Beinkette (ebenes IK, Winkelziele = Winkel des Clips)
loest nach; ausserhalb bleibt die Pose des Clips. Schleife: die letzten `ueberblend` Sekunden werden zum ersten Bild hin ueberblendet, das Schlussbild ist eine Kopie von Bild 0.
Schreibt <aus>/<name>_J.npz (Gelenkmatrizen J, lokale Drehungen R, Verschiebungen T, fps) und <aus>/<name>_messung.json (Gleiten Ecke fuer Ecke, Schweben, Naht).
"""
import json
import math
import os
import sys

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import pferd2_b as B  # noqa: E402
import pferd2_bind as bd  # noqa: E402
import pferd2_gang as G  # noqa: E402
import pferd2_clipkorr as K  # noqa: E402

OPT = G.OPT
POS = G.POS
KETTEN = {
    "LF": ["BN_L_Clavicle_038_039", "BN_L_UpperArm_039_040", "BN_l_Forearm_040_041", "BN_L_Hand_041_042", "BN_L_Toe_042_043"],
    "RF": ["BN_R_Clavicle_043_045", "BN_R_UpperArm_044_046", "BN_R_Forearm_045_047", "BN_R_Hand_046_048", "BN_R_Toe_047_049"],
    "LH": ["BN_L_Thing_051_054", "BN_L_Calf_052_055", "BN_L_HorseLink_053_056", "BN_L_Foot_054_057", "BN_L_Toe_2_055_058"],
    "RH": ["BN_R_Thing_056_060", "BN_R_Calf_057_061", "BN_R_HorseLink_058_062", "BN_R_Foot_00_063", "BN_R_Toe_2_059_064"],
}


def sanft(t):
    t = np.clip(t, 0.0, 1.0)
    return t * t * (3 - 2 * t)


class Halter:
    def __init__(self):
        self.sk = G.Skelett()
        sk = self.sk
        self.G0 = sk.global_()
        self.shoe = next(p for p in sk.H.prims if p["mat"] == "Horseshoe")
        w0, _ = sk.H.haut(self.shoe, self.skin_j(self.G0))
        Q0 = B.anwenden(sk.mn, B.blender_koord(w0))
        xm = float(np.median(Q0[:, 0]))
        self.s12, self.alle, self.sohle0, self.info = {}, {}, {}, {}
        for nm in KETTEN:
            seite = 1 if nm[0] == "L" else -1
            vorn = nm[1] == "F"
            sel = np.where((np.sign(Q0[:, 0] - xm) == seite) & ((Q0[:, 1] < 0.0) == vorn))[0]
            tief = sel[np.argsort(Q0[sel, 2])[:12]]
            self.s12[nm] = tief
            self.alle[nm] = np.where((np.sign(Q0[:, 0] - xm) == seite) & ((Q0[:, 1] < 0.0) == vorn) & (Q0[:, 2] < 0.06))[0]
            self.sohle0[nm] = Q0[tief].mean(0)
            pts = [sk.norm(self.G0[sk.id[k]][:3, 3]) for k in KETTEN[nm]] + [self.sohle0[nm]]
            P2 = [(-p[1], p[2]) for p in pts]
            L = [math.hypot(P2[i + 1][0] - P2[i][0], P2[i + 1][1] - P2[i][1]) for i in range(len(P2) - 1)]
            ph = [math.atan2(P2[i + 1][0] - P2[i][0], -(P2[i + 1][1] - P2[i][1])) for i in range(len(P2) - 1)]
            selh = self.alle[nm]
            neig = math.atan(np.polyfit(-Q0[selh, 1], Q0[selh, 2], 1)[0])
            self.info[nm] = dict(pts=pts, L=L, ph0=ph, flach=ph[-1] - neig, sohle_g=G.norm_zu_g(sk, self.sohle0[nm]))

    def skin_j(self, Gm):
        sk = self.sk
        return np.array([Gm[ji] @ sk.ibm[k].T for k, ji in enumerate(sk.joints)])

    def schuhe(self, Gm):
        w, _ = self.sk.H.haut(self.shoe, self.skin_j(Gm))
        return B.anwenden(self.sk.mn, B.blender_koord(w))

    def winkel_ist(self, Gm, nm):
        """Absolute Glied-Winkel (Rad) des Beins nm aus den Gelenkpunkten und dem Sohlenpunkt im aktuellen Bild (Gm)."""
        sk = self.sk
        pts = [sk.norm(Gm[sk.id[k]][:3, 3]) for k in KETTEN[nm]]
        Q = self.schuhe(Gm)
        pts.append(Q[self.s12[nm]].mean(0))
        P2 = [(-p[1], p[2]) for p in pts]
        return np.array([math.atan2(P2[i + 1][0] - P2[i][0], -(P2[i + 1][1] - P2[i][1])) for i in range(len(P2) - 1)]), P2


def main():
    h = Halter()
    sk = h.sk
    clip = OPT["clip"]
    name = OPT.get("name", "Clip")
    aus = POS[0]
    os.makedirs(aus, exist_ok=True)
    fps = int(OPT.get("fps", 24))
    ende = max(float(sk.j["accessors"][s["input"]]["max"][0]) for s in next(a for a in sk.j["animations"] if a["name"] == clip)["samplers"])
    n = int(round(ende * fps))
    if "dauer" in OPT:
        n = int(round(float(OPT["dauer"]) * fps))
    reps = int(OPT.get("reps", 1))
    n_p = n // reps
    assert n_p * reps == n, "Bilder muessen durch die Wiederholungen teilbar sein"
    mfade = int(round(float(OPT.get("ueberblend", 0.4)) * fps))
    ZOFF = {nm: 0.0 for nm in KETTEN}
    # Basisbilder (Clip + Zehenkorrektur)
    basis = []
    for f in range(n_p):
        T, R = K.skelett_pose(sk, clip, f / n_p)
        basis.append((T, K.zehen_korrektur(sk, T, R)))
    # Schleife: letzte mfade Bilder zum ersten Bild hin ueberblenden (Drehungen: Matrix-Mischung + Orthonormierung, Verschiebungen linear)
    for k in range(mfade):
        f = n_p - mfade + k
        w = sanft((k + 1) / (mfade + 1))
        T = basis[f][0] * (1 - w) + basis[0][0] * w
        R = basis[f][1].copy()
        for i in range(R.shape[0]):
            M = basis[f][1][i] * (1 - w) + basis[0][1][i] * w
            u, s_, vt = np.linalg.svd(M)
            R[i] = u @ vt
        basis[f] = (T, R)
    basis = basis * reps
    if "rand" in OPT:                                   # Anfang und Ende gehen in 0,5 s in Bild 0 des Stehens ueber (Uebergaenge Stehen <-> Clip)
        s0 = np.load(OPT["rand"])
        R0, T0 = s0["R"][0], s0["T"][0]
        BL = int(round(0.5 * fps))
        BE = int(round(float(OPT.get("rand_ende", 0.5)) * fps))        # Laenge der Ueberblendung am Ende (laenger, um die Schreckhaltung des Autors zu vermeiden)
        wr = float(OPT.get("rumpf", 0.0))                              # Anteil, zu dem Wurzel und Wirbelsaeule in der Pose von Stehen Bild 0 gehalten werden (Rumpf des Autors senkt sich sonst zu stark)
        rn = [sk.id[k] for k in sk.id if k.startswith("BN_Root") or k.startswith("BN_Spine_0")]
        if wr > 0:
            for f in range(n):
                Rn = basis[f][1].copy(); Tn = basis[f][0].copy()
                for i in rn:
                    M = basis[f][1][i] * (1 - wr) + R0[i] * wr
                    u, s_, vt = np.linalg.svd(M)
                    Rn[i] = u @ vt
                    wt = float(OPT.get("rumpf_t", wr))                     # Verschiebung der Wurzel getrennt begrenzen (Rumpf wandert sonst relativ zu den festgehaltenen Hufen)
                    Tn[i] = basis[f][0][i] * (1 - wt) + T0[i] * wt
                basis[f] = (Tn, Rn)
        if "weide4" in OPT:                                            # Fressen v4: Hals am Ansatz nach vorn unten (planares IK ueber alle Halsknochen), Kopf behaelt seine Haltung (Genick offen), Maul vor die Vorderhufe
            hn = [sk.id[k] for k in ("BN_Neck_00_06_06", "BN_Neck_01_07_07", "BN_Neck_02_011_011", "BN_Neck_03_012_012", "BN_Head_00_016_015")]
            horse_ = next(q for q in sk.H.prims if q["mat"] == "Horse")
            w0_, _ = sk.H.haut(horse_, h.skin_j(h.G0))
            P0_ = B.anwenden(sk.mn, B.blender_koord(w0_))
            kv_ = np.where((P0_[:, 1] < -0.95) & (P0_[:, 2] > 1.0))[0]
            kn_ = kv_[np.argmin(P0_[kv_, 1])]                       # Nasenspitze
            kp_ = kv_[np.argmax(P0_[kv_, 2])]                       # Genick (hoechster Kopfpunkt)
            sub = dict(mat="Horse", pos=horse_["pos"][[kn_, kp_]], jo=horse_["jo"][[kn_, kp_]], w=horse_["w"][[kn_, kp_]], uv=None, idx=None)
            achse_g = sk.Rm.T @ np.array([1.0, 0.0, 0.0])
            achse_g = achse_g / np.linalg.norm(achse_g)
            y_t, z_t = [float(x) for x in OPT["weide4"].split(":")[:2]]
            stirn_t = float(OPT["weide4"].split(":")[2]) if len(OPT["weide4"].split(":")) > 2 else 55.0

            def pose4(f, grad):
                T_, Rn = basis[f][0], basis[f][1].copy()
                for i in hn:
                    Rn[i] = R0[i]
                for k_, i in enumerate(hn):
                    Gm = sk.global_(T_, Rn)
                    Gp = Gm[sk.eltern[i]][:3, :3]
                    Gp = Gp / np.linalg.norm(Gp, axis=0, keepdims=True)
                    ax = np.linalg.solve(Gp, achse_g)
                    ax = ax / np.linalg.norm(ax)
                    a1 = math.radians(grad[k_])
                    K_ = np.array([[0, -ax[2], ax[1]], [ax[2], 0, -ax[0]], [-ax[1], ax[0], 0]])
                    Rn[i] = (np.eye(3) + math.sin(a1) * K_ + (1 - math.cos(a1)) * (K_ @ K_)) @ Rn[i]
                return Rn

            def kopfpunkte(f, grad):
                w_, _ = sk.H.haut(sub, h.skin_j(sk.global_(basis[f][0], pose4(f, grad))))
                return B.anwenden(sk.mn, B.blender_koord(w_))
            # tiefstes Bild des Autors: Referenz fuer das Loesen
            za = []
            for f in range(n):
                Rb = basis[f][1]
                w_, _ = sk.H.haut(sub, h.skin_j(sk.global_(basis[f][0], Rb)))
                za.append(float(B.anwenden(sk.mn, B.blender_koord(w_))[0, 2]))
            za = np.array(za)
            fref = int(np.argmin(za))
            z_ruhe = float(za[0])
            vz = 1.0 if kopfpunkte(fref, np.array([10.0, 0, 0, 0, 0]))[0, 2] < kopfpunkte(fref, np.array([-10.0, 0, 0, 0, 0]))[0, 2] else -1.0
            prior = np.array([0.34, 0.30, 0.22, 0.10, 0.04])

            def res(x):
                grad = vz * x
                P = kopfpunkte(fref, grad)
                nase, gen = P[0], P[1]
                stirn = math.degrees(math.atan2(gen[2] - nase[2], -(nase[1] - gen[1]) + 1e-9))      # Winkel der Stirnlinie zum Boden (Nase vorn, Genick hinten oben)
                r = [(nase[1] - y_t) * 300.0, (nase[2] - z_t) * 300.0, (stirn - stirn_t) * float(OPT.get("wstirn", 3.0))]
                r += list((x / max(x.sum(), 1.0) - prior) * 6.0)
                return np.array(r)
            x = G.lm_loesen(res, 100.0 * prior)
            gv = vz * x
            Pk = kopfpunkte(fref, gv)
            print("WEIDE4 Grad je Knochen", np.round(x, 1), "Nase y %.3f z %.3f" % (Pk[0, 1], Pk[0, 2]), "Stirn %.0f" % math.degrees(math.atan2(Pk[1, 2] - Pk[0, 2], -(Pk[0, 1] - Pk[1, 1]))))
            for f in range(n):
                s = min(1.0, max(0.0, (z_ruhe - za[f]) / max(z_ruhe - za.min(), 1e-6)) * float(OPT.get("weide_s", 1.3)))
                s = s * s * (3 - 2 * s)
                kf = 1.0
                if s >= 0.999 and "weide_korr" in OPT:               # in der Weidephase je Bild so skalieren, dass das Maul auf der Zielhoehe liegt
                    lo_, hi_ = 0.85, 1.35
                    if kopfpunkte(f, gv * hi_)[0, 2] > z_t:
                        kf = hi_
                    elif kopfpunkte(f, gv * lo_)[0, 2] < z_t:
                        kf = lo_
                    else:
                        for _ in range(8):
                            mid = (lo_ + hi_) / 2
                            if kopfpunkte(f, gv * mid)[0, 2] > z_t:
                                lo_ = mid
                            else:
                                hi_ = mid
                        kf = (lo_ + hi_) / 2
                basis[f] = (basis[f][0], pose4(f, gv * s * kf))
        hmax = float(OPT.get("hals", 0.0))                          # Hals und Genick zusaetzlich nach unten nicken (Grad insgesamt, hoechstens --hals), bis das Maul den Boden erreicht (Zielhoehe --maul)
        if hmax != 0.0:
            hn = [sk.id[k] for k in ("BN_Neck_00_06_06", "BN_Neck_01_07_07", "BN_Neck_02_011_011", "BN_Neck_03_012_012", "BN_Head_00_016_015")]
            zmaul = float(OPT.get("maul", 0.08))
            horse_ = next(q for q in sk.H.prims if q["mat"] == "Horse")
            w0_, _ = sk.H.haut(horse_, h.skin_j(h.G0))
            P0_ = B.anwenden(sk.mn, B.blender_koord(w0_))
            kopfv = np.where((P0_[:, 1] < -0.95) & (P0_[:, 2] > 1.0))[0]
            achse_g = sk.Rm.T @ np.array([1.0, 0.0, 0.0])             # Seitenachse (glTF-Welt)
            achse_g = achse_g / np.linalg.norm(achse_g)

            def senken(f, grad):
                T_, Rn = basis[f][0], basis[f][1].copy()
                a1 = math.radians(grad) / len(hn)
                for i in hn:
                    Gm = sk.global_(T_, Rn)
                    Gp = Gm[sk.eltern[i]][:3, :3]
                    Gp = Gp / np.linalg.norm(Gp, axis=0, keepdims=True)
                    ax = np.linalg.solve(Gp, achse_g)
                    ax = ax / np.linalg.norm(ax)
                    K_ = np.array([[0, -ax[2], ax[1]], [ax[2], 0, -ax[0]], [-ax[1], ax[0], 0]])
                    Rl = np.eye(3) + math.sin(a1) * K_ + (1 - math.cos(a1)) * (K_ @ K_)
                    Rn[i] = Rl @ Rn[i]
                return Rn

            def kopf_z(f, grad):
                w_, _ = sk.H.haut(horse_, h.skin_j(sk.global_(basis[f][0], senken(f, grad))))
                return float(B.anwenden(sk.mn, B.blender_koord(w_))[kopfv, 2].min())
            vz = 1.0 if kopf_z(0, 10.0) < kopf_z(0, -10.0) else -1.0   # Vorzeichen: nach unten
            for f in range(n):
                if kopf_z(f, 0.0) <= zmaul:
                    continue
                lo_, hi_ = 0.0, vz * hmax
                if kopf_z(f, hi_) > zmaul:
                    g = hi_
                else:
                    for _ in range(9):
                        mid = (lo_ + hi_) / 2
                        if kopf_z(f, mid) > zmaul:
                            lo_ = mid
                        else:
                            hi_ = mid
                    g = hi_
                basis[f] = (basis[f][0], senken(f, g))
            if "weide" in OPT:                                         # Weidephase "a:b": Hals/Kopf-Drehungen aus dem tiefsten Bild halten (Mischung 0,85), Rand 14 Bilder weich, damit das Maul am Boden bleibt
                fa, fb = [int(x) for x in OPT["weide"].split(":")]
                fref = min(range(fa, fb), key=lambda f_: kopf_z(f_, 0.0))
                Rref = basis[fref][1].copy()
                for f in range(fa, fb):
                    w = 0.85 * sanft(min(1.0, (f - fa) / 14.0, (fb - f) / 14.0))
                    Rn = basis[f][1].copy()
                    for i in hn:
                        M = basis[f][1][i] * (1 - w) + Rref[i] * w
                        u, s_, vt = np.linalg.svd(M)
                        Rn[i] = u @ vt
                    basis[f] = (basis[f][0], Rn)
        for f in range(n):
            d_ = min(f / BL, (n - 1 - f) / BE)
            if d_ < 1.0:
                w = 1 - sanft(d_)
                Rn = basis[f][1].copy()
                for i in range(Rn.shape[0]):
                    M = basis[f][1][i] * (1 - w) + R0[i] * w
                    u, s_, vt = np.linalg.svd(M)
                    Rn[i] = u @ vt
                basis[f] = (basis[f][0] * (1 - w) + T0 * w, Rn)
    # Kontaktlaeufe je Bein aus der Sohlenlage des Basis-Clips
    sohle_s, sohle_z, sohle_zp = {nm: [] for nm in KETTEN}, {nm: [] for nm in KETTEN}, {nm: [] for nm in KETTEN}
    for f in range(n):
        Gm = sk.global_(basis[f][0], basis[f][1])
        Q = h.schuhe(Gm)
        for nm in KETTEN:
            m = Q[h.s12[nm]].mean(0)
            sohle_s[nm].append(-m[1])
            sohle_zp[nm].append(m[2])
            sohle_z[nm].append(Q[h.alle[nm]][:, 2].min())
    # Kontaktlaeufe: Sohle tief UND ruhig (< 0,06 m/s); tiefe, aber bewegte Stuecke (Rutschen im Clip) werden zu kleinen Schritten (Bogen 5 cm), hohe Stuecke bleiben wie im Clip
    kontakt, lock = {}, {}
    tgt_s, tgt_z, gew, tgt_abs = {}, {}, {}, {}
    for nm in KETTEN:
        z = np.array(sohle_z[nm])
        s = np.array(sohle_s[nm])
        tief = z < z.min() + float(OPT.get("schwelle", 0.015))
        v = np.abs(np.roll(s, -1) - np.roll(s, 1)) * fps / 2.0
        ruhig = tief & (v < float(OPT.get("vmax", 0.06)))
        # Laeufe ruhig (zyklisch), kurze Laeufe (< 3 Bilder) verwerfen
        lauf = [False] * n
        runs = []
        f = 0
        dbl = np.concatenate([ruhig, ruhig])
        k = 0
        while k < n:
            if dbl[k] and not dbl[k - 1 if k > 0 else 2 * n - 1]:
                e = k
                while e + 1 < 2 * n and dbl[e + 1]:
                    e += 1
                if e - k + 1 >= 3 and e - k + 1 < 2 * n - 1:
                    runs.append((k, min(e, k + n - 1)))
                k = e + 1
            else:
                k += 1
        if not runs and ruhig.all():
            runs = [(0, n - 1)]
        ts = np.zeros(n)
        tz = np.zeros(n)
        w = np.zeros(n)
        kont = np.zeros(n, bool)
        locks = []
        for (a, b) in runs:
            idx = [m % n for m in range(a, b + 1)]
            ls = float(np.median(s[idx]))
            locks.append((a, b, ls))
            for m in idx:
                kont[m] = True
                ts[m] = ls
                tz[m] = 0.0
                w[m] = 1.0
        # Luecken zwischen den Laeufen: tief und bewegt -> Bogen, sonst Clip-Pose behalten
        if len(runs) >= 1:
            for ri in range(len(locks)):
                a, b, ls = locks[ri]
                a2, b2, ls2 = locks[(ri + 1) % len(locks)]
                start, ende_ = b + 1, (a2 if a2 > b else a2 + n) - 1
                if ende_ < start:
                    continue
                fr = list(range(start, ende_ + 1))
                hoch = max(float(z[m % n]) for m in fr) - float(z.min()) > 0.03
                if not hoch:
                    for q, m in enumerate(fr):
                        u = (q + 1) / (len(fr) + 1)
                        ts[m % n] = ls + (ls2 - ls) * sanft(u)
                        tz[m % n] = 0.05 * math.sin(math.pi * u) ** 1.0
                        w[m % n] = 1.0
        # weich ein/aus an den Rändern von w (Mittelung ueber 5 Bilder)
        w2 = np.zeros(n)
        for m in range(n):
            w2[m] = np.mean([w[(m + d) % n] for d in range(-3, 4)]) if w[m] > 0 else np.mean([w[(m + d) % n] for d in range(-3, 4)]) * 0.0
        # Frei bewegte Hufe, die den Boden streifen oder durchstossen (Rutschen im Clip): auf 10 mm Abstand anheben (absolutes Ziel), Bewegung sonst wie im Clip
        absz = np.full(n, np.nan)
        zp_ = np.array(sohle_zp[nm])
        for m in range(n):
            if w[m] == 0 and z[m] < float(OPT.get("heben", 0.008)):
                ts[m] = s[m]
                absz[m] = zp_[m] + (0.010 - z[m])
                w[m] = 1.0
        tgt_abs[nm] = absz
        kontakt[nm] = kont
        lock[nm] = float(np.median(s[kont])) if kont.any() else 0.0
        tgt_s[nm], tgt_z[nm] = ts, tz
        gew[nm] = np.where(w > 0, np.minimum(1.0, np.maximum(w2, w * 0.0) + 0.0) + (w > 0) * 0.0, 0.0)
        gew[nm] = w
    if OPT.get("modus") == "stand":                     # alle vier Hufe in der Vierbein-Stehpose festhalten (Locks aus der Stehen-Messung), Rumpf nur absenken, wenn Hufe schweben
        lk = json.load(open(OPT["lockdatei"]))["lock_s"]
        for nm in KETTEN:
            tgt_s[nm] = np.full(n, float(lk[nm]))
            tgt_z[nm] = np.zeros(n)
            tgt_abs[nm] = np.full(n, np.nan)
            gew[nm] = np.ones(n)
            kontakt[nm] = np.ones(n, bool)
    ziel_z = {nm: 0.0 for nm in KETTEN}
    wurzel = sk.id["BN_Root_01_01"]
    senk = np.zeros(n)
    ergebnis = None
    x_prev = {nm: np.array(h.info[nm]["ph0"]) for nm in KETTEN}
    for durchlauf in range(4):
        ref_toe = {}
        rahmen = []
        zhoehen = {nm: [] for nm in KETTEN}
        for f in range(n):
            T, R = basis[f][0].copy(), basis[f][1].copy()
            if senk[f] != 0.0:
                Gp_ = sk.global_(T, R)
                T[wurzel] = T[wurzel] + np.linalg.solve(Gp_[sk.eltern[wurzel]][:3, :3], sk.Rm.T @ np.array([0, 0, -senk[f]]) / sk.sk)
            for nm, ks in KETTEN.items():
                w = gew[nm][f]
                if w <= 0:
                    continue
                Gm = sk.global_(T, R)
                S_n = sk.norm(Gm[sk.id[ks[0]]][:3, 3])
                ang, P2 = h.winkel_ist(Gm, nm)
                s_orig, z_orig = P2[-1]
                s_t = (1 - w) * s_orig + w * tgt_s[nm][f]
                z_t = (1 - w) * z_orig + w * ((tgt_z[nm][f] + ZOFF[nm]) if np.isnan(tgt_abs[nm][f]) else tgt_abs[nm][f])
                L = h.info[nm]["L"]
                inf = h.info[nm]
                x0 = ang
                vorn = nm[1] == "F"

                def fkt(x):
                    so = G.kette_pos(-S_n[1], S_n[2], L, x)[-1]
                    res = [(so[0] - s_t) * float(OPT.get("wpos", 4000.0)), (so[1] - z_t) * float(OPT.get("wpos", 4000.0)), math.degrees(x[-1] - inf["flach"]) * float(OPT.get("wa", 30.0)) * w]
                    res += list(np.degrees(x - x0) * float(OPT.get("wfit", 0.25)))                      # nah an der Beinhaltung des Clips bleiben
                    d = np.degrees(x)
                    if vorn:
                        res += [max(180 + (d[2] - d[1]) - 181.5, 0) * 5.0, max(180 - (d[1] - d[0]) - 182, 0) * 5.0]
                        if "karpus" in OPT:                          # Karpus (Innenwinkel) nicht unter den Mindestwinkel einknicken lassen
                            res += [max(float(OPT["karpus"]) - (180 + (d[2] - d[1])), 0) * float(OPT.get("wkarpus", 3.0))]
                    else:
                        res += [max(180 + (d[1] - d[0]) - 181.5, 0) * 5.0, max(180 - (d[2] - d[1]) - 181.5, 0) * 5.0]
                    return np.array(res)
                x = G.lm_loesen(fkt, x_prev[nm] if f > 0 else x0)
                for start_ in (x0, np.array(inf["ph0"]), x_prev[nm] * 0.5 + x0 * 0.5):
                    so_ = G.kette_pos(-S_n[1], S_n[2], L, x)[-1]
                    if abs(so_[0] - s_t) < 0.003 and abs(so_[1] - z_t) < 0.003:
                        break
                    x2 = G.lm_loesen(fkt, np.array(start_), it=80)
                    if np.linalg.norm(fkt(x2)) < np.linalg.norm(fkt(x)):
                        x = x2
                x_prev[nm] = x
                p2 = G.kette_pos(-S_n[1], S_n[2], L, x)
                pn = inf["pts"]
                for gi, kname in enumerate(ks):
                    Gm = sk.global_(T, R)
                    i = sk.id[kname]
                    lat = pn[gi + 1][0] - pn[gi][0]
                    d_n = np.array([lat, -(p2[gi + 1][0] - p2[gi][0]), p2[gi + 1][1] - p2[gi][1]])
                    if gi + 1 < len(ks):
                        akt = Gm[sk.id[ks[gi + 1]]][:3, 3] - Gm[i][:3, 3]
                    else:
                        sg = (Gm[i] @ np.linalg.inv(h.G0[i]) @ np.append(inf["sohle_g"], 1.0))[:3]
                        akt = sg - Gm[i][:3, 3]
                    Rd = G.rot_zw(akt, sk.Rm.T @ d_n)
                    R[i] = np.linalg.solve(Gm[sk.eltern[i]][:3, :3], Rd @ Gm[i][:3, :3]) / sk.S[i][None, :]
                if w >= 0.99:                                     # Hufdrehung um die Senkrechte (Verwindung der Beinkette) festhalten: Huf rutscht sonst im Stehen um einige mm
                    it = sk.id[ks[-1]]
                    Gm = sk.global_(T, R)
                    if nm not in ref_toe:
                        ref_toe[nm] = Gm[it][:3, :3].copy()
                    elif OPT.get("yaw", "1") == "1":
                        R[it] = np.linalg.solve(Gm[sk.eltern[it]][:3, :3], ref_toe[nm]) / sk.S[it][None, :]
            rahmen.append((T, R))
            Gm = sk.global_(T, R)
            Q = h.schuhe(Gm)
            for nm in KETTEN:
                zhoehen[nm].append(float(Q[h.alle[nm]][:, 2].min()))
        if OPT.get("modus") == "stand":
            fehl = np.zeros(n)
            for f in range(n):
                Gm = sk.global_(rahmen[f][0], rahmen[f][1])
                Q = h.schuhe(Gm)
                for nm in KETTEN:
                    fehl[f] = max(fehl[f], float(Q[h.alle[nm]][:, 2].min()) - 0.003)       # Huf schwebt mehr als 3 mm: Rumpf absenken
            fehl = np.maximum(fehl, 0.0)
            senk = np.convolve(np.pad(senk + fehl, 2, mode="wrap"), np.ones(5) / 5.0, mode="valid")
            print("SENK max mm", round(float(senk.max()) * 1000, 1))
        # Bodenhoehe nachfuehren: tiefste Hufeisenecke im Kontakt soll auf z = 0,001 liegen
        for nm in KETTEN:
            zz = np.array(zhoehen[nm])
            if kontakt[nm].any():
                ZOFF[nm] -= float(np.median(zz[kontakt[nm]])) - 0.001
        print("PASS", durchlauf, {nm: round(ZOFF[nm], 4) for nm in KETTEN})
    rahmen.append(rahmen[0])
    # Ausgabe
    Jfr, Gfr = [], []
    for T, R in rahmen:
        Gm = sk.global_(T, R)
        Gfr.append(Gm)
        Jfr.append(h.skin_j(Gm))
    np.savez_compressed(os.path.join(aus, f"{name}_J.npz"), J=np.array(Jfr, np.float32), R=np.array([r for _, r in rahmen]), T=np.array([t for t, _ in rahmen]), fps=fps, V=0.0, Tz=n / fps)
    # Messung: Gleiten Ecke fuer Ecke (jede Hufeisenecke fuer sich, Kontakt z < 4 mm ueber der tiefsten Lage des Beins), Schweben (tiefster Punkt ueber dem Boden), Naht
    schuhw = [h.schuhe(Gm) for Gm in Gfr]
    erg = {"clip": clip, "bilder": n, "dauer": n / fps}
    for nm in KETTEN:
        ix = h.alle[nm]
        zz = np.array([q[ix][:, 2] for q in schuhw[:n]])
        xx = np.array([-q[ix][:, 1] for q in schuhw[:n]])
        boden = 0.0
        worst = 0.0
        for k in range(zz.shape[1]):
            kont = zz[:, k] < boden + 0.004
            f = 0
            while f < n:
                if kont[f]:
                    e = f
                    while e + 1 < n and kont[e + 1]:
                        e += 1
                    worst = max(worst, float((xx[f:e + 1, k].max() - xx[f:e + 1, k].min()) * 1000))
                    f = e + 1
                else:
                    f += 1
        erg[nm] = {"gleiten_mm": round(worst, 1), "tiefster_punkt_mm_min_max": [round(float(zz.min(1).min() * 1000), 1), round(float(zz.min(1).max() * 1000), 1)],
                   "kontaktanteil": round(float(kontakt[nm].mean()), 2)}
    erg["naht_mm"] = round(float(np.abs(schuhw[0] - schuhw[-1]).max() * 1000), 3)
    erg["lock_s"] = {nm: round(v, 3) for nm, v in lock.items()}
    json.dump(erg, open(os.path.join(aus, f"{name}_messung.json"), "w"), indent=1)
    print(json.dumps(erg, indent=1))


if __name__ == "__main__":
    main()











