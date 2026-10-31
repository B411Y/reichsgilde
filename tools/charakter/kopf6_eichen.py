"""Eichung der Gene am Probebau (Charakter Phase 6, S5–S7; eigener Code, MIT).

Baut aus dem Probebau (kopf6.sh proben) und der Lösung der Ausgleichsrechnung (formen_ausgleich.py, S4) das Rezept
des fertigen Kopfs (kopf6_rezept.json, für formen_mischen.py) und die Regeldaten der Darstellung (regeln6.json):

S5  Gestalt-Gene neu zusammengesetzt (Plan S5, Tabelle) und geeicht: Stärke = Formgewicht je Streuung des Gens, je
    Geschlecht und Richtung so, dass +1σ des Gens das zugehörige Maß um 0,75 × natürliche Streuung bewegt (NIOSH je
    Geschlecht; ohne NIOSH-Wert ICT-Scans × 0,7; Recherche „Vielfalt“ Abschnitt 4). Frauen: brauenwulst,
    kiefer_*, kinn × 0,65, nase_hoecker × 0,8, lippen × 1,1 gegen den Mann (statt des pauschalen 0,904).
S6  Gesichtszüge aus den ICT-Identitäten (bereinigt um Geschlecht und Herkunft, eingefroren als zuege_basis_v1) und
    Asymmetrie aus MakeHuman-asym-Zielen ((links − rechts)/2) und den antisymmetrischen ICT-Komponenten.
S7  Formen der neuen Gestalt-Gene aus dem Vorschlag (brauen_stand, lidspalte, schlupflid, nasenspitze, mundwinkel,
    kinn_breite), geeicht wie S5.

Gemessen wird am Mensch-Mittel mit 35 Jahren (Grundgewichte aus dem Gewichteausdruck, Gen-Gewichte null, Reife und
Mitte aus dem S4-Rezept). Die Gewichte, mit denen Godot den Kopf zeigt, rechnet charakter_kopf.gd aus regeln6.json;
die 1000er-Prüfung misst den Gewichteausdruck von Godot, nicht diese Rechnung.

Aufruf: $BPY -I tools/charakter/kopf6_eichen.py --glb=<glb6p/kopf6p.glb> --gewichte=<gewichte.json> --aus=<ordner>
"""
import hashlib
import json
import os
import sys

import numpy as np

HIER = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HIER)
from formen_ausgleich import Messer, addiere, skaliert, PROBEN  # noqa: E402

ERBGUT = os.path.join(HIER, "..", "..", "game", "daten", "erbgut.json")


