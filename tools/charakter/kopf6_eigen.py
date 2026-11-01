"""Eigene Formen direkt auf dem dichten ICT-Netz (Charakter Phase 6, S4) – ohne MPFB-Übertragung, ohne Glättung.

Aufgerufen von kopf2_bauen.py (Option eigen=1) nach dem Muster des Adamsapfels: jede Form ist ein Versatz der Hautecken
(Blender-Achsen: x = linke Seite der Figur, −y = vorn, z = oben; Meter). Augapfel und Zähne bleiben fest; Brauen,
Wimpern und Kragen folgen im Bau über haare_und_kragen(). Masken aus den 68 ICT-Merkpunkten (iBUG-Reihenfolge: 0–16
Kontur von der rechten Seite der Figur (−x) über das Kinn (8) zur linken, 17–26 Brauen, 27–35 Nase, 36–47 Augen,
48–67 Mund).

Formen (Stärke bei Gewicht 1; die Ausgleichsrechnung formen_ausgleich.py wählt das Gewicht in [−1; 1,2]):
  eigen_kieferkante   Weichteil unter dem Unterkieferrand (Kontur lm2–14) nach innen und oben, Spitze 3,5 mm etwa 8 mm
                      unter der Kante, aus bei 1,5 mm (die Kante selbst bleibt) und bei 30 mm; der Kieferwinkel bleibt Ecke
  eigen_unterwange    Unterwange bei |x| 40–55 mm, 55–85 mm unter n, 4 mm nach innen (weiche Ränder)
  eigen_jochbein      Jochbeinvorsprung 1,5 mm nach außen (unter dem äußeren Lidwinkel, |x| ≈ 46 mm)
  eigen_lidfalte_m/_w Oberlidfalte als eine weiche Rinne (σ 1,6 mm), 1,0 mm tief, 7,5 mm (Mann) bzw. 9,5 mm (Frau) über
                      dem Lidrand, zu den Lidwinkeln flacher und auslaufend; Lidrand und Wimpernwurzeln bleiben (Maske 0
                      bis 2,5 mm vom Rand). Der Kopf blendet sie beim Lidschluss aus (eigene Form „lidfalte_<g>“).
  eigen_traenenrinne  Tränenrinne vom inneren Lidwinkel schräg nach außen unten, 0,75 mm tief
  eigen_kopfnicker    Kopfnicker (Halsmuskel) vom Warzenfortsatz hinter dem Ohr zum Brustbein, Wulst 1,5 mm
  eigen_kieferwinkel  Kieferwinkel (um lm3/lm13) 4 mm zur Seite (S5: Gen kiefer_breite; mit negativem Gewicht schmaler)
  eigen_hoecker       Höcker auf dem Nasenrücken (zwischen lm28 und lm29), 2 mm nach vorn (S5: Gen nase_hoecker)

Etappe D (Augenpartie, nur mit etappe_d=True bzw. kopf2_bauen.py eigen=2; Halt B und B2 bauen ohne sie gleich):
  eigen_augenhoehle   Augenhöhle tiefer: Rinne unter der Braue über der inneren Hälfte des Oberlids (oberer Sulcus,
                      bis 1,8 mm) und eine Mulde am inneren Lidwinkel neben dem Nasenrücken (bis 1,0 mm); Lidrand und
                      Braue bleiben (die gemittelten Scans glätten diese Höhlungen weg – Prüferbefund „Augenpartie flach“)
  eigen_deckfalte_m/_w  Deckfalte über der Lidfalte: die Haut 0–4,5 mm über der Falte (7,5 bzw. 9,5 mm über dem Lidrand)
                      sinkt 0,7 mm und tritt 0,5 mm vor, außen stärker (Schlupf zur Seite); mit der Lidfalte zusammen
                      ausgeblendet, wenn das Lid schließt (kopf6_eichen.py legt sie in „lidfalte_<g>“)
  eigen_unterlid      Unterlid: Lidwulst (Pars tarsalis) 0,45 mm vor, 2 mm unter dem Rand, darunter die Lid-Wangen-Furche
                      0,35 mm tief bei 5,5 mm; zu den Lidwinkeln auslaufend, der Lidrand selbst bleibt
"""
import numpy as np


