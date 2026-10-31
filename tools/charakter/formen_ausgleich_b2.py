"""Ausgleichsrechnung „Halt B2“: erwachsene Grundgesichter mit Volumen statt Abmagerung (Charakter Phase 6; eigener Code, MIT).

Anlass (Diagnose 31.10.2026): Die Basis (ICT-Scanmittel) ist normal; „abgemagert“ wirkten die Köpfe durch die eigenen
S4-Ziele von formen_ausgleich.py – Wangenwölbung ≤ 0 (Scanmittel +7 mm), Unterwange 15–23 mm hinter dem Scanmittel,
Lippen platt, Augen klein (Lidspalte der Frau −5 mm), Glabella und Nase +6…8 mm vor. B2 rechnet mit denselben Bausteinen
(Probebau kopf6p.glb, Gauß-Newton aus formen_ausgleich.py), aber mit Volumenzielen und Deckeln:

  Wangenwölbung +3…+6 mm · Unterwange x30/x40/x50 höchstens 3 mm hinter dem Makro-Grund · Lippen vor der Linie Sn–Pg
  (Mann +2…+4 / +1,5…+3,5, Frau +2,5…+4,5 / +2…+4 mm) · Augentiefe höchstens wenig über Phase 5 · Lidspalte (Breite,
  Höhe) höchstens 0,6 mm unter dem Makro-Grund · Profilwinkel g–sn–pg 166–171° · Glabella und Nasenspitze nur mäßig vor.
  Deckel: cheek_volume_decr ≤ 0,3 (+ Mitte 0,2 × 0,55), head_fat_decr ≤ 0,3, neck_double_decr ≤ 0,3,
  eyebrows/nose_trans_forward ≤ 0,3, eye_scale_decr ≤ 0,15; Frau chin_width_decr ≤ 0,6, chin_jaw_drop_decr ≤ 0,5,
  head_invertedtriangular ≤ 0,1; eigen_unterwange (Höhlung) entfällt. Breite über Knochen (Jochbein, Kinn, Kieferwinkel).

„Scanmittel“ ist hier der Makro-Grund je Geschlecht (ICT-Scanmittel mit den MakeHuman-Makros für Mann bzw. Frau, 35):
beim Mann fast gleich dem nackten Scanmittel (≤ 0,6 mm), bei der Frau ist das ganze Gesicht 4–7 mm flacher.

Profil- und Schrägmaße (Abnahme ab B2) stehen für Scanmittel, Makro-Grund, Halt B und B2 im Bericht (profil_masse).

Aufruf: $BPY -I tools/charakter/formen_ausgleich_b2.py --glb=<glb6p/kopf6p.glb> --gewichte=<gewichte.json> --aus=<ordner>
        [--rezept_haltb=<kopf6_rezept_s4.json>]   (Vergleich Halt B; Standard tools/charakter/kopf6_rezept_s4.json)
Ergebnis: <aus>/rezept_s4_b2.json (gleicher Aufbau wie kopf6_rezept_s4.json) und <aus>/ausgleich_b2.json.
"""
import json
import os
import sys

import numpy as np

HIER = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HIER)
from formen_ausgleich import Messer, addiere, skaliert, zerlegen, gegenform, ist_oben, ausgleich  # noqa: E402
from messen.netz_masse import _winkel  # noqa: E402


def _abstand_linie(p, a, b):
    """Abstand von p zur Linie a–b in der Profilebene (y, z); + = vor der Linie."""
    a2, b2, p2 = a[[1, 2]], b[[1, 2]], p[[1, 2]]
    d = b2 - a2
    n = np.array([d[1], -d[0]]) / np.linalg.norm(d)
    if n[1] < 0:
        n = -n
    return float(np.dot(p2 - a2, n))


