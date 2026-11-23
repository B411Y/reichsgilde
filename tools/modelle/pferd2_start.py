"""Anfahren_* und Anhalten_* (Pferd B): eigene Schrittplanung ab dem Stehen bzw. bis zum Stehen, Hufe per ebenem IK (pferd2_hufhalter.Halter), Rumpf in der Stelle (Weg aus zugtiere.json).

    python -I tools/modelle/pferd2_start.py <Ausgabeordner> --modus=anfahren|anhalten --name=Anfahren_Gehen --gehen=<gehen_J.npz> --stehen=<Stehen_J.npz>
           --stehenmessung=<Stehen_messung.json> [--ziehen=<Ziehen-gehen_J.npz>]

Plan: Zeitgetaktete Schrittfolge wie im Gehen (Viertakt LH, LF, RH, RF, Stand 0,58/0,60, Zyklus 26 Bilder), die Schrittweite wird mit dem Tempoverlauf `anlauf` aus
zugtiere.json skaliert: Landeposition (Rumpfsystem) s_land = Stand_i + (s_td_i - Stand_i) * anlauf(Landezeit). Stehende Hufe sind in der Welt fest (Rumpfsystem = Welt - Rumpfweg).
Schwung: Bahn des Gehens (Bogen, Hub 12/14 cm, senkrechtes Abheben/Aufsetzen) mit Korrektur der Welt-Lage von der Startlage zur Landelage (weich, Geschwindigkeit 0 an den Enden).
Anfahren: erste 34 Bilder Planung, die letzten 26 Bilder sind das Gehen (Bild 0 des Gehens = letztes Bild); Anhalten: Gehen Bild 0 = erstes Bild, dann Planung bis zum Stehen.
Rumpf (alle Knoten ausser den Beinketten): Ueberblendung Stehen <-> Gehen mit anlauf. Ausgabe: <name>_J.npz (J, R, T, fps) und <name>_messung.json.
"""
import json
import math
import os
import sys

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import pferd2_b as B  # noqa: E402
import pferd2_gang as G  # noqa: E402
import pferd2_hufhalter as HH  # noqa: E402

OPT, POS = G.OPT, G.POS
KETTEN = HH.KETTEN
FPS = 24
V = 1.3
TZ = 26 / FPS
STAND = {"LF": float(OPT.get("stand_v", 0.58)), "RF": float(OPT.get("stand_v", 0.58)), "LH": float(OPT.get("stand_h", 0.60)), "RH": float(OPT.get("stand_h", 0.60))}      # wie im Gehen (--stand_v/--stand_h)
VIERTAKT = G.VIERTAKT
HUBH = {"F": float(OPT.get("hub", 0.12)), "H": float(OPT.get("hub_h", OPT.get("hub", 0.14)))}
P5 = OPT.get("tab") in ("p5", "p6")                                         # Schwung nach Muybridge wie pferd2_gang.py --tab=p5
THETA = math.radians(float(OPT.get("theta", 32.0)))


def sanft(t):
    t = min(max(t, 0.0), 1.0)
    return t * t * (3 - 2 * t)


def mischen_R(Ra, Rb, w):
    out = Ra.copy()
    for i in range(Ra.shape[0]):
        M = Ra[i] * (1 - w) + Rb[i] * w
        u, s_, vt = np.linalg.svd(M)
        out[i] = u @ vt
    return out


