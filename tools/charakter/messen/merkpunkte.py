"""Merkpunkte am Kopfnetz: die 68 ICT-Merkpunkte und die anthropometrischen Punkte daraus (Charakter Phase 6, S0).

Koordinaten wie in der .glb und in Godot: x nach links der Figur (im Bild von vorn rechts), y nach oben, z nach vorn
(Blickrichtung des Gesichts); hier in Millimetern. Ein Satz Merkpunkte für alle Messungen (netz_masse, bild_werte).

Definitionen (Weichteil, Netz „Haut“ Primitiv 0, nach den Formgewichten verformt; „Profil“ = vorderes Profil der
Mittellinie: Ecken mit |x| < 1,5 mm, je 0,5 mm Höhe die vorderste, obere Hülle über ±1,5 mm, ohne Hinterkopf):
  lm00 … lm67  ICT-Merkpunkte (iBUG-Reihenfolge, merkpunkte_ict68.json)
  ps_r, ps_l   Pupille: Mitte der Pupillenöffnung im Iris-Netz (Netz „Augen“, Primitiv 1, innerer Rand) je Seite (r: x < 0);
               so liegt sie wie die gemalte, sichtbare Pupille (0,9 mm weiter außen als die Mitte aller Iris-Ecken)
  hs_r, hs_l   Hornhautscheitel: vorderste Ecke der Hornhaut (Augen, Primitiv 4) je Seite
  en_r, en_l   Endocanthion (innerer Lidwinkel) = lm39 / lm42
  ex_r, ex_l   Exocanthion (äußerer Lidwinkel) = lm36 / lm45
  n            Nasion (Weichteil) = Sellion se: tiefste Stelle (kleinstes z) des Profils zwischen lm28 und g
  g            Glabella: vorderste Stelle des Profils zwischen Brauenhöhe (lm21/22) − 8 mm und + 15 mm
  prn          Pronasale: vorderste Stelle des Profils zwischen lm33 und lm28
  sn           Subnasale = lm33 (ICT: Übergang Nasensteg/Oberlippe; im Profil liegt dort der Steg vor der Lippe)
  ls           Labrale superius: vorderste Stelle des Profils zwischen sto + 1 mm und sn − 2 mm
  sto          Stomion: Mitte von lm62 und lm66 (Lippenschluss, innen)
  li           Labrale inferius: vorderste Stelle des Profils zwischen sto − 18 mm und sto
  sm           Supramentale: tiefste Stelle des Profils zwischen lm08 + 5 mm und li − 3 mm
  pg           Pogonion: vorderste Stelle des Profils zwischen lm08 − 8 mm und sm
  me           Menton: unterste Stelle des vorderen Profils, die vom Pogonion aus zusammenhängend noch vor z(pg) − 10 mm liegt
  zy_r, zy_l   Zygion: größte Breite vor dem Ohr, Höhe n − 45 … n − 10 mm, nur Ecken mindestens 15 mm vor der Ohrwurzel
               (z > z(t) + 15 mm; dort liegt der Jochbogen, weiter hinten wird der Schädel zum Ohr hin breiter)
  go_r, go_l   Gonion: Kieferwinkel der ICT-Kontur = lm3 / lm13
  al_r, al_l   Alare: seitlichste Ecke des Nasenflügels, Höhe sn − 2 … prn + 5 mm, z ≥ z(sn) − 8 mm, |x| < 25 mm
  ch_r, ch_l   Cheilion (Mundwinkel) = lm48 / lm54
  t_r, t_l     Tragion (vereinfacht: Ohrwurzel vorn): Höhe ps − 12 mm; Kopfbreite je 2 mm Tiefe von vorn nach hinten,
               das Ohr beginnt, wo sie die Breite 6 mm weiter vorn um ≥ 3 mm übersteigt – t liegt davor
  br_r, br_l   Braue (Unterkante) über der Pupille: unterste Ecke der Brauenkarte (Netz „Haare“, Primitiv 0) mit
               |x − x(ps)| < 3 mm
"""
import json
import os

import numpy as np

HIER = os.path.dirname(os.path.abspath(__file__))
_LM = json.load(open(os.path.join(HIER, "merkpunkte_ict68.json"), encoding="utf-8"))["ecken"]
MITTE = 1.5           # mm: Mittellinie