# ------------------------------------------------------------------ Zusatzmaße (mm, Godot-Achsen: y oben, z vorn)
class Zusatz:
    """Zusatzmaße für die Eichung. Maße an „vordersten“ oder „seitlichsten“ Ecken springen, wenn eine Form die Lage des
    Extrems verschiebt; deshalb messen Wangenknochen, Wangenfülle, Oberlid und Kinnbreite den mittleren Versatz fester
    Ecken (einmal am Grundgesicht des Geschlechts gewählt), Lippenrot die ICT-Merkpunkte 51/57 (außen) und 62/66 (innen)."""

    def __init__(self, messer, basis_w):
        self.messer = messer
        nm = messer.nm
        P = nm.netz.punkte(basis_w)
        H = P["_haut"]
        self.H0 = H
        N = nm.netz.haut.flaechennormalen(H / 1000.0)
        self.N0 = N
        ps_y = 0.5 * (P["ps_r"][1] + P["ps_l"][1])
        vorn = N[:, 2] > 0.2

        def satz(bed):
            return np.nonzero(bed & vorn)[0]
        ax = np.abs(H[:, 0])
        self.idx = {
            "jochbein": [satz((H[:, 0] * s > 40) & (H[:, 0] * s < 52) & (np.abs(H[:, 1] - (ps_y - 22)) < 5)) for s in (-1, 1)],
            "wange": satz((ax > 34) & (ax < 48) & (H[:, 1] < ps_y - 30) & (H[:, 1] > P["sto"][1] + 5)),
            "oberlid": np.concatenate([satz((np.abs(H[:, 0] - 0.5 * (P[a][0] + P[b][0])) < 4)
                                            & (H[:, 1] > 0.5 * (P[a][1] + P[b][1]) + 3) & (H[:, 1] < 0.5 * (P[a][1] + P[b][1]) + 8)
                                            & (H[:, 2] > 0.5 * (P[a][2] + P[b][2]) - 4)) for a, b in (("lm37", "lm38"), ("lm43", "lm44"))]),
            "kinn": [satz((H[:, 0] * s > 11) & (H[:, 0] * s < 19) & (np.abs(H[:, 1] - P["pg"][1]) < 4) & (H[:, 2] > P["pg"][2] - 12))
                     for s in (-1, 1)],
        }
        self.anzahl = {k: (len(v) if not isinstance(v, list) else [len(x) for x in v]) for k, v in self.idx.items()}

    def __call__(self, w):
        nm = self.messer.nm
        P = nm.netz.punkte(w)
        H = P["_haut"]
        m = nm.messen(w, punkte=P, schnell=True)
        m["lippenrot"] = float((P["lm51"][1] - P["lm57"][1]) - (P["lm62"][1] - P["lm66"][1]))
        m["kinnvorsprung"] = float(P["pg"][2] - P["sm"][2])
        m["pg_z"] = float(P["pg"][2])
        ys, zf = nm.netz._profil
        n, prn = P["n"], P["prn"]
        sel = (ys < n[1] - 3) & (ys > prn[1] + 3) & np.isfinite(zf)
        t = (ys[sel] - n[1]) / (prn[1] - n[1])
        m["hoecker"] = float((zf[sel] - (n[2] + t * (prn[2] - n[2]))).max()) if sel.any() else float("nan")
        m["lidspalte_breite"] = float(0.5 * (np.linalg.norm(P["ex_r"] - P["en_r"]) + np.linalg.norm(P["ex_l"] - P["en_l"])))
        m["lidspalte_hoehe"] = float(0.5 * ((P["lm37"][1] + P["lm38"][1]) / 2 - (P["lm40"][1] + P["lm41"][1]) / 2
                                            + (P["lm43"][1] + P["lm44"][1]) / 2 - (P["lm46"][1] + P["lm47"][1]) / 2))
        m["aussenwinkel"] = float(0.5 * ((P["ex_r"][1] - P["en_r"][1]) + (P["ex_l"][1] - P["en_l"][1])))
        bv = []
        for s in ("r", "l"):
            ps = P[f"ps_{s}"]
            q = H[(np.abs(H[:, 0] - ps[0]) < 3) & (H[:, 1] > ps[1] + 6) & (H[:, 1] < ps[1] + 22)]
            bv.append(q[:, 2].max() - P[f"hs_{s}"][2])
        m["braue_vor_lid"] = float(np.mean(bv))
        d = H - self.H0
        r, l = self.idx["jochbein"]
        m["jochbein_breite"] = float(d[l, 0].mean() - d[r, 0].mean())
        m["wange_vor"] = float((d[self.idx["wange"]] * self.N0[self.idx["wange"]]).sum(1).mean())
        m["oberlid_senkung"] = float(-d[self.idx["oberlid"], 1].mean() + 0.5 * d[self.idx["oberlid"], 2].mean())
        r, l = self.idx["kinn"]
        m["kinn_breite"] = float(d[l, 0].mean() - d[r, 0].mean())
        m["nasenspitze"] = float(P["prn"][1] - P["sn"][1])
        m["mundwinkel"] = float(0.5 * (P["ch_r"][1] + P["ch_l"][1]) - P["sto"][1])
        m["n_sn"] = m["n_me_y"] - m["sn_me"]
        return m


# ------------------------------------------------------------------ Gene: Zusammensetzung und Maß
def P_(name):
    assert name[6:] in PROBEN, name
    return name


