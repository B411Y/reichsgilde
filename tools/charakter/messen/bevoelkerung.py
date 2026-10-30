"""Kennzahlen einer Stichprobe von Köpfen (Charakter Phase 6, S0; voll genutzt ab S5).

Eingabe: der Gewichteausdruck der Testszene (charakter_schau.gd --gewichte=…, je Person die Formgewichte aus der
Szene) – Python misst genau, was Godot zeigt. Je Geschlecht:
  - Mittel und Streuung je Maß gegen NIOSH (Zhuang & Bradtmiller 2005, Teilgruppe „White“, US-Behördendaten; nur
    Zahlen) bzw. gegen die ICT-Scans, wo NIOSH nichts hat (S5 ergänzt die Tabelle)
  - Karikaturmaß K = √(Mittel z²) über die σ-normierten Maße
  - Abstand zum nächsten Nachbarn (σ-normiert, alle Maße), Median und 5-%-Wert
  - Spiegelabstand (Asymmetrie) und Durchdringungen (Lid–Augapfel, Lippe–Zahn)
"""
import numpy as np

# NIOSH (mm): Mittel, Streuung; Schlüssel = Maß aus netz_masse
NIOSH = {
    "m": {"se_me": (122.6, 6.9), "zy": (142.9, 6.7), "mundbreite": (50.7, 4.0), "n_sn": (52.3, 4.1)},
    "w": {"se_me": (113.0, 5.8), "zy": (133.8, 6.2), "mundbreite": (47.1, 3.5), "n_sn": (48.6, 3.7)},
}
MASSE = ["se_me", "zy", "b_mund", "b_kinnmitte", "unterkiefer", "mundbreite", "nasenbreite", "n_sn", "sn_sto",
         "nasenvorsprung", "braue_pupille", "pg_hinter_li"]


def ergaenzen(m, punkte=None):
    """Abgeleitete Maße, die netz_masse nicht direkt ausgibt (n_sn)."""
    if "n_sn" not in m and "n_me_y" in m and "sn_me" in m:
        m["n_sn"] = m["n_me_y"] - m["sn_me"]
    return m


def kennzahlen(messungen, geschlecht):
    """messungen: Liste von dicts (netz_masse.messen). Gibt je Maß Mittel, Streuung, NIOSH und Anteil zurück, dazu K,
    Nachbarabstände, Asymmetrie und Durchdringungen."""
    ms = [ergaenzen(dict(m)) for m in messungen]
    aus = {"anzahl": len(ms), "masse": {}}
    for k in MASSE:
        werte = np.array([m[k] for m in ms if k in m and np.isfinite(m[k])])
        if not len(werte):
            continue
        e = {"mittel": float(werte.mean()), "streuung": float(werte.std(ddof=1)) if len(werte) > 1 else 0.0}
        if k in NIOSH[geschlecht]:
            nm, ns = NIOSH[geschlecht][k]
            e.update({"niosh_mittel": nm, "niosh_streuung": ns, "anteil_streuung": e["streuung"] / ns,
                      "mittel_in_sigma": (e["mittel"] - nm) / ns})
        aus["masse"][k] = e
    # σ-normierte Matrix (Streuung der Stichprobe; bei NIOSH-Maßen die NIOSH-Streuung)
    spalten = [k for k in MASSE if k in aus["masse"] and aus["masse"][k]["streuung"] > 0]
    if len(ms) > 2 and spalten:
        Z = np.array([[(m[k] - aus["masse"][k]["mittel"]) / NIOSH[geschlecht].get(k, (0, aus["masse"][k]["streuung"]))[1]
                       for k in spalten] for m in ms])
        K = np.sqrt((Z ** 2).mean(axis=1))
        aus["K"] = {"median": float(np.median(K)), "p95": float(np.percentile(K, 95)), "max": float(K.max()),
                    "ueber_2sigma_max": int((np.abs(Z) > 2).sum(axis=1).max())}
        d = np.sqrt(((Z[:, None, :] - Z[None, :, :]) ** 2).sum(axis=2) / len(spalten))
        np.fill_diagonal(d, np.inf)
        nn = d.min(axis=1)
        aus["nachbar"] = {"median": float(np.median(nn)), "p5": float(np.percentile(nn, 5))}
        aus["spalten"] = spalten
    for k in ("spiegel_rms", "lid_augapfel", "lippe_zahn"):
        w = np.array([m[k] for m in ms if k in m])
        if len(w):
            aus[k] = {"min": float(w.min()), "median": float(np.median(w)), "max": float(w.max())}
    return aus