def main():
    h = HH.Halter()
    sk = h.sk
    modus = OPT["modus"]
    name = OPT.get("name", modus)
    aus = POS[0]
    os.makedirs(aus, exist_ok=True)
    zt = json.load(open(os.path.join(os.path.dirname(__file__), "..", "..", "game", "daten", "zugtiere.json"), encoding="utf-8"))["tiere"]["pferd"]["klips"]
    klip = zt[OPT.get("json", "Anfahren_Gehen" if modus == "anfahren" else "Anhalten_Gehen")]
    weg = np.array(klip["weg"])
    anl = np.array(klip["anlauf"])
    n = len(weg)                                                    # 61 Bilder
    ge = np.load(OPT["gehen"])
    st = np.load(OPT["stehen"])
    lock = json.load(open(OPT["stehenmessung"]))["lock_s"]
    # Mitte der Standphase und nominelle Landeposition je Bein wie im Gehen (pferd2_gang: vorn Drehpunkt + 0,03, hinten Huefte + 0,06; Schritt = V * Stand * Zyklus)
    td_s, lo_s = {}, {}
    for nm in KETTEN:
        s0 = -h.info[nm]["pts"][0][1]
        mitte = s0 + (float(OPT.get("vorn_off", 0.03)) if nm[1] == "F" else float(OPT.get("hint_off", OPT.get("hint", 0.06))))              # wie --vorn_off / --hint_off im Gehen
        ls = V * STAND[nm] * TZ
        td_s[nm], lo_s[nm] = mitte + ls / 2, mitte - ls / 2

    def a_t(t):
        return float(np.interp(t * FPS, np.arange(n), anl))

    def x_b(t):
        return float(np.interp(t * FPS, np.arange(n), weg))

    tref = (n - 1 - 26) / FPS if modus == "anfahren" else 0.0       # Zeit, zu der die Gangphase 0 ist (Gehen Bild 0)
    # Schwunge: Zeiten aus dem Viertakt, Landung nach anlauf skaliert
    plan = {}
    for nm in KETTEN:
        ph0 = (0.0 - VIERTAKT[nm]) % 1.0 if modus == "anhalten" else None
        sw = []
        for k in range(-4, 8):
            td = tref + (VIERTAKT[nm] + k) * TZ                                    # Aufsetzzeit k
            lo = td - (1 - STAND[nm]) * TZ
            if modus == "anfahren":
                if lo >= 0.0 and td <= (n - 1) / FPS + 1e-9 and td <= tref + 1e-9 + 0.0:
                    sw.append((lo, td))
                elif lo >= 0.0 and lo < tref:                           # Schwung ueber den Anschluss hinaus (nur bis Anschluss geplant)
                    sw.append((lo, td))
            else:
                if td > 0.0 and td <= (n - 1) / FPS + 1e-9:
                    sw.append((lo, td))
        plan[nm] = sw
    # Bahn des Gehens im Rumpfsystem (Schwungform), u in 0..1: s_gait, z_gait  (wie pferd2_gang, ohne Abrollen)
    def bahn(nm, u):
        Ts = (1 - STAND[nm]) * TZ
        m0 = -V * Ts
        u0, u1 = 0.10, 0.88
        sa, sb = lo_s[nm] + m0 * u0, td_s[nm] - m0 * (1 - u1)
        if u < u0:
            s = lo_s[nm] + m0 * u
        elif u > u1:
            s = td_s[nm] + m0 * (u - 1)
        else:
            w = (u - u0) / (u1 - u0)
            d_ = u1 - u0
            s = (2 * w ** 3 - 3 * w ** 2 + 1) * sa + (w ** 3 - 2 * w ** 2 + w) * m0 * d_ + (-2 * w ** 3 + 3 * w ** 2) * sb + (w ** 3 - w ** 2) * m0 * d_
        up = 0.33
        return s, HUBH[nm[1]] * (sanft(u / up) if u < up else sanft((1 - u) / (1 - up)))

    # Anschlussposition: Welt-x der Sohle im Gehen-Bild 0 (Anfahren: bei Bild n-26; Anhalten: Bild 0) fuer Beine im Stand
    Qg0 = h.schuhe(sk.global_(ge["T"][0], ge["R"][0]))
    x_join = {nm: float(-Qg0[h.s12[nm]].mean(0)[1]) + (float(weg[n - 27]) if modus == "anfahren" else 0.0) for nm in KETTEN}
    Qst0 = h.schuhe(sk.global_(st["T"][0], st["R"][0]))
    sg_g = {nm: np.array([-h.schuhe(sk.global_(ge["T"][g], ge["R"][g]))[h.s12[nm]].mean(0)[1] for g in range(26)]) for nm in KETTEN}   # Sohlenlage (Rumpfsystem) je Gehbild

    def landung(nm, td):
        x = x_b(td) + lock[nm] + (td_s[nm] - lock[nm]) * min(1.0, a_t(td) / float(OPT.get("a_ref", 0.9)))
        if modus == "anhalten" and td <= 0.3 and a_t(td) > 0.97:              # Landung noch im vollen Gehen: dort, wo der Huf im Gehen-Clip landet
            g2 = int(math.ceil(td * FPS - 1e-9)) + 1
            return float(weg[min(g2, n - 1)]) + float(sg_g[nm][g2 % 26])
        if modus == "anfahren" and td >= tref - 1e-9:                      # Landung im Gehen: genau dort, wo der Huf im Gehen-Clip steht (Hufe stehen in der Welt fest)
            g2 = int(math.ceil((td - tref) * FPS - 1e-9)) + 1
            return float(weg[min(n - 27 + g2, n - 1)]) + float(sg_g[nm][g2 % 26])
        if modus == "anfahren":
            vor = [t_ for (_, t_) in plan[nm] if t_ <= tref + 1e-9]
            if vor and abs(td - max(vor)) < 1e-9 and abs(x - x_join[nm]) < 0.02:
                x = x_join[nm]
        return x

    # Ziele je Bein und Bild: Welt-x der Sohle (Rumpfweg einberechnet) und Hoehe
    standf = {nm: np.zeros(n, bool) for nm in KETTEN}
    ziel_s = {nm: np.zeros(n) for nm in KETTEN}
    ziel_z = {nm: np.zeros(n) for nm in KETTEN}
    schwung_u = {nm: np.full(n, -1.0) for nm in KETTEN}                        # Anteil des Schwungs je Bild (-1 = Stand), fuer --tab=p5
    schwung_r = {nm: np.ones(n) for nm in KETTEN}                              # Schrittlaenge des Schwungs gegen den vollen Schritt (0..1), fuer --tab=p5
    for nm in KETTEN:
        # Ausgangslage: Anfahren: Stand (lock) ; Anhalten: Sohlenlage des Gehens Bild 0
        if modus == "anfahren":
            x_pos = lock[nm]                                              # Welt-x (Rumpfweg 0 bei t = 0)
        else:
            Gm0 = sk.global_(ge["T"][0], ge["R"][0])
            Q0 = h.schuhe(Gm0)
            x_pos = float(-Q0[h.s12[nm]].mean(0)[1])
            zsoh = [h.schuhe(sk.global_(ge["T"][g], ge["R"][g]))[h.s12[nm]].mean(0)[2] for g in range(26)]
            gstar = next((g for g in range(26) if zsoh[g] < Qst0[h.s12[nm]].mean(0)[2] + 0.004), 0)      # erstes Gehbild mit Huf am Boden
            x_pos = float(weg[gstar]) + float(sg_g[nm][gstar])
        x_cur = x_pos
        sw = sorted(plan[nm])
        for f in range(n):
            t = f / FPS
            aktiv = None
            for (lo, td) in sw:
                if lo - 1e-9 <= t <= td + 1e-9:
                    aktiv = (lo, td)
                    break
            if aktiv is None:
                # Stand (Welt fest) oder vor dem ersten Schwung: x_cur gilt
                for (lo, td) in sw:
                    if td < t - 1e-9:
                        x_cur = landung(nm, td)
                ziel_s[nm][f] = x_cur - x_b(t)
                ziel_z[nm][f] = 0.0
                standf[nm][f] = True
                continue
            lo, td = aktiv
            u = (t - lo) / (td - lo)
            schwung_u[nm][f] = u
            # Startlage des Schwungs: Welt-x des letzten Standes
            x_start = x_pos
            for (lo2, td2) in sw:
                if td2 < lo - 1e-9:
                    x_start = landung(nm, td2)
            if modus == "anhalten" and lo < 0:
                x_start = x_pos
            x_land = landung(nm, td)
            # Bahn des Gehens in Welt-x: V * Zeit + s_gait (stetig), Korrektur c(u) von der Startlage zur Landelage
            sg, zg = bahn(nm, min(max(u, 0.0), 1.0))
            sg0, _ = bahn(nm, 0.0)
            sg1, _ = bahn(nm, 1.0)
            x_ws = V * (t - lo) + sg + 0.0
            x_ws0 = sg0
            x_ws1 = V * (td - lo) + sg1
            c0 = x_start - x_ws0
            c1 = x_land - x_ws1
            s_ = sanft((u - 0.06) / 0.78)
            x_w = x_ws + c0 * (1 - s_) + c1 * s_
            ziel_s[nm][f] = x_w - x_b(t)
            if P5:                                                       # kurze Schritte (Anlauf aus dem Stand, Auslauf): Hub und Beugung nach der Schrittlaenge
                r_ = min(1.0, abs(x_land - x_start) / (V * TZ))
                schwung_r[nm][f] = r_
                hm = float(OPT.get("hub_min", 0.35))
                zg = zg * (hm + (1 - hm) * r_)
            ziel_z[nm][f] = zg
    # Anschluss der Hufziele an das Gehen: in den letzten bzw. ersten 10 Bildern vor dem Gehen werden die Ziele (Rumpfsystem) zu den Sohlenlagen des Gehens ueberblendet
    NB = int(OPT.get("nb", 6))
    Qst0 = h.schuhe(sk.global_(st["T"][0], st["R"][0]))
    if NB > 0:
        sg_, zg_ = {}, {}
        for nm in KETTEN:
            pts = np.array([h.schuhe(sk.global_(ge["T"][g], ge["R"][g]))[h.s12[nm]].mean(0) for g in range(26)])
            sg_[nm], zg_[nm] = -pts[:, 1], pts[:, 2]
        n0j = n - 1 - 26
        for nm in KETTEN:
            for f in range(n):
                if modus == "anfahren" and n0j - NB <= f < n0j:
                    w = sanft((f - (n0j - NB)) / float(NB)); gi = (f - n0j) % 26
                elif modus == "anhalten" and f < NB:
                    w = sanft(1 - f / float(NB)); gi = f % 26
                else:
                    continue
                ziel_s[nm][f] = (1 - w) * ziel_s[nm][f] + w * sg_[nm][gi]
                ziel_z[nm][f] = (1 - w) * ziel_z[nm][f] + w * (zg_[nm][gi] - float(Qst0[h.s12[nm]].mean(0)[2]))
    # Rumpf-Basis: Stehen <-> Gehen ueberblenden
    basis = []
    for f in range(n):
        t = f / FPS
        g = int(round(((t - tref) / TZ % 1.0) * 26)) % 26
        wgt = min(max(a_t(t), 0.0), 1.0)
        R = mischen_R(st["R"][0], ge["R"][g], wgt)
        T = st["T"][0] * (1 - wgt) + ge["T"][g] * wgt
        basis.append((T, R))
    kopf0 = float(OPT.get("kopf", 0.0))                      # Gewichtsverlagerung: Hals/Kopf nicken beim Antreten nach unten (Anfahren) bzw. heben sich beim Anhalten, Hoechstwert in Grad (Summe ueber die Halsknochen)
    if kopf0 != 0.0:
        hn = [sk.id[k] for k in ("BN_Neck_00_06_06", "BN_Neck_01_07_07", "BN_Neck_02_011_011", "BN_Neck_03_012_012", "BN_Head_00_016_015")]
        prior = [0.34, 0.30, 0.22, 0.10, 0.04]
        achse_g = sk.Rm.T @ np.array([1.0, 0.0, 0.0])
        achse_g = achse_g / np.linalg.norm(achse_g)
        horse_ = next(q for q in sk.H.prims if q["mat"] == "Horse")
        w0_, _ = sk.H.haut(horse_, h.skin_j(h.G0))
        P0_ = B.anwenden(sk.mn, B.blender_koord(w0_))
        kv_ = np.where((P0_[:, 1] < -0.95) & (P0_[:, 2] > 1.0))[0]

        def nicken(f, grad):
            T_, Rn = basis[f][0], basis[f][1].copy()
            for k_, i in enumerate(hn):
                Gm = sk.global_(T_, Rn)
                Gp = Gm[sk.eltern[i]][:3, :3]
                Gp = Gp / np.linalg.norm(Gp, axis=0, keepdims=True)
                ax = np.linalg.solve(Gp, achse_g)
                ax = ax / np.linalg.norm(ax)
                a1 = math.radians(grad * prior[k_])
                K_ = np.array([[0, -ax[2], ax[1]], [ax[2], 0, -ax[0]], [-ax[1], ax[0], 0]])
                Rn[i] = (np.eye(3) + math.sin(a1) * K_ + (1 - math.cos(a1)) * (K_ @ K_)) @ Rn[i]
            return Rn

        def nase_z(f, grad):
            w_, _ = sk.H.haut(horse_, h.skin_j(sk.global_(basis[f][0], nicken(f, grad))))
            return float(B.anwenden(sk.mn, B.blender_koord(w_))[kv_, 2].min())
        vz = 1.0 if nase_z(0, 5.0) < nase_z(0, -5.0) else -1.0           # +: Nase nach unten
        n0_ = n - 1 - 26
        for f in range(n):
            if modus == "anfahren":
                kurve = math.sin(math.pi * f / max(n0_ - 6, 1)) ** 2 if f < n0_ - 6 else 0.0
                gr = vz * kopf0 * kurve                              # Kopf senken und vorschieben beim Antreten
            else:
                kurve = math.sin(math.pi * min(max((f - 4) / (n - 1 - 4), 0.0), 1.0)) ** 2
                gr = -vz * 0.6 * kopf0 * kurve                       # beim Anhalten umgekehrt: Kopf leicht heben/zurueck
            if gr != 0.0:
                basis[f] = (basis[f][0], nicken(f, gr))
    Gst = sk.global_(st["T"][0], st["R"][0])
    ref_toe = {nm: Gst[sk.id[KETTEN[nm][-1]]][:3, :3].copy() for nm in KETTEN}
    flach_ref = {nm: float(h.winkel_ist(Gst, nm)[0][-1]) for nm in KETTEN}                 # Hufwinkel der Stehpose (flach, wie dort gemessen)
    # IK je Bild (Kopie der Schleife aus pferd2_hufhalter, alle Beine, w = 1)
    Qst = h.schuhe(Gst)
    ZOFF = {nm: float(Qst[h.s12[nm]].mean(0)[2]) for nm in KETTEN}                   # Sohlenhoehe der Stehpose = Bodenziel je Huf
    x_prev = {nm: np.array(h.info[nm]["ph0"]) for nm in KETTEN}
    zadd = {nm: np.zeros(n) for nm in KETTEN}                                         # Hoehenkorrektur je Huf und Bild (Eintauchen beim Aufsetzen ausgleichen)
    for durchlauf in range(int(OPT.get("passes", 1))):
        rahmen, zh = [], {nm: [] for nm in KETTEN}
        for f in range(n):
            T, R = basis[f][0].copy(), basis[f][1].copy()
            for nm, ks in KETTEN.items():
                Gm = sk.global_(T, R)
                S_n = sk.norm(Gm[sk.id[ks[0]]][:3, 3])
                ang, P2 = h.winkel_ist(Gm, nm)
                L = h.info[nm]["L"]
                inf = h.info[nm]
                s_t, z_t = ziel_s[nm][f], ziel_z[nm][f] + ZOFF[nm] + zadd[nm][f]
                vorn = nm[1] == "F"
                x0 = ang
                wflat = 30.0 if ziel_z[nm][f] < 0.004 else 3.0
                u_ = schwung_u[nm][f]
                p5 = P5 and u_ >= 0.0 and not standf[nm][f]
                if p5:                                                       # --tab=p5: Schwung wie im Gehen (Gelenkziele der Tabelle, Huf klappt nach hinten ab)
                    ph_ = STAND[nm] + u_ * (1 - STAND[nm])
                    alpha = G.schwung_huf(vorn, u_, THETA) * G.glatt((u_ - 0.08) / 0.22)       # ohne Abrollen im Stand: erst abheben, dann abklappen (Zehe schleift nicht)
                    wflat = 0.1 * (0.4 + float(OPT.get("wa_mitte", 6.0)) + float(OPT.get("wa_end", 30.0)) * G.glatt((u_ - float(OPT.get("wa_start", 0.45))) / 0.3)
                                   + 12.0 * (1 - G.glatt(u_ / 0.12)) + 8.0 * (1 - G.glatt(u_ / 0.35)))           # Gewichte wie im Gehen (pferd2_gang.loese)
                    wflat = max(wflat, 3.0 * (1 - G.glatt(u_ / 0.15)))                                               # beim Abheben fest wie bisher
                    zk = [G.tab("karpus", ph_), G.tab("fessel_v", ph_), G.tab("ellbogen", ph_)] if vorn else [G.tab("knie", ph_), G.tab("sprung", ph_), G.tab("fessel_h", ph_)]
                    r_ = schwung_r[nm][f]                                    # kurzer Schritt: Beugung und Abklappen des Hufs anteilig (aus dem Stand kleine Schritte)
                    z0 = [G.tab("karpus", 0.0), G.tab("fessel_v", 0.0), G.tab("ellbogen", 0.0)] if vorn else [G.tab("knie", 0.0), G.tab("sprung", 0.0), G.tab("fessel_h", 0.0)]
                    zk = [a_ * r_ + b_ * (1 - r_) for a_, b_ in zip(zk, z0)]
                    alpha *= r_
                    kr0 = 180 + math.degrees(inf["ph0"][4] - inf["ph0"][3])
                    fmin = float(OPT.get("fmin_v", 150)) if vorn else float(OPT.get("fmin_h", 140))
                else:
                    alpha = 0.0

                xv = np.array(x_prev[nm] if f > 0 else x0)                    # Loesung des vorigen Bildes (Glaette, --tab=p6)

                def fkt(x):
                    so = G.kette_pos(-S_n[1], S_n[2], L, x)[-1]
                    res = [(so[0] - s_t) * 4000.0, (so[1] - z_t) * 4000.0, math.degrees(x[-1] - flach_ref[nm] - alpha) * wflat]
                    res += list(np.degrees(x - x0) * 0.05)
                    d = np.degrees(x)
                    if vorn:
                        res += [max(180 + (d[2] - d[1]) - 181.5, 0) * 5.0, max(180 - (d[1] - d[0]) - 182, 0) * 5.0]
                    else:
                        res += [max(180 + (d[1] - d[0]) - 181.5, 0) * 5.0, max(180 - (d[2] - d[1]) - 181.5, 0) * 5.0]
                    if p5:
                        wz = G.glatt((u_ - 0.05) / 0.2) * (1 - G.glatt((u_ - 0.75) / 0.2))      # Gelenkziele nur nach dem Abheben und vor dem Aufsetzen (sonst Schleifen bzw. schraeg landender Huf)
                        if vorn:
                            ell, kar, fet = 180 - (d[1] - d[0]), 180 + (d[2] - d[1]), 180 + (d[3] - d[2])
                            res += [(kar - zk[0]) * G.WK * wz, (fet - zk[1]) * G.WF * wz, (ell - zk[2]) * G.WE * wz]
                        else:
                            kn, spr, fet = 180 + (d[1] - d[0]), 180 - (d[2] - d[1]), 180 + (d[3] - d[2])
                            res += [(kn - zk[0]) * G.WKN * wz, (spr - zk[1]) * G.WS * wz, (fet - zk[2]) * G.WF * wz]
                        kr = 180 + (d[4] - d[3])
                        res += [max(fmin - fet, 0) * 3.0, max(fet - 238, 0) * 3.0, max(kr - kr0 - 20, 0) * 3.0, max(kr0 - 60 - kr, 0) * 3.0]
                        if OPT.get("tab") == "p6":                       # wie im Gehen: Oberarm/Oberschenkel im Fenster um die Ruhelage, Glaette gegen das vorige Bild
                            o0 = math.degrees(inf["ph0"][0])
                            fen = float(OPT.get("ober_fenster_v", 15)) if vorn else float(OPT.get("ober_fenster_h", 20))
                            res += [max(abs(d[0] - o0) - fen, 0) * float(OPT.get("wober", 1.5)) * (1 - G.glatt((u_ - 0.7) / 0.25))]
                            wg =(float(OPT.get("wglatt", 0.15)) if vorn else float(OPT.get("wglatt_h", OPT.get("wglatt", 0.15)))) * (1 - G.glatt((u_ - 0.7) / 0.25))   # vor dem Aufsetzen frei (Huf flach)
                            res += list((d - np.degrees(xv)) * wg)
                    return np.array(res)
                x = G.lm_loesen(fkt, x_prev[nm] if f > 0 else x0)
                for start_ in (x0, np.array(inf["ph0"])):
                    so_ = G.kette_pos(-S_n[1], S_n[2], L, x)[-1]
                    if abs(so_[0] - s_t) < 0.003 and abs(so_[1] - z_t) < 0.003:
                        break
                    x2 = G.lm_loesen(fkt, np.array(start_), it=80)
                    if np.linalg.norm(fkt(x2)) < np.linalg.norm(fkt(x)):
                        x = x2
                x_prev[nm] = x
                p2 = G.kette_pos(-S_n[1], S_n[2], L, x)
                if OPT.get("debug") and f in (0, 10, 20) and nm == "RH":
                    print("DBG", f, "S", np.round(S_n, 3), "ziel", round(s_t, 3), round(z_t, 3), "ist", np.round(p2[-1], 3), "ang0", np.round(np.degrees(x0), 0), "x", np.round(np.degrees(x), 0))
                pn = inf["pts"]
                for gi, kname in enumerate(ks):
                    Gm = sk.global_(T, R)
                    i = sk.id[kname]
                    lat = pn[gi + 1][0] - pn[gi][0]
                    d_n = np.array([lat, -(p2[gi + 1][0] - p2[gi][0]), p2[gi + 1][1] - p2[gi][1]])
                    if gi + 1 < len(ks):
                        akt = Gm[sk.id[ks[gi + 1]]][:3, 3] - Gm[i][:3, 3]
                    else:
                        sg_ = (Gm[i] @ np.linalg.inv(h.G0[i]) @ np.append(inf["sohle_g"], 1.0))[:3]
                        akt = sg_ - Gm[i][:3, 3]
                    Rd = G.rot_zw(akt, sk.Rm.T @ d_n)
                    R[i] = np.linalg.solve(Gm[sk.eltern[i]][:3, :3], Rd @ Gm[i][:3, :3]) / sk.S[i][None, :]
                if standf[nm][f]:                                  # stehender Huf: Drehung wie im Stehen (flach, ohne Verwindung)
                    it = sk.id[ks[-1]]
                    Gm = sk.global_(T, R)
                    R[it] = np.linalg.solve(Gm[sk.eltern[it]][:3, :3], ref_toe[nm]) / sk.S[it][None, :]
            rahmen.append((T, R))
            Q = h.schuhe(sk.global_(T, R))
            for nm in KETTEN:
                zh[nm].append(float(Q[h.alle[nm]][:, 2].min()))
        for nm in KETTEN:                                                             # tiefer als 1 mm im Boden: Ziel dieses Bildes anheben
            zadd[nm] += np.maximum(0.0, 0.001 - np.array(zh[nm]))
        print("PASS", durchlauf, {nm: round(ZOFF[nm], 4) for nm in KETTEN})
    # Anschluss an das Gehen: Anfahren: letzte 26 Bilder (ab 34) = Gehen; Anhalten: Bild 0 = Gehen Bild 0 (Ueberblendung ueber 6 Bilder)
    if modus == "anfahren":
        n0 = n - 1 - 26
        for f in range(n0, n):
            rahmen[f] = (ge["T"][(f - n0) % 26], ge["R"][(f - n0) % 26])
        for k in range(1, 7):
            f = n0 - k
            w = sanft(1 - k / 7.0)
            gi = (f - n0) % 26
            Rn = mischen_R(rahmen[f][1], ge["R"][gi], w)
            for nm in KETTEN:
                if standf[nm][f] or OPT.get("blendbeine", "0") == "0":                 # Beine bleiben aus dem IK (stehende immer), sonst taucht ein Huf in der Ueberblendung ein
                    for kn in KETTEN[nm]:
                        Rn[sk.id[kn]] = rahmen[f][1][sk.id[kn]]
            rahmen[f] = (rahmen[f][0] * (1 - w) + ge["T"][gi] * w, Rn)
    else:
        for f in range(0, 7):
            w = sanft(1 - f / 7.0)
            Rn = mischen_R(rahmen[f][1], ge["R"][f % 26], w)
            if OPT.get("tab") == "p6":                                   # Bein, das vom Gehen her noch steht (Abrollen ueber die Zehe): ganz aus dem Gehen, sonst gleitet der Huf
                for nm in KETTEN:
                    ab = next((k for k in range(n) if not standf[nm][k]), n)     # erstes Bild im Schwung
                    if ab <= 7 and f < ab:
                        for kn in KETTEN[nm]:
                            Rn[sk.id[kn]] = ge["R"][f % 26][sk.id[kn]]
            rahmen[f] = (rahmen[f][0] * (1 - w) + ge["T"][f % 26] * w, Rn)
    Gfr, Jfr = [], []
    for T, R in rahmen:
        Gm = sk.global_(T, R)
        Gfr.append(Gm)
        Jfr.append(h.skin_j(Gm))
    np.savez_compressed(os.path.join(aus, f"{name}_J.npz"), J=np.array(Jfr, np.float32), R=np.array([r for _, r in rahmen]), T=np.array([t for t, _ in rahmen]), fps=FPS, V=0.0, Tz=(n - 1) / FPS, weg=weg)
    schuhw = [h.schuhe(Gm) for Gm in Gfr]
    if OPT.get("debug2"):
        for nm in ("LF", "RF", "RH"):
            print("DIAG", nm, " ".join("%d%s:%.0f/%.0f/%.0f" % (f, "S" if standf[nm][f] else "w", ziel_s[nm][f] * 1000, (-schuhw[f][h.s12[nm]].mean(0)[1]) * 1000, schuhw[f][h.alle[nm]][:, 2].min() * 1000) for f in range(0, n, 2)))
    erg = {"clip": name, "bilder": n, "dauer": (n - 1) / FPS}
    for nm in KETTEN:
        ix = h.alle[nm]
        zz = np.array([q[ix][:, 2] for q in schuhw])
        xx = np.array([-q[ix][:, 1] + weg[f] for f, q in enumerate(schuhw)])
        worst = 0.0
        for k in range(zz.shape[1]):
            kont = zz[:, k] < 0.004
            f = 0
            while f < n:
                if kont[f]:
                    e = f
                    while e + 1 < n and kont[e + 1]:
                        e += 1
                    g_ = float((xx[f:e + 1, k].max() - xx[f:e + 1, k].min()) * 1000)
                    if OPT.get("diag_gleiten") and g_ > 4.0:
                        print("GLEITEN", nm, "Ecke", k, "Bilder", f, e, "mm %.1f" % g_)
                    worst = max(worst, g_)
                    f = e + 1
                else:
                    f += 1
        erg[nm] = {"gleiten_mm": round(worst, 1), "hub_cm": round(float(zz.min(1).max() * 100), 1), "boden_mm_min": round(float(zz.min(1).min() * 1000), 1)}
    # Uebergaenge: Abstand (groesste Eckenverschiebung der ganzen Figur, mm) Anfahren Bild 0 / Anhalten letztes Bild gegen Stehen Bilder (Rumpfpose ohne Weg)
    def figur(Gm):
        w, _ = sk.H.haut(sk.H.prims[next(i for i, p in enumerate(sk.H.prims) if p["mat"] == "Horse")], h.skin_j(Gm))
        return B.anwenden(sk.mn, B.blender_koord(w))
    pf = figur(Gfr[0] if modus == "anfahren" else Gfr[-1])
    stehen_d = []
    for f in range(st["R"].shape[0] - 1):
        stehen_d.append(float(np.abs(figur(sk.global_(st["T"][f], st["R"][f])) - pf).max() * 1000))
    erg["abstand_zu_stehen_mm"] = {"bild0": round(stehen_d[0], 2), "min": round(min(stehen_d), 2), "max": round(max(stehen_d), 2)}
    pg = figur(Gfr[-1 if modus == "anfahren" else 0])
    erg["abstand_zu_gehen_bild0_mm"] = round(float(np.abs(pg - figur(sk.global_(ge["T"][0], ge["R"][0]))).max() * 1000), 3)
    json.dump(erg, open(os.path.join(aus, f"{name}_messung.json"), "w"), indent=1)
    print(json.dumps(erg, indent=1))


if __name__ == "__main__":
    main()
















