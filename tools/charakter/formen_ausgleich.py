"""Ausgleichsrechnung der erwachsenen Grundgesichter (Charakter Phase 6, S4; eigener Code, MIT).

Ziel: Mann und Frau (Mensch-Mittel, 35 Jahre) erreichen die Netzmaße der Norm (Plan S4, Tabelle „Abnahme“), ohne dass
sich der Pupillenabstand ändert. Grundlage ist der Probebau (kopf6.sh proben): jedes Kandidatenziel liegt dort als
eigene Form. Die Reifeform der Phase 5 wird in ihre Ziele zerlegt (dieselben Ziele als Probeformen), dann sucht die
Rechnung Zusatzgewichte je Probeform:

  min Σ ((Maß − Ziel) / Toleranz)² + λ Σ (rms_k · Δc_k)²    unter  unten_k ≤ c_k ≤ oben_k

Maße sind stückweise linear in den Gewichten (Merkpunkte als vorderste/seitlichste Ecken); gelöst wird mit
Gauß-Newton: je Runde die Ableitung jeder Probeform (Differenzenquotient), dann begrenzte kleinste Quadrate mit
projiziertem Gradienten (numpy, ohne scipy), dann Neumessung. Einseitige Ziele (≤, ≥) zählen nur, wenn sie verletzt
oder fast verletzt sind.

Ergebnis: Rezeptteil (formen_mischen.py) mit alter_reife_<g>_oben (Augen, Brauen, Stirn, Nase) und
alter_reife_<g>_unten (Wangen, Kiefer, Kinn, Mund, Hals) sowie alter_mitte_<g> (25 → 45 Jahre), dazu ein Bericht
mit Ist/Ziel je Maß. Grundgewichte (Makro, Ruhelage des Lids) kommen aus dem Gewichteausdruck der Testszene;
die Gen-Gewichte des Mensch-Mittels sind ab S5 null (Nullpunkt Mensch-Mittel) und werden hier auf null gesetzt.

Aufruf: $BPY -I tools/charakter/formen_ausgleich.py --glb=<glb6p/kopf6p.glb> --gewichte=<gewichte.json> --aus=<ordner>
"""
import json
import os
import sys

import numpy as np

HIER = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HIER)
from messen.glb import Glb  # noqa: E402
from messen.netz_masse import Netzmesser  # noqa: E402

PROBEN = json.load(open(os.path.join(HIER, "kopf6_proben.json"), encoding="utf-8"))
OBEN_VORSILBEN = ("probe_eye_", "probe_eyebrows_", "probe_forehead_", "probe_nose_", "eigen_lidfalte", "gen_augen_tiefe",
                  "gen_brauenwulst", "eigen_augenhoehle", "eigen_deckfalte", "eigen_unterlid")   # letzte drei: Etappe D


def ist_oben(name):
    return name.startswith(OBEN_VORSILBEN)


def zerlegen(ziele):
    """MakeHuman-Ziele einer Mischform (Phase 5) -> Probeformen (l-/r-Paare zusammen)."""
    zu_probe = {}
    for n, t in PROBEN.items():
        if n.startswith("_"):
            continue
        zu_probe[tuple(sorted(t))] = "probe_" + n
    aus, rest = {}, dict(ziele)
    for z, w in list(rest.items()):
        if z not in rest:
            continue
        paar = z.replace("/l-", "/r-") if "/l-" in z else (z.replace("/r-", "/l-") if "/r-" in z else None)
        schl = tuple(sorted([z, paar])) if paar and paar in rest else (z,)
        if schl not in zu_probe:
            raise KeyError(f"keine Probeform für {schl}")
        if len(schl) == 2:
            assert abs(rest[schl[0]] - rest[schl[1]]) < 1e-9, schl
        aus[zu_probe[schl]] = float(w)
        for k in schl:
            rest.pop(k, None)
    return aus


class Messer:
    """Maße am Probebau für Gewichte (Grundgewichte + Probeformen)."""

    def __init__(self, glb_pfad):
        self.glb = Glb(glb_pfad)
        self.nm = Netzmesser(self.glb)
        self.haut = self.nm.netz.haut
        self._rms = {}

    def masse(self, w):
        return self.nm.messen(w, schnell=True)

    def rms(self, form):
        """RMS-Versatz einer Form über die Gesichtsecken (mm) – Maßstab für die Glättungsstrafe."""
        if form not in self._rms:
            d = self.haut.versatz(form)
            g = self.haut.grund * 1000
            vorn = g[:, 2] > np.percentile(g[:, 2], 40)
            self._rms[form] = float(np.sqrt(((d[vorn] * 1000) ** 2).sum(1).mean())) if d is not None else 0.0
        return self._rms[form]


