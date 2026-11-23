"""Gehen (Schritt) fuer Pferd B: ebenes IK je Bein nach Sollwert-Tabelle (Muybridge 563 / Gelenkwinkel), schreibt Bilder (Gelenkmatrizen), Messwerte und Tabelle.
Reines numpy (normales Python): python -I tools/modelle/pferd2_gang.py <Ausgabeordner> [--v=1.3] [--T=1.3333] [--hub=0.13] [--ueberfuss=...]

Ablauf: Ausgang = Pose "Skeleton|1 Ilde" Bild 0 (Stand). Pro Bein vier (vorn) bzw. fuenf (hinten) Glieder in der Laengsebene; Sohlenweg: Stand 60 % mit -v (Rumpfsystem),
Schwung 40 % mit Hermite-Bogen (Endgeschwindigkeit -v, Gipfel bei 72 % des Zyklus), Gelenkwinkel als weiche Ziele der Tabelle, Lage der Sohle hart.
Ergebnis je Bild: lokale Drehungen der Beinknochen und des Halses, Wurzelhoehe (Wippen). Gelenkmatrizen = Welt * inverse Bindematrix (wie pferd2_b.py).
"""
import json
import math
import os
import sys

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import pferd2_b as B  # noqa: E402
import pferd2_bind as bd  # noqa: E402

ARGS = sys.argv[1:]
POS = [a for a in ARGS if not a.startswith("--")]
OPT = dict(a[2:].split("=", 1) if "=" in a else (a[2:], "1") for a in ARGS if a.startswith("--"))
V = float(OPT.get("v", 1.3))
T = float(OPT.get("T", 32 / 24))
HUB = float(OPT.get("hub", 0.13))
SCAP = float(OPT.get("scap", 0.10))
HUB_H = float(OPT.get("hub_h", HUB))
WK = float(OPT.get("wk", 0.6))
WF = float(OPT.get("wf", 0.6))
WS = float(OPT.get("ws", 0.6))
WKN = float(OPT.get("wkn", 0.6))
WE = float(OPT.get("we", 0.3))
NICKV = float(OPT.get("nickv", 1.0))
PD = float(OPT.get("pd", 0.0))
KOPFTIEF = float(OPT.get("kopftief", 0.0))
VL = float(OPT.get("vorlauf", 0.0))
FPS = 24
NF = int(round(T * FPS))
STAND = 0.6
KNOTEN = np.array([0, .15, .30, .45, .60, .70, .80, .90, 1.0])
TAB = {"karpus": [183, 185, 183, 175, 160, 135, 139, 160, 183], "ellbogen": [150, 154, 152, 148, 135, 115, 109, 125, 150], "fessel_v": [195, 215, 222, 205, 185, 181, 180, 185, 195],
       "knie": [150, 155, 157, 150, 140, 130, 135, 145, 150], "sprung": [160, 165, 167, 160, 145, 127, 135, 150, 160],
       "fessel_h": [195, 212, 216, 200, 175, 156, 160, 175, 195]}
# --tab=p5: Schwung nach Muybridge 563 (Bild 3 und 12 gemessen: Karpus etwa 95-107 Grad, Roehrbein nach hinten, Huf haengt mit der Sohle nach hinten);
# Stand unveraendert. Fessel vorn im Schwung gebeugt (165), Hufdrehung gegen den Stand als eigenes Ziel (SCHWUNG_HUF).
TAB_P5 = {"karpus": [183, 185, 183, 175, 155, 105, 110, 160, 183], "fessel_v": [195, 215, 222, 205, 185, 165, 165, 180, 195]}
if OPT.get("tab") in ("p5", "p6"):
    TAB.update(TAB_P5)
# --tab=p6: Muybridge Bild 12 (Phase etwa 0,78): Unterarm fast waagerecht vor, Karpus etwa 95-105 Grad: dafuer beugt der Ellbogen staerker (der Oberarm bleibt im Fenster)
TAB_P6 = {"karpus": [183, 185, 183, 175, 155, 105, 100, 160, 183], "ellbogen": [150, 154, 152, 148, 135, 105, 95, 125, 150]}
if OPT.get("tab") == "p6":
    TAB.update(TAB_P6)
VIERTAKT = {"LH": 0.0, "LF": 0.25, "RH": 0.5, "RF": 0.75}
if "lap" in OPT:                                                       # Phasenlage Vorderhuf nach Hinterhuf derselben Seite (Schritt mit diagonalen Paaren: > 0,25)
    VIERTAKT.update({"LF": float(OPT["lap"]), "RF": (0.5 + float(OPT["lap"])) % 1.0})
C = np.array([[1, 0, 0], [0, 0, -1], [0, 1, 0.0]])


def tab(name, ph):
    return float(np.interp(ph, KNOTEN, TAB[name]))


def kurven_biegung(sk, links="Skeleton|Walk_L", gerade="Skeleton|Walk"):
    """Kurvenhaltung aus dem Original (nur als Messwert, keine Beinbahn): mittlere Zusatzdrehung jedes Wirbelsaeulenknochens in Walk_L gegen Walk
    (Mittel ueber 20 Stellen des Zyklus), als (Knochen, Achse im Elternraum, Grad). Ergebnis Pferd B: Spine_00 1,4, Spine_01 9,7, Spine_02 14,0, Spine_03 4,8 Grad,
    jeweils um die Hochachse; zusammen 30,0 Grad, verteilt nach --kurve_anteile."""
    def mittel(clip):
        acc = {}
        for a in np.linspace(0, 0.95, 20):
            for i, v in bd.abtasten(sk.j, sk.H.buf, clip, float(a)).items():
                if "rotation" in v:
                    acc.setdefault(i, []).append(np.array(v["rotation"]))
        return {i: np.mean(v, 0) / np.linalg.norm(np.mean(v, 0)) for i, v in acc.items()}
    w, l = mittel(gerade), mittel(links)
    aus = []
    for name in ("BN_Spine_00_02_02", "BN_Spine_01_03_03", "BN_Spine_02_04_04", "BN_Spine_03_05_05"):
        i = sk.id[name]
        D = bd.quat_matrix(l[i]) @ bd.quat_matrix(w[i]).T
        grad = math.degrees(math.acos(max(-1.0, min(1.0, (np.trace(D) - 1) / 2))))
        ax = np.array([D[2, 1] - D[1, 2], D[0, 2] - D[2, 0], D[1, 0] - D[0, 1]])
        aus.append((i, ax / max(np.linalg.norm(ax), 1e-12), grad))
    # Verteilung (Kritik 23.11.2026): die Gesamtbiegung bleibt der Messwert, aber mehr im Rumpf und weniger am Widerrist (sonst wirkt sie wie ein Scharnier
    # am Kummet): Anteile Spine_00/01/02/03 = 20/40/30/10 % statt gemessen 5/32/47/16 %
    anteil = [float(x) for x in OPT.get("kurve_anteile", "0.2,0.4,0.3,0.1").split(",")]
    gesamt = sum(g for _, _, g in aus)
    return [(i, ax, gesamt * a) for (i, ax, _), a in zip(aus, anteil)]