def stufe(x, a, b):
    """Glatte Stufe 0 → 1 von a nach b (a > b erlaubt)."""
    t = np.clip((x - a) / (b - a), 0.0, 1.0)
    return t * t * (3 - 2 * t)


def eckennormalen(ecken, flaechen):
    """Normalen je Ecke (Flächenmittel, Fächer-Dreiecke), nach außen wie die Flächen des Netzes."""
    n = np.zeros_like(ecken)
    for f in flaechen:
        a = ecken[f[0]]
        for i in range(1, len(f) - 1):
            c = np.cross(ecken[f[i]] - a, ecken[f[i + 1]] - a)
            n[f[0]] += c
            n[f[i]] += c
            n[f[i + 1]] += c
    return n / np.maximum(np.linalg.norm(n, axis=1, keepdims=True), 1e-12)


def _linie(punkte, je=40):
    """Polylinie dicht abtasten (je Abschnitt `je` Punkte); gibt Punkte und Bogenlänge (0..1) zurück."""
    aus = []
    for a, b in zip(punkte[:-1], punkte[1:]):
        for t in np.linspace(0, 1, je, endpoint=False):
            aus.append(a * (1 - t) + b * t)
    aus.append(punkte[-1])
    aus = np.array(aus)
    s = np.concatenate([[0], np.cumsum(np.linalg.norm(np.diff(aus, axis=0), axis=1))])
    return aus, s / max(s[-1], 1e-12)


def _naechster(p, linie):
    """Je Punkt Abstand und Index des nächsten Linienpunkts (in Blöcken, damit der Speicher klein bleibt)."""
    d = np.empty(len(p))
    k = np.empty(len(p), dtype=np.int64)
    for i in range(0, len(p), 4000):
        q = p[i:i + 4000]
        a = np.linalg.norm(q[:, None, :] - linie[None, :, :], axis=2)
        k[i:i + 4000] = a.argmin(1)
        d[i:i + 4000] = a.min(1)
    return d, k