class Kopfnetz:
    """Die für Merkpunkte nötigen Teile einer Kopf-.glb (Haut, Iris, Hornhaut, Brauen)."""

    def __init__(self, glb):
        self.glb = glb
        self.haut = glb.teil("Haut", 0)
        augen = glb.teile("Augen")
        self.iris = augen[1]
        self.hornhaut = augen[4]
        haare = glb.teile("Haare")
        self.brauen = haare[0]
        self.lm = np.array(_LM)
        g = self.haut.grund
        self._mitte = np.nonzero(np.abs(g[:, 0]) * 1000 < MITTE)[0]
        self._pupille = _pupillenrand(self.iris)

    def punkte(self, gewichte):
        """Alle Merkpunkte (mm) als dict Name -> np.array([x, y, z]); dazu „_haut“ (alle Hautecken in mm)."""
        H = self.haut.lage(gewichte) * 1000.0
        P = {f"lm{i:02d}": H[e] for i, e in enumerate(self.lm)}
        I = self.iris.lage(gewichte) * 1000.0
        K = self.hornhaut.lage(gewichte) * 1000.0
        for s in ("r", "l"):
            P[f"ps_{s}"] = I[self._pupille[s]].mean(axis=0)
        for s, sel in (("r", K[:, 0] < 0), ("l", K[:, 0] > 0)):
            k = K[sel]
            P[f"hs_{s}"] = k[np.argmax(k[:, 2])]
        P["en_r"], P["en_l"] = P["lm39"], P["lm42"]
        P["ex_r"], P["ex_l"] = P["lm36"], P["lm45"]
        P["ch_r"], P["ch_l"] = P["lm48"], P["lm54"]
        P["go_r"], P["go_l"] = P["lm03"], P["lm13"]
        # Vorderes Profil der Mittellinie: je 0,5 mm Höhe die vorderste Ecke (Gesicht, nicht Hinterkopf)
        M = H[self._mitte]
        M = M[M[:, 2] > P["ps_r"][2] - 60.0]
        ys = np.arange(np.floor(M[:, 1].min()), np.ceil(M[:, 1].max()) + 0.5, 0.5)
        zf = np.full(len(ys), np.nan)
        k = np.clip(((M[:, 1] - ys[0]) / 0.5).round().astype(int), 0, len(ys) - 1)
        for i, z in zip(k, M[:, 2]):
            if np.isnan(zf[i]) or z > zf[i]:
                zf[i] = z
        ok = ~np.isnan(zf)
        zf = np.interp(ys, ys[ok], zf[ok])
        # obere Hülle über ±1,5 mm: Fächer, in denen nur innere Ecken liegen (Lippeninnenseite, Nasensteg), zählen nicht
        zf = np.array([zf[max(0, i - 3):i + 4].max() for i in range(len(zf))])
        self._profil = (ys, zf)

        def mitte_extrem(y_von, y_bis, art, z_min=-1e9):
            a, b = min(y_von, y_bis), max(y_von, y_bis)
            sel = (ys >= a) & (ys <= b) & (zf >= z_min)
            if not sel.any():
                return np.array([np.nan] * 3)
            yy, zz = ys[sel], zf[sel]
            i = np.argmax(zz) if art == "vorn" else np.argmin(zz) if art == "hinten" else np.argmin(yy)
            return np.array([0.0, yy[i], zz[i]])

        braue_y = 0.5 * (P["lm21"][1] + P["lm22"][1])
        P["g"] = mitte_extrem(braue_y - 8, braue_y + 15, "vorn")
        P["n"] = mitte_extrem(P["lm28"][1], P["g"][1], "hinten")
        P["se"] = P["n"]
        P["prn"] = mitte_extrem(P["lm33"][1], P["lm28"][1], "vorn")
        P["sto"] = 0.5 * (P["lm62"] + P["lm66"])
        # Subnasale: ICT-Merkpunkt 33 (Übergang Nasensteg/Oberlippe); im Profil liegt dort der Steg vor der Lippe, die
        # obere Hülle taugt hier nicht
        P["sn"] = P["lm33"]
        P["ls"] = mitte_extrem(P["sto"][1] + 1.0, P["sn"][1] - 2.0, "vorn")
        P["li"] = mitte_extrem(P["sto"][1] - 18, P["sto"][1], "vorn")
        # Kinn: Supramentale tiefste Stelle zwischen Unterlippe und Kinn, Pogonion vorderste Stelle darunter, Menton
        # unterste Stelle des Profils, die vom Pogonion aus zusammenhängend noch vor z(pg) − 10 mm liegt (unter dem
        # Kinn biegt das Profil zum Hals nach hinten ab)
        P["sm"] = mitte_extrem(P["lm08"][1] + 5, P["li"][1] - 3, "hinten")
        P["pg"] = mitte_extrem(P["lm08"][1] - 8, P["sm"][1], "vorn")
        pg = P["pg"]
        j = np.nonzero(ys <= pg[1])[0][::-1]
        me_y = pg[1]
        for i in j:
            if zf[i] < pg[2] - 10.0:
                break
            me_y = ys[i]
        P["me"] = np.array([0.0, me_y, float(np.interp(me_y, ys, zf))])
        # Tragion (vereinfacht) und Zygion
        ps_y = 0.5 * (P["ps_r"][1] + P["ps_l"][1])
        ps_z = 0.5 * (P["ps_r"][2] + P["ps_l"][2])
        for s, vz in (("r", -1), ("l", 1)):
            P[f"t_{s}"] = _tragion(H, ps_y - 12.0, ps_z, vz)
        z_ohr = max(P["t_r"][2], P["t_l"][2]) + 15.0
        n_y = P["n"][1]
        sel = (H[:, 1] >= n_y - 45) & (H[:, 1] <= n_y - 10) & (H[:, 2] > z_ohr)
        for s, vz in (("r", -1), ("l", 1)):
            q = H[sel & (vz * H[:, 0] > 0)]
            P[f"zy_{s}"] = q[np.argmax(np.abs(q[:, 0]))]
        # Nasenflügel
        sn, prn = P["sn"], P["prn"]
        sel = (H[:, 1] >= sn[1] - 2) & (H[:, 1] <= prn[1] + 5) & (H[:, 2] >= sn[2] - 8) & (np.abs(H[:, 0]) < 25)
        for s, vz in (("r", -1), ("l", 1)):
            q = H[sel & (vz * H[:, 0] > 0)]
            P[f"al_{s}"] = q[np.argmax(np.abs(q[:, 0]))]
        # Braue über der Pupille (Unterkante der Brauenkarte)
        B = self.brauen.lage(gewichte) * 1000.0
        for s in ("r", "l"):
            x = P[f"ps_{s}"][0]
            q = B[(np.abs(B[:, 0] - x) < 3.0) & (B[:, 1] > ps_y)]
            P[f"br_{s}"] = q[np.argmin(q[:, 1])] if len(q) else np.array([np.nan] * 3)
        P["_haut"] = H
        P["_z_ohr"] = z_ohr
        return P


