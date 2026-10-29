"""Charakter Phase 2: ICT-Gesicht auf die MPFB-Büste legen (Registrierung, eigener Code, MIT).

1. Merkpunkte auf beiden Netzen mit derselben Regel (Augenmitten, Mundwinkel, Nasenspitze, Kinn, Glabella,
   Scheitel, Hinterkopf, Ohren, Hals)
2. Ähnlichkeit (Umeyama): ICT in den Raum der MPFB-Büste („ausgerichtet“, bleibt die Form des Spielkopfs)
3. Nicht-starre Anpassung einer Kopie („angepasst“): Thin-Plate-Spline auf den Merkpunkten, dann mehrmals
   nächster Punkt auf der MPFB-Haut mit geglättetem Versatz (eine einfache NICP)
4. Bindung jeder ICT-Hautecke (aus „angepasst“) an ein MPFB-Dreieck: darüber kommen Alter, Geschlecht und Formregler
"""
import numpy as np

import kopf2_quellen as kq
import kopf2_werkzeug as kw


def _band(p, maske, bedingung):
    idx = np.where(maske & bedingung)[0]
    return idx


def merkpunkte(p, haut, auge_l, auge_r, mund_l, mund_r):
    """Merkpunkte nach derselben Regel für beide Netze. p: Ecken; haut: Indizes der äußeren Haut;
    auge_*: Augapfelmitten; mund_*: Mundwinkel. „l“ = +x = linke Seite der Figur."""
    m = np.zeros(len(p), dtype=bool)
    m[haut] = True
    aa = float(np.linalg.norm(auge_l - auge_r))
    az = (auge_l[2] + auge_r[2]) / 2.0
    mund = (mund_l + mund_r) / 2.0
    x, y, z = p[:, 0], p[:, 1], p[:, 2]
    mitte = np.abs(x) < 0.05 * aa

    def vorn(bed):
        i = _band(p, m, mitte & bed)
        return p[i[np.argmin(y[i])]]

    def hinten(bed):
        i = _band(p, m, mitte & bed)
        return p[i[np.argmax(y[i])]]
    erg = {"auge_l": auge_l, "auge_r": auge_r, "mund_l": mund_l, "mund_r": mund_r,
           "nase": vorn((z < az) & (z > mund[2])),
           "kinn": vorn((z < mund[2] - 0.25 * aa) & (z > mund[2] - 0.8 * aa)),
           "glabella": vorn((z > az) & (z < az + 0.4 * aa)),
           "hinterkopf": hinten(np.abs(z - az) < 0.1 * aa)}
    erg["scheitel"] = p[haut[np.argmax(z[haut])]]
    band = m & (z > mund[2]) & (z < az + 0.5 * aa)
    for seite, s in (("ohr_l", 1.0), ("ohr_r", -1.0)):
        i = np.where(band & (s * x > 0))[0]
        grenze = 0.85 * (s * x[i]).max()
        erg[seite] = p[i[s * x[i] > grenze]].mean(axis=0)
    # Hals: vorn und hinten auf der Mittellinie (seitlich träfe die Regel bei MPFB schon den Trapezmuskel)
    z0 = mund[2] - 1.1 * aa
    i = np.where(m & (np.abs(x) < 0.2 * aa) & (np.abs(z - z0) < 0.15 * aa))[0]
    vorn_, hinten_ = i[np.argmin(y[i])], i[np.argmax(y[i])]
    assert y[hinten_] - y[vorn_] > 0.05, "Hals hinten nicht gefunden"
    erg["hals_vorn"] = np.array([0.0, y[vorn_], z[vorn_]])
    erg["hals_hinten"] = np.array([0.0, y[hinten_], z[hinten_]])
    return erg, aa


def ict_augen(v):
    """Augapfelmitten des ICT-Netzes: Kugel durch die Lederhaut ohne die Hornhautwölbung (zweiter Durchgang)."""
    aus = {}
    for s, (a, b) in kq.ICT_LEDERHAUT.items():
        q = v[a:b]
        c, r = kw.kugel(q)
        d = np.abs(np.linalg.norm(q - c, axis=1) - r)
        c, r = kw.kugel(q[d < np.median(d) * 2.0])
        aus[s] = (c, r)
    return aus


