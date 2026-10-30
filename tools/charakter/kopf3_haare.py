"""Charakter Phase 3: Karten für Brauen und Wimpern (eigener Code, MIT; läuft in Blenders Python mit numpy).

Statt der MakeHuman-Karten (Streifen mit grobem Alpha) eigene Karten, deren Bild Haar für Haar gezeichnet wird
(kopf3_haartexturen.py):
- Brauen: zwei Lagen je Seite entlang der ICT-Merkpunkte 17–21 bzw. 22–26 (oberer Brauenrand), auf die Haut gelegt
  (0,35 mm und 0,9 mm darüber). UV: u = innen (0) → außen (1), v = unten (0) → oben (1); untere Lage im Bild
  v 0..0,5, obere Lage v 0,5..1.
- Wimpern (Phase 5): Sie wachsen aus der **vorderen Lidkante** (nicht aus dem Lid darüber): Die Wurzellinie ist die
  der ICT-Wimpern (dort, wo deren Streifen die Haut berühren); in Phase 4 lag sie an der Naht Gesicht/Augenhöhle, also
  an der hinteren Lidkante, und die Wimpern stachen 2–3 mm über dem Lidrand aus der Haut. Oben drei unregelmäßige
  Reihen (vorn lang, dahinter kürzer, je 0,3 mm tiefer in der Lidkante), unten eine lichte Reihe; Länge oben 6–11 mm
  (nach außen länger), unten 3–6 mm. Jede Reihe hat im Bild ein eigenes Band (WIMPER_BAENDER): UV u = innen → außen,
  v = Wurzel → Spitze im Band (Band 0 Unterlid, 1–3 Oberlid vorn → hinten). Die Wimpern folgen beim Blinzeln dem
  Lidrand (kopf2_bauen.py dreht jede Wimper um die Augenmitte, gebunden an die Haut an ihrer Wurzel).
Achsen wie im Bau: Blender (z oben, Gesicht nach -y, +x = linke Seite der Figur), Meter.
"""
import numpy as np

# Brauenkarte: Höhe (m) innen/außen und Lage der Merkpunktlinie in v (Merkpunkte = oberer Rand der Braue)
BRAUE_HOEHE = (0.0165, 0.0115)
BRAUE_V_MERK = 0.80
BRAUE_LAGEN = (0.00035, 0.0009)
# Wimpern (Phase 5): Länge der Karte (m) entlang des Lids (0 = innen, 1 = außen); das Bild (kopf3_haartexturen.py)
# zeichnet die einzelnen Wimpern relativ dazu (mit Streuung kürzer)
WIMPER_OBEN = ((0.0, 0.15, 0.5, 0.8, 1.0), (0.0055, 0.0075, 0.0098, 0.0108, 0.0085))
WIMPER_UNTEN = ((0.0, 0.3, 0.7, 1.0), (0.003, 0.0046, 0.006, 0.0052))
# Reihen: (Oberlid?, Band im Bild, Längenfaktor, Biegung nach oben/unten, Tiefe in der Lidkante (m), Neigung nach außen)
WIMPER_LAGEN = ((0, 0, 1.0, 0.8, 0.0001, 0.8), (1, 1, 1.0, 1.0, 0.0, 1.0), (1, 2, 0.84, 0.9, 0.0003, 0.85),
                (1, 3, 0.62, 0.75, 0.0006, 0.6))
WIMPER_BAENDER = 4


def wimper_laenge(t, oben):
    """Kartenlänge (m) an der Stelle t (0 innen, 1 außen) – auch für das Bild."""
    st, la = WIMPER_OBEN if oben else WIMPER_UNTEN
    return np.interp(t, st, la)


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


