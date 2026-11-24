"""Neubau Dom: Soll gegen Ist aus den fertigen GLBs (reines Python mit numpy, ohne Blender).

    python tools/modelle/neubau/dom_messen.py [--modell=dom_katholisch] [--md=<datei>]

Liest game/assets/modelle/gebaeude/neubau/<modell>.glb und <modell>_innen.glb und prueft gegen die Massdatei (dom_masse.py):
  * Dreiecke je Knoten, Huelle im Budget des Style-Guides (Monument 8.000-15.000),
  * belegte Flaeche und Hoehe der Huelle, Unterkante bei Z = 0,
  * UV2 (TEXCOORD_1) aller Netze der Huelle und des Raums liegt in der Zelle des Modells im AO-Atlas (ao_atlas.json),
  * Fenster: jede Oeffnung der Massdatei hat aussen (Fenster) und innen (Innenglas) eine Scheibe in derselben Ebene und Lage,
  * Raum: Huellkiste des Innenraums liegt in der Huelle, lichte Hoehe,
  * Anker: Blick_Auge 1,60 ueber dem Boden, Tuer_Dom_Stadt im Portal, 48 Sitzplaetze Glaeubig_<n> auf den Baenken (Sitzflaeche darunter),
    Predigt_1 auf dem Kanzelboden, Anker_Bischof, Anker_Pfarrer; die Knoten der Einrichtung je Stadtglaube.
Dazu rechnet es die Tabelle aus dom_masse.soll_ist() mit (Masse, die schon ohne GLB feststehen).
"""
import json
import os
import sys

import numpy as np

HIER = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HIER)
import dom_masse as M                  # noqa: E402
from rathaus_messen import Glb         # noqa: E402

ORDNER = os.path.join(os.path.dirname(os.path.dirname(os.path.dirname(HIER))), "game", "assets", "modelle", "gebaeude", "neubau")
f = lambda v: f"{v:.2f}".replace(".", ",")


def uv2(glb, name):
    i = glb.knoten()[name]
    aus = []
    for p in glb.j["meshes"][glb.j["nodes"][i]["mesh"]]["primitives"]:
        if "TEXCOORD_1" not in p["attributes"]: return None
        aus.append(glb.accessor(p["attributes"]["TEXCOORD_1"]))
    return np.concatenate(aus)


def zelle(gruppe, modell):
    d = json.load(open(os.path.join(HIER, "ao_atlas.json"), encoding="utf-8"))[gruppe]
    n = d["zellen"]; nr = d["plaetze"][modell]
    return n, nr % n, nr // n