def formen(H, n_haut, flaechen, L, augen, ausschluss, hals_abstand, mm=0.001, etappe_d=False):
    """Alle eigenen Formen. H: Hautecken (m), n_haut: Zahl der ICT-Hautecken (davor), flaechen: Hautflächen,
    L: 68 Merkpunkte (m), augen: {"l"/"r": (Mitte, Halbmesser)}, ausschluss: Ecken ohne Versatz (Mund- und
    Augenhöhlen), hals_abstand: Abstand jeder ICT-Hautecke zur Halsnaht (m), etappe_d: dazu die Formen der Augenpartie
    (Etappe D). Gibt {Name: Versatz (len(H), 3)} und einen Prüfbericht zurück."""
    N = eckennormalen(H, flaechen)
    V = H[:n_haut]
    Nv = N[:n_haut]
    frei = np.ones(n_haut)
    frei[ausschluss] = 0.0
    frei *= stufe(hals_abstand, 0.005, 0.02)          # an der Naht zur Büste kein Versatz
    y_ohr = 0.5 * (L[0, 1] + L[16, 1])                 # Kontur auf Ohrhöhe (vor dem Tragus)
    vorn = stufe(V[:, 1], y_ohr + 0.012, y_ohr)        # vor dem Ohr (−y = vorn)
    z_n = L[27, 2]                                     # Nasion (Nasenwurzel) ≈ ICT-Merkpunkt 27
    ps = {s: augen[s][0] for s in ("l", "r")}
    z_p = 0.5 * (ps["l"][2] + ps["r"][2])
    aus = {}
    bericht = {}

    def ablegen(name, d):
        voll = np.zeros_like(H)
        voll[:n_haut] = d * frei[:, None]
        aus[name] = voll
        b = np.linalg.norm(voll, axis=1)
        bericht[name] = {"max_mm": round(float(b.max() * 1000), 2), "ecken_ueber_0_1mm": int((b > 1e-4).sum())}

    # ---------------------------------------------------------------- Kieferkante
    kontur, _ = _linie(L[2:15], 30)
    d, k = _naechster(V, kontur)
    c = kontur[k]
    unter = stufe(V[:, 2] - c[:, 2], 0.001, -0.002)             # unter der Kante
    unter_kontur = unter
    nach_unten = stufe(-Nv[:, 2], 0.15, 0.55)                    # Fläche schaut nach unten (Unterseite des Kiefers)
    buckel = stufe(d, 0.0015, 0.007) * (1 - stufe(d, 0.014, 0.030))
    richt = -Nv + np.array([0.0, 0.0, 0.4])
    richt /= np.linalg.norm(richt, axis=1, keepdims=True)
    w = unter * nach_unten * buckel * stufe(V[:, 1], y_ohr + 0.015, y_ohr + 0.003)
    ablegen("eigen_kieferkante", richt * (3.5 * mm * w)[:, None])

    # ---------------------------------------------------------------- Unterwange
    ax = np.abs(V[:, 0])
    w = stufe(ax, 0.034, 0.042) * (1 - stufe(ax, 0.053, 0.061))
    w *= stufe(V[:, 2], z_n - 0.047, z_n - 0.055) * (1 - stufe(V[:, 2], z_n - 0.085, z_n - 0.093))
    w *= vorn * stufe(Nv[:, 1], 0.3, -0.1)                       # Fläche schaut nach vorn oder zur Seite
    ablegen("eigen_unterwange", -Nv * (4.0 * mm * w)[:, None])

    # ---------------------------------------------------------------- Jochbein
    w = np.exp(-0.5 * (((ax - 0.046) / 0.009) ** 2 + ((V[:, 2] - (z_p - 0.024)) / 0.008) ** 2))
    w *= vorn * stufe(Nv[:, 1], 0.3, -0.1)
    ablegen("eigen_jochbein", Nv * (1.5 * mm * w)[:, None])

    # ---------------------------------------------------------------- Oberlidfalte (eine Rinne)
    ober = {"r": [36, 37, 38, 39], "l": [42, 43, 44, 45]}
    unter_rand = {"r": [39, 40, 41, 36], "l": [45, 46, 47, 42]}
    for wer, h in (("m", 7.5), ("w", 9.5)):
        dd = np.zeros_like(V)
        for s in ("r", "l"):
            rand = L[ober[s]]
            hub = np.array([0.45, 1.0, 1.0, 0.45]) * h * mm   # zu den Lidwinkeln flacher
            falte = rand.copy()
            falte[:, 2] += hub
            fl, t = _linie(falte, 30)
            rl, _ = _linie(np.vstack([rand, L[unter_rand[s]][1:]]), 30)
            xz = V[:, [0, 2]]
            dfalte, kf = _naechster(np.c_[xz[:, 0], np.zeros(len(V)), xz[:, 1]], np.c_[fl[:, 0], np.zeros(len(fl)), fl[:, 2]])
            drand, _ = _naechster(np.c_[xz[:, 0], np.zeros(len(V)), xz[:, 1]], np.c_[rl[:, 0], np.zeros(len(rl)), rl[:, 2]])
            nah_auge = np.linalg.norm(V - ps[s], axis=1) < 0.028
            vorn_auge = (V[:, 1] < ps[s][1]) & (Nv[:, 1] < -0.15)
            # Etappe B (Prüferbefund): breiter, flacher, weiche Enden – die schmale Rinne (σ 1 mm, 1,25 mm tief, kurze
            # Enden) zeichnete sich beim geschlossenen Auge als harte Platte ab
            enden = stufe(t[kf], 0.0, 0.25) * (1 - stufe(t[kf], 0.75, 1.0))
            rinne = np.exp(-0.5 * (dfalte / (1.6 * mm)) ** 2) * enden
            rinne *= stufe(drand, 0.0025, 0.005) * nah_auge * vorn_auge
            dd += -Nv * (1.0 * mm * rinne)[:, None]
        ablegen(f"eigen_lidfalte_{wer}", dd)
        # Prüfung: Versatz am Lidrand (Ecken bis 1 mm vom Rand in der Vorderansicht)
        b = np.linalg.norm(aus[f"eigen_lidfalte_{wer}"][:n_haut], axis=1)
        rand_ecken = []
        for s in ("r", "l"):
            rl, _ = _linie(np.vstack([L[ober[s]], L[unter_rand[s]][1:]]), 30)
            dr, _ = _naechster(np.c_[V[:, 0], np.zeros(len(V)), V[:, 2]], np.c_[rl[:, 0], np.zeros(len(rl)), rl[:, 2]])
            rand_ecken.append((dr < 0.001) & (np.linalg.norm(V - ps[s], axis=1) < 0.028))
        bericht[f"eigen_lidfalte_{wer}"]["lidrand_max_mm"] = round(float(b[rand_ecken[0] | rand_ecken[1]].max() * 1000), 3)

    # ---------------------------------------------------------------- Tränenrinne
    dd = np.zeros_like(V)
    for s, en in (("r", 39), ("l", 42)):
        sg = np.sign(ps[s][0])
        a = L[en] + np.array([sg * 0.001, 0, -0.0035])
        b = np.array([ps[s][0], 0, L[en, 2] - 0.011])
        e = np.array([ps[s][0] + sg * 0.008, 0, L[en, 2] - 0.012])
        li, t = _linie(np.array([[a[0], 0, a[2]], b, e]), 30)
        dr, kr = _naechster(np.c_[V[:, 0], np.zeros(len(V)), V[:, 2]], li)
        enden = stufe(t[kr], 0.0, 0.15) * (1 - stufe(t[kr], 0.75, 1.0))
        w = np.exp(-0.5 * (dr / (2.0 * mm)) ** 2) * enden
        w *= (V[:, 1] < ps[s][1]) * stufe(Nv[:, 1], 0.0, -0.4) * (np.linalg.norm(V - ps[s], axis=1) < 0.035)
        dd += -Nv * (0.75 * mm * w)[:, None]
    ablegen("eigen_traenenrinne", dd)

    # ---------------------------------------------------------------- Kopfnicker
    dd = np.zeros_like(V)
    z_kinn = L[8, 2]
    hals = (V[:, 2] < L[0, 2] - 0.010) & (V[:, 2] > z_kinn - 0.110)
    for s, ohr in (("r", 0), ("l", 16)):
        sg = np.sign(L[ohr, 0])
        ziel_a = np.array([L[ohr, 0] - sg * 0.006, L[ohr, 1] + 0.022, L[ohr, 2] - 0.032])     # Warzenfortsatz
        ziel_b = np.array([sg * 0.018, -1.0, z_kinn - 0.085])                               # Brustbein (vorn)
        sel = np.nonzero(hals)[0]
        a = V[sel[np.argmin(np.linalg.norm(V[sel] - ziel_a, axis=1))]]
        q = sel[np.abs(V[sel, 2] - ziel_b[2]) < 0.004]
        q = q[np.abs(V[q, 0] - ziel_b[0]) < 0.006]
        b = V[q[np.argmin(V[q, 1])]] if len(q) else a
        # zylindrisch um die Halsachse: (Bogen, Höhe)
        yc = np.median(V[hals, 1])
        r = 0.055

        def zyl(p):
            th = np.arctan2(p[..., 0], -(p[..., 1] - yc))
            return np.stack([th * r, p[..., 2]], axis=-1)
        za, zb = zyl(a), zyl(b)
        pz = zyl(V)
        u = zb - za
        if float(u @ u) < 1e-8:
            continue
        t = np.clip(((pz - za) @ u) / (u @ u), 0, 1)
        dr = np.linalg.norm(pz - (za + t[:, None] * u), axis=1)
        w = np.exp(-0.5 * (dr / 0.007) ** 2) * stufe(t, 0.0, 0.12) * (1 - stufe(t, 0.85, 1.0)) * hals
        w *= (np.sign(V[:, 0]) == sg)
        w *= np.maximum(unter_kontur, stufe(V[:, 1], y_ohr - 0.005, y_ohr + 0.010))   # nicht im Gesicht über dem Kiefer
        dd += Nv * (1.5 * mm * w)[:, None]
        bericht.setdefault("kopfnicker_enden_mm", {})[s] = [[round(float(v) * 1000, 1) for v in a], [round(float(v) * 1000, 1) for v in b]]
    ablegen("eigen_kopfnicker", dd)

    # ---------------------------------------------------------------- Kieferwinkel (seitlich)
    dd = np.zeros_like(V)
    for go in (3, 13):
        G = L[go]
        sg = np.sign(G[0])
        a = np.linalg.norm((V - G) / np.array([0.016, 0.016, 0.018]), axis=1)
        w = np.exp(-0.5 * a ** 2) * (np.sign(V[:, 0]) == sg) * stufe(V[:, 2], L[33, 2] + 0.005, L[33, 2] - 0.005)
        w *= stufe(V[:, 1], y_ohr + 0.012, y_ohr)
        dd[:, 0] += sg * 4.0 * mm * w
    ablegen("eigen_kieferwinkel", dd)

    # ---------------------------------------------------------------- Nasenhöcker
    mitte = L[28] + 0.5 * (L[29] - L[28])
    achse = (L[30] - L[27]) / np.linalg.norm(L[30] - L[27])
    rel = V - mitte
    laengs = rel @ achse
    quer = np.linalg.norm(rel - laengs[:, None] * achse, axis=1)
    w = np.exp(-0.5 * ((laengs / 0.006) ** 2 + (np.abs(V[:, 0]) / 0.0045) ** 2)) * (quer < 0.012) * stufe(Nv[:, 1], -0.2, -0.6)
    ablegen("eigen_hoecker", Nv * (2.0 * mm * w)[:, None])
    if etappe_d:
        augenpartie(V, Nv, L, ps, ober, unter_rand, ablegen, aus, bericht, n_haut, mm)
    return aus, bericht


