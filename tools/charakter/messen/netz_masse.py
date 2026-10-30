"""Netzmaße eines Kopfes in einer Definition (Charakter Phase 6, S0). Alle Längen in mm, Winkel in Grad.

Grundlage sind die Merkpunkte aus merkpunkte.py (ein Satz für alles). Höhen und Breiten sind frontal projiziert
(y bzw. x), „n_me“ und „se_me“ sind Raumabstände (Messzirkel, wie NIOSH „Menton–Sellion length“).

  ipd                 Pupillenabstand |x(ps_l) − x(ps_r)|
  zy                  Jochbeinbreite x(zy_l) − x(zy_r) (größte Breite ≥ 15 mm vor der Ohrwurzel, Höhe n − 45 … n − 10)
  n_me, se_me         Gesichtshöhe Nasion–Menton (Raum); se = n
  gesichtsindex       n_me / zy
  zy_ipd              zy / ipd
  b_mund, b_kinnmitte Breite des Gesichts (vor der Ohrwurzel wie zy) in Höhe sto bzw. (sto + me)/2
  mund_zy, kinnmitte_zy  dieselben / zy
  unterkiefer         Breite go–go (ICT-Kieferwinkel lm3/lm13)
  sn_sto, sn_me, mundlage   Oberlippe, Untergesicht (Höhen) und ihr Verhältnis
  ps_me_ipd, ps_sn_ipd, sn_sto_ipd, sto_me_ipd   Höhen ab der Pupillenlinie / IPD (wie in den Vorlagen gemessen)
  braue_pupille       Unterkante der Braue über der Pupille (Mittel beider Seiten)
  wangenwoelbung      Pfeilhöhe der seitlichen Wange (|x| 36–44 mm, vorderste Haut, Mittel beider Seiten) bei t = 0,5
                      gegen die Sehne t = 0,15 … 0,8; t = 0 Pupillenhöhe, t = 1 Kinn (ICT-Merkpunkt 8 = „kinn“ der
                      Kopfbeschreibung, wie in der Bestandsaufnahme); + = voll, − = hohl
  augentiefe          Hornhautscheitel hinter der Linie Braue–Jochbein (Schnitt x = x(ps), + = tiefer)
  nasofrontalwinkel   Winkel g–n–prn
  pg_hinter_li        z(li) − z(pg): Kinn hinter der Unterlippe
  nasenvorsprung      z(prn) − z(sn)
  nasenbreite, mundbreite   al–al, ch–ch
  augapfel_ipd        Augapfel (Lederhaut, Ausdehnung in x) / IPD – Iris : IPD misst bild_werte im Bild
  kieferknick         größter Normalenknick der Kieferkante auf 5 mm (Schnitte x = ±0,7·x(go)) mit den Normalen, die Godot
                      zeigt (Grundnetz + Normalen der Formen, soweit die .glb sie hat); kieferknick_form: mit Normalen
                      aus der verformten Form (die geometrische Kante)
  kinn_hals_winkel    Winkel me–C–N: C tiefster Punkt des Profils unter dem Kinn, N 20 mm tiefer am Hals
  kinn_kragen         me über der Oberkante des Kragens (vorn, Mitte)
  spiegel_rms         Spiegelabstand: RMS |P(i) − gespiegelt P(Partner)| über die Gesichtsecken vor der Ohrwurzel
  lid_augapfel        kleinster Abstand der Lidrand-Ecken (2 mm um lm36–47) zur Augenoberfläche (Lederhaut, Hornhaut) entlang
                      deren Normale (− = Durchdringung)
  lippe_zahn          kleinster Abstand Lippe (innen) – Zahn nach vorn (− = Durchdringung)
"""
import numpy as np

from .merkpunkte import Kopfnetz


def _breite(H, y, z_min, band=1.0):
    q = H[(np.abs(H[:, 1] - y) < band) & (H[:, 2] > z_min)]
    if len(q) < 2:
        return float("nan")
    return float(q[:, 0].max() - q[:, 0].min())