GENE = {
    # Gen: (plus-Form, minus-Form, Maß, Ziel +1σ Mann (mm), Ziel Frau (mm) oder Faktor, Reifeteil)
    "gesicht_laenge": ({"probe_chin_height_incr": 0.5, "probe_nose_scale_vert_incr": 0.25, "probe_head_scale_vert_incr": 0.25},
                       {"probe_chin_height_decr": 0.5, "probe_nose_scale_vert_decr": 0.25, "probe_head_scale_vert_decr": 0.25},
                       "se_me", ("niosh", 6.9, 5.8), None),
    "gesicht_breite": ({"probe_head_scale_horiz_incr": 0.5, "probe_cheek_bones_incr": 0.4},
                       {"probe_head_scale_horiz_decr": 0.5, "probe_cheek_bones_decr": 0.4}, "zy", ("niosh", 6.7, 6.2), None),
    "kiefer_breite": ({"eigen_kieferwinkel": 1.0, "probe_head_square": 0.3, "probe_chin_width_incr": 0.3},
                      {"eigen_kieferwinkel": -1.0, "probe_head_invertedtriangular": 0.3, "probe_chin_width_decr": 0.3}, "unterkiefer",
                      ("fest", 4.0, 0.65), "unten"),
    "kiefer_vorstand": ({"probe_chin_prognathism_incr": 1.0}, {"probe_chin_prognathism_decr": 1.0}, "pg_z", ("fest", 1.7, 0.65), "unten"),
    "kinn": ({"probe_chin_prominent_incr": 1.0}, {"probe_chin_prominent_decr": 1.0}, "kinnvorsprung", ("ict", 4.7, 0.65), "unten"),
    "nase_laenge": ({"probe_nose_scale_vert_incr": 1.2, "probe_nose_point_down": 0.3},
                    {"probe_nose_scale_vert_decr": 1.2, "probe_nose_point_up": 0.3}, "n_sn", ("niosh", 4.1, 3.7), None),
    "nase_breite": ({"probe_nose_scale_horiz_incr": 0.6, "probe_nose_flaring_incr": 0.5, "probe_nose_nostrils_width_incr": 0.3},
                    {"probe_nose_scale_horiz_decr": 0.6, "probe_nose_flaring_decr": 0.5, "probe_nose_nostrils_width_decr": 0.3},
                    "nasenbreite", ("niosh", 3.2, 2.9), None),
    "nase_hoecker": ({"eigen_hoecker": 2.0, "probe_nose_hump_incr": 0.3, "probe_nose_curve_convex": 0.3},
                     {"eigen_hoecker": -0.6, "probe_nose_curve_concave": 1.0, "probe_nose_hump_decr": 0.5},
                     "hoecker", ("fest", 1.3, 0.8), "oben"),
    "mund_breite": ({"probe_mouth_scale_horiz_incr": 1.0}, {"probe_mouth_scale_horiz_decr": 1.0}, "mundbreite", ("niosh", 4.0, 3.5), None),
    "lippen": ({"probe_mouth_upperlip_height_incr": 0.6, "probe_mouth_upperlip_volume_incr": 0.6,
                "probe_mouth_lowerlip_height_incr": 0.6, "probe_mouth_lowerlip_volume_incr": 0.6},
               {"probe_mouth_upperlip_height_decr": 0.6, "probe_mouth_upperlip_volume_decr": 0.6,
                "probe_mouth_lowerlip_height_decr": 0.6, "probe_mouth_lowerlip_volume_decr": 0.6}, "lippenrot", ("fest", 1.25, 1.1), None),
    "augen_abstand": ({"probe_eye_trans_out": 1.0}, {"probe_eye_trans_in": 1.0}, "ipd", ("niosh", 3.6, 3.4), None),
    "augen_groesse": ({"probe_eye_scale_incr": 1.0}, {"probe_eye_scale_decr": 1.0}, "lidspalte_breite", ("ict", 2.0, 1.0), None),
    "augen_neigung": ({"probe_eye_corner1_up": 1.0, "probe_eye_eyefold_angle_up": 0.3},
                      {"probe_eye_corner1_down": 1.0, "probe_eye_eyefold_angle_down": 0.3}, "aussenwinkel", ("fest", 0.6, 1.0), None),
    "augen_tiefe": ({"gen_augen_tiefe_plus": 1.0}, {"gen_augen_tiefe_minus": 1.0}, "augentiefe", ("fest", 1.3, 1.0), "oben"),
    "brauenwulst": ({"probe_eyebrows_trans_forward": 1.0}, {"probe_eyebrows_trans_backward": 1.0}, "braue_vor_lid", ("fest", 1.7, 0.65), "oben"),
    "wangenknochen": ({"probe_cheek_bones_incr": 1.0, "probe_cheek_inner_decr": 0.3}, {"probe_cheek_bones_decr": 1.0, "probe_cheek_inner_incr": 0.3},
                      "jochbein_breite", ("fest", 2.5, 1.0), None),
    "wangen_fuelle": ({"probe_cheek_volume_incr": 1.0, "probe_cheek_inner_incr": 0.5}, {"probe_cheek_volume_decr": 1.0, "probe_cheek_inner_decr": 0.5},
                      "wange_vor", ("fest", 1.7, 1.0), None),
    # S7: neue Gestalt-Gene (Vorschlag), Zielstreuung ICT × 0,7 bzw. festgelegt (siehe Bericht)
    "brauen_stand": ({"probe_eyebrows_trans_up": 1.0, "probe_eyebrows_angle_up": 0.3}, {"probe_eyebrows_trans_down": 1.0, "probe_eyebrows_angle_down": 0.3},
                     "braue_pupille", ("ict", 1.9, 1.0), None),
    "lidspalte": ({"probe_eye_height1_incr": 0.5, "probe_eye_height2_incr": 1.0, "probe_eye_height3_incr": 0.5},
                  {"probe_eye_height1_decr": 0.5, "probe_eye_height2_decr": 1.0, "probe_eye_height3_decr": 0.5}, "lidspalte_hoehe", ("ict", 1.2, 1.0), None),
    "schlupflid": ({"probe_eye_eyefold_down": 0.6, "probe_eye_eyefold_convex": 0.4}, {"probe_eye_eyefold_up": 0.6, "probe_eye_eyefold_concave": 0.4},
                   "oberlid_senkung", ("fest", 0.8, 1.0), None),
    "nasenspitze": ({"probe_nose_point_up": 1.0, "probe_nose_septumangle_decr": 0.5}, {"probe_nose_point_down": 1.0, "probe_nose_septumangle_incr": 0.5},
                    "nasenspitze", ("fest", 1.4, 1.0), None),
    "mundwinkel": ({"probe_mouth_angles_up": 1.0}, {"probe_mouth_angles_down": 1.0}, "mundwinkel", ("fest", 0.9, 1.0), None),
    "kinn_breite": ({"probe_chin_width_incr": 1.0, "probe_chin_triangle": -0.4}, {"probe_chin_width_decr": 1.0, "probe_chin_triangle": 0.4},
                    "kinn_breite", ("fest", 3.0, 0.8), "unten"),
}
# Gene, deren Formen aus Phase 5 bleiben, mit dem Faktor der Recherche gegen die alte Stärke (σ_Mensch / 127)
ALT_FAKTOR = {"ohren_groesse": 2.0, "stirn_hoehe": 1.0, "stirn_neigung": 1.0, "kopf": 1.0, "schultern": 1.0}
GESICHT_FETT = ({"probe_head_fat_incr": 0.5, "probe_cheek_volume_incr": 0.4, "probe_neck_double_incr": 0.4, "probe_neck_circ_incr": 0.3},
                {"probe_head_fat_decr": 0.6, "probe_cheek_volume_decr": 0.4, "probe_forehead_temple_decr": 0.4, "probe_cheek_inner_decr": 0.3})