def schwung_huf(vorn, u, theta):
    """--tab=p5: Hufwinkel (rad, negativ = Ferse hoch, Sohle nach hinten) im Schwung bei Anteil u: bis u_ab senkrecht abheben (kein Schleifen der Zehe),
    bis u_auf auf amax abklappen, bis u_halt haengen, bis u_flach zum Aufsetzen flach werden."""
    amax = math.radians(float(OPT.get("amax_v" if vorn else "amax_h", 70 if vorn else 40)))
    ab, a0, a1 = float(OPT.get("u_ab", 0.12)), float(OPT.get("u_auf", 0.28)), float(OPT.get("u_halt", 0.40))
    if u < a0:
        return -theta - (amax - theta) * glatt((u - ab) / (a0 - ab))
    return -amax * (1 - glatt((u - a1) / (float(OPT.get("u_flach", 0.80)) - a1)))


def glatt(t):
    t = np.clip(t, 0, 1)
    return t * t * (3 - 2 * t)


def rot_zw(a, b):
    """Kleinste Drehung (3x3), die Richtung a nach b bringt."""
    a, b = a / np.linalg.norm(a), b / np.linalg.norm(b)
    v = np.cross(a, b)
    c = float(np.dot(a, b))
    if c < -0.999999:
        ax = np.cross(a, [1, 0, 0])
        if np.linalg.norm(ax) < 1e-6:
            ax = np.cross(a, [0, 1, 0])
        ax /= np.linalg.norm(ax)
        return 2 * np.outer(ax, ax) - np.eye(3)
    vx = np.array([[0, -v[2], v[1]], [v[2], 0, -v[0]], [-v[1], v[0], 0]])
    return np.eye(3) + vx + vx @ vx / (1 + c)


def mat_quat(R):
    t = np.trace(R)
    if t > 0:
        s = math.sqrt(t + 1) * 2
        q = [(R[2, 1] - R[1, 2]) / s, (R[0, 2] - R[2, 0]) / s, (R[1, 0] - R[0, 1]) / s, s / 4]
    elif R[0, 0] > R[1, 1] and R[0, 0] > R[2, 2]:
        s = math.sqrt(1 + R[0, 0] - R[1, 1] - R[2, 2]) * 2
        q = [s / 4, (R[0, 1] + R[1, 0]) / s, (R[0, 2] + R[2, 0]) / s, (R[2, 1] - R[1, 2]) / s]
    elif R[1, 1] > R[2, 2]:
        s = math.sqrt(1 + R[1, 1] - R[0, 0] - R[2, 2]) * 2
        q = [(R[0, 1] + R[1, 0]) / s, s / 4, (R[1, 2] + R[2, 1]) / s, (R[0, 2] - R[2, 0]) / s]
    else:
        s = math.sqrt(1 + R[2, 2] - R[0, 0] - R[1, 1]) * 2
        q = [(R[0, 2] + R[2, 0]) / s, (R[1, 2] + R[2, 1]) / s, s / 4, (R[1, 0] - R[0, 1]) / s]
    q = np.array(q)
    return q / np.linalg.norm(q)


class Skelett:
    def __init__(self):
        self.H = B.Haut()
        j = self.H.j
        self.j = j
        self.namen = [n["name"] for n in j["nodes"]]
        self.id = {n: i for i, n in enumerate(self.namen)}
        self.eltern = self.H.eltern
        ueber = bd.abtasten(j, self.H.buf, "Skeleton|1 Ilde", 0.0)
        n = len(j["nodes"])
        self.T = np.zeros((n, 3))
        self.S = np.ones((n, 3))
        self.R = np.tile(np.eye(3), (n, 1, 1))
        self.M = [None] * n                                             # Knoten mit "matrix"
        for i, nd in enumerate(j["nodes"]):
            nd = dict(nd)
            if i in ueber:
                nd.pop("matrix", None)
                nd.update(ueber[i])
            if "matrix" in nd:
                self.M[i] = bd.knoten_matrix(nd)
                continue
            self.T[i] = nd.get("translation", (0, 0, 0))
            self.S[i] = nd.get("scale", (1, 1, 1))
            self.R[i] = bd.quat_matrix(nd.get("rotation", (0, 0, 0, 1)))
        self.ordnung = self._ordnung()
        self.mn = B.lade_norm("C:/Reichsgilde_Austausch/pferd2/schritt_a2/roh/henry_norm.json")
        A = self.mn[:3, :3] @ C
        s = np.linalg.norm(A[:, 0])
        self.sk = s
        self.Rm = A / s                                               # glTF-Weltrichtung -> normierte Richtung
        self.joints = self.H.skin["joints"]
        self.ibm = self.H.ibm

    def _ordnung(self):
        o, gesehen = [], set()

        def besuche(i):
            if i in gesehen:
                return
            if i in self.eltern:
                besuche(self.eltern[i])
            gesehen.add(i)
            o.append(i)
        for i in range(len(self.namen)):
            besuche(i)
        return o

    def global_(self, T=None, R=None):
        T = self.T if T is None else T
        R = self.R if R is None else R
        G = [None] * len(self.namen)
        for i in self.ordnung:
            if self.M[i] is not None:
                m = self.M[i]
            else:
                m = np.eye(4)
                m[:3, :3] = R[i] * self.S[i][None, :]
                m[:3, 3] = T[i]
            G[i] = (G[self.eltern[i]] @ m) if i in self.eltern else m
        return G

    def norm(self, p):
        """glTF-Weltpunkt -> normierte Meter (x seitlich, y vorn = -, z oben)."""
        return B.anwenden(self.mn, B.blender_koord(np.atleast_2d(p)))[0]

    def richtung_n_zu_g(self, d):
        return self.Rm.T @ d


def lm_loesen(f, x0, grenzen=None, it=40):
    """Levenberg-Marquardt mit numerischem Jacobian."""
    x = np.array(x0, float)
    lam = 1e-2
    r = f(x)
    for _ in range(it):
        J = np.zeros((len(r), len(x)))
        for k in range(len(x)):
            dx = np.zeros(len(x))
            dx[k] = 1e-5
            J[:, k] = (f(x + dx) - r) / 1e-5
        A = J.T @ J + lam * np.eye(len(x))
        try:
            dxs = -np.linalg.solve(A, J.T @ r)
        except np.linalg.LinAlgError:
            break
        xn = x + dxs
        rn = f(xn)
        if (rn @ rn) < (r @ r):
            x, r, lam = xn, rn, lam * 0.4
            if np.linalg.norm(dxs) < 1e-7:
                break
        else:
            lam *= 4
    return x