def _tragion(H, y, z_auge, seite):
    """Ohrwurzel vorn in Höhe y: Kopfbreite w(z) je 2 mm Tiefe von vorn nach hinten (z_auge − 40 … − 110 mm); das Ohr
    beginnt, wo w(z) die Breite 6 mm weiter vorn um ≥ 3 mm übersteigt. t = Mitte des Fachs davor."""
    sel = (np.abs(H[:, 1] - y) < 1.5) & (seite * H[:, 0] > 0)
    q = H[sel]
    zs = np.arange(z_auge - 40.0, z_auge - 112.0, -2.0)
    w = np.array([np.abs(q[(q[:, 2] <= z) & (q[:, 2] > z - 2)][:, 0]).max() if ((q[:, 2] <= z) & (q[:, 2] > z - 2)).any() else np.nan
                  for z in zs])
    ok = ~np.isnan(w)
    if ok.sum() < 5:
        return np.array([np.nan] * 3)
    w = np.interp(np.arange(len(zs)), np.nonzero(ok)[0], w[ok])
    for i in range(3, len(zs)):
        if w[i] >= w[i - 3] + 3.0:
            z_t = zs[i] + 1.0 + 2.0
            return np.array([seite * float(np.interp(z_t, zs[::-1], w[::-1])), y, z_t])
    return np.array([np.nan] * 3)


def _pupillenrand(iris):
    """Ecken des inneren Rands der Iris (Pupillenöffnung im Netz) je Seite: Randkanten (nur ein Dreieck) zu Schleifen,
    je Seite die Schleife mit dem kleinsten Radius."""
    from collections import Counter
    kanten = Counter()
    for a, b, c in iris.dreiecke:
        for u, v in ((a, b), (b, c), (c, a)):
            kanten[(min(u, v), max(u, v))] += 1
    nach = {}
    for (u, v), n in kanten.items():
        if n == 1:
            nach.setdefault(u, []).append(v)
            nach.setdefault(v, []).append(u)
    gesehen, schleifen = set(), []
    for s in nach:
        if s in gesehen:
            continue
        schleife, cur = [s], s
        gesehen.add(s)
        while True:
            nxt = [n for n in nach[cur] if n not in gesehen]
            if not nxt:
                break
            cur = nxt[0]
            gesehen.add(cur)
            schleife.append(cur)
        schleifen.append(schleife)
    G = iris.grund
    aus = {}
    for seite, vz in (("r", -1), ("l", 1)):
        beste = None
        for sch in schleifen:
            q = G[sch]
            c = q.mean(axis=0)
            if vz * c[0] <= 0:
                continue
            r = np.linalg.norm(q[:, :2] - c[:2], axis=1).mean()
            if beste is None or r < beste[0]:
                beste = (r, sch)
        aus[seite] = np.array(beste[1])
    return aus


def name_seite(name):
    """„zy_r“ -> („zy“, „r“)."""
    return (name[:-2], name[-1]) if name[-2:] in ("_r", "_l") else (name, "")