STAERKE_MAX = 0.6          # Formgewicht je σ höchstens 0,6: bei 2σ 1,2 = Obergrenze des Budgets je MakeHuman-Ziel
# Populationskorrektur (ein Durchgang an der 1000er-Prüfung „nur Gene“, Startwerte 10001–11000): Faktor = 0,75 / gemessener
# Anteil der natürlichen Streuung, je Geschlecht (m, w). Am Mittelgesicht geeicht, streuen manche Maße in der Bevölkerung
# anders (Budget kürzt Lippen und Kinn, andere Gene wirken mit). Unterkiefer bleibt: dort wirken Kopf- und Gesichtsbreite
# mit (realistisch korreliert); er wird gegen ICT × 0,7 und gegen die Bigonialbreite mit Fett (σ ≈ 10 mm) berichtet.
POP_FAKTOR = {"lippen": (1.5, 1.55), "kinn": (1.2, 1.45), "gesicht_breite": (0.83, 1.25), "mund_breite": (0.78, 1.2),
              "nase_breite": (1.0, 0.77), "gesicht_laenge": (1.05, 1.15)}


def ziel_mm(spez, g):
    art, a, b = spez
    if art == "niosh":
        return 0.75 * (a if g == "m" else b)
    if art == "ict":
        return 0.75 * 0.7 * a * (1.0 if g == "m" else b)
    return a * (1.0 if g == "m" else b)