def anpassen(ict, mp, basis_lage, bericht):
    """Ausrichtung, Anpassung und Bindung. Rückgabe dict mit Arrays (alle in MPFB-Metern, Blender-Achsen)."""
    v = ict["ecken"]
    n = len(v)
    haut_ict = np.arange(0, kq.ICT_TEILE["mundhoehle"][0])          # Gesicht + Hinterkopf/Hals
    augen = ict_augen(v)
    lm = ict["merkpunkte68"]
    # Multi-PIE 48 und 54 sind die Mundwinkel; welcher +x hat, entscheidet die Lage
    w48, w54 = v[lm[48]], v[lm[54]]
    ml, mr = (w54, w48) if w54[0] > w48[0] else (w48, w54)
    mp_ict, aa_ict = merkpunkte(v, haut_ict, augen["l"][0], augen["r"][0], ml, mr)

    p = basis_lage
    koerper = mp.gruppen["body"]
    lippen = mp.gruppen["lips"]
    lip = p[lippen]
    m_l, m_r = lip[np.argmax(lip[:, 0])], lip[np.argmin(lip[:, 0])]
    auge_l, r_l = kw.kugel(p[mp.gruppen["helper-l-eye"]])
    auge_r, r_r = kw.kugel(p[mp.gruppen["helper-r-eye"]])
    # nur die Kopf- und Halshaut als „haut“ (sonst finden Hals/Ohr-Regeln Schultern oder Hände)
    kopf = koerper[p[koerper, 2] > auge_l[2] - 0.30]
    kopf = kopf[np.abs(p[kopf, 0]) < 0.12]
    mp_mpfb, aa_mpfb = merkpunkte(p, kopf, auge_l, auge_r, m_l, m_r)
    namen = sorted(mp_ict)
    quelle = np.array([mp_ict[k] for k in namen])
    ziel = np.array([mp_mpfb[k] for k in namen])
    # 1. Ähnlichkeit nur aus dem Gesicht (Augen, Mund, Nase, Kinn, Glabella, Ohren)
    gesicht = [i for i, k in enumerate(namen) if not k.startswith("hals") and k not in ("scheitel", "hinterkopf")]
    s, r, t = kw.umeyama(quelle[gesicht], ziel[gesicht])
    ausgerichtet = (s * (r @ v.T)).T + t
    q_aus = (s * (r @ quelle.T)).T + t
    bericht["ausrichtung"] = {"massstab": round(float(s), 4), "drehung_grad": round(float(np.degrees(np.arccos(
        np.clip((np.trace(r) - 1) / 2, -1, 1)))), 2), "augenabstand_ict_mm": round(aa_ict * 1000, 1),
        "augenabstand_mpfb_mm": round(aa_mpfb * 1000, 1)}
    bericht["merkpunkte_mm"] = {k: [[round(float(x) * 1000) for x in q_aus[i]], [round(float(x) * 1000) for x in ziel[i]]]
                                for i, k in enumerate(namen)}
    bericht["merkpunkt_abstand_nach_ausrichtung_mm"] = {k: round(float(np.linalg.norm(q_aus[i] - ziel[i]) * 1000), 1)
                                                         for i, k in enumerate(namen)}
    # 2. TPS auf allen Merkpunkten
    tps = kw.Tps(q_aus, ziel)
    angepasst = tps(ausgerichtet)
    # 3. Nächster Punkt auf der MPFB-Kopfhaut, Versatz geglättet (grob -> fein)
    kopf_fl = [f for f in mp.koerper_flaechen if all(p[i, 2] > auge_l[2] - 0.40 for i in f)]
    flaeche = kw.Flaeche(p, kw.dreiecke(kopf_fl))
    haut_fl = [f for f in ict["flaechen"] if max(f) < len(haut_ict)]
    kanten, grad = kw.kanten(haut_fl, len(haut_ict))
    tri_haut = kw.dreiecke(haut_fl)
    for schritte in (60, 40, 25, 15, 8, 4):
        q = angepasst[haut_ict]
        lage, nrm, dr, ab = flaeche.naechste(q, 0.03)
        n_ict = kw.ecken_normalen(q, tri_haut)
        d = lage - q
        gut = (dr >= 0) & ((n_ict * nrm).sum(1) > 0.3)
        d[~gut] = 0.0
        d = kw.glaetten(d, kanten, grad, schritte, gewicht=gut.astype(float))
        angepasst[haut_ict] += d
    lage, nrm, dr, ab = flaeche.naechste(angepasst[haut_ict], 0.05)
    bericht["anpassung_rest_mm"] = {"mittel": round(float(np.mean(ab[np.isfinite(ab)]) * 1000), 2),
                                    "95%": round(float(np.percentile(ab[np.isfinite(ab)], 95) * 1000), 2),
                                    "max": round(float(np.max(ab[np.isfinite(ab)]) * 1000), 2)}
    # Die übrigen ICT-Teile (Höhlen, Zähne, Augen) folgen der Anpassung über die TPS plus den Hautversatz der
    # nächsten Hautecken (nur für die Bindung; sie bekommen ihre Formen später eigens)
    rest = np.arange(len(haut_ict), n)
    idx, gew = kw.k_naechste(angepasst[haut_ict] - 0.0, ausgerichtet[rest], k=6)
    zusatz = angepasst[haut_ict] - tps(ausgerichtet[haut_ict])
    angepasst[rest] += (zusatz[idx] * gew[..., None]).sum(1)
    return {"ausgerichtet": ausgerichtet, "angepasst": angepasst, "s": s, "r": r, "t": t,
            "flaeche_kopf": flaeche, "augen_ict": {k: ((s * r @ c) + t, r_ * s) for k, (c, r_) in augen.items()},
            "augen_mpfb": {"l": (auge_l, r_l), "r": (auge_r, r_r)}, "merkpunkte_mpfb": mp_mpfb,
            "merkpunkte_ict": {k: q_aus[i] for i, k in enumerate(namen)}, "augenabstand": aa_mpfb}