def _winkel(a, b, c):
    """Winkel bei b (Grad)."""
    u, v = a - b, c - b
    return float(np.degrees(np.arccos(np.clip(np.dot(u, v) / (np.linalg.norm(u) * np.linalg.norm(v)), -1, 1))))


class Netzmesser:
    def __init__(self, glb):
        self.netz = Kopfnetz(glb)
        self.glb = glb
        self._paare = None
        self._lidring = None
        self._kugel = None
        try:
            self.kragen = glb.teil("Kragen", 0)
        except KeyError:
            self.kragen = None
        z = glb.teile("Zaehne")
        self.zaehne = z[0]

    # ------------------------------------------------------------------ Spiegelpartner (einmal am Grundnetz)
    def _spiegelpaare(self):
        if self._paare is None:
            G = self.netz.haut.grund * 1000.0
            schl = {}
            for i, p in enumerate(np.round(G / 0.05).astype(np.int64)):
                schl.setdefault((p[0], p[1], p[2]), i)
            paare = []
            for i, p in enumerate(np.round(G * np.array([-1, 1, 1]) / 0.05).astype(np.int64)):
                j = None
                for dx in (0, -1, 1):
                    for dy in (0, -1, 1):
                        for dz in (0, -1, 1):
                            j = schl.get((p[0] + dx, p[1] + dy, p[2] + dz))
                            if j is not None:
                                break
                        if j is not None:
                            break
                    if j is not None:
                        break
                if j is not None and G[i, 0] < -0.5:
                    paare.append((i, j))
            self._paare = np.array(paare)
        return self._paare

    def _lidecken(self):
        """Ecken der Lidränder: Hautecken im Umkreis von 2 mm um die Strecken der Augenmerkpunkte (Grundnetz)."""
        if self._lidring is None:
            G = self.netz.haut.grund * 1000.0
            L = G[self.netz.lm]
            sel = np.zeros(len(G), bool)
            for ring in (range(36, 42), range(42, 48)):
                r = list(ring)
                for a, b in zip(r, r[1:] + r[:1]):
                    for t in np.linspace(0, 1, 6):
                        p = L[a] * (1 - t) + L[b] * t
                        sel |= np.linalg.norm(G - p, axis=1) < 2.0
            self._lidring = np.nonzero(sel)[0]
        return self._lidring

    # ------------------------------------------------------------------ Maße
    def messen(self, gewichte, punkte=None):
        P = punkte or self.netz.punkte(gewichte)
        H = P["_haut"]
        z_ohr = P["_z_ohr"]
        m = {}
        m["ipd"] = float(P["ps_l"][0] - P["ps_r"][0])
        m["zy"] = float(P["zy_l"][0] - P["zy_r"][0])
        m["n_me"] = float(np.linalg.norm(P["n"] - P["me"]))
        m["se_me"] = m["n_me"]
        m["n_me_y"] = float(P["n"][1] - P["me"][1])
        m["gesichtsindex"] = m["n_me"] / m["zy"]
        m["zy_ipd"] = m["zy"] / m["ipd"]
        y_km = 0.5 * (P["sto"][1] + P["me"][1])
        m["b_mund"] = _breite(H, P["sto"][1], z_ohr)
        m["b_kinnmitte"] = _breite(H, y_km, z_ohr)
        m["mund_zy"] = m["b_mund"] / m["zy"]
        m["kinnmitte_zy"] = m["b_kinnmitte"] / m["zy"]
        m["unterkiefer"] = float(P["go_l"][0] - P["go_r"][0])
        m["sn_sto"] = float(P["sn"][1] - P["sto"][1])
        m["sn_me"] = float(P["sn"][1] - P["me"][1])
        m["mundlage"] = m["sn_sto"] / m["sn_me"]
        ps_y = 0.5 * (P["ps_r"][1] + P["ps_l"][1])
        for name, a, b in (("ps_me", ps_y, P["me"][1]), ("ps_sn", ps_y, P["sn"][1]), ("sn_sto", P["sn"][1], P["sto"][1]),
                           ("sto_me", P["sto"][1], P["me"][1])):
            m[f"{name}_ipd"] = float(a - b) / m["ipd"]
        m["braue_pupille"] = float(0.5 * (P["br_r"][1] + P["br_l"][1]) - ps_y)
        m["wangenwoelbung"] = self._wange(H, ps_y, P["lm08"][1])
        m["augentiefe"] = self._augentiefe(H, P)
        m["nasofrontalwinkel"] = _winkel(P["g"], P["n"], P["prn"])
        m["pg_hinter_li"] = float(P["li"][2] - P["pg"][2])
        m["nasenvorsprung"] = float(P["prn"][2] - P["sn"][2])
        m["nasenbreite"] = float(P["al_l"][0] - P["al_r"][0])
        m["mundbreite"] = float(P["ch_l"][0] - P["ch_r"][0])
        # Iris : IPD misst bild_werte (die Iris ist eine Grenze im Shader, keine Netzkante); am Netz zählt die Größe des
        # Augapfels (Ausdehnung der Lederhaut in x) gegen den IPD
        A = self.glb.teile("Augen")[0].lage(gewichte) * 1000.0
        d = [np.ptp(A[A[:, 0] < 0][:, 0]), np.ptp(A[A[:, 0] > 0][:, 0])]
        m["augapfel_ipd"] = float(np.mean(d)) / m["ipd"]
        m["kieferknick"] = self._kieferknick(gewichte, H, P, gerendert=True)
        m["kieferknick_form"] = self._kieferknick(gewichte, H, P, gerendert=False)
        m["kinn_hals_winkel"] = self._kinn_hals(P)
        m["kinn_kragen"] = self._kinn_kragen(gewichte, P)
        m["spiegel_rms"] = self._spiegel(H, z_ohr)
        m["lid_augapfel"] = self._lid(gewichte, H)
        m["lippe_zahn"] = self._lippe_zahn(gewichte, H, P)
        return m

    def _wange(self, H, y_auge, y_kinn):
        sel = (np.abs(np.abs(H[:, 0]) - 40.0) <= 4.0) & (H[:, 2] > 40)
        q = H[sel]
        ts = np.array([0.15, 0.5, 0.8])
        zs = []
        for t in ts:
            y = y_auge - t * (y_auge - y_kinn)
            r = q[np.abs(q[:, 1] - y) < 1.5]
            # beide Seiten getrennt, dann Mittel
            z = [r[r[:, 0] < 0][:, 2].max(), r[r[:, 0] > 0][:, 2].max()]
            zs.append(np.mean(z))
        sehne = zs[0] + (zs[2] - zs[0]) * (ts[1] - ts[0]) / (ts[2] - ts[0])
        return float(zs[1] - sehne)

    def _augentiefe(self, H, P):
        werte = []
        for s in ("r", "l"):
            x, y = P[f"ps_{s}"][0], P[f"ps_{s}"][1]
            q = H[np.abs(H[:, 0] - x) < 2.0]
            b = q[(q[:, 1] > y + 8) & (q[:, 1] < y + 25)]
            j = q[(q[:, 1] > y - 30) & (q[:, 1] < y - 12)]
            B, J = b[np.argmax(b[:, 2])], j[np.argmax(j[:, 2])]
            hs = P[f"hs_{s}"]
            z_linie = B[2] + (J[2] - B[2]) * (hs[1] - B[1]) / (J[1] - B[1])
            werte.append(z_linie - hs[2])
        return float(np.mean(werte))

    def _kieferknick(self, gewichte, H, P, gerendert=True):
        # gerendert: Normalen wie Godot sie zeigt (Grundnetz + Normalen-Versatz der Formen, die NORMAL haben);
        # sonst aus der verformten Form neu gerechnet (geometrische Kante)
        N = self.netz.haut.normale(gewichte) if gerendert else self.netz.haut.flaechennormalen(H)
        best = 0.0
        for s in ("r", "l"):
            x0 = 0.7 * P[f"go_{s}"][0]
            sel = (np.abs(H[:, 0] - x0) < 1.5) & (H[:, 1] < P["sto"][1] - 5) & (H[:, 1] > P["me"][1] - 40) & (H[:, 2] > P["_z_ohr"])
            q, n = H[sel], N[sel]
            if len(q) < 6:
                continue
            # vordere Hülle: je 1 mm Höhe die vorderste Ecke, sortiert von oben nach unten
            o = np.argsort(-q[:, 1])
            q, n = q[o], n[o]
            pts, nrm = [], []
            for yb in np.arange(q[:, 1].max(), q[:, 1].min(), -1.0):
                k = np.nonzero(np.abs(q[:, 1] - yb) < 0.5)[0]
                if len(k):
                    i = k[np.argmax(q[k, 2])]
                    pts.append(q[i, 1:]), nrm.append(n[i, 1:])
            pts, nrm = np.array(pts), np.array(nrm)
            if len(pts) < 6:
                continue
            bogen = np.concatenate([[0], np.cumsum(np.linalg.norm(np.diff(pts, axis=0), axis=1))])
            winkel = np.degrees(np.arctan2(nrm[:, 0], nrm[:, 1]))  # Richtung der Normalen in der y-z-Ebene
            for i in range(len(bogen)):
                j = np.searchsorted(bogen, bogen[i] + 5.0)
                if j < len(bogen):
                    best = max(best, abs(winkel[j] - winkel[i]))
        return float(best)

    def _kinn_hals(self, P):
        ys, zf = self.netz._profil
        me = P["me"]
        sel = (ys < me[1] - 5) & (ys > me[1] - 35)
        if not sel.any():
            return float("nan")
        i = np.argmin(np.where(sel, zf, np.inf))
        C = np.array([0.0, ys[i], zf[i]])
        yN = C[1] - 20.0
        N = np.array([0.0, yN, float(np.interp(yN, ys, zf))])
        return _winkel(me, C, N)

    def _kinn_kragen(self, gewichte, P):
        if self.kragen is None:
            return float("nan")
        K = self.kragen.lage(gewichte) * 1000.0
        k = K[(np.abs(K[:, 0]) < 20) & (K[:, 2] > P["me"][2] - 70)]
        return float(P["me"][1] - k[:, 1].max()) if len(k) else float("nan")

    def _spiegel(self, H, z_ohr):
        paare = self._spiegelpaare()
        a, b = H[paare[:, 0]], H[paare[:, 1]] * np.array([-1, 1, 1])
        vorn = (H[paare[:, 0], 2] > z_ohr) & (H[paare[:, 0], 1] > 680)
        d = np.linalg.norm(a - b, axis=1)[vorn]
        return float(np.sqrt(np.mean(d ** 2)))

    def _lid(self, gewichte, H):
        """Kleinster Abstand der Lidrand-Ecken zur Augenoberfläche entlang deren Normale (− = Lid im Auge)."""
        teile = self.glb.teile("Augen")
        E = np.concatenate([teile[0].lage(gewichte), teile[4].lage(gewichte)]) * 1000.0
        N = np.concatenate([teile[0].normale(gewichte), teile[4].normale(gewichte)])
        lid = H[self._lidecken()]
        best = np.inf
        for p in lid:
            d = np.linalg.norm(E - p, axis=1)
            i = np.argmin(d)
            if d[i] < 4.0:
                best = min(best, float(np.dot(p - E[i], N[i])))
        return best

    def _lippe_zahn(self, gewichte, H, P):
        Z = self.zaehne.lage(gewichte) * 1000.0
        sto = P["sto"]
        z = Z[(np.abs(Z[:, 0]) < 15) & (np.abs(Z[:, 1] - sto[1]) < 10)]
        lippe = H[(np.abs(H[:, 0]) < 16) & (np.abs(H[:, 1] - sto[1]) < 11) & (H[:, 2] > sto[2] - 8)]
        if not len(z) or not len(lippe):
            return float("nan")
        best = np.inf
        for p in z[np.argsort(-z[:, 2])][:200]:
            v = lippe[(np.abs(lippe[:, 0] - p[0]) < 1.0) & (np.abs(lippe[:, 1] - p[1]) < 1.0)]
            if len(v):
                best = min(best, float(v[:, 2].max() - p[2]))
        return best