def vorderste_z(H, dreiecke, x0, y0):
    """Tiefe z der vordersten Hautfläche über dem Punkt (x0, y0) der Frontansicht: Schnitt der Geraden parallel zu z mit
    allen Dreiecken (baryzentrisch in x, y), davon das größte z. Stetig in den Formgewichten, anders als die vorderste
    Ecke in einem Suchfenster (das Netz ist an der Wange gröber als 3 mm; das Fenster war dort manchmal leer)."""
    A, B, C = H[dreiecke[:, 0]], H[dreiecke[:, 1]], H[dreiecke[:, 2]]
    v0, v1 = B[:, :2] - A[:, :2], C[:, :2] - A[:, :2]
    v2 = np.array([x0, y0]) - A[:, :2]
    den = v0[:, 0] * v1[:, 1] - v1[:, 0] * v0[:, 1]
    ok = np.abs(den) > 1e-9
    den = np.where(ok, den, 1.0)
    u = (v2[:, 0] * v1[:, 1] - v1[:, 0] * v2[:, 1]) / den
    v = (v0[:, 0] * v2[:, 1] - v2[:, 0] * v0[:, 1]) / den
    innen = ok & (u >= 0) & (v >= 0) & (u + v <= 1)
    if not innen.any():
        return float("nan")
    z = A[:, 2] + u * (B[:, 2] - A[:, 2]) + v * (C[:, 2] - A[:, 2])
    return float(z[innen].max())


_BEZUG = {}


def bezug_z(nm, w, P):
    """Tiefenbezug: Tragion-Mitte des nackten Scanmittels, mitgeführt über die Augäpfel (in allen Formen fest). Die
    Tragion selbst ist als Bezug zu wacklig: Bei der B2-Frau lenkte die vollere Wange vor dem Ohr die Suche um 10,7 mm
    nach vorn ab, alle Tiefen schienen dann 12 mm kleiner."""
    augen = nm.glb.teile("Augen")[0]
    z_aug = float(augen.lage(w)[:, 2].mean() * 1000.0)
    if id(nm) not in _BEZUG:
        P0 = nm.netz.punkte({})
        _BEZUG[id(nm)] = float(augen.lage({})[:, 2].mean() * 1000.0) - float(0.5 * (P0["t_r"][2] + P0["t_l"][2]))
    return z_aug - _BEZUG[id(nm)]


def profil_masse(nm, w, P=None, bezug="tragion"):
    """Profil- und Schrägmaße (mm, Grad), Tiefen z ab der Mitte der Tragion-Punkte (Ohr), + = vorn:
    t_<p>     Tiefe von Glabella, Nasion, Nasenspitze, Subnasale, Ober-/Unterlippe, Pogonion, Menton
    uw30/40/50  vorderste Haut der Unterwange in Höhe der Mundwinkel bei |x| = 30/40/50 mm (Schrägansicht)
    mw25/35   vorderste Haut der Mittelwange auf halber Höhe Pupille–Subnasale bei |x| = 25/35 mm
    ul_snpg, ll_snpg  Ober-/Unterlippe vor der Linie Subnasale–Pogonion; ul_e, ll_e vor der E-Linie (Nasenspitze–Pogonion)
    konvex    Profilwinkel g–sn–pg (180 = gerade, kleiner = konvexer)
    lid_b, lid_h  Lidspalte: Breite en–ex (Raum) und Höhe (ICT-Lidpunkte 37/38–40/41), Mittel beider Augen
    schlaefe  Kopfbreite 10 mm über der Braue vor dem Ohr
    bezug="augen": Tiefen gegen die Tragion des Scanmittels, mitgeführt über die (festen) Augäpfel – robust für die Abnahme"""
    P = P or nm.netz.punkte(w)
    H = P["_haut"]
    t = 0.5 * (P["t_r"] + P["t_l"])
    if bezug == "augen":                # Abnahme ab B2 (Ausgleichsrechnung Lauf 3 noch mit Tragion)
        t = np.array([t[0], t[1], bezug_z(nm, w, P)])
    m = {}
    y_u = 0.5 * (P["ch_r"][1] + P["ch_l"][1])
    y_w = 0.5 * (P["ps_r"][1] + P["sn"][1])
    tri = nm.netz.haut.dreiecke
    for name, y0, xs in (("uw", y_u, (30, 40, 50)), ("mw", y_w, (25, 35))):
        for xx in xs:
            # beide Seiten gemittelt
            m[f"{name}{xx}"] = float(np.mean([vorderste_z(H, tri, s * xx, y0) for s in (-1, 1)]) - t[2])
    for p in ("g", "n", "prn", "sn", "ls", "li", "pg", "me"):
        m["t_" + p] = float(P[p][2] - t[2])
    m["ul_snpg"] = _abstand_linie(P["ls"], P["sn"], P["pg"])
    m["ll_snpg"] = _abstand_linie(P["li"], P["sn"], P["pg"])
    m["ul_e"] = _abstand_linie(P["ls"], P["prn"], P["pg"])
    m["ll_e"] = _abstand_linie(P["li"], P["prn"], P["pg"])
    m["konvex"] = _winkel(P["g"], P["sn"], P["pg"])
    m["lid_b"] = float(0.5 * (np.linalg.norm(P["ex_r"] - P["en_r"]) + np.linalg.norm(P["ex_l"] - P["en_l"])))
    m["lid_h"] = float(0.25 * ((P["lm37"][1] + P["lm38"][1]) - (P["lm40"][1] + P["lm41"][1])
                               + (P["lm43"][1] + P["lm44"][1]) - (P["lm46"][1] + P["lm47"][1])))
    q = H[(np.abs(H[:, 1] - (P["br_r"][1] + 10)) < 1.0) & (H[:, 2] > t[2] + 15)]
    m["schlaefe"] = float(np.ptp(q[:, 0])) if len(q) else float("nan")
    return m