def addiere(*dicts):
    aus = {}
    for d in dicts:
        for k, v in d.items():
            aus[k] = aus.get(k, 0.0) + float(v)
    return aus


def skaliert(d, f):
    return {k: v * f for k, v in d.items()}


def loesen(A, b, gewicht_strafe, x0, unten, oben, schritte=6000):
    """min |A x − b|² + Σ g_k (x_k − x0_k)²  unter unten ≤ x ≤ oben (projizierter Gradient)."""
    G = A.T @ A + np.diag(gewicht_strafe)
    h = A.T @ b + gewicht_strafe * x0
    L = np.linalg.eigvalsh(G).max()
    x = np.clip(x0.copy(), unten, oben)
    for _ in range(schritte):
        x = np.clip(x - (G @ x - h) / L, unten, oben)
    return x


GEGEN = (("_incr", "_decr"), ("_up", "_down"), ("_in", "_out"), ("_forward", "_backward"), ("_convex", "_concave"))


def gegenform(name):
    """probe_chin_height_incr -> probe_chin_height_decr (falls es die Probeform gibt), sonst None."""
    for a, b in GEGEN:
        for x, y in ((a, b), (b, a)):
            if name.endswith(x):
                n = name[: -len(x)] + y
                return n if n[6:] in PROBEN else None
    return None