def wimpern(wurzel_punkte, lidnaht_punkte, augen, flaeche_haut, n=26, nv=6):
    """wurzel_punkte: {"l"/"r": Punkte der vorderen Lidkante (Wurzeln der ICT-Wimpern)}; lidnaht_punkte: {"l"/"r":
    Naht Gesicht/Augenhöhle (hintere Lidkante)}; augen: {"l"/"r": (Mitte, Halbmesser)}; flaeche_haut: kw.Flaeche des
    Gesichts. Gibt (ecken, flächen, uv je Schleife, oben je Ecke 1/0, seite je Ecke als "l"/"r", wurzel je Ecke) zurück.
    Achsen wie im Bau (z oben, Gesicht nach -y)."""
    ecken, flaechen, uvs, oben_je, seite, wurzel_je = [], [], [], [], [], []
    vorn = np.array([0.0, -1.0, 0.0])
    for s in ("l", "r"):
        mitte, halb = augen[s]
        oben, unten = lidkante_ordnen(wurzel_punkte[s], mitte)
        naht = np.asarray(lidnaht_punkte[s])
        for ist_oben, band, l_f, biegung, tiefe, neigung in WIMPER_LAGEN:
            folge = oben if ist_oben else unten
            if len(folge) < 4:
                continue
            k = _glatte_folge(folge, n)
            # Lidwinkel freilassen (dort wachsen kaum Wimpern; die Karte endet knapp davor)
            k = _glatte_folge(k[1:-1], n) if len(k) > 6 else k
            t = np.linspace(0, 1, len(k))
            tang = np.gradient(k, axis=0)
            tang /= np.linalg.norm(tang, axis=1, keepdims=True)
            # Reihen hinter der ersten liegen tiefer in der Lidkante (zur hinteren Kante, der Naht, hin)
            zur_naht = naht[np.argmin(np.linalg.norm(k[:, None] - naht[None], axis=2), axis=1)] - k
            zur_naht /= np.maximum(np.linalg.norm(zur_naht, axis=1, keepdims=True), 1e-9)
            wurzel = k + zur_naht * tiefe
            auf, nrm, _, _ = flaeche_haut.naechste(wurzel, 0.004)
            nrm = np.where(((wurzel - mitte) * nrm).sum(1, keepdims=True) < 0, -nrm, nrm)
            wurzel = auf - nrm * 0.0001                  # 0,1 mm in die Haut: keine Lücke an der Wurzel
            radial = wurzel - mitte
            radial -= np.outer(radial @ vorn, vorn)
            radial /= np.linalg.norm(radial, axis=1, keepdims=True)
            L = wimper_laenge(t, ist_oben) * l_f
            # nach außen geneigt, außen stärker; innen leicht zur Nase
            seitlich = tang * ((-0.12 + 0.5 * t ** 1.3) * neigung)[:, None]
            o = len(ecken)
            pkt = []
            for i in range(len(k)):
                if ist_oben:
                    # aus der Lidkante zuerst nach vorn und eine Spur abwärts, dann nach oben gebogen
                    p1 = wurzel[i] + L[i] * 0.4 * (0.96 * vorn - 0.16 * radial[i])
                    p2 = wurzel[i] + L[i] * (0.72 * vorn + 0.18 * biegung * radial[i]) + 0.5 * L[i] * seitlich[i]
                    p3 = wurzel[i] + L[i] * ((0.66 - 0.1 * biegung) * vorn + 0.66 * biegung * radial[i]) + L[i] * seitlich[i]
                else:
                    # unten: nach vorn, dann abwärts (radial zeigt am Unterlid nach unten)
                    p1 = wurzel[i] + L[i] * 0.4 * (0.95 * vorn + 0.1 * radial[i])
                    p2 = wurzel[i] + L[i] * (0.72 * vorn + 0.3 * biegung * radial[i]) + 0.5 * L[i] * seitlich[i]
                    p3 = wurzel[i] + L[i] * (0.6 * vorn + 0.7 * biegung * radial[i]) + L[i] * seitlich[i]
                kurve = np.array([(1 - v) ** 3 * wurzel[i] + 3 * (1 - v) ** 2 * v * p1 + 3 * (1 - v) * v ** 2 * p2 + v ** 3 * p3
                                  for v in np.linspace(0, 1, 24)])
                # auf die Länge L bringen (Bogenlänge), dann nv Stützen gleichmäßig
                bogen = np.concatenate([[0], np.cumsum(np.linalg.norm(np.diff(kurve, axis=0), axis=1))])
                kurve = wurzel[i] + (kurve - wurzel[i]) * (L[i] / bogen[-1])
                bogen *= L[i] / bogen[-1]
                for j in range(nv):
                    ziel = L[i] * j / (nv - 1)
                    pkt.append([np.interp(ziel, bogen, kurve[:, a]) for a in range(3)])
                wurzel_je.extend([wurzel[i]] * nv)
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
    return np.array(ecken), flaechen, uvs, np.array(oben_je), np.array(seite), np.array(wurzel_je)