class MesserB2(Messer):
    """Wie Messer, dazu die Profil- und Schrägmaße."""

    def masse(self, w):
        P = self.nm.netz.punkte(w)
        m = self.nm.messen(w, punkte=P, schnell=True)
        m.update(profil_masse(self.nm, w, P))
        for k, v in m.items():
            if isinstance(v, float) and not np.isfinite(v):
                m[k] = -99.0           # nicht messbar: als stark verletzt werten (NaN zählte sonst als erfüllt)
        return m


# Deckel je Probeform (größtes Gewicht in Richtung des Namens; die Gegenform hat ihren eigenen Deckel oder 0)
DECKEL = {
    "probe_cheek_volume_decr": {"m": 0.3, "w": 0.3}, "probe_cheek_volume_incr": {"m": 0.6, "w": 0.6},
    "probe_head_fat_decr": {"m": 0.3, "w": 0.3}, "probe_head_fat_incr": {"m": 0.3, "w": 0.3},
    "probe_neck_double_decr": {"m": 0.3, "w": 0.3}, "probe_neck_double_incr": {"m": 0.0, "w": 0.0},
    "probe_eyebrows_trans_forward": {"m": 0.3, "w": 0.3}, "probe_nose_trans_forward": {"m": 0.3, "w": 0.3},
    "probe_eye_scale_decr": {"m": 0.15, "w": 0.15}, "probe_eye_scale_incr": {"m": 0.3, "w": 0.3},
    "probe_chin_width_decr": {"m": 0.6, "w": 0.6}, "probe_chin_jaw_drop_decr": {"m": 0.5, "w": 0.5},
    "probe_head_invertedtriangular": {"m": 0.1, "w": 0.1},
    "probe_mouth_upperlip_volume_decr": {"m": 0.3, "w": 0.2}, "probe_mouth_lowerlip_volume_decr": {"m": 0.3, "w": 0.2},
    "probe_mouth_upperlip_volume_incr": {"m": 0.6, "w": 0.7}, "probe_mouth_lowerlip_volume_incr": {"m": 0.6, "w": 0.7},
    "probe_eye_eyefold_down": {"m": 0.8, "w": 0.8}, "probe_chin_jaw_drop_incr": {"m": 0.8, "w": 0.8},
    "probe_forehead_temple_decr": {"m": 0.0, "w": 0.0}, "probe_forehead_temple_incr": {"m": 0.6, "w": 0.6},
    "eigen_jochbein": {"m": 1.0, "w": 1.0}, "eigen_unterwange": {"m": 0.0, "w": 0.0},
    # Lauf 2: Stirn kippte bis an die Rechengrenze nach hinten, Kopf eckig und Kinn breit machten den Unterkiefer des
    # Mannes 132 mm breit (Scanmittel 127)
    "probe_forehead_trans_backward": {"m": 0.6, "w": 0.6}, "probe_head_square": {"m": 0.3, "w": 0.3},
    "probe_chin_width_incr": {"m": 0.4, "w": 0.3},
}
# Reifeziele der Phase 5, die ins Gegenteil kippen dürfen (wie S4, dazu die Volumenformen)
KIPPEN = {"probe_nose_scale_vert_incr", "probe_chin_height_incr", "probe_cheek_bones_incr", "probe_chin_bones_decr",
          "probe_chin_bones_incr", "probe_mouth_scale_horiz_incr", "probe_nose_point_down", "probe_cheek_volume_decr",
          "probe_head_fat_decr", "probe_mouth_upperlip_volume_decr", "probe_mouth_lowerlip_volume_decr",
          "probe_eye_scale_decr"}