def ausgleich(messer, grund, fest, variablen, ziele, runden=20, lam=0.008, protokoll=None):
    """grund: feste Gewichte; fest: feste Zusatzformen; variablen: {Name: (plus-dict, minus-dict, c0, unten, oben)} –
    c ≥ 0 setzt die plus-Formen × c, c < 0 die minus-Formen × |c| (Gegenpaare als eine Variable, damit die Rechnung
    nicht Kinn höher und niedriger zugleich setzt); ziele: [(Maß, Art, a, b, Toleranz)] mit Art 'ziel' (Mitte a),
    'max' (≤ a), 'min' (≥ a), 'bereich' (a…b). Gauß-Newton mit Schrittweitensuche; zurück kommt der beste Stand."""
    namen = list(variablen)
    c = np.array([variablen[n][2] for n in namen], float)
    c0 = c.copy()
    unten = np.array([variablen[n][3] for n in namen], float)
    oben = np.array([variablen[n][4] for n in namen], float)

    def rms_von(dct):
        return np.sqrt(sum((messer.rms(f) * abs(w)) ** 2 for f, w in dct.items())) if dct else 0.0
    rms_p = np.array([rms_von(variablen[n][0]) for n in namen])
    rms_m = np.array([rms_von(variablen[n][1]) or rms_von(variablen[n][0]) for n in namen])

    def gewichte(cv):
        w = addiere(grund, fest)
        for n, ci in zip(namen, cv):
            if ci > 0:
                w = addiere(w, skaliert(variablen[n][0], ci))
            elif ci < 0:
                w = addiere(w, skaliert(variablen[n][1], -ci))
        return w

    def residuen(m, nahe=0.5):
        r, zeilen = [], []
        for (k, art, a, b, tol) in ziele:
            v = m[k]
            if art == "ziel":
                r.append((v - a) / tol)
                zeilen.append(k)
            elif art == "max":
                if v > a - nahe * tol:
                    r.append((v - (a - nahe * tol)) / tol)
                    zeilen.append(k)
            elif art == "min":
                if v < a + nahe * tol:
                    r.append((v - (a + nahe * tol)) / tol)
                    zeilen.append(k)
            elif art == "bereich":
                mitte = 0.5 * (a + b)
                halb = 0.5 * (b - a)
                if abs(v - mitte) > 0.6 * halb:
                    r.append((v - (mitte + np.sign(v - mitte) * 0.6 * halb)) / tol)
                    zeilen.append(k)
        return np.array(r), zeilen

    def kosten(cv):
        m = messer.masse(gewichte(cv))
        r, _ = residuen(m, nahe=0.0)
        strafe = lam * (np.where(cv >= 0, rms_p, rms_m) ** 2 * (cv - c0) ** 2).sum()
        return float((r ** 2).sum() + strafe), m

    bester, m_best = kosten(c)
    c_best = c.copy()
    for runde in range(runden):
        m0 = messer.masse(gewichte(c))
        r0, zeilen = residuen(m0)
        if protokoll is not None:
            protokoll.append({"runde": runde, "kosten": bester, "zeilen": zeilen})
        if not len(zeilen):
            break
        J = np.zeros((len(zeilen), len(namen)))
        u_r, o_r = unten.copy(), oben.copy()          # diese Runde nur auf der aktuellen Seite (kein Vorzeichenwechsel)
        for j, n in enumerate(namen):
            def steigung(d):
                cv = c.copy()
                cv[j] += d
                m1 = messer.masse(gewichte(cv))
                return np.array([(m1[k] - m0[k]) / d / next(z[4] for z in ziele if z[0] == k) for k in zeilen])
            if c[j] > 0.02 or (c[j] >= -0.02 and unten[j] >= 0):
                sp = steigung(0.2 if c[j] + 0.2 <= oben[j] else -0.2)
                seite = 1
                if c[j] <= 0.02 and unten[j] < 0:
                    sm = steigung(-0.2)
                    # bei 0: die Seite, in die es bergab geht
                    if -(sm @ r0) < sp @ r0:
                        sp, seite = sm, -1
                J[:, j] = sp
                if seite > 0:
                    u_r[j] = max(unten[j], 0.0)
                else:
                    o_r[j] = min(oben[j], 0.0)
            else:
                J[:, j] = steigung(-0.2 if c[j] - 0.2 >= unten[j] else 0.2)
                o_r[j] = min(oben[j], 0.0)
        strafe = lam * np.where(c >= 0, rms_p, rms_m) ** 2
        neu = loesen(J, J @ c - r0, strafe, c0, u_r, o_r)
        # Schrittweitensuche: den besten von voll, halb, viertel, achtel nehmen
        schritt, gefunden = 1.0, False
        for _ in range(7):
            cv = c + schritt * (neu - c)
            k_, m_ = kosten(cv)
            if k_ < bester:
                bester, m_best, c_best, gefunden = k_, m_, cv.copy(), True
                break
            schritt *= 0.5
        if not gefunden:
            break
        c = c_best.copy()
    # Feinsuche je Variable auf den echten Kosten (die Maße haben Knicke: vorderste/seitlichste Ecken, Gegenpaare)
    for schritt in (0.2, 0.1, 0.05, 0.025):
        for _ in range(12):
            besser = False
            for j in range(len(namen)):
                for vz in (1, -1):
                    cv = c_best.copy()
                    cv[j] = np.clip(cv[j] + vz * schritt, unten[j], oben[j])
                    if cv[j] == c_best[j]:
                        continue
                    k_, m_ = kosten(cv)
                    if k_ < bester - 1e-6:
                        bester, m_best, c_best, besser = k_, m_, cv, True
                        break
            if not besser:
                break
        if protokoll is not None:
            protokoll.append({"feinsuche": schritt, "kosten": bester})
    return {n: float(ci) for n, ci in zip(namen, c_best)}, m_best, gewichte(c_best), \
        {n: (variablen[n][0] if ci >= 0 else variablen[n][1], ci) for n, ci in zip(namen, c_best)}


