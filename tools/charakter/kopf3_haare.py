"""Charakter Phase 3: Karten für Brauen und Wimpern (eigener Code, MIT; läuft in Blenders Python mit numpy).

Statt der MakeHuman-Karten (Streifen mit grobem Alpha) eigene Karten, deren Bild Haar für Haar gezeichnet wird
(kopf3_haartexturen.py):
- Brauen: zwei Lagen je Seite entlang der ICT-Merkpunkte 17–21 bzw. 22–26 (oberer Brauenrand), auf die Haut gelegt
  (0,35 mm und 0,9 mm darüber). UV: u = innen (0) → außen (1), v = unten (0) → oben (1); untere Lage im Bild
  v 0..0,5, obere Lage v 0,5..1.
- Wimpern (Phase 4): am Oberlid drei Lagen geschwungener Streifen (Hauptlage, lange lockere Lage mit stärkerem
  Schwung, kurze Fülllage an der Wurzel), am Unterlid eine; jede Lage hat im Bild ein eigenes Band (WIMPER_BAENDER):
  UV u = innen → außen, v = Wurzel → Spitze im Band (Band 0 Unterlid, 1–3 Oberlid). Die Lagen folgen beim Blinzeln
  dem Lidrand wie bisher (kopf2_bauen.py dreht jede Wimperecke mit dem Lid um die Augenmitte).
Achsen wie im Bau: Blender (z oben, Gesicht nach -y, +x = linke Seite der Figur), Meter.
"""
import numpy as np

# Brauenkarte: Höhe (m) innen/außen und Lage der Merkpunktlinie in v (Merkpunkte = oberer Rand der Braue)
BRAUE_HOEHE = (0.0165, 0.0115)
BRAUE_V_MERK = 0.80
BRAUE_LAGEN = (0.00035, 0.0009)
# Wimpern: Länge (m) innen/Mitte/außen
WIMPER_OBEN = (0.0058, 0.0088, 0.0082)
WIMPER_UNTEN = (0.0026, 0.0042, 0.0043)
# Lagen: (Oberlid?, Band im Bild, Längenfaktor, Schwung nach oben, Wurzel nach hinten (m), seitliche Neigung)
WIMPER_LAGEN = ((0, 0, 1.0, 1.0, 0.0, 1.0), (1, 1, 1.0, 1.0, 0.0, 1.0), (1, 2, 1.12, 1.3, 0.00022, 1.3),
                (1, 3, 0.62, 0.75, -0.00018, 0.7))
WIMPER_BAENDER = 4


def catmull_rom(p, n):
    """Kurve durch die Punkte p (k,3), n Stützen gleichmäßig nach Bogenlänge."""
    p = np.vstack([2 * p[0] - p[1], p, 2 * p[-1] - p[-2]])
    fein = []
    for i in range(1, len(p) - 2):
        for t in np.linspace(0, 1, 16, endpoint=False):
            a, b, c, d = p[i - 1], p[i], p[i + 1], p[i + 2]
            fein.append(0.5 * ((2 * b) + (-a + c) * t + (2 * a - 5 * b + 4 * c - d) * t * t + (-a + 3 * b - 3 * c + d) * t ** 3))
    fein.append(p[-2])
    fein = np.array(fein)
    seg = np.linalg.norm(np.diff(fein, axis=0), axis=1)
    s = np.concatenate([[0], np.cumsum(seg)])
    ziele = np.linspace(0, s[-1], n)
    return np.stack([np.interp(ziele, s, fein[:, k]) for k in range(3)], axis=1), s[-1]


def _gitter_flaechen(nu, nv, versatz):
    fl = []
    for i in range(nu - 1):
        for j in range(nv - 1):
            a = versatz + i * nv + j
            fl.append([a, a + nv, a + nv + 1, a + 1])
    return fl


def _ausrichten(ecken, flaechen, soll_normale):
    """Umlaufsinn so, dass die Flächennormalen im Mittel mit soll_normale (je Ecke) gehen."""
    aus = []
    for f in flaechen:
        n = np.cross(ecken[f[1]] - ecken[f[0]], ecken[f[2]] - ecken[f[0]])
        if n @ soll_normale[f].mean(0) < 0:
            f = f[::-1]
        aus.append(f)
    return aus


