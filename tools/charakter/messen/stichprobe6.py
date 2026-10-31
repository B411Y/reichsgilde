"""1000er-Prüfung der Etappe B (Charakter Phase 6, S5–S7): misst den Gewichteausdruck von Godot (charakter_schau.gd
--gewichte, Startwerte 10001–11000 je Geschlecht) am fertigen Kopf und schreibt die Kennzahlen.

Je Geschlecht und Maß: Mittel und Streuung gegen die natürliche Streuung (NIOSH; ohne NIOSH-Wert ICT-Scans × 0,7),
Abweichung des Mittels vom Mittelgesicht (in σ), Karikaturmaß K (Godot: σ-Werte der Gene nach der Bremse; dazu K über
die gemessenen, σ-normierten Maße), Merkmale über 2σ, Abstand zum nächsten Nachbarn (12 Maße, σ-normiert), Spiegelabstand
(Asymmetrie) und die extremsten Köpfe (für Bogen P14).

Aufruf: $BPY -I tools/charakter/messen/stichprobe6.py <gewichte.json> <kopf.glb> <aus.json> [prozesse=4]
"""
import json
import os
import sys
from multiprocessing import Pool

import numpy as np

HIER = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.dirname(HIER))
from messen.glb import Glb  # noqa: E402
from messen.netz_masse import Netzmesser  # noqa: E402

# Natürliche Streuung (mm) je Geschlecht: NIOSH (Zhuang & Bradtmiller 2005) bzw. ICT-Scans × 0,7 (Recherche Vielfalt 3.4)
STREUUNG = {
    "se_me": ("NIOSH Gesichtslänge", 6.9, 5.8), "zy": ("NIOSH Gesichtsbreite", 6.7, 6.2),
    "unterkiefer": ("ICT Kieferbreite × 0,7", 4.9, 4.9), "mundbreite": ("NIOSH Mundbreite", 4.0, 3.5),
    "n_sn": ("NIOSH Nasenlänge", 4.1, 3.7), "nasenbreite": ("Nasenbreite (Recherche)", 3.2, 2.9),
    "lippenrot": ("ICT Lippenrot × 0,7", 2.5, 2.5), "kinnvorsprung": ("ICT Kinnvorsprung × 0,7", 3.3, 3.3),
}
NACHBAR = ["se_me", "zy", "unterkiefer", "mundbreite", "n_sn", "nasenbreite", "lippenrot", "kinnvorsprung",
           "b_kinnmitte", "braue_pupille", "nasenvorsprung", "ipd"]
_nm = None


def _start(glb):
    global _nm
    _nm = Netzmesser(Glb(glb))


def _messen(p):
    P = _nm.netz.punkte(p["szene"])
    m = _nm.messen(p["szene"], punkte=P, schnell=True)
    m["lippenrot"] = float((P["lm51"][1] - P["lm57"][1]) - (P["lm62"][1] - P["lm66"][1]))
    m["kinnvorsprung"] = float(P["pg"][2] - P["sm"][2])
    m["spiegel_rms"] = _nm._spiegel(P["_haut"], P["_z_ohr"])
    return {"name": p["name"], "geschlecht": p["geschlecht"], "saat": p.get("saat"), "masse": m,
            "K_gene": p.get("karikatur", {}).get("K_nachher"), "K_gene_vorher": p.get("karikatur", {}).get("K_vorher"),
            "asym_faktor": p.get("karikatur", {}).get("asymmetrie_faktor"),
            "z": p.get("gene_z", {})}