# Augentiefe der Phase 5 (Prüfmappe A2, Mittelgesicht 35)
P5_AUGENTIEFE = {"m": 7.80, "w": 6.67}
# Mitte (25 → 45 Jahre): Wangenfett weniger stark als in S4 (0,35/0,4 -> 0,2), sonst wie S4
MITTE_WANGE = 0.2
SCHLUESSEL = ("ipd", "augapfel_ipd", "zy", "n_me", "gesichtsindex", "kinnmitte_zy", "mund_zy", "mundlage", "braue_pupille",
              "wangenwoelbung", "augentiefe", "nasofrontalwinkel", "pg_hinter_li", "kinn_hals_winkel", "unterkiefer",
              "n_sn", "mundbreite", "nasenbreite", "uw30", "uw40", "uw50", "mw25", "mw35", "t_g", "t_n", "t_prn", "t_sn",
              "t_ls", "t_li", "t_pg", "ul_snpg", "ll_snpg", "ul_e", "ll_e", "konvex", "lid_b", "lid_h", "schlaefe")


def main():
    opt = dict(a[2:].split("=", 1) for a in sys.argv[1:] if a.startswith("--") and "=" in a)
    aus = opt.get("aus", ".")
    os.makedirs(aus, exist_ok=True)
    messer = MesserB2(os.path.expanduser(opt["glb"]))
    pers = {p["name"]: p for p in json.load(open(opt["gewichte"], encoding="utf-8"))["personen"]}
    regler = json.load(open(os.path.join(HIER, "kopf2_regler.json"), encoding="utf-8"))
    haltb = json.load(open(opt.get("rezept_haltb", os.path.join(HIER, "kopf6_rezept_s4.json")), encoding="utf-8"))
    bericht = {"glb": opt["glb"], "gewichte": opt["gewichte"], "deckel": DECKEL, "mitte_wange": MITTE_WANGE}
    rezept = {}

    def grund_von(name):
        w = dict(pers[name]["szene"])
        for k in list(w):
            if k.startswith(("gen_", "alter_reife", "alter_greis", "alter_mitte")) or abs(w[k]) < 1e-9:
                w.pop(k)
        return w

    reife_alt = {g: zerlegen(regler["alter_formen"][f"alter_reife_{g}"]["ziele"]) for g in "mw"}
    reife_alt["m"]["eigen_adamsapfel"] = 1.0
    # Mitte wie S4, nur das Wangenfett schwächer
    for g in "mw":
        rezept[f"alter_mitte_{g}"] = dict(haltb[f"alter_mitte_{g}"], probe_cheek_volume_decr=MITTE_WANGE)

    vergleich = {"scanmittel": {k: round(v, 3) for k, v in messer.masse({}).items() if k in SCHLUESSEL}}
    ergebnisse = {}
    for g in "mw":
        makro = grund_von(f"mittel_{g}35")
        ref = messer.masse(makro)
        hb = messer.masse(addiere(makro, haltb[f"alter_reife_{g}_oben"], haltb[f"alter_reife_{g}_unten"],
                                  skaliert(haltb[f"alter_mitte_{g}"], 0.55)))
        vergleich[f"makro_{g}"] = {k: round(ref[k], 3) for k in SCHLUESSEL}
        vergleich[f"haltb_{g}"] = {k: round(hb[k], 3) for k in SCHLUESSEL}
        grund = addiere(makro, skaliert(rezept[f"alter_mitte_{g}"], 0.55))
        vorher = messer.masse(addiere(grund, {f"alter_reife_{g}": 1.0}))
        ipd0 = vorher["ipd"]

        def V(form, c0=0.0, u=None, o=1.0):
            gf_ = gegenform(form) if form.startswith("probe_") else None
            o = min(o, DECKEL.get(form, {}).get(g, 9.0))
            u_ = (-1.0 if gf_ else 0.0) if u is None else u
            if gf_:
                u_ = max(u_, -DECKEL.get(gf_, {}).get(g, 1.0))
            return ({form: 1.0}, {gf_: 1.0} if gf_ else {}, float(np.clip(c0, u_, o)), u_, o)

        # Volumen- und Profilziele (beide Geschlechter); Bezug Makro-Grund
        b2 = [("wangenwoelbung", "bereich", 3.0, 6.0, 0.4),
              ("uw30", "min", ref["uw30"] - 3.0, 0, 0.8), ("uw40", "min", ref["uw40"] - 3.0, 0, 0.8),
              ("uw50", "min", ref["uw50"] - 3.0, 0, 0.8),
              ("lid_b", "min", ref["lid_b"] - 0.6, 0, 0.25), ("lid_h", "min", ref["lid_h"] - 0.6, 0, 0.25),
              ("konvex", "bereich", 166.0, 171.0, 1.0)]
        if g == "m":
            b2 += [("ul_snpg", "bereich", 2.0, 4.0, 0.4), ("ll_snpg", "bereich", 1.5, 3.5, 0.4),
                   ("augentiefe", "bereich", P5_AUGENTIEFE["m"] - 0.5, P5_AUGENTIEFE["m"] + 1.0, 0.25),
                   ("t_g", "bereich", ref["t_g"] + 2.0, ref["t_g"] + 5.0, 0.6),
                   ("t_prn", "bereich", ref["t_prn"] + 0.5, ref["t_prn"] + 3.5, 0.6)]
            ziele = [("ipd", "ziel", ipd0, 0, 0.12), ("augapfel_ipd", "ziel", vorher["augapfel_ipd"], 0, 0.004 * vorher["augapfel_ipd"]),
                     ("gesichtsindex", "bereich", 0.85, 0.90, 0.008),
                     ("zy", "bereich", 140.0, 146.0, 1.0), ("n_me", "bereich", 119.0, 126.0, 1.0),
                     ("kinnmitte_zy", "bereich", 0.70, 0.76, 0.012), ("mund_zy", "bereich", 0.84, 0.90, 0.01),
                     ("mundlage", "bereich", 0.31, 0.35, 0.008), ("braue_pupille", "bereich", 9.5, 12.0, 0.5),
                     ("kinn_hals_winkel", "bereich", 108, 122, 2.0), ("n_sn", "ziel", 52.3, 0, 1.0), ("mundbreite", "ziel", 50.7, 0, 1.5),
                     ("pg_hinter_li", "bereich", 0.0, 4.0, 0.3),
                     ("unterkiefer", "bereich", ref["unterkiefer"] - 8.0, ref["unterkiefer"] + 1.0, 0.8)] + b2
            fest = {"eigen_lidfalte_m": 1.0, "eigen_kopfnicker": 1.0}
        else:
            uk_m = ergebnisse["m"]["masse"]["unterkiefer"]
            b2 += [("ul_snpg", "bereich", 2.5, 4.5, 0.4), ("ll_snpg", "bereich", 2.0, 4.0, 0.4),
                   ("augentiefe", "bereich", P5_AUGENTIEFE["w"] - 0.5, P5_AUGENTIEFE["w"] + 0.5, 0.25),
                   ("t_g", "bereich", ref["t_g"], ref["t_g"] + 3.0, 0.6),
                   ("t_prn", "bereich", ref["t_prn"], ref["t_prn"] + 2.5, 0.6)]
            ziele = [("ipd", "ziel", ipd0, 0, 0.12), ("augapfel_ipd", "ziel", vorher["augapfel_ipd"], 0, 0.004 * vorher["augapfel_ipd"]),
                     ("gesichtsindex", "bereich", 0.84, 0.90, 0.008),
                     ("zy", "bereich", 134.0, 139.0, 1.0), ("n_me", "bereich", 113.0, 119.0, 1.2),
                     ("kinnmitte_zy", "bereich", 0.66, 0.72, 0.01), ("mund_zy", "bereich", 0.84, 0.90, 0.008),
                     ("mundlage", "bereich", 0.31, 0.35, 0.008), ("braue_pupille", "bereich", 12.0, 14.0, 0.4),
                     ("nasofrontalwinkel", "bereich", 134.0, 142.0, 1.5), ("pg_hinter_li", "bereich", 0.0, 4.0, 0.3),
                     ("kinn_hals_winkel", "bereich", 108, 122, 2.0), ("unterkiefer", "bereich", 0.88 * uk_m, 0.92 * uk_m, 0.6),
                     ("n_sn", "ziel", 48.6, 0, 1.0), ("mundbreite", "ziel", 47.1, 0, 1.5)] + b2
            fest = {"eigen_lidfalte_w": 1.0, "eigen_kopfnicker": 0.5}
        var = {}
        for f, w in reife_alt[g].items():
            var[f] = V(f, w, None if f in KIPPEN else 0.0, max(1.2, w))
        for f in ("eigen_kieferkante", "eigen_jochbein"):
            var[f] = V(f, 0.0, 0.0, 1.2 if g == "m" else 1.0)
        extra = ["probe_cheek_volume_decr", "probe_head_fat_decr", "probe_forehead_temple_incr", "probe_mouth_upperlip_volume_decr",
                 "probe_mouth_lowerlip_volume_decr", "probe_mouth_trans_forward", "probe_eye_scale_decr", "probe_eye_height2_incr",
                 "probe_cheek_bones_incr", "probe_chin_bones_incr", "probe_cheek_inner_decr", "probe_mouth_trans_down",
                 "probe_neck_double_decr", "probe_eye_trans_out", "probe_chin_width_decr", "probe_head_invertedtriangular",
                 "probe_nose_trans_forward", "probe_eyebrows_trans_forward"]
        if g == "w":
            extra += ["probe_eyebrows_trans_down", "probe_nose_greek_decr", "probe_chin_jaw_drop_decr", "probe_head_square",
                      "probe_cheek_bones_decr"]
            # Kopf schmaler nur zusammen mit Augen nach außen (Pupillenabstand bleibt), wie S4
            g0 = addiere(grund, reife_alt["w"])
            m0 = messer.masse(g0)
            d1 = messer.masse(addiere(g0, {"probe_head_scale_horiz_decr": 1.0}))["ipd"] - m0["ipd"]
            d2 = messer.masse(addiere(g0, {"probe_eye_trans_out": 1.0}))["ipd"] - m0["ipd"]
            var["kopf_schmal"] = ({"probe_head_scale_horiz_decr": 1.0, "probe_eye_trans_out": -d1 / d2}, {}, 0.0, 0.0, 1.0)
        for f in extra:
            if f not in var and not any(gegenform(f) == k for k in var):
                var[f] = V(f, 0.0, -0.6 if gegenform(f) else 0.0, 0.6)     # Zusatzformen nur mäßig (±0,6)
        # Deckel auch auf übernommene Reifeziele (Startwert in den Deckel)
        for f in list(var):
            if f in DECKEL:
                p, mi, c0, u, o = var[f]
                o = min(o, DECKEL[f][g])
                var[f] = (p, mi, float(np.clip(c0, u, o)), u, o)
        var["gen_augen_tiefe_plus"] = ({"gen_augen_tiefe_plus": 1.0}, {"gen_augen_tiefe_minus": 1.0}, 0.0, -1.0, 1.0)
        prot = []
        c, m, w_end, gewaehlt = ausgleich(messer, grund, fest, var, ziele, runden=25, protokoll=prot)
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
        vergleich[f"b2_{g}"] = {k: round(m[k], 3) for k in SCHLUESSEL}
        verfehlt = []
        for (k, art, a, b, tol) in ziele:
            v = m[k]
            ok = {"ziel": abs(v - a) <= tol, "max": v <= a + 1e-9, "min": v >= a - 1e-9, "bereich": a - 1e-9 <= v <= b + 1e-9}[art]
            if not ok:
                verfehlt.append([k, art, round(a, 3), round(b, 3) if art == "bereich" else None, round(v, 3)])
        ergebnisse[g] = {"masse": m, "c": c, "protokoll": prot, "ziele": [list(z) for z in ziele], "verfehlt": verfehlt}
        print(g, "verfehlt:", verfehlt)
        print("  c", {k: round(v, 2) for k, v in c.items() if abs(v) > 0.01})
    bericht["ergebnisse"] = ergebnisse
    bericht["profil_vergleich"] = vergleich
    print("%-15s" % "Maß", "".join("%10s" % z for z in vergleich))
    for k in SCHLUESSEL:
        print("%-15s" % k, "".join("%10.2f" % vergleich[z][k] for z in vergleich))
    json.dump(rezept, open(os.path.join(aus, "rezept_s4_b2.json"), "w", encoding="utf-8"), indent=1, ensure_ascii=False)
    json.dump(bericht, open(os.path.join(aus, "ausgleich_b2.json"), "w", encoding="utf-8"), indent=1, ensure_ascii=False,
              default=float)


if __name__ == "__main__":
    main()