def brauen(merk, flaeche_haut, nu=30, nv=6):
    """merk: Merkpunkte (68,3) im Raum der Büste; flaeche_haut: kw.Flaeche der Gesichtshaut (Form des Spielkopfs).
    Gibt (ecken, flächen, uv je Schleife, lage je Ecke 0/1, seite je Ecke) zurück."""
    ecken, flaechen, uvs, lage, seite = [], [], [], [], []
    for sx, idx in ((1.0, [22, 23, 24, 25, 26]), (-1.0, [21, 20, 19, 18, 17])):
        p = merk[idx]
        # innen 3 mm, außen 6 mm verlängern
        d0 = (p[0] - p[1]) / np.linalg.norm(p[0] - p[1])
        d1 = (p[-1] - p[-2]) / np.linalg.norm(p[-1] - p[-2])
        p = np.vstack([p[0] + d0 * 0.003, p, p[-1] + d1 * 0.006])
        kurve, laenge = catmull_rom(p, nu)
        lage_k, nrm_k, _, _ = flaeche_haut.naechste(kurve, 0.03)
        tang = np.gradient(kurve, axis=0)
        tang /= np.linalg.norm(tang, axis=1, keepdims=True)
        hoch = np.cross(nrm_k, tang)
        hoch *= np.sign(hoch[:, 2:3])            # nach oben
        hoch /= np.linalg.norm(hoch, axis=1, keepdims=True)
        u = np.linspace(0, 1, nu)
        h = BRAUE_HOEHE[0] + (BRAUE_HOEHE[1] - BRAUE_HOEHE[0]) * u
        for li, ueber in enumerate(BRAUE_LAGEN):
            o = len(ecken)
            pkt = []
            for i in range(nu):
                for j in range(nv):
                    v = j / (nv - 1)
                    pkt.append(kurve[i] + hoch[i] * (v - BRAUE_V_MERK) * h[i])
            pkt = np.array(pkt)
            auf, n_, _, _ = flaeche_haut.naechste(pkt, 0.03)
            n_ = np.where((n_ * (pkt - flaeche_haut.p.mean(0))).sum(1, keepdims=True) < 0, -n_, n_)
            pkt = auf + n_ * ueber
            fl = _gitter_flaechen(nu, nv, o)
            alle = np.vstack([np.array(ecken).reshape(-1, 3), pkt]) if ecken else pkt
            nrm_alle = np.vstack([np.zeros((o, 3)), n_])
            fl = _ausrichten(alle, fl, nrm_alle)
            ecken.extend(pkt.tolist())
            for f in fl:
                uvs.append([((f_ - o) // nv / (nu - 1), (li + ((f_ - o) % nv) / (nv - 1)) * 0.5) for f_ in f])
            flaechen.extend(fl)
            lage.extend([li] * len(pkt))
            seite.extend([sx] * len(pkt))
    return np.array(ecken), flaechen, uvs, np.array(lage), np.array(seite)


def lidkante_ordnen(punkte, mitte):
    """Lidnaht (Punkte um das Auge) in Ober- und Unterlid teilen, je von innen (nahe der Nase) nach außen.
    Die Lidwinkel gehören zu beiden Folgen."""
    sx = 1.0 if mitte[0] > 0 else -1.0
    rel = punkte - mitte
    w = np.arctan2(rel[:, 2], rel[:, 0] * sx)            # 0 = außen, π/2 = oben
    innen = int(np.argmin(punkte[:, 0] * sx))
    aussen = int(np.argmax(punkte[:, 0] * sx))
    w = np.mod(w - w[aussen], 2 * np.pi)                 # Außenwinkel bei 0
    wi = w[innen]
    oben = [k for k in np.argsort(-w) if w[k] <= wi]     # von innen (wi) abnehmend nach außen (0)
    unten = [k for k in np.argsort(w) if w[k] >= wi] + [aussen]
    return punkte[oben], punkte[unten]


def _glatte_folge(p, n):
    """Punktfolge entlang der Bogenlänge gleichmäßig neu verteilen und leicht glätten."""
    seg = np.linalg.norm(np.diff(p, axis=0), axis=1)
    s = np.concatenate([[0], np.cumsum(seg)])
    ziele = np.linspace(0, s[-1], n)
    q = np.stack([np.interp(ziele, s, p[:, k]) for k in range(3)], axis=1)
    for _ in range(3):
        q[1:-1] = 0.25 * q[:-2] + 0.5 * q[1:-1] + 0.25 * q[2:]
    return q


def wimpern(lidnaht_punkte, augen, n=22, nv=5):
    """lidnaht_punkte: {"l"/"r": Punkte der Lidnaht}; augen: {"l"/"r": (Mitte, Halbmesser)}.
    Gibt (ecken, flächen, uv je Schleife, oben je Ecke 1/0, seite je Ecke als "l"/"r") zurück."""
    ecken, flaechen, uvs, oben_je, seite = [], [], [], [], []
    vorn = np.array([0.0, -1.0, 0.0])
    for s in ("l", "r"):
        mitte, halb = augen[s]
        oben, unten = lidkante_ordnen(lidnaht_punkte[s], mitte)
        for ist_oben, band, l_f, schwung, zurueck, neigung in WIMPER_LAGEN:
            folge, laengen = (oben, WIMPER_OBEN) if ist_oben else (unten, WIMPER_UNTEN)
            if len(folge) < 4:
                continue
            k = _glatte_folge(folge, n)
            t = np.linspace(0, 1, len(k))
            tang = np.gradient(k, axis=0)
            tang /= np.linalg.norm(tang, axis=1, keepdims=True)
            radial = k - mitte
            radial -= np.outer(radial @ vorn, vorn)
            radial /= np.linalg.norm(radial, axis=1, keepdims=True)
            # Wurzel 0,5 mm vor der Lidnaht, auf der Außenkante des Lids (Lagen leicht versetzt)
            wurzel = k + vorn * (0.0005 - zurueck) + radial * 0.0003
            L = np.interp(t, [0.0, 0.55, 1.0], laengen) * l_f
            seitlich = tang * ((0.04 + 0.12 * t) * neigung)[:, None]      # nach außen geneigt, außen stärker
            o = len(ecken)
            pkt = []
            for i in range(len(k)):
                if ist_oben:
                    # erst nach vorn und leicht abwärts aus der Lidkante, dann nach oben gebogen (von vorn sieht man
                    # die Wimper ab der Lidkante); „schwung“ biegt die Spitze weiter hinauf
                    p1 = wurzel[i] + L[i] * 0.5 * (0.92 * vorn - 0.12 * radial[i]) + L[i] * 0.3 * seitlich[i]
                    p2 = wurzel[i] + L[i] * ((0.62 - 0.12 * (schwung - 1)) * vorn + 0.62 * schwung * radial[i]) + L[i] * seitlich[i]
                else:
                    p1 = wurzel[i] + L[i] * 0.5 * (0.85 * vorn + 0.35 * radial[i]) + L[i] * 0.3 * seitlich[i]
                    p2 = wurzel[i] + L[i] * (0.55 * vorn + 0.8 * radial[i]) + L[i] * seitlich[i]
                for j in range(nv):
                    v = j / (nv - 1)
                    pkt.append((1 - v) ** 2 * wurzel[i] + 2 * (1 - v) * v * p1 + v ** 2 * p2)
            pkt = np.array(pkt)
            fl = _gitter_flaechen(len(k), nv, o)
            # Vorderseite zeigt zur Kamera (nach vorn)
            alle = np.vstack([np.array(ecken).reshape(-1, 3), pkt]) if ecken else pkt
            fl = _ausrichten(alle, fl, np.tile(vorn, (len(alle), 1)))
            ecken.extend(pkt.tolist())
            for f in fl:
                uvs.append([((f_ - o) // nv / (len(k) - 1), (band + ((f_ - o) % nv) / (nv - 1)) / WIMPER_BAENDER) for f_ in f])
            flaechen.extend(fl)
            oben_je.extend([ist_oben] * len(pkt))
            seite.extend([s] * len(pkt))
    return np.array(ecken), flaechen, uvs, np.array(oben_je), np.array(seite)