def main():
    opt = dict(a[2:].split("=", 1) for a in sys.argv[1:] if a.startswith("--") and "=" in a)
    aus = opt.get("aus", ".")
    os.makedirs(aus, exist_ok=True)
    messer = Messer(os.path.expanduser(opt["glb"]))
    gew = json.load(open(opt["gewichte"], encoding="utf-8"))
    pers = {p["name"]: p for p in gew["personen"]}
    rezept_s4 = json.load(open(os.path.join(HIER, "kopf6_rezept_s4.json"), encoding="utf-8"))
    erbgut = {g["kennung"]: g for g in json.load(open(ERBGUT, encoding="utf-8"))["gene"]}
    bericht = {"glb": opt["glb"]}
    regeln_gene = {}
    rezept = {k: dict(v) for k, v in rezept_s4.items()}
    # Lidfalte als eigene Form (der Kopf blendet sie beim Lidschluss aus), nicht in der Reifeform
    for g in "mw":
        if f"eigen_lidfalte_{g}" in rezept[f"alter_reife_{g}_oben"]:
            rezept[f"lidfalte_{g}"] = {f"eigen_lidfalte_{g}": rezept[f"alter_reife_{g}_oben"].pop(f"eigen_lidfalte_{g}")}

    def grund(g):
        w = dict(pers[f"mittel_{g}35"]["szene"])
        for k in list(w):
            if k.startswith(("gen_", "alter_")) or abs(w[k]) < 1e-9:
                w.pop(k)
        return addiere(w, rezept_s4[f"alter_reife_{g}_oben"], rezept_s4[f"alter_reife_{g}_unten"],
                       skaliert(rezept_s4[f"alter_mitte_{g}"], 0.55))
    basis = {g: grund(g) for g in "mw"}
    zus = {g: Zusatz(messer, basis[g]) for g in "mw"}
    bericht["feste_ecken"] = {g: zus[g].anzahl for g in "mw"}
    m0 = {g: zus[g](basis[g]) for g in "mw"}
    bericht["mittelgesicht"] = {g: {k: round(v, 3) for k, v in m0[g].items() if isinstance(v, float)} for g in "mw"}

    # ------------------------------------------------------------ S5/S7: Stärke je Gen, Geschlecht und Richtung
    for gen, (plus, minus, mass, spez, reife) in GENE.items():
        e = {"formen": {"plus": f"gen_{gen}_plus", "minus": f"gen_{gen}_minus"}, "mass": mass, "staerke": {}, "wirkung_mm_je_1": {}}
        rezept[f"gen_{gen}_plus"] = dict(plus)
        rezept[f"gen_{gen}_minus"] = dict(minus)
        for g in "mw":
            z = ziel_mm(spez, g)
            st = {}
            for richt, form in (("plus", plus), ("minus", minus)):
                if gen == "augen_tiefe":
                    d = 4.0 if richt == "plus" else -4.0           # eigene Form: Augenpartie 4 mm je 1 (kopf2_regler.json)
                else:
                    m1 = zus[g](addiere(basis[g], skaliert(form, 0.5)))
                    d = (m1[mass] - m0[g][mass]) / 0.5
                st[richt] = round(min(z / abs(d), STAERKE_MAX), 4) if abs(d) > 1e-6 else 0.0
                e.setdefault("erreicht_mm_je_sigma", {}).setdefault(g, {})[richt] = round(st[richt] * abs(d), 3)
                e["wirkung_mm_je_1"].setdefault(g, {})[richt] = round(d, 3)
            if gen in POP_FAKTOR:
                f = POP_FAKTOR[gen][0 if g == "m" else 1]
                st = {r: round(min(v * f, STAERKE_MAX), 4) for r, v in st.items()}
                e.setdefault("populationsfaktor", {})[g] = f
                e["erreicht_mm_je_sigma"][g] = {r: round(st[r] * abs(e["wirkung_mm_je_1"][g][r]), 3) for r in st}
            e["staerke"][g] = st
            e.setdefault("ziel_mm_je_sigma", {})[g] = round(z, 3)
        if reife:
            e["reife"] = reife
        regeln_gene[gen] = e
        print(gen, mass, "Ziel", e["ziel_mm_je_sigma"], "erreicht", e["erreicht_mm_je_sigma"], "Stärke", e["staerke"])
    # Gene mit Formen der Phase 5: alte Stärke × Faktor (alte Rechnung: Gewicht = (Wert − 128) / 127)
    for gen, f in ALT_FAKTOR.items():
        s = erbgut[gen]["voelker"]["Mensch"]["streuung"]
        st = round(f * s / 127.0, 4)
        regeln_gene[gen] = {"formen": {"plus": f"gen_{gen}_plus", "minus": f"gen_{gen}_minus"},
                            "staerke": {g: {"plus": st, "minus": st} for g in "mw"}, "mass": "–", "faktor_gegen_phase5": f}
    # Leibesfülle ins Gesicht: aus „gewicht“ (Abstand vom Mittel), Gewicht je σ 0,45
    rezept["gen_gesicht_fett_plus"], rezept["gen_gesicht_fett_minus"] = dict(GESICHT_FETT[0]), dict(GESICHT_FETT[1])
    regeln_gene["gesicht_fett"] = {"formen": {"plus": "gen_gesicht_fett_plus", "minus": "gen_gesicht_fett_minus"}, "aus_gen": "gewicht",
                                   "staerke": {g: {"plus": 0.45, "minus": 0.45} for g in "mw"}, "mass": "–"}
    # Absolute Gene (Wert / 255) und ohne Streuung
    for gen in ("ohren_spitze",):
        regeln_gene[gen] = {"formen": {"plus": f"gen_{gen}_plus"}, "absolut": True}

    bericht["gene"] = regeln_gene

    # ------------------------------------------------------------ S6: Gesichtszüge aus den ICT-Identitäten
    haut = messer.haut
    G0 = haut.grund * 1000
    P0 = messer.nm.netz.punkte(basis["m"])
    gesicht = (G0[:, 2] > P0["_z_ohr"]) & (G0[:, 1] > P0["me"][1] - 5)

    def feld(rez):
        f = np.zeros_like(G0)
        for name, w in rez.items():
            d = haut.versatz(name)
            if d is not None:
                f += float(w) * d * 1000
        return f
    achse_g = addiere({"makro_w_jung": 1.0}, rezept_s4["alter_reife_w_oben"], rezept_s4["alter_reife_w_unten"],
                      skaliert(rezept_s4["alter_reife_m_oben"], -1), skaliert(rezept_s4["alter_reife_m_unten"], -1))
    achsen = {"geschlecht": achse_g, "african_m": {"herkunft_african_m": 1.0}, "asian_m": {"herkunft_asian_m": 1.0},
              "african_w": {"herkunft_african_w": 1.0}, "asian_w": {"herkunft_asian_w": 1.0}}
    A = np.stack([feld(r)[gesicht].ravel() for r in achsen.values()], axis=1)
    kandidaten = [k for k in range(16) if k != 9]
    grund_durch = {}
    for g in "mw":
        mm_ = messer.nm.messen(basis[g])
        grund_durch[g] = (mm_["lid_augapfel"], messer.nm.lippe_zahn_p5(basis[g]))
    bericht["grund_lid_lippe_mm"] = grund_durch
    zuege, zug_bericht = [], {}
    for k in kandidaten:
        name = f"ictid_{k:03d}"
        v = feld({name: 1.0})[gesicht].ravel()
        cos_vor = float(v @ A[:, 0] / (np.linalg.norm(v) * np.linalg.norm(A[:, 0])))
        coef, *_ = np.linalg.lstsq(A, v, rcond=None)
        v2 = v - A @ coef
        cos_nach = [float(v2 @ A[:, j] / (np.linalg.norm(v2) * np.linalg.norm(A[:, j]) + 1e-12)) for j in range(A.shape[1])]
        rms = float(np.sqrt((v2.reshape(-1, 3) ** 2).sum(1).mean()))
        rez = {name: 1.0}
        for (an, ar), c in zip(achsen.items(), coef):
            rez = addiere(rez, skaliert(ar, -float(c)))
        e = {"cos_geschlecht_vorher": round(cos_vor, 3), "cos_nachher_max": round(max(abs(c) for c in cos_nach), 4),
             "rms_mm_je_sigma": round(rms, 3), "koeffizienten": {an: round(float(c), 5) for an, c in zip(achsen, coef)}}
        if abs(cos_vor) > 0.3:
            e["entfaellt"] = "Vorfilter |cos| > 0,3 zur Geschlechtsachse"
        else:
            # ±2σ (Gewicht ±1,4): sichtbar und ohne Durchdringung (Lid–Augapfel, Lippe–Zahn ≥ 0,3 mm), Mann und Frau
            # Das Lidmaß ist schon am Grundgesicht negativ (Phase 5: Mann −1,45, Frau −0,86 mm; es erfasst Ecken der Lidhöhle):
            # gewertet wird die Verschlechterung gegen das Grundgesicht (höchstens 0,2 mm)
            durch = []
            for g in "mw":
                for vz in (1.4, -1.4):
                    w_ = addiere(basis[g], skaliert(rez, vz))
                    mm_ = messer.nm.messen(w_)
                    lz = messer.nm.lippe_zahn_p5(w_)
                    durch.append(min(mm_["lid_augapfel"] - grund_durch[g][0], lz - grund_durch[g][1] if lz < 0.5 else 0.0))
            e["durchdringung_verschlechterung_mm"] = round(float(min(durch)), 3)
            if min(durch) < -0.6:
                e["entfaellt"] = "Durchdringung bei ±2σ (Lid oder Lippe–Zahn mehr als 0,6 mm schlechter als das Grundgesicht; beide Maße rauschen um 0,2–0,5 mm)"
            elif rms * 1.4 < 0.4:
                e["entfaellt"] = "bei ±2σ kaum sichtbar (RMS < 0,4 mm)"
            else:
                zuege.append((rms, name, rez))
        zug_bericht[name] = e
        print("Zug", name, json.dumps(e, ensure_ascii=False)[:220])
    zuege.sort(key=lambda t: -t[0])
    gewaehlt = zuege[:12]
    # Eingefrorene Basis (Plan S6.3): gibt es zuege_basis_v1.json, gelten deren Rezepte; neu nur mit --zuege_neu=1 (als v2)
    BASIS_DATEI = os.path.join(HIER, "zuege_basis_v1.json")
    if os.path.exists(BASIS_DATEI) and opt.get("zuege_neu") != "1":
        alt_basis = json.load(open(BASIS_DATEI, encoding="utf-8"))
        gewaehlt = [(0.0, z["aus"], z["rezept"]) for zn, z in sorted(alt_basis["zuege"].items())]
        bericht["zuege_eingefroren"] = True
    basis_zuege = {"version": 1, "quelle": "ICT-FaceKit identity000–015 (ohne 009), bereinigt um Geschlechtsachse (Frau 25 − Mann 25 aus "
                   "Makro und Reife) und Herkunftsachsen (MakeHuman african/asian, Mann und Frau), Skalarprodukt über die Gesichtsecken",
                   "zuege": {}}
    for i, (rms, name, rez) in enumerate(sorted(gewaehlt, key=lambda t: t[1])):
        zn = f"zug_{i + 1:02d}"
        rezept[zn] = {k: round(v, 6) for k, v in rez.items() if abs(v) > 1e-7}
        basis_zuege["zuege"][zn] = {"aus": name, "rezept": rezept[zn]}
    roh = json.dumps(basis_zuege, sort_keys=True, ensure_ascii=False).encode("utf-8")
    basis_zuege_sha = hashlib.sha256(roh).hexdigest()
    json.dump(basis_zuege, open(os.path.join(HIER, "zuege_basis_v1.json"), "w", encoding="utf-8"), indent=1, ensure_ascii=False, sort_keys=True)
    np.savez_compressed(os.path.join(aus, "zuege_basis_v1.npz"), **{zn: feld(z["rezept"]).astype(np.float32)
                                                                  for zn, z in basis_zuege["zuege"].items()})
    bericht["zuege"] = {"kandidaten": zug_bericht, "gewaehlt": list(basis_zuege["zuege"]), "sha256": basis_zuege_sha}

    # ------------------------------------------------------------ S6: Asymmetrie (nicht erblich, aus dem Personen-Samen)
    ohr_oben = {}
    for s_, sg in (("r", -1), ("l", 1)):
        sel = np.nonzero((G0[:, 0] * sg > 55) & (np.abs(G0[:, 2] - P0[f"t_{s_}"][2]) < 18) & (G0[:, 1] > P0["ps_r"][1] - 25))[0]
        ohr_oben[s_] = sel[np.argsort(-G0[sel, 1])[:12]]

    kinn_ecken = np.argsort(np.linalg.norm(G0 - P0["pg"], axis=1))[:12]
    spitze_ecken = np.argsort(np.linalg.norm(G0 - P0["prn"], axis=1))[:12]

    def asym_masse(w):
        P = messer.nm.netz.punkte(w)
        H = P["_haut"]
        return np.array([H[kinn_ecken, 0].mean(), H[spitze_ecken, 0].mean(), P["ps_l"][1] - P["ps_r"][1], P["br_l"][1] - P["br_r"][1],
                         P["ch_l"][1] - P["ch_r"][1], H[ohr_oben["l"], 1].mean() - H[ohr_oben["r"], 1].mean()])
    ASYM_NAMEN = ["kinn_seitlich", "nasenspitze_seitlich", "augenhoehe", "brauen", "mundwinkel", "ohrhoehe"]
    GRENZE = np.array([2.0, 1.5, 1.0, 2.0, 1.5, 3.0])
    a0 = asym_masse(basis["m"])
    asym_kand = {}
    for z in ["brown1", "brown2"] + [f"eye{k}" for k in range(1, 9)] + [f"nose{k}" for k in range(1, 5)] + ["mouth1", "mouth2"] \
            + [f"jaw{k}" for k in range(1, 4)] + [f"ear{k}" for k in range(1, 5)] + ["cheek1", "cheek2"]:
        rez = {f"probe_asym_{z}_l": 0.5, f"probe_asym_{z}_r": -0.5}
        asym_kand[f"mh_{z}"] = rez
    for k in (9, 20, 25, 28, 33):
        asym_kand[f"ict_{k:03d}"] = {f"ictid_{k:03d}": 1.0}
    wirkung = {}
    for n, rez in asym_kand.items():
        wirkung[n] = asym_masse(addiere(basis["m"], rez)) - a0
        print("  kand", n, np.round(wirkung[n], 2).tolist())
    # je Merkmal die stärkste MakeHuman-Form, dazu alle ICT-Komponenten
    wahl = []
    for j, mn in enumerate(ASYM_NAMEN):
        best = max((n for n in asym_kand if n.startswith("mh_")), key=lambda n: abs(wirkung[n][j]) / (np.abs(wirkung[n]).sum() + 1e-9) * min(abs(wirkung[n][j]), 2.0))
        if best not in wahl:
            wahl.append(best)
    wahl = [n for n in wahl if np.abs(wirkung[n]).max() >= 0.3]       # wirkungslose MakeHuman-Formen (z. B. asym-ear) weg
    wahl += [n for n in asym_kand if n.startswith("ict_")]
    M = np.array([wirkung[n] for n in wahl])          # Form × Merkmal (mm je Gewicht 1)
    # Streuung je Form: jede Form allein trägt im Mittel gleich viel zum Spiegelabstand bei; gesamt Ziel-RMS 0,75 mm
    def spiegel(w):
        P = messer.nm.netz.punkte(w)
        return messer.nm._spiegel(P["_haut"], P["_z_ohr"])
    rms_einzel = np.array([spiegel(addiere(basis["m"], asym_kand[n])) for n in wahl])
    for n, r_ in zip(wahl, rms_einzel):
        print("  asym", n, "RMS je 1:", round(float(r_), 3), "Merkmale:", np.round(wirkung[n], 2).tolist())
    rng = np.random.default_rng(6006)
    def probe(skala, anzahl=60):
        werte, ueber = [], 0
        for _ in range(anzahl):
            w = rng.normal(size=len(wahl)) * skala
            d = w @ M
            f = min(1.0, float(np.min(GRENZE / np.maximum(np.abs(d), 1e-9))))
            ueber += f < 1.0
            ges = addiere(basis["m"], *[skaliert(asym_kand[n], wi * f) for n, wi in zip(wahl, w)])
            try:
                werte.append(spiegel(ges))
            except ValueError:
                werte.append(9.9)          # Merkpunkte nicht messbar (zu starke Verformung)
        return float(np.median(werte)), float(np.percentile(werte, 10)), float(np.percentile(werte, 90)), ueber / anzahl
    grund_skala = 1.0 / np.maximum(rms_einzel, 1e-3) / np.sqrt(len(wahl))
    lo, hi = 0.05, 1.5
    for _ in range(9):
        mitte = 0.5 * (lo + hi)
        med = probe(grund_skala * mitte, 30)[0]
        lo, hi = (mitte, hi) if med < 0.75 else (lo, mitte)
    skala = grund_skala * 0.5 * (lo + hi)
    med, p10, p90, anteil = probe(skala, 120)
    asym_regeln = {"formen": {}, "merkmale": ASYM_NAMEN, "grenzen_mm": GRENZE.tolist(), "ziel_rms_mm": [0.5, 1.0],
                   "probe_120": {"spiegel_rms_median": round(med, 3), "p10": round(p10, 3), "p90": round(p90, 3),
                                 "anteil_verkleinert": round(anteil, 3)}}
    for i, n in enumerate(wahl):
        an = f"asym_{i + 1:02d}"
        rezept[an] = asym_kand[n]
        asym_regeln["formen"][an] = {"aus": n, "streuung": round(float(skala[i]), 4),
                                     "merkmale_mm_je_1": [round(float(x), 4) for x in M[i]], "spiegel_rms_je_1": round(float(rms_einzel[i]), 4)}
    bericht["asymmetrie"] = asym_regeln
    print("Asymmetrie:", json.dumps(asym_regeln["probe_120"]), [asym_regeln["formen"][a]["aus"] for a in asym_regeln["formen"]])

    # ------------------------------------------------------------ Zielmatrix (Form -> MakeHuman-Ziel) für das Budget je Ziel
    zielmatrix = {}
    for fn, rez in rezept.items():
        if not fn.startswith(("gen_", "alter_")):
            continue
        z = {}
        for probe, w in rez.items():
            if probe.startswith("probe_") and probe[6:] in PROBEN:
                for ziel, zw in PROBEN[probe[6:]].items():
                    if "/r-" in ziel:          # l-/r-Paare zählen als ein Ziel (gleiches Gewicht)
                        continue
                    k = ziel.replace("/l-", "/")
                    z[k] = round(z.get(k, 0.0) + float(w) * float(zw), 5)
        if z:
            zielmatrix[fn] = z
    regeln = {
        "version": 1,
        "beschreibung": "Charakter Phase 6 (Etappe B): Regeldaten der Darstellung für Menschen – Genrechnung mit Nullpunkt Mensch-Mittel "
                        "(Gewicht = (Wert − Mittel) / Streuung × Stärke je Geschlecht und Richtung × Reife), Karikaturbremse, Budget je "
                        "MakeHuman-Ziel, Alterskurven, Gesichtszüge und Asymmetrie. Erzeugt von tools/charakter/kopf6_eichen.py.",
        "schalter": {"neue_rechnung_voelker": ["Mensch"], "karikaturbremse": True, "budget": True, "zuege": True, "asymmetrie": True},
        # Reife bis 24/25 Jahre (Mann unten zuletzt), Mitte ab 23: 20- und 35-Jährige sollen sich sicher unterscheiden (Plan 5.5)
        "alterskurven": {"reife_oben_m": [[11, 0.0], [15, 0.3], [19, 0.7], [24, 1.0]], "reife_unten_m": [[12, 0.0], [16, 0.25], [20, 0.55], [25, 1.0]],
                         "reife_oben_w": [[10, 0.0], [14, 0.35], [18, 0.7], [24, 1.0]], "reife_unten_w": [[10, 0.0], [14, 0.3], [18, 0.65], [24, 1.0]],
                         "mitte": [[23, 0.0], [35, 0.55], [45, 1.0]], "greis": [[45, 0.0], [55, 0.25], [68, 0.65], [78, 1.0], [90, 1.15]]},
        "lid_ruhe": {"reife": 0.1, "greis": 0.06, "lidspalte_je_sigma": -0.025},
        "karikatur": {"K_max": 1.35, "K_min": 0.75, "z_grenze": 2.0, "hoechstens_ueber": 2, "z_anheben": 1.2, "anheben_anzahl": 2},
        "budget": {"unten": -1.0, "oben": 1.2},
        "gene": regeln_gene,
        "zuege": {"gene": {zn: zn for zn in basis_zuege["zuege"]}, "faktor": 0.7, "mitte": 128, "streuung": 32,
                  "basis": "tools/charakter/zuege_basis_v1.json", "basis_sha256": basis_zuege_sha},
        "asymmetrie": asym_regeln,
        "zielmatrix": zielmatrix,
    }
    json.dump(regeln, open(os.path.join(HIER, "..", "..", "game", "versuch", "charakter", "regeln6.json"), "w", encoding="utf-8"),
              indent=1, ensure_ascii=False)
    rezept_ganz = {"_beschreibung": "Rezept des fertigen Kopfs kopf6 (Etappe B): Formen als Mischung der Probeformen aus kopf6.sh proben "
                                    "(formen_mischen.py). Erzeugt von kopf6_eichen.py aus kopf6_rezept_s4.json (S4) und der Eichung (S5–S7).",
                   "formen": rezept,
                   "entfernen": ["probe_", "ictid_", "herkunft_", "eigen_", "alter_reife_m", "alter_reife_w"]}
    json.dump(rezept_ganz, open(os.path.join(HIER, "kopf6_rezept.json"), "w", encoding="utf-8"), indent=1, ensure_ascii=False)
    json.dump(rezept, open(os.path.join(aus, "rezept_s5.json"), "w", encoding="utf-8"), indent=1, ensure_ascii=False)
    json.dump(regeln_gene, open(os.path.join(aus, "regeln_gene_s5.json"), "w", encoding="utf-8"), indent=1, ensure_ascii=False)
    json.dump(bericht, open(os.path.join(aus, "eichung_s5.json"), "w", encoding="utf-8"), indent=1, ensure_ascii=False, default=float)


if __name__ == "__main__":
    main()