def main():
    opt = dict(a[2:].split("=", 1) for a in sys.argv[1:] if a.startswith("--") and "=" in a)
    aus = opt.get("aus", ".")
    os.makedirs(aus, exist_ok=True)
    messer = Messer(os.path.expanduser(opt["glb"]))
    gew = json.load(open(opt["gewichte"], encoding="utf-8"))
    pers = {p["name"]: p for p in gew["personen"]}
    regler = json.load(open(os.path.join(HIER, "kopf2_regler.json"), encoding="utf-8"))
    bericht = {"glb": opt["glb"], "gewichte": opt["gewichte"]}
    rezept = {}

    # ------------------------------------------------------------ Grundgewichte (35 Jahre) ohne Gene und Reife
    def grund_von(name):
        w = dict(pers[name]["szene"])
        for k in list(w):
            if k.startswith(("gen_", "alter_reife", "alter_greis")) or abs(w[k]) < 1e-9:
                w.pop(k)
        return w

    reife_alt = {g: zerlegen(regler["alter_formen"][f"alter_reife_{g}"]["ziele"]) for g in "mw"}
    reife_alt["m"]["eigen_adamsapfel"] = 1.0

    # Prüfung der Linearität: alte Reifeform gegen ihre zerlegte Mischung
    lin = {}
    for g in "mw":
        grund = grund_von(f"mittel_{g}35")
        a = messer.masse(addiere(grund, {f"alter_reife_{g}": 1.0}))
        b = messer.masse(addiere(grund, reife_alt[g]))
        lin[g] = {k: round(b[k] - a[k], 3) for k in ("zy", "n_me", "kinnmitte_zy", "mund_zy", "mundlage", "braue_pupille",
                                                      "wangenwoelbung", "ipd")}
    bericht["linearitaet_reife_zerlegt_minus_ganz"] = lin
    print("Linearität (zerlegt − ganz):", json.dumps(lin))

    # ------------------------------------------------------------ alter_mitte (25 -> 45): von Hand, Nase/Mund breiter
    mitte = {}
    for g in "mw":
        grund = addiere(grund_von(f"mittel_{g}35"), reife_alt[g])
        m0 = messer.masse(grund)
        dn = messer.masse(addiere(grund, {"probe_nose_scale_horiz_incr": 1.0}))["nasenbreite"] - m0["nasenbreite"]
        dm = messer.masse(addiere(grund, {"probe_mouth_scale_horiz_incr": 1.0}))["mundbreite"] - m0["mundbreite"]
        mitte[g] = {"probe_cheek_volume_decr": 0.35 if g == "m" else 0.4, "eigen_traenenrinne": 1.0,
                    "probe_mouth_laugh_lines_in": 0.5, "probe_mouth_upperlip_volume_decr": 0.3,
                    "probe_mouth_lowerlip_volume_decr": 0.2,
                    "probe_nose_scale_horiz_incr": round(1.2 / dn, 3), "probe_mouth_scale_horiz_incr": round(1.5 / dm, 3)}
        rezept[f"alter_mitte_{g}"] = mitte[g]
        bericht.setdefault("mitte", {})[g] = {"nase_mm_je_1": round(dn, 2), "mund_mm_je_1": round(dm, 2)}

    # ------------------------------------------------------------ Ziele (Plan S4, Abnahme)
    # forehead-nubian (Nasenwurzel) ist kein Kandidat mehr: es hebt den ganzen Scheitel (Frau +20 mm bei 0,6, Kegelschädel)
    # Augentiefe „gegen heute“ = Phase 5 (Prüfmappe A2, Mittelgesicht 35, alte Genrechnung): Mann 7,80, Frau 6,67 mm
    HEUTE_AUGENTIEFE = {"m": 7.80, "w": 6.67}
    # Obergrenzen gegen Übertreibung (sonst landen Lippen, Lider, Jochbein an der Rechengrenze 1,2): Lippen der Frau
    # voller als beim Mann (S5: lippen × 1,1), Jochbein 1,5 mm (Plan S4: 1–1,5 mm)
    DECKEL = {"probe_mouth_upperlip_volume_decr": {"m": 0.6, "w": 0.4}, "probe_mouth_lowerlip_volume_decr": {"m": 0.6, "w": 0.4},
              "probe_eye_eyefold_down": {"m": 0.8, "w": 0.8}, "probe_eye_scale_decr": {"m": 1.0, "w": 1.0},
              "probe_head_fat_decr": {"m": 1.0, "w": 1.0}, "eigen_jochbein": {"m": 1.0, "w": 1.0},
              "probe_nose_trans_forward": {"m": 0.8, "w": 0.8}, "probe_chin_jaw_drop_incr": {"m": 0.8, "w": 0.8}}
    # Reifeziele, die ins Gegenteil kippen dürfen (Nase kürzer bis NIOSH, Kinn kürzer, Frau: Jochbein/Unterkiefer schmaler,
    # Mund schmaler); alle anderen nur zwischen 0 und Obergrenze
    KIPPEN = {"probe_nose_scale_vert_incr", "probe_chin_height_incr", "probe_cheek_bones_incr", "probe_chin_bones_decr",
              "probe_chin_bones_incr", "probe_mouth_scale_horiz_incr", "probe_nose_point_down"}
    ergebnisse = {}
    for g in "mw":
        grund = addiere(grund_von(f"mittel_{g}35"), skaliert(mitte[g], 0.55))    # Mitte mit 35 Jahren (regeln6: 0,55)
        vorher = messer.masse(addiere(grund, {f"alter_reife_{g}": 1.0}))
        ipd0 = vorher["ipd"]

        def V(form, c0=0.0, u=None, o=1.0):
            gf_ = gegenform(form) if form.startswith("probe_") else None
            o = min(o, DECKEL.get(form, {}).get(g, 9.0))
            u_ = (-1.0 if gf_ else 0.0) if u is None else u
            if gf_ in DECKEL:
                u_ = max(u_, -DECKEL[gf_][g])
            return ({form: 1.0}, {gf_: 1.0} if gf_ else {}, min(c0, o), u_, o)
        if g == "m":
            ziele = [("ipd", "ziel", ipd0, 0, 0.12), ("augapfel_ipd", "ziel", vorher["augapfel_ipd"], 0, 0.004 * vorher["augapfel_ipd"]),
                     ("gesichtsindex", "ziel", 0.875, 0, 0.008),
                     ("zy", "bereich", 140.0, 146.0, 1.0), ("n_me", "bereich", 120.0, 126.0, 1.0),
                     ("kinnmitte_zy", "ziel", 0.725, 0, 0.012), ("mund_zy", "ziel", 0.865, 0, 0.01),
                     ("mundlage", "ziel", 0.33, 0, 0.008), ("braue_pupille", "ziel", 10.5, 0, 0.5),
                     ("wangenwoelbung", "bereich", -2.0, 0.0, 0.4), ("augentiefe", "ziel", HEUTE_AUGENTIEFE["m"] + 1.75, 0, 0.2),
                     ("kinn_hals_winkel", "bereich", 108, 122, 2.0), ("n_sn", "ziel", 52.3, 0, 1.0), ("mundbreite", "ziel", 50.7, 0, 1.5),
                     ("pg_hinter_li", "bereich", 0.0, 4.0, 0.3)]
            var = {}
            for f, w in reife_alt["m"].items():
                var[f] = V(f, w, None if f in KIPPEN else 0.0, max(1.2, w))
            for f in ("eigen_kieferkante", "eigen_unterwange", "eigen_jochbein"):
                var[f] = V(f, 0.0, 0.0, 1.2)
            for f in ("probe_cheek_inner_decr", "probe_chin_width_decr", "probe_head_invertedtriangular",
                      "probe_mouth_trans_down", "probe_neck_double_decr", "probe_mouth_scale_horiz_decr",
                      "probe_eye_trans_out"):
                if f not in var and not any(gegenform(f) == k for k in var):
                    var[f] = V(f)
            var["gen_augen_tiefe_plus"] = ({"gen_augen_tiefe_plus": 1.0}, {"gen_augen_tiefe_minus": 1.0}, 0.0, -1.0, 1.0)
            fest = {"eigen_lidfalte_m": 1.0, "eigen_kopfnicker": 1.0}
        else:
            uk_m = ergebnisse["m"]["masse"]["unterkiefer"]
            ziele = [("ipd", "ziel", ipd0, 0, 0.12), ("augapfel_ipd", "ziel", vorher["augapfel_ipd"], 0, 0.004 * vorher["augapfel_ipd"]),
                     ("gesichtsindex", "ziel", 0.87, 0, 0.008),
                     ("zy", "ziel", round(vorher["zy"] * 0.92, 1), 0, 1.0), ("n_me", "ziel", 116.0, 0, 1.2),
                     ("kinnmitte_zy", "ziel", 0.69, 0, 0.01), ("mund_zy", "ziel", 0.87, 0, 0.006),
                     ("mundlage", "ziel", 0.33, 0, 0.008), ("braue_pupille", "ziel", 13.0, 0, 0.4),
                     ("wangenwoelbung", "bereich", -1.0, 2.0, 0.4), ("augentiefe", "ziel", HEUTE_AUGENTIEFE["w"], 0, 0.2),
                     ("nasofrontalwinkel", "ziel", 138.0, 0, 1.5), ("pg_hinter_li", "bereich", 0.0, 4.0, 0.3),
                     ("kinn_hals_winkel", "bereich", 108, 122, 2.0), ("unterkiefer", "ziel", 0.90 * uk_m, 0, 0.6),
                     ("n_sn", "ziel", 48.6, 0, 1.0), ("mundbreite", "ziel", 47.1, 0, 1.5)]
            var = {}
            for f, w in reife_alt["w"].items():
                var[f] = V(f, w, None if f in KIPPEN else 0.0, max(1.2, w))
            for f in ("eigen_unterwange", "eigen_jochbein", "eigen_kieferkante"):
                var[f] = V(f, 0.0, 0.0, 1.0)
            # Kopf schmaler nur zusammen mit Augen nach außen (Pupillenabstand bleibt)
            g0 = addiere(grund, reife_alt["w"])
            m0 = messer.masse(g0)
            d1 = messer.masse(addiere(g0, {"probe_head_scale_horiz_decr": 1.0}))["ipd"] - m0["ipd"]
            d2 = messer.masse(addiere(g0, {"probe_eye_trans_out": 1.0}))["ipd"] - m0["ipd"]
            var["kopf_schmal"] = ({"probe_head_scale_horiz_decr": 1.0, "probe_eye_trans_out": -d1 / d2}, {}, 0.0, 0.0, 1.0)
            bericht["kopf_schmal_w"] = {"probe_eye_trans_out_je_1": round(-d1 / d2, 4)}
            for f in ("probe_cheek_bones_decr", "probe_cheek_inner_decr", "probe_mouth_trans_down",
                      "probe_eyebrows_trans_down", "probe_nose_greek_decr", "probe_eye_trans_out",
                      "probe_head_invertedtriangular", "probe_chin_jaw_drop_decr", "probe_head_square",
                      "probe_chin_bones_incr"):
                if f not in var and not any(gegenform(f) == k for k in var):
                    var[f] = V(f)
            var["gen_augen_tiefe_plus"] = ({"gen_augen_tiefe_plus": 1.0}, {"gen_augen_tiefe_minus": 1.0}, 0.0, -1.0, 1.0)
            var["probe_neck_double_decr"] = V("probe_neck_double_decr", 0.0, 0.0, 1.0)
            fest = {"eigen_lidfalte_w": 1.0, "eigen_kopfnicker": 0.5}
        prot = []
        c, m, w_end, gewaehlt = ausgleich(messer, grund, fest, var, ziele, protokoll=prot)
        # Rezept: Probeformen der Lösung, je Variable oben oder unten (gekoppelte Paare bleiben zusammen)
        ober, unter = {}, {}
        for n, (formen_, ci) in gewaehlt.items():
            ziel_ = ober if ist_oben(n) else unter
            for f, wf in formen_.items():
                if abs(ci * wf) < 1e-4:
                    continue
                ziel_[f] = round(ziel_.get(f, 0.0) + abs(ci) * wf, 4)
        for f, wf in fest.items():
            (ober if ist_oben(f) else unter)[f] = round(wf, 4)
        rezept[f"alter_reife_{g}_oben"] = ober
        rezept[f"alter_reife_{g}_unten"] = unter
        ergebnisse[g] = {"masse": m, "vorher": vorher, "c": c, "protokoll": prot,
                         "ziele": [list(z) for z in ziele]}
        print(g, "fertig:", {k: round(m[k], 3) for k in ("ipd", "augapfel_ipd", "zy", "n_me", "gesichtsindex", "kinnmitte_zy", "mund_zy",
                                                      "mundlage", "braue_pupille", "wangenwoelbung", "augentiefe", "nasofrontalwinkel",
                                                      "pg_hinter_li", "kinn_hals_winkel", "unterkiefer")})
        print("  vorher", {k: round(vorher[k], 3) for k in ("augapfel_ipd", "n_sn", "mundbreite", "nasenbreite")}, "jetzt n_sn/mund",
              round(m["n_sn"], 1), round(m["mundbreite"], 1), "Kosten", [round(p["kosten"], 1) for p in prot])
        print("  c", {k: round(v, 2) for k, v in c.items() if abs(v) > 0.01})
    bericht["ergebnisse"] = ergebnisse
    json.dump(rezept, open(os.path.join(aus, "rezept_s4.json"), "w", encoding="utf-8"), indent=1, ensure_ascii=False)
    json.dump(bericht, open(os.path.join(aus, "ausgleich_s4.json"), "w", encoding="utf-8"), indent=1, ensure_ascii=False,
              default=float)


if __name__ == "__main__":
    main()