def pruefen(modell):
    z = []
    add = lambda name, soll, ist, ok: z.append((name, soll, ist, bool(ok)))
    z.extend(M.soll_ist())
    aussen = Glb(os.path.join(ORDNER, modell + ".glb"))
    innen = Glb(os.path.join(ORDNER, modell + "_innen.glb"))
    # ── Dreiecke ──
    netze_a = [n for n, i in aussen.knoten().items() if "mesh" in aussen.j["nodes"][i]]
    netze_i = [n for n, i in innen.knoten().items() if "mesh" in innen.j["nodes"][i]]
    summe = 0
    for n in netze_a:
        d = aussen.dreiecke(n); summe += d
        add(f"Dreiecke Huelle {n}", "", str(d), True)
    add("Dreiecke Huelle Summe", f"{M.BUDGET[0]}-{M.BUDGET[1]}", str(summe), M.BUDGET[0] <= summe <= M.BUDGET[1])
    summe_i = 0
    for n in netze_i:
        d = innen.dreiecke(n); summe_i += d
        add(f"Dreiecke innen {n}", "", str(d), True)
    add("Dreiecke innen Summe", "(kein Budget im Style-Guide; Hinweis)", str(summe_i), True)
    # ── Huelle: Flaeche, Hoehe ──
    alle = np.concatenate([p for n in ("Haus", "Dach") for p, _ in aussen.ecken(n).values()])
    lo, hi = alle.min(axis=0), alle.max(axis=0)
    add("Huelle Breite", f"<= {f(M.FLAECHE[0])}", f(hi[0] - lo[0]), hi[0] - lo[0] <= M.FLAECHE[0] + 1e-3)
    add("Huelle Tiefe", f"<= {f(M.FLAECHE[1])}", f(hi[1] - lo[1]), hi[1] - lo[1] <= M.FLAECHE[1] + 1e-3)
    add("Huelle mittig in der Flaeche", "|Mitte| <= 0,60", f"{f((lo[0] + hi[0]) / 2)} / {f((lo[1] + hi[1]) / 2)}", abs(lo[0] + hi[0]) / 2 <= 0.6 and abs(lo[1] + hi[1]) / 2 <= 0.6)
    add("Huelle Unterkante", "0,00", f(lo[2]), abs(lo[2]) < 1e-3)
    add("Huelle Hoehe (Kreuz)", f(M.Z_KREUZ), f(hi[2]), abs(hi[2] - M.Z_KREUZ) < 0.02)
    dach = np.concatenate([p for p, _ in aussen.ecken("Dach").values()])
    add("First des Hochschiffs", f"{f(M.Z_FIRST)} (+ Deckung)", f(dach[np.abs(dach[:, 0]) < 0.5][:, 2][dach[np.abs(dach[:, 0]) < 0.5][:, 1] > M.Y_TURM].max()), True)
    # ── UV2 ──
    for glb, gruppe, namen in ((aussen, "dom", ("Haus", "Dach")), (innen, "dom_innen", [n for n in netze_i if not n.startswith("Glas_")])):
        n, sp, ze = zelle(gruppe, modell)
        for name in namen:
            uv = uv2(glb, name)
            if uv is None:
                add(f"UV2 {name}", "vorhanden", "fehlt", False); continue
            u, v = uv[:, 0], 1 - uv[:, 1]            # glTF zaehlt v von oben, der Atlas (Blender) von unten
            ok = u.min() >= sp / n - 1e-4 and u.max() <= (sp + 1) / n + 1e-4 and v.min() >= ze / n - 1e-4 and v.max() <= (ze + 1) / n + 1e-4
            add(f"UV2 {name}", f"in Zelle {sp}/{ze} von {n}x{n} ({gruppe})", f"u {f(u.min())}-{f(u.max())}, v {f(v.min())}-{f(v.max())}", ok)
    # ── Fenster innen = aussen ──
    fa = np.concatenate([p for p, _ in aussen.ecken("Scheiben", "Fenster").values()])
    fi = np.concatenate([p for p, _ in innen.ecken("Glas_Dom", "Innenglas").values()])
    fehlt_a = fehlt_i = 0
    groesste = 0.0
    anzahl = 0
    for o in M.OEFFNUNGEN:
        name, s0, s1, z0, z1, art, _ = o
        if art == "portal": continue
        anzahl += 1
        d = M.wand(name)[4]
        zp = (z0 + z1) / 2 if art == "rose" else z0                 # Rose: aeusserste Punkte auf halber Hoehe, sonst die Ecken der Bruestung
        for s in (s0, s1):
            p = np.array(M.punkt(name, s, zp, -d / 2))
            da, di = np.linalg.norm(fa - p, axis=1).min(), np.linalg.norm(fi - p, axis=1).min()
            groesste = max(groesste, da, di)
            fehlt_a += da > 0.002; fehlt_i += di > 0.002
    add("Fenster aussen in der Wandmitte", f"{anzahl} Oeffnungen, Abweichung <= 0,002", f"{anzahl - (fehlt_a + 1) // 2} passen", fehlt_a == 0)
    add("Fenster innen = aussen", f"{anzahl} Oeffnungen, Abweichung <= 0,002", f"{anzahl - (fehlt_i + 1) // 2} passen, groesste Abweichung {groesste:.4f}".replace(".", ","), fehlt_i == 0)
    # ── Raum ──
    raum = np.concatenate([p for p, _ in innen.ecken("Raum_Dom").values()])
    rlo, rhi = raum.min(axis=0), raum.max(axis=0)
    add("Innenraum in der Huelle (x, y)", "ja", f"x {f(rlo[0])}...{f(rhi[0])}, y {f(rlo[1])}...{f(rhi[1])}",
        rlo[0] >= -M.X_QUER and rhi[0] <= M.X_QUER and rlo[1] >= M.Y_WESTWAND and rhi[1] <= M.Y_APSIS + M.R_APSIS)
    add("Innenraum Scheitel", f"<= {f(M.Z_SCHEITEL)}", f(rhi[2]), rhi[2] <= M.Z_SCHEITEL + 1e-3)
    add("Innenraum Boden", f(M.Z_SOCKEL), f(raum[:, 2][raum[:, 2] > 0.2].min()) if (raum[:, 2] > 0.2).any() else "-", True)
    # ── Anker ──
    kn = innen.knoten()
    auge, _ = innen.anker("Blick_Auge")
    add("Blick_Auge", f"{f(M.Z_SOCKEL + 1.6)} hoch, im Mittelgang", f"{f(auge[0])}/{f(auge[1])}/{f(auge[2])}", abs(auge[2] - M.Z_SOCKEL - 1.6) < 0.01 and abs(auge[0]) < 0.7)
    tuer, ex = innen.anker("Tuer_Dom_Stadt")
    add("Tuer_Dom_Stadt", f"im Portal, {f(M.PORTAL[0])} breit, {f(M.PORTAL[2] - M.PORTAL[1])} hoch, ziel stadt",
        f"{f(tuer[0])}/{f(tuer[1])}, {f(float(ex.get('breite', 0)))} x {f(float(ex.get('hoehe', 0)))}, ziel {ex.get('ziel')}",
        abs(tuer[0]) < 0.01 and M.Y_WESTWAND <= tuer[1] <= M.Y_WESTWAND + M.T_WAND + 0.05 and ex.get("ziel") == "stadt"
        and abs(float(ex.get("breite", 0)) - M.PORTAL[0]) < 0.01)
    moebel = np.concatenate([p for p, _ in innen.ecken("Moebel_Dom").values()])
    sitz = moebel[np.abs(moebel[:, 2] - (M.Z_SOCKEL + M.SITZHOEHE)) < 0.003]
    ok_anz, ok_lage, ok_bank = 0, 0, 0
    for nr, x, y, links in M.sitzplaetze():
        if f"Glaeubig_{nr}" not in kn: continue
        ok_anz += 1
        p, ex = innen.anker(f"Glaeubig_{nr}")
        ok_lage += np.linalg.norm(p - np.array([x, y, M.Z_SOCKEL])) < 0.005 and abs(float(ex.get("sitzhoehe", 0)) - M.SITZHOEHE) < 1e-4
        nah = sitz[(np.abs(sitz[:, 1] - y) < 0.3)]
        ok_bank += len(nah) > 0 and nah[:, 0].min() <= x - 0.2 and nah[:, 0].max() >= x + 0.2
    n = len(M.sitzplaetze())
    add("Sitzplaetze Glaeubig_<n>", f"{n} Anker an ihrem Platz", f"{ok_anz} Anker, {ok_lage} an ihrem Platz", ok_anz == n and ok_lage == n)
    add("Sitzflaeche unter jedem Platz", f"{n} (Hoehe {f(M.SITZHOEHE)})", str(ok_bank), ok_bank == n)
    pred, _ = innen.anker("Predigt_1")
    add("Predigt_1 auf dem Kanzelboden", f"{f(M.KANZEL[0])}/{f(M.KANZEL[1])}/{f(M.Z_SOCKEL + M.KANZEL[2])}", f"{f(pred[0])}/{f(pred[1])}/{f(pred[2])}",
        np.linalg.norm(pred - np.array([M.KANZEL[0], M.KANZEL[1], M.Z_SOCKEL + M.KANZEL[2]])) < 0.01)
    for name, soll in (("Anker_Bischof", M.PLATZ_BISCHOF), ("Anker_Pfarrer", M.PLATZ_PFARRER)):
        p, _ = innen.anker(name)
        add(name, "/".join(f(v) for v in soll), "/".join(f(v) for v in p), np.linalg.norm(p - np.array(soll)) < 0.01)
    for name in ("Einrichtung_Katholisch", "Weihrauch", "Einrichtung_Evangelisch", "Kreuz_Evangelisch", "Einrichtung_Simultan"):
        add(f"Knoten {name}", "vorhanden", "vorhanden" if name in kn else "fehlt", name in kn)
    return z


def main():
    opt = dict((a[2:].split("=", 1) + ["1"])[:2] for a in sys.argv[1:] if a.startswith("--"))
    zeilen = pruefen(opt.get("modell", "dom_katholisch"))
    text = M.tabelle(zeilen)
    print(text)
    fehler = [n for n, _, _, ok in zeilen if not ok]
    print(f"\n{len(zeilen)} Pruefungen, {len(fehler)} Fehler" + (": " + ", ".join(fehler) if fehler else ""))
    if "md" in opt:
        with open(opt["md"], "w", encoding="utf-8") as d: d.write(text + "\n")
    sys.exit(1 if fehler else 0)


if __name__ == "__main__":
    main()