def augenpartie(V, Nv, L, ps, ober, unter_rand, ablegen, aus, bericht, n_haut, mm):
    """Etappe D: Augenhöhle, Deckfalte und Unterlid (siehe Kopf der Datei). Lage in der Vorderansicht (x, z) gegen die
    Lidränder der ICT-Merkpunkte; „seitlich“ 0 am inneren, 1 am äußeren Lidwinkel; ps = Augapfelmitten."""
    xz = np.c_[V[:, 0], np.zeros(len(V)), V[:, 2]]
    brauen = {"r": [17, 18, 19, 20, 21], "l": [22, 23, 24, 25, 26]}
    innen_w = {"r": 39, "l": 42}
    aussen_w = {"r": 36, "l": 45}

    def seitlich(s):
        a, b = abs(L[innen_w[s], 0]), abs(L[aussen_w[s], 0])
        return np.clip((np.abs(V[:, 0]) - a) / (b - a), -0.5, 1.5) * (np.sign(V[:, 0]) == np.sign(ps[s][0]))

    def rand_abstand(s):
        rl, _ = _linie(np.vstack([L[ober[s]], L[unter_rand[s]][1:]]), 30)
        d, _ = _naechster(xz, np.c_[rl[:, 0], np.zeros(len(rl)), rl[:, 2]])
        return d

    def kante_z(punkte, x):
        o = np.argsort(punkte[:, 0])
        return np.interp(x, punkte[o, 0], punkte[o, 2])

    seite = {s: (np.sign(V[:, 0]) == np.sign(ps[s][0])) for s in ("r", "l")}
    rand_d = {s: rand_abstand(s) for s in ("r", "l")}

    # ---------------------------------------------------------------- Augenhöhle (oberer Sulcus, innerer Winkel)
    dd = np.zeros_like(V)
    for s in ("r", "l"):
        sg = np.sign(ps[s][0])
        lat = seitlich(s)
        z_rand = kante_z(L[ober[s]], V[:, 0])                     # Oberlidrand über der Ecke
        z_braue = kante_z(L[brauen[s]], V[:, 0]) - 0.003          # Unterseite der Braue (Merkpunkte liegen mittig)
        dz = V[:, 2] - z_rand
        hoehe = np.maximum(z_braue - z_rand, 0.006)
        t = dz / hoehe                                           # 0 Lidrand, 1 Brauenunterseite
        # Rinne über der Lidfalte, Mitte bei 55 % zur Braue, innen (lat 0–0,5) am tiefsten, außen flach
        rinne = np.exp(-0.5 * ((t - 0.55) / 0.2) ** 2) * stufe(dz, 0.006, 0.009)
        rinne *= stufe(lat, -0.35, -0.05) * (1 - stufe(lat, 0.55, 1.0))
        nah = np.linalg.norm(V - ps[s], axis=1) < 0.03
        vorn_auge = (V[:, 1] < ps[s][1]) & (Nv[:, 1] < -0.1)
        w = 1.8 * mm * rinne * nah * vorn_auge * seite[s]
        # Mulde am inneren Lidwinkel, 3 mm zur Nase und 1 mm über dem Winkel (nicht auf dem Lid)
        en = L[innen_w[s]]
        m = np.array([en[0] - sg * 0.003, en[2] + 0.001])
        mulde = np.exp(-0.5 * (np.linalg.norm(V[:, [0, 2]] - m, axis=1) / 0.0035) ** 2)
        mulde *= (V[:, 1] < ps[s][1]) * stufe(rand_d[s], 0.0012, 0.003) * (np.abs(V[:, 0]) > 0.004)
        w = w + 1.0 * mm * mulde
        dd += -Nv * w[:, None]
    ablegen("eigen_augenhoehle", dd)

    # ---------------------------------------------------------------- Deckfalte über der Lidfalte (wie eigen_lidfalte)
    for wer, h in (("m", 7.5), ("w", 9.5)):
        dd = np.zeros_like(V)
        for s in ("r", "l"):
            rand = L[ober[s]]
            hub = np.array([0.45, 1.0, 1.0, 0.45]) * h * mm
            falte = rand.copy()
            falte[:, 2] += hub
            ueber = V[:, 2] - kante_z(falte, V[:, 0])           # Abstand über der Falte (m)
            band = np.exp(-0.5 * ((ueber - 1.6 * mm) / (1.3 * mm)) ** 2) * stufe(ueber, -0.4 * mm, 0.6 * mm)
            lat = seitlich(s)
            enden = stufe(lat, -0.1, 0.2) * (1 - stufe(lat, 0.9, 1.25)) * (0.65 + 0.55 * np.clip(lat, 0, 1))
            nah = np.linalg.norm(V - ps[s], axis=1) < 0.028
            vorn_auge = (V[:, 1] < ps[s][1]) & (Nv[:, 1] < -0.15)
            w = band * enden * nah * vorn_auge * seite[s] * stufe(rand_d[s], 0.0025, 0.005)
            dd += (Nv * 0.5 * mm + np.array([0.0, 0.0, -0.7 * mm])) * w[:, None]
        ablegen(f"eigen_deckfalte_{wer}", dd)

    # ---------------------------------------------------------------- Unterlid: Lidwulst und Lid-Wangen-Furche
    dd = np.zeros_like(V)
    for s in ("r", "l"):
        tief = kante_z(L[unter_rand[s]], V[:, 0]) - V[:, 2]      # Abstand unter dem Unterlidrand (m)
        lat = seitlich(s)
        enden = stufe(lat, 0.0, 0.2) * (1 - stufe(lat, 0.8, 1.05))
        wulst = 0.45 * mm * np.exp(-0.5 * ((tief - 2.0 * mm) / (1.0 * mm)) ** 2)
        furche = -0.35 * mm * np.exp(-0.5 * ((tief - 5.5 * mm) / (1.3 * mm)) ** 2)
        nah = np.linalg.norm(V - ps[s], axis=1) < 0.028
        vorn_auge = (V[:, 1] < ps[s][1]) & (Nv[:, 1] < -0.1)
        w = (wulst + furche) * enden * nah * vorn_auge * seite[s] * (tief > 0) * stufe(rand_d[s], 0.0006, 0.0014)
        dd += Nv * w[:, None]
    ablegen("eigen_unterlid", dd)
    # Prüfung: Versatz am Lidrand (Ecken bis 1 mm vom Rand) – höchstens etwa 0,15 mm, sonst klafft das Lid vom Augapfel
    rand = np.zeros(len(V), bool)
    for s in ("r", "l"):
        rand |= (rand_d[s] < 0.001) & (np.linalg.norm(V - ps[s], axis=1) < 0.028)
    for name in ("eigen_augenhoehle", "eigen_deckfalte_m", "eigen_deckfalte_w", "eigen_unterlid"):
        b = np.linalg.norm(aus[name][:n_haut], axis=1)
        bericht[name]["lidrand_max_mm"] = round(float(b[rand].max() * 1000), 3)