def main():
    ein, glb, aus = sys.argv[1], sys.argv[2], sys.argv[3]
    opt = dict(a.split("=", 1) for a in sys.argv[4:])
    d = json.load(open(ein, encoding="utf-8"))
    with Pool(int(opt.get("prozesse", "4")), initializer=_start, initargs=(glb,)) as pool:
        erg = pool.map(_messen, d["personen"], chunksize=20)
    mittel = {e["name"][7:]: e["masse"] for e in erg if e["name"].startswith("mittel_")}
    bericht = {"anzahl": len(erg), "glb": glb, "gewichte": ein, "geschlechter": {}}
    for g, gi in (("m", 1), ("w", 2)):
        es = [e for e in erg if e.get("saat") and abs(e["geschlecht"] - (1.0 if g == "m" else 0.0)) < 0.5]
        mm = {k: np.array([e["masse"][k] for e in es]) for k in NACHBAR + ["spiegel_rms"]}
        b = {"anzahl": len(es), "masse": {}}
        for k, (quelle, sm, sw) in STREUUNG.items():
            s_nat = sm if g == "m" else sw
            werte = mm[k][np.isfinite(mm[k])]
            ref = mittel.get(f"{g}35", {}).get(k)
            b["masse"][k] = {"quelle": quelle, "mittel": round(float(werte.mean()), 2), "streuung": round(float(werte.std(ddof=1)), 2),
                             "natuerlich": s_nat, "anteil": round(float(werte.std(ddof=1)) / s_nat, 3),
                             "mittelgesicht": round(float(ref), 2) if ref is not None else None,
                             "mittel_minus_mittelgesicht_sigma": round((float(werte.mean()) - ref) / s_nat, 3) if ref is not None else None}
        # σ-normierte Matrix über 12 Maße (natürliche Streuung, sonst Streuung der Stichprobe)
        Z = np.stack([(mm[k] - np.nanmean(mm[k])) / (STREUUNG[k][gi] if k in STREUUNG else np.nanstd(mm[k])) for k in NACHBAR], 1)
        Z = np.nan_to_num(Z)
        K = np.sqrt((Z ** 2).mean(1))
        dist = np.sqrt(((Z[:, None, :] - Z[None, :, :]) ** 2).sum(2) / Z.shape[1])
        np.fill_diagonal(dist, np.inf)
        nn = dist.min(1)
        kg = np.array([e["K_gene"] or 0.0 for e in es])
        kgv = np.array([e["K_gene_vorher"] or 0.0 for e in es])
        ueber2 = np.array([sum(1 for v in e["z"].values() if abs(v) > 2.0 + 1e-6) for e in es])
        b["K_gene"] = {"p50": round(float(np.median(kg)), 3), "p95": round(float(np.percentile(kg, 95)), 3), "max": round(float(kg.max()), 3),
                       "vorher_p95": round(float(np.percentile(kgv, 95)), 3), "anteil_gebremst": round(float((kgv > 1.35).mean()), 3),
                       "max_merkmale_ueber_2sigma": int(ueber2.max())}
        b["K_masse"] = {"p50": round(float(np.median(K)), 3), "p95": round(float(np.percentile(K, 95)), 3), "max": round(float(K.max()), 3),
                        "max_masse_ueber_2sigma": int((np.abs(Z) > 2).sum(1).max())}
        b["nachbar"] = {"median": round(float(np.median(nn)), 3), "p5": round(float(np.percentile(nn, 5)), 3), "masse": NACHBAR}
        sp = mm["spiegel_rms"]
        b["spiegel_rms"] = {"median": round(float(np.median(sp)), 3), "p10": round(float(np.percentile(sp, 10)), 3),
                            "p90": round(float(np.percentile(sp, 90)), 3), "anteil_0_5_bis_1_0": round(float(((sp >= 0.5) & (sp <= 1.0)).mean()), 3)}
        b["asymmetrie_verkleinert"] = round(float(np.mean([(e["asym_faktor"] or 1.0) < 1.0 for e in es])), 3)
        reihen = np.argsort(-kg)
        b["extreme_20"] = [int(es[i]["saat"]) for i in reihen[:20]]
        b["extreme_20_K_masse"] = [int(es[i]["saat"]) for i in np.argsort(-K)[:20]]
        rng = np.random.default_rng(14)
        b["zufall_10"] = [int(es[i]["saat"]) for i in rng.choice(len(es), 10, replace=False)]
        bericht["geschlechter"][g] = b
    bericht["mittelgesicht"] = {k: {kk: round(float(v), 3) for kk, v in m.items() if isinstance(v, float)} for k, m in mittel.items()}
    json.dump(bericht, open(aus, "w", encoding="utf-8"), indent=1, ensure_ascii=False)
    for g, b in bericht["geschlechter"].items():
        print(g, "K_gene", b["K_gene"], "K_masse", b["K_masse"], "Nachbar", {k: v for k, v in b["nachbar"].items() if k != "masse"}, "Spiegel", b["spiegel_rms"])
        for k, e in b["masse"].items():
            print("  ", k, "σ", e["streuung"], "/", e["natuerlich"], "=", e["anteil"], "| Mittel", e["mittel"], "MG", e["mittelgesicht"], "Δσ", e["mittel_minus_mittelgesicht_sigma"])


if __name__ == "__main__":
    main()