def kette_pos(s0, z0, laengen, phis):
    p = [(s0, z0)]
    for L, ph in zip(laengen, phis):
        s, z = p[-1]
        p.append((s + L * math.sin(ph), z - L * math.cos(ph)))
    return p


def rot_axis(a, w):
    a = a / np.linalg.norm(a)
    K = np.array([[0, -a[2], a[1]], [a[2], 0, -a[0]], [-a[1], a[0], 0]])
    return np.eye(3) + math.sin(w) * K + (1 - math.cos(w)) * K @ K


def norm_zu_g(sk, p):
    """Normierter Punkt -> glTF-Weltpunkt (Umkehrung von norm)."""
    pb = np.linalg.solve(sk.mn[:3, :3], np.asarray(p) - sk.mn[:3, 3])          # Blender-Koordinate
    return np.array([pb[0], pb[2], -pb[1]])


def main():
    sk = Skelett()
    aus = POS[0]
    os.makedirs(aus, exist_ok=True)
    G0 = sk.global_()
    ketten = {
        "LF": ["BN_L_Clavicle_038_039", "BN_L_UpperArm_039_040", "BN_l_Forearm_040_041", "BN_L_Hand_041_042", "BN_L_Toe_042_043"],
        "RF": ["BN_R_Clavicle_043_045", "BN_R_UpperArm_044_046", "BN_R_Forearm_045_047", "BN_R_Hand_046_048", "BN_R_Toe_047_049"],
        "LH": ["BN_L_Thing_051_054", "BN_L_Calf_052_055", "BN_L_HorseLink_053_056", "BN_L_Foot_054_057", "BN_L_Toe_2_055_058"],
        "RH": ["BN_R_Thing_056_060", "BN_R_Calf_057_061", "BN_R_HorseLink_058_062", "BN_R_Foot_00_063", "BN_R_Toe_2_059_064"],
    }
    shoe = next(p for p in sk.H.prims if p["mat"] == "Horseshoe")
    def skin_j(G):
        return np.array([G[ji] @ sk.ibm[k].T for k, ji in enumerate(sk.joints)])
    w0, _ = sk.H.haut(shoe, skin_j(G0))
    Q0 = B.anwenden(sk.mn, B.blender_koord(w0))
    xm = float(np.median(Q0[:, 0]))
    sohlenecken, sohle0 = {}, {}
    for nm in ketten:
        seite = 1 if nm[0] == "L" else -1
        vorn = nm[1] == "F"
        sel = np.where((np.sign(Q0[:, 0] - xm) == seite) & ((Q0[:, 1] < 0.0) == vorn))[0]
        tief = sel[np.argsort(Q0[sel, 2])[:12]]
        sohle12 = getattr(main, "_s12", {})
        sohle12[nm] = tief
        main._s12 = sohle12
        sohlenecken[nm] = np.where((np.sign(Q0[:, 0] - xm) == seite) & ((Q0[:, 1] < 0.0) == vorn) & (Q0[:, 2] < 0.06))[0]      # alle Ecken dieses Hufeisens (unterer Teil)
        sohle0[nm] = Q0[tief].mean(0)
        sohle_min = float(Q0[sohlenecken[nm]][:, 2].min())
    info = {}
    for nm, ks in ketten.items():
        pts = [sk.norm(G0[sk.id[k]][:3, 3]) for k in ks] + [sohle0[nm]]
        P2 = [(-p[1], p[2]) for p in pts]
        L = [math.hypot(P2[i + 1][0] - P2[i][0], P2[i + 1][1] - P2[i][1]) for i in range(len(P2) - 1)]
        ph = [math.atan2(P2[i + 1][0] - P2[i][0], -(P2[i + 1][1] - P2[i][1])) for i in range(len(P2) - 1)]
        # Neigung der Hufeisen-Ebene im Stand (Gerade z = a*s + b durch die unteren Ecken dieses Hufs): flach heisst Hufwinkel ph0 - Neigung
        seite = 1 if nm[0] == "L" else -1
        vorn = nm[1] == "F"
        selh = np.where((np.sign(Q0[:, 0] - xm) == seite) & ((Q0[:, 1] < 0.0) == vorn) & (Q0[:, 2] < 0.06))[0]
        a_ = np.polyfit(-Q0[selh, 1], Q0[selh, 2], 1)[0]
        neigung = math.atan(a_)
        info[nm] = dict(pts=pts, P2=P2, L=L, ph0=ph, sohle_g=norm_zu_g(sk, sohle0[nm]), flach=ph[-1] - neigung, neig=math.degrees(neigung))
        cl = Q0[sohlenecken[nm]]
        low = cl[cl[:, 2] < cl[:, 2].min() + 0.02]
        toe = low[np.argmin(low[:, 1])]                                   # vorderste tiefe Ecke (Zehe), y negativ = vorn
        v0 = np.array([-toe[1] + sohle0[nm][1], toe[2] - sohle0[nm][2]])      # Schwerpunkt der Sohle -> Zehe (s, z) im Stand
        a_ = -neigung
        info[nm]["toe_v"] = np.array([v0[0] * math.cos(a_) - v0[1] * math.sin(a_), v0[0] * math.sin(a_) + v0[1] * math.cos(a_)])
        print("KETTE", nm, "Hufneigung Grad", round(info[nm]["neig"], 1), "Laengen", [round(x, 3) for x in L], "Winkel0", [round(math.degrees(x), 1) for x in ph], "Sohle s,z", [round(v, 3) for v in P2[-1]])
    ZOFF = {nm: 0.0 for nm in ketten}
    THETA = math.radians(float(OPT.get("theta", 32.0)))
    P5 = OPT.get("tab") in ("p5", "p6")
    P6 = OPT.get("tab") == "p6"                                        # dazu: Oberarm/Oberschenkel im Fenster, Glätte über die Bilder (gegen Zucken)
    ROLLF = float(OPT.get("roll", 0.15))
    STANDN = {nm: (float(OPT.get("stand_v", 0.58)) if nm[1] == "F" else float(OPT.get("stand_h", 0.6)) + (float(OPT.get("rh_plus", 0.0)) if nm == "RH" else 0.0)) for nm in ketten}
    LSN = {nm: V * STANDN[nm] * T for nm in ketten}
    TSN = {nm: (1 - STANDN[nm]) * T for nm in ketten}
    # Standmitte: Vorderhuf unter dem Drehpunkt des Schulterblatts (Widerrist), Hinterhuf wie im Stand
    mitte = {nm: (info[nm]["P2"][0][0] + float(OPT.get("vorn_off", -0.02)) if nm[1] == "F" else (info[nm]["P2"][0][0] + float(OPT.get("hint_off", 0.0)) if "hint_off" in OPT else info[nm]["P2"][-1][0])) for nm in ketten}
    lo = {nm: mitte[nm] - LSN[nm] / 2 for nm in ketten}
    td = {nm: mitte[nm] + LSN[nm] / 2 for nm in ketten}

    KORR = {nm: np.zeros((NF + 1, 2)) for nm in ketten}
    aktfr = [0]

    def sohle_ziel(nm, ph):
        s_, z_ = sohle_ziel0(nm, ph)
        k = KORR[nm][aktfr[0]]
        return s_ + k[0], z_ + k[1]

    def rollen(nm, ph):
        """Abrollen ueber die Zehe in den letzten ROLLF des Stands: (Schwerpunkt s, z, Hufdrehung th >= 0 = Ferse hoch)."""
        STAND = STANDN[nm]
        rf_ = float(OPT.get("roll_v" if nm[1] == "F" else "roll_h", ROLLF))     # Abrollanteil je Bein vorn/hinten (Standard: --roll)
        r0 = STAND * (1 - rf_)
        vf = info[nm]["toe_v"]
        c0s = td[nm] - V * min(ph, STAND) * T
        if ph < r0:
            return c0s, ZOFF[nm], 0.0
        th = THETA * glatt((min(ph, STAND) - r0) / (STAND - r0))
        toe_s = td[nm] - V * r0 * T + vf[0] - V * (min(ph, STAND) - r0) * T
        toe_z = ZOFF[nm] + vf[1]
        dsv, dzv = -vf[0], -vf[1]
        return toe_s + dsv * math.cos(th) + dzv * math.sin(th), toe_z - dsv * math.sin(th) + dzv * math.cos(th), th

    def alpha_d(nm, ph):
        STAND = STANDN[nm]
        if ph < STAND:
            return -rollen(nm, ph)[2]
        u = (ph - STAND) / (1 - STAND)
        if P5:
            return schwung_huf(nm[1] == "F", u, THETA)
        return -THETA * (1 - glatt(u / 0.35))

    def sohle_ziel0(nm, ph):
        STAND, Ts = STANDN[nm], TSN[nm]
        if ph < STAND:
            s_, z_, _ = rollen(nm, ph)
            return s_, z_
        s_e, z_e, _ = rollen(nm, STAND)
        u = (ph - STAND) / (1 - STAND)
        m0 = -V * Ts
        u0, u1 = 0.10, 0.88                                              # senkrecht abheben und aufsetzen: Weltgeschwindigkeit waagerecht 0 am Anfang und am Ende
        sa, sb = s_e + m0 * u0, td[nm] - m0 * (1 - u1)
        if u < u0:
            s = s_e + m0 * u
        elif u > u1:
            s = td[nm] + m0 * (u - 1)
        else:
            w = (u - u0) / (u1 - u0)
            if P6 and nm[1] == "F":                                     # Vorderhuf bleibt im Schwung laenger hinten und schwingt spaet vor (Muybridge Bild 12: Huf haengt unter dem Karpus);
                w = w - 16 * float(OPT.get("spaet_v", 0.0)) * w * w * (1 - w) * (1 - w)   # Anfang und Ende (Lage und Geschwindigkeit) unveraendert
            d_ = (u1 - u0)
            s = (2 * w ** 3 - 3 * w ** 2 + 1) * sa + (w ** 3 - 2 * w ** 2 + w) * m0 * d_ + (-2 * w ** 3 + 3 * w ** 2) * sb + (w ** 3 - w ** 2) * m0 * d_
        up = 0.33
        hub_ = HUB_H if nm[1] == "H" else HUB
        return s, ZOFF[nm] + (z_e - ZOFF[nm]) * (1 - glatt(u / 0.3)) + hub_ * (glatt(u / up) if u < up else glatt((1 - u) / (1 - up)))

    def loese(nm, ph, S2, x0):
        vorn = nm[1] == "F"
        inf = info[nm]
        L, ph0 = inf["L"], inf["ph0"]
        s_t, z_t = sohle_ziel(nm, ph)
        STAND = STANDN[nm]
        if ph < STAND:
            w_a = float(OPT.get("wa_stand", 30.0))
        else:
            u_ = (ph - STAND) / (1 - STAND)
            w_a = 0.4 + float(OPT.get("wa_end", 30.0)) * glatt((u_ - float(OPT.get("wa_start", 0.45))) / 0.3) + 12.0 * (1 - glatt(u_ / 0.12))
            if P5:
                w_a += float(OPT.get("wa_mitte", 6.0))
        ALPHA = alpha_d(nm, ph)
        kr0 = 180 + math.degrees(ph0[4] - ph0[3])                       # Krongelenk/Hufgelenk im Stand (Fesselbein gegen Huf)
        fmin = (float(OPT.get("fmin_v", 150)) if vorn else float(OPT.get("fmin_h", 140))) if P5 else (172 if vorn else 150)
        if ph >= STAND:
            w_a += 8.0 * (1 - glatt(((ph - STAND) / (1 - STAND)) / 0.35))
        tk = tab("karpus", ph) if vorn else tab("knie", ph)
        tf = tab("fessel_v", ph) if vorn else tab("fessel_h", ph)
        ts = tab("sprung", ph)

        def f(x):
            so = kette_pos(S2[0], S2[1], L, x)[-1]
            res = [(so[0] - s_t) * 4000.0, (so[1] - z_t) * 4000.0, math.degrees(x[-1] - inf["flach"] - ALPHA) * w_a * 0.1]
            d = np.degrees(x)
            if vorn:
                ell, kar, fet = 180 - (d[1] - d[0]), 180 + (d[2] - d[1]), 180 + (d[3] - d[2])
                res += [(kar - tk) * WK, (fet - tf) * WF, (ell - tab("ellbogen", ph)) * WE, max(kar - 181.5, 0) * 5.0, max(fet - 238, 0) * 3.0, max(fmin - fet, 0) * 3.0, max(ell - 182, 0) * 5.0]
            else:
                kn, spr, fet = 180 + (d[1] - d[0]), 180 - (d[2] - d[1]), 180 + (d[3] - d[2])
                res += [(kn - tk) * WKN, (spr - ts) * WS, (fet - tf) * WF, max(kn - 181.5, 0) * 5.0, max(spr - 181.5, 0) * 5.0, max(fet - 238, 0) * 3.0, max(fmin - fet, 0) * 3.0]
            if P5 and ph >= STAND:                                      # Hufgelenk im Schwung in anatomischen Grenzen: hoechstens 20 Grad Streckung, 60 Grad Beugung gegen den Stand
                kr = 180 + (d[4] - d[3])
                res += [max(kr - kr0 - float(OPT.get("kr_streck", 20)), 0) * 3.0, max(kr0 - 60 - kr, 0) * 3.0]
            res += list((x - x0) * 0.5)
            if P6 and ph >= STAND:
                # nur im Schwung: Oberarm bzw. Oberschenkel nur in einem Fenster um die Ruhelage schwingen lassen (das Pferd hat kein Schlüsselbein: der Oberarm liegt
                # fest am Rumpf, das Vorführen kommt aus dem Drehen des Schulterblatts, hier als Verschieben des Schultergelenks, --pd)
                o0 = math.degrees(ph0[0])
                fen = float(OPT.get("ober_fenster_v", 15)) if vorn else float(OPT.get("ober_fenster_h", 20))
                res += [max(abs(d[0] - o0) - fen, 0) * float(OPT.get("wober", 1.5))]
                # Glätte: Abstand zur Lösung des vorigen Bildes (in Grad), gegen Springen zwischen zwei Lösungen
                res += list((d - np.degrees(x0)) * (float(OPT.get("wglatt", 0.15)) if vorn else float(OPT.get("wglatt_h", OPT.get("wglatt", 0.15)))))
            elif P6 and vorn and "ober_stand_v" in OPT:
                # Stand vorn: der Oberarm bleibt nahe der Ruhelage (Schultergelenk-Winkel im Stand fast fest), das Bein dreht sich als Ganzes um den Huf
                o0 = math.degrees(ph0[0]) + float(OPT.get("ober_stand_mitte", 0.0))
                res += [max(abs(d[0] - o0) - float(OPT["ober_stand_v"]), 0) * float(OPT.get("wober_stand", 1.0))]
            return np.array(res)
        return lm_loesen(f, x0)

    root, hals = sk.id["BN_Root_01_01"], sk.id["BN_Neck_00_06_06"]
    KURVE = kurven_biegung(sk) if "kurve" in OPT else []
    if KURVE and "kurve_stand" not in OPT:
        # Der Stand jedes Beins folgt seinem Ansatz: um so viel, wie die Biegung Schulter- bzw. Hueftgelenk in der Ruhepose laengs verschiebt
        Rb = sk.R.copy()
        Gb = sk.global_(sk.T, Rb)
        for i_, ax_p, grad in KURVE:
            ax_w = Gb[sk.eltern[i_]][:3, :3] @ ax_p
            rot = rot_axis(ax_w / np.linalg.norm(ax_w), math.radians(grad) * float(OPT["kurve"]))
            Rb[i_] = np.linalg.solve(Gb[sk.eltern[i_]][:3, :3], rot @ Gb[i_][:3, :3]) / sk.S[i_][None, :]
            Gb = sk.global_(sk.T, Rb)
        for nm, ks in ketten.items():
            ds = -sk.norm(Gb[sk.id[ks[0]]][:3, 3])[1] - info[nm]["P2"][0][0]
            td[nm] += ds
            lo[nm] += ds
            print("KURVE Stand", nm, "verschoben um %.1f cm" % (ds * 100))
    ax_lat_g = sk.Rm.T @ np.array([1.0, 0.0, 0.0])
    x_prev = {nm: np.array(info[nm]["ph0"]) for nm in ketten}

    def baue(fr, voll=False):
        aktfr[0] = fr
        tz = fr / FPS
        R, Tt = sk.R.copy(), sk.T.copy()
        bob = -float(OPT.get("drop", 0.045)) + 0.007 * math.cos(4 * math.pi * tz / T + 0.6)
        Gp = sk.global_(Tt, R)
        pr = Gp[sk.eltern[root]][:3, :3]
        Tt[root] = sk.T[root] + np.linalg.solve(pr, sk.Rm.T @ np.array([0, 0, bob]) / sk.sk)
        G = sk.global_(Tt, R)
        nick = math.radians(float(OPT.get("nick", 6.5))) * NICKV * math.cos(4 * math.pi * (tz - VIERTAKT["LF"] * T) / T) + math.radians(KOPFTIEF)
        R[hals] = np.linalg.solve(G[sk.eltern[hals]][:3, :3], rot_axis(ax_lat_g, nick) @ G[hals][:3, :3]) / sk.S[hals][None, :]
        G = sk.global_(Tt, R)
        beck, hueft = float(OPT.get("beck", 0.0)), float(OPT.get("hueft", 0.0))
        if beck or hueft:                                            # Becken kippt (Rollen um die Laengsachse) und dreht (Giern) im Schritt; die Wirbelsaeule davor gleicht aus, Schultern bleiben ruhig
            ax_len = sk.Rm.T @ np.array([0.0, -1.0, 0.0])
            ax_up = sk.Rm.T @ np.array([0.0, 0.0, 1.0])
            ph_ = 2 * math.pi * (tz / T) + float(OPT.get("beck_ph", 0.0))
            for name, f_roll, f_gier in (("BN_Spine_00_02_02", 1.0, 1.0), ("BN_Spine_01_03_03", -1.0, -1.0)):
                i_ = sk.id[name]
                rot = rot_axis(ax_len, math.radians(beck) * f_roll * math.sin(ph_)) @ rot_axis(ax_up, math.radians(hueft) * f_gier * math.cos(ph_))
                R[i_] = np.linalg.solve(G[sk.eltern[i_]][:3, :3], rot @ G[i_][:3, :3]) / sk.S[i_][None, :]
                G = sk.global_(Tt, R)
        if KURVE:                                                    # Kurvenhaltung: Biegung der Wirbelsaeule wie Walk_L gegen Walk des Originals (Mittel), mal --kurve (+1 links, -1 rechts);
            G = sk.global_(Tt, R)                                    # vor dem IK der Beine: deren Ansatz behaelt die Richtung in der Welt, das IK rechnet vom versetzten Schultergelenk aus
            fest = {ks[0]: G[sk.id[ks[0]]][:3, :3].copy() for ks in ketten.values()}
            for i_, ax_p, grad in KURVE:
                ax_w = G[sk.eltern[i_]][:3, :3] @ ax_p
                ax_w = ax_w / np.linalg.norm(ax_w)
                rot = rot_axis(ax_w, math.radians(grad) * float(OPT["kurve"]))
                R[i_] = np.linalg.solve(G[sk.eltern[i_]][:3, :3], rot @ G[i_][:3, :3]) / sk.S[i_][None, :]
                G = sk.global_(Tt, R)
            for kn, Mw in fest.items():
                i_ = sk.id[kn]
                R[i_] = np.linalg.solve(G[sk.eltern[i_]][:3, :3], Mw) / sk.S[i_][None, :]
                G = sk.global_(Tt, R)
        for nm, ks in ketten.items():
            phase = ((tz / T) - VIERTAKT[nm] + VL) % 1.0
            if nm[1] == "F" and PD:
                stz = STANDN[nm]
                dp = PD * (1 - 2 * phase / stz) if phase < stz else PD * (-1 + 2 * (phase - stz) / (1 - stz))       # Schulterblatt-Drehpunkt vor beim Aufsetzen, zurueck beim Abfussen
                if OPT.get("tab") == "p6":                                  # weich (Kosinus): ohne Knick in der Geschwindigkeit beim Wechsel Stand/Schwung
                    dp = PD * math.cos(math.pi * phase / stz) if phase < stz else -PD * math.cos(math.pi * (phase - stz) / (1 - stz))
                ci = sk.id[ks[0]]
                Gp_ = sk.global_(Tt, R)
                d_g = sk.Rm.T @ np.array([0.0, -dp, 0.0]) / sk.sk
                Tt[ci] = sk.T[ci] + np.linalg.solve(Gp_[sk.eltern[ci]][:3, :3], d_g)
                G = sk.global_(Tt, R)
            if False:
                # Schulterblatt: Schulter vor beim Aufsetzen, zurueck beim Abfussen (Schwung: zurueck nach vorn)
                ds = SCAP * (1 - 2 * phase / STAND) if phase < STAND else SCAP * (-1 + 2 * (phase - STAND) / (1 - STAND))
                ci = sk.id[ks[0]]
                ci = sk.eltern[ci]
                Gc = sk.global_(Tt, R)
                kopf_c = norm_zu_g(sk, sk.norm(Gc[ci][:3, 3]))
                sch = sk.norm(Gc[sk.id[ks[0]]][:3, 3])
                hebel = np.linalg.norm(sch - sk.norm(Gc[ci][:3, 3]))
                ang = math.asin(max(-0.9, min(0.9, ds / hebel)))
                best = None
                for sg in (1.0, -1.0):
                    Rn = R.copy()
                    Rn[ci] = np.linalg.solve(Gc[sk.eltern[ci]][:3, :3], rot_axis(ax_lat_g, sg * ang) @ Gc[ci][:3, :3]) / sk.S[ci][None, :]
                    Gn = sk.global_(Tt, Rn)
                    sn = sk.norm(Gn[sk.id[ks[0]]][:3, 3])
                    s_vor = -(sn[1] - sch[1])
                    if best is None or abs(s_vor - ds) < best[0]:
                        best = (abs(s_vor - ds), Rn)
                R = best[1]
                G = sk.global_(Tt, R)
            S_n = sk.norm(G[sk.id[ks[0]]][:3, 3])
            x = loese(nm, phase, (-S_n[1], S_n[2]), x_prev[nm])
            x_prev[nm] = x
            p2 = kette_pos(-S_n[1], S_n[2], info[nm]["L"], x)
            if OPT.get("debug") and nm == "LF":
                st = sohle_ziel(nm, phase)
                d_ = np.degrees(x); print("DBG LF fr", fr, "ph %.2f" % phase, "S %.3f %.3f" % (-S_n[1], S_n[2]), "ziel %.3f %.3f" % st, "ist %.3f %.3f" % p2[-1], "phis", np.round(d_, 0), "ell %.0f kar %.0f fet %.0f" % (180 - (d_[1] - d_[0]), 180 + d_[2] - d_[1], 180 + d_[3] - d_[2]))
            pn = info[nm]["pts"]
            for gi, kname in enumerate(ks):
                G = sk.global_(Tt, R)
                i = sk.id[kname]
                lat = pn[gi + 1][0] - pn[gi][0]
                d_n = np.array([lat, -(p2[gi + 1][0] - p2[gi][0]), p2[gi + 1][1] - p2[gi][1]])
                if gi + 1 < len(ks):
                    akt = G[sk.id[ks[gi + 1]]][:3, 3] - G[i][:3, 3]
                else:
                    sg = (G[i] @ np.linalg.inv(G0[i]) @ np.append(info[nm]["sohle_g"], 1.0))[:3]
                    akt = sg - G[i][:3, 3]
                Rd = rot_zw(akt, sk.Rm.T @ d_n)
                R[i] = np.linalg.solve(G[sk.eltern[i]][:3, :3], Rd @ G[i][:3, :3]) / sk.S[i][None, :]
        return R, Tt

    pass_rahmen = None
    for durchlauf in range(int(OPT.get("passes", 6))):
        pass_rahmen = [baue(fr) for fr in range(NF)]
        # Regelkreis: tatsaechliche Sohlenlage (Mittel der tiefsten Hufeisenecken) gegen Ziel, Korrektur fuer den naechsten Durchlauf
        fehler = 0.0
        for fr in range(NF):
            G = sk.global_(pass_rahmen[fr][1], pass_rahmen[fr][0])
            w, _ = sk.H.haut(shoe, skin_j(G))
            Qf = B.anwenden(sk.mn, B.blender_koord(w))
            for nm in ketten:
                ist = Qf[main._s12[nm]].mean(0)
                phase = ((fr / FPS / T) - VIERTAKT[nm] + VL) % 1.0
                s_t, z_t = sohle_ziel0(nm, phase)
                e = np.array([s_t - (-ist[1]), (z_t + 0.004) - ist[2]])
                if OPT.get("fehler") and durchlauf == int(OPT.get("passes", 6)) - 1 and phase < STAND:
                    print("ERR", nm, fr, "ph %.2f" % phase, "es %.1f ez %.1f mm" % (e[0] * 1000, e[1] * 1000))
                if phase < STANDN[nm] - float(OPT.get("korr_ende", 0.03)) or phase > 0.93:
                    KORR[nm][fr] = np.clip(KORR[nm][fr] + 0.8 * e, -0.03, 0.03)
                fehler = max(fehler, float(np.abs(e).max()))
        print("PASS", durchlauf, "max Fehler mm", round(fehler * 1000, 1))
        if False:                                      # Sohlenhoehe nachfuehren: tiefste Ecke des Hufeisens im Stand soll auf z = 0 liegen
            for nm in ketten:
                zz = []
                for fr in range(0, int(NF * 0.5), 2):
                    ph_ = ((fr / FPS / T) - VIERTAKT[nm]) % 1.0
                    if ph_ < 0.5:
                        G = sk.global_(pass_rahmen[fr][1], pass_rahmen[fr][0])
                        w, _ = sk.H.haut(shoe, skin_j(G))
                        zz.append(float(B.anwenden(sk.mn, B.blender_koord(w))[sohlenecken[nm]][:, 2].min()))
                for fr in range(NF):
                    ph_ = ((fr / FPS / T) - VIERTAKT[nm]) % 1.0
                    if 0.05 < ph_ < 0.45:
                        G = sk.global_(pass_rahmen[fr][1], pass_rahmen[fr][0])
                        w, _ = sk.H.haut(shoe, skin_j(G))
                        zz.append(float(B.anwenden(sk.mn, B.blender_koord(w))[sohlenecken[nm]][:, 2].min()))
                ZOFF[nm] -= float(np.median(zz)) - 0.001
            print("ZOFF", {k: round(v, 4) for k, v in ZOFF.items()})
    pass_rahmen.append(pass_rahmen[0])
    Jfr, Gfr = [], []
    for R, Tt in pass_rahmen:
        G = sk.global_(Tt, R)
        Gfr.append(G)
        Jfr.append(skin_j(G))
    Jfr = np.array(Jfr, np.float32)
    np.savez_compressed(os.path.join(aus, "gehen_J.npz"), J=Jfr, R=np.array([r for r, _ in pass_rahmen]), T=np.array([t for _, t in pass_rahmen]), V=V, Tz=T, fps=FPS)
    # ---- Messung ----
    shoe_w = []
    for G in Gfr:
        w, _ = sk.H.haut(shoe, skin_j(G))
        shoe_w.append(B.anwenden(sk.mn, B.blender_koord(w)))
    bericht = {"T": T, "v": V, "bilder": NF, "hub_soll": HUB}
    kontakt, hub, gleiten, ueber_td = {}, {}, {}, {}
    for nm in ketten:
        zs = np.array([shoe_w[f][sohlenecken[nm]][:, 2].min() for f in range(NF)])
        pos_s = np.array([-shoe_w[f][sohlenecken[nm]][:, 1].mean() for f in range(NF)])
        wx = pos_s + V * np.arange(NF) / FPS                         # Weltweg der Sohle
        print("ZS", nm, " ".join("%.0f" % (z * 1000) for z in zs))
        if OPT.get("diag"):
            print("DIAG", nm, " ".join("%d:%.0f/%.0f" % (f, zs[f] * 1000, (wx[f] - wx[0]) * 1000) for f in range(NF)))
        am_boden = zs < 0.006
        kontakt[nm] = am_boden
        hub[nm] = float(zs.max())
        # laengste zusammenhaengende Stand-Folge (zyklisch)
        best = (0.0, 0)
        runs = []
        f = 0
        dbl = np.concatenate([am_boden, am_boden])
        k = 0
        while k < len(dbl):
            if dbl[k]:
                e = k
                while e + 1 < len(dbl) and dbl[e + 1]:
                    e += 1
                runs.append((k, e))
                k = e + 1
            else:
                k += 1
        runs = [(a, b) for a, b in runs if a < NF and b - a >= 3]
        if runs:
            a, b = max(runs, key=lambda r: r[1] - r[0])
            idx = [m % NF for m in range(a, b + 1)]
            wxx = np.array([wx[m % NF] + (V * T if m >= NF else 0.0) for m in range(a, b + 1)])
            gleiten[nm] = float((wxx.max() - wxx.min()) * 1000)
        else:
            gleiten[nm] = None
    kh = [float(sk.norm(Gfr[f][sk.id["BN_Head_00_016_015"]][:3, 3])[2]) for f in range(NF)]
    bericht["kopf_z_mm"] = [round(k * 1000, 1) for k in kh]
    bericht["kopf_nick_hub_mm"] = round((max(kh) - min(kh)) * 1000, 1)
    bericht["kopf_tiefst_bild"] = int(np.argmin(kh))
    bericht["gleiten_mm"] = gleiten
    # Gleiten nur ueber die Standphase des Zyklus (Phase 0,02 bis 0,56 des jeweiligen Beins), Messpunkt: Mittel der tiefsten Hufeisenecken des Stehens
    gl2 = {}
    for nm in ketten:
        wxs = []
        for f in range(NF):
            ph_ = ((f / FPS / T) - VIERTAKT[nm]) % 1.0
            if 0.02 <= ph_ <= 0.56:
                ist = shoe_w[f][main._s12[nm]].mean(0)
                wxs.append(-ist[1] + V * f / FPS + (V * T if ((f / FPS / T) - VIERTAKT[nm]) < 0 else 0.0))
        wxs = np.array(wxs)
        gl2[nm] = float((wxs.max() - wxs.min()) * 1000) if len(wxs) else None
    bericht["gleiten_stand_mm"] = gl2
    # Strenge Messung: tiefste Ecke des Hufeisens je Bild; Kontakt wenn sie < 4 mm hoch liegt; Gleiten = Spanne ihrer Weltlage in Laufrichtung ueber eine zusammenhaengende Bodenphase
    gl3 = {}
    for nm in ketten:
        zmin_, xw = [], []
        for f in range(NF):
            q = shoe_w[f][sohlenecken[nm]]
            z0_ = float(q[:, 2].min())
            patch = q[q[:, 2] < z0_ + 0.003]
            zmin_.append(z0_)
            xw.append(float(-patch[:, 1].mean() + V * f / FPS))
        zmin_, xw = np.array(zmin_), np.array(xw)
        kont = zmin_ < 0.004
        besten, k = 0.0, 0
        dbl = np.concatenate([kont, kont])
        xw2 = np.concatenate([xw, xw + V * T])
        while k < NF:
            if dbl[k] and not (k > 0 and dbl[k - 1]) or (k == 0 and dbl[0] and not dbl[-1 + NF]):
                e = k
                while e + 1 < len(dbl) and dbl[e + 1]:
                    e += 1
                besten = max(besten, float((xw2[k:e + 1].max() - xw2[k:e + 1].min()) * 1000))
                k = e + 1
            else:
                k += 1
        gl3[nm] = besten
    bericht["gleiten_streng_mm"] = gl3
    # Vertexweise: jede Hufeisenecke fuer sich; Kontakt wenn z < 4 mm; Gleiten = Spanne der Weltlage in Laufrichtung ueber eine zusammenhaengende Bodenphase der Ecke (zyklisch)
    gl4 = {}
    for nm in ketten:
        ix = sohlenecken[nm]
        zz = np.array([shoe_w[f][ix][:, 2] for f in range(NF)])
        xx = np.array([-shoe_w[f][ix][:, 1] + V * f / FPS for f in range(NF)])
        worst = 0.0
        for kk in range(len(ix)):
            kont = np.concatenate([zz[:, kk] < 0.004] * 2)
            xs = np.concatenate([xx[:, kk], xx[:, kk] + V * T])
            f = 0
            while f < NF:
                if kont[f] and not kont[f - 1 if f > 0 else 2 * NF - 1]:
                    e = f
                    while e + 1 < 2 * NF and kont[e + 1]:
                        e += 1
                    if e - f + 1 < 2 * NF:
                        worst = max(worst, float((xs[f:e + 1].max() - xs[f:e + 1].min()) * 1000))
                        if OPT.get("diag_gleiten") and (xs[f:e + 1].max() - xs[f:e + 1].min()) > 0.004:
                            print("GLEITEN", nm, "Ecke", kk, "Bilder", f, e, "mm %.1f" % ((xs[f:e + 1].max() - xs[f:e + 1].min()) * 1000), "x", " ".join("%.0f" % (v * 1000) for v in xs[f:e + 1]))
                    f = e + 1
                else:
                    f += 1
        gl4[nm] = worst
    bericht["gleiten_vertex_mm"] = gl4
    # Spur: Weltlage des Sohlenmittelpunkts in der Standmitte (Beinphase 0,25); Hinterhuf gegen Abdruck des gleichseitigen Vorderhufs aus dem vorigen Zyklus
    xs_ = {}
    for nm in ketten:
        fm = int(round(((0.25 + VIERTAKT[nm]) % 1.0) * NF)) % NF
        xs_[nm] = float(-shoe_w[fm][main._s12[nm]].mean(0)[1] + V * (fm / FPS) + (V * T if (0.25 + VIERTAKT[nm]) >= 1.0 else 0.0))
    bericht["spur_cm"] = {"links": round((xs_["LH"] - (xs_["LF"] - V * T)) * 100, 1), "rechts": round((xs_["RH"] - (xs_["RF"] - V * T)) * 100, 1)}
    bericht["hub_m"] = hub
    bericht["hub_sohlenpunkt_m"] = {nm: float(max(shoe_w[f][main._s12[nm]].mean(0)[2] for f in range(NF)) - min(shoe_w[f][main._s12[nm]].mean(0)[2] for f in range(NF))) for nm in ketten}
    bericht["stand_anteil"] = {nm: float(kontakt[nm].mean()) for nm in ketten}
    # Aufsetzreihenfolge
    reihe = []
    for nm in ketten:
        a = kontakt[nm]
        for f in range(NF):
            if a[f] and not a[f - 1]:
                reihe.append((f / NF, nm))
    reihe.sort()
    bericht["aufsetzen"] = [(round(p, 3), n) for p, n in reihe]
    # Gelenkwinkel aus den echten Gelenkpunkten (FK), je Bein und Phase
    winkel = {}
    for nm, ks in ketten.items():
        vorn = nm[1] == "F"
        rows = []
        for f in range(NF):
            pts = [sk.norm(Gfr[f][sk.id[k]][:3, 3]) for k in ks] + [shoe_w[f][sohlenecken[nm]].mean(0)]
            P2 = [(-p[1], p[2]) for p in pts]
            phs = [math.degrees(math.atan2(P2[i + 1][0] - P2[i][0], -(P2[i + 1][1] - P2[i][1]))) for i in range(len(P2) - 1)]
            ph_ = ((f / FPS / T) - VIERTAKT[nm]) % 1.0
            kr_ = 180 + phs[4] - phs[3]                                     # Krongelenk (Fesselbein gegen Huf)
            hd_ = phs[4]                                                    # Hufrichtung; unten gegen die Standmitte (Phase 0,25) gerechnet
            # Zusatz: Neigung des ersten Glieds (vorn Oberarm = Schulterwinkel gegen die Senkrechte, hinten Oberschenkel = Hueftwinkel) und Laengslage des Kettenanfangs (Schultergelenk bzw. Hueftgelenk)
            if vorn:
                rows.append((ph_, 180 + phs[2] - phs[1], 180 + phs[3] - phs[2], 180 - (phs[1] - phs[0]), kr_, hd_, phs[0], P2[0][0], phs[1], phs[2], phs[3]))
            else:
                rows.append((ph_, 180 + phs[1] - phs[0], 180 - (phs[2] - phs[1]), 180 + phs[3] - phs[2], kr_, hd_, phs[0], P2[0][0], phs[1], phs[2], phs[3]))
        ref = min(rows, key=lambda r: abs(r[0] - 0.25))[5]                  # Hufdrehung gegen die Standmitte (negativ: Sohle nach hinten)
        winkel[nm] = [r[:5] + (r[5] - ref,) for r in rows]
        bericht.setdefault("winkel_bilder", {})[nm] = {"spalten": ["phase", "karpus|knie", "fessel_v|sprung", "ellbogen|fessel_h", "krone", "huf", "oberarm|oberschenkel", "gelenk_s_m", "neigung_glied1", "neigung_glied2", "neigung_glied3"],
                                                       "werte": [[round(float(v), 3) for v in r[:5] + (r[5] - ref,) + r[6:]] for r in rows]}
    bericht["winkel_extrema"] = {}
    for nm, rows in winkel.items():
        a = np.array(rows)
        bericht["winkel_extrema"][nm] = {"min": a[:, 1:].min(0).round(1).tolist(), "max": a[:, 1:].max(0).round(1).tolist()}
    # Tabellenvergleich bei 0/15/30/45/60/70/80/90 % (gemittelt ueber die vier Beine je Gruppe)
    vergleich = []
    for ph_k in KNOTEN[:-1]:
        zeile = {"phase": float(ph_k)}
        vor = [r for nm in ("LF", "RF") for r in winkel[nm] if abs(((r[0] - ph_k + 0.5) % 1) - 0.5) < 0.5 / NF * 1.5]
        hin = [r for nm in ("LH", "RH") for r in winkel[nm] if abs(((r[0] - ph_k + 0.5) % 1) - 0.5) < 0.5 / NF * 1.5]
        if vor:
            v = np.array(vor)[:, 1:].mean(0)
            zeile["karpus"] = [round(float(v[0]), 1), tab("karpus", ph_k)]
            zeile["fessel_v"] = [round(float(v[1]), 1), tab("fessel_v", ph_k)]
            zeile["ellbogen"] = [round(float(v[2]), 1), tab("ellbogen", ph_k)]
            zeile["krone_v"] = round(float(v[3]), 1)
            zeile["huf_v"] = round(float(v[4]), 1)
        if hin:
            h = np.array(hin)[:, 1:].mean(0)
            zeile["knie"] = [round(float(h[0]), 1), tab("knie", ph_k)]
            zeile["sprung"] = [round(float(h[1]), 1), tab("sprung", ph_k)]
            zeile["fessel_h"] = [round(float(h[2]), 1), tab("fessel_h", ph_k)]
            zeile["krone_h"] = round(float(h[3]), 1)
            zeile["huf_h"] = round(float(h[4]), 1)
        vergleich.append(zeile)
    bericht["tabelle"] = vergleich
    # Naht: Unterschied Bild NF zu Bild 0 (immer 0, da kopiert) und Schritt zum letzten Bild
    bericht["naht_mm"] = float(np.abs(shoe_w[0] - shoe_w[0]).max() * 1000)
    # Ueberschreiten: Hinterhuf-Aufsetzpunkt gegen Vorderhuf (Welt), links
    json.dump(bericht, open(os.path.join(aus, "gehen_messung.json"), "w"), indent=1, default=float)
    print(json.dumps(bericht, indent=1, default=float)[:3500])


if __name__ == "__main__":
    main()

































