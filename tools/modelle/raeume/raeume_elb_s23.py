"""Hinterzimmer und Bibliothek fuer die Wohnhaeuser Elb (Stufe 2 und 3): Schicht auf den Raumskripten der Menschen (unveraendert).

    blender -b --factory-startup --python tools/modelle/raeume/raeume_elb_s23.py -- --haus=wohnhaus_elb_s<2|3>_g<n> --raum=hinterzimmer|bibliothek [--ohne-ao] [--bilder=<ordner>]

Schreibt game/assets/modelle/gebaeude/neubau/<haus>_<raum>_innen.glb (im Spiel „InteriorRoot_<Raum>“, Stadtansicht.Wohnraeume.cs). Muster:
raeume_zwerg_s23.py. Das Raummodul ist je Haus dasselbe wie beim Menschen (raeume_s23.py: im Obergeschoss, gleiche Masse, Fenster und Tueren:
innen = aussen); anders:
  * Zimmertuer 2,10 m licht (groesster Elb 1,90 + 0,20, wie die Zimmertuer der Wohnstube, wohnhaus_elb_s23.py), Blick aus 1,72 m;
  * Steinhaus: die Bibliothek bleibt rechteckig im Obergeschoss am Platz des Hinterzimmers wie im Buergerhaus (Nutzer 17.11.2026; das
    Steinhaus der Elben hat keine Gaube, die Bibliothek der Menschen und Zwerge liegt im Dachgeschoss unter den Schraegen);
  * Trim-Sheet raeume_innen_trim_elb (raeume_trimsheet_elb.py), AO-Gruppe wohnhaus_elb_raeume_innen (Plaetze hinterzimmer_s23, bibliothek_s23).
"""
import os
import sys

HIER = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HIER)
sys.path.insert(0, os.path.join(os.path.dirname(HIER), "neubau"))
import raeume_masse as M                                                  # noqa: E402
import raeume_bib as R                                                    # noqa: E402
# raeume_s23 erst unten laden: es setzt beim Import die Masse in raeume_masse.py auf das Obergeschoss um

VOLK = "elb"
TUER_H = 2.10                     # Zimmertuer licht (wohnhaus_elb_s23.ZIMMERTUER_HOCH)
BLICK_HOEHER = 0.12               # Augenhoehe 1,72 m (wohnhaus_elb_s23.BLICK_HOEHER)


def umbenennen(text):
    return text.replace("mensch", VOLK)


def teile_y(pts, uv, werte):
    """Teilt ein Viereck, dessen Kantenpaar P0-P1 / P3-P2 entlang y laeuft, an den y-Werten `werte` (wie wohnhaus_elb_s23.schnitt)."""
    for dreh in (0, 1):
        P, U = pts[dreh:] + pts[:dreh], list(uv[dreh:] + uv[:dreh])
        a0, a1, b0, b1 = P[0].y, P[1].y, P[3].y, P[2].y
        ts = [((w - a0) / (a1 - a0), (w - b0) / (b1 - b0)) for w in werte
              if min(a0, a1) + 1e-4 < w < max(a0, a1) - 1e-4 and min(b0, b1) + 1e-4 < w < max(b0, b1) - 1e-4]
        if not ts: continue
        ts = [(0.0, 0.0)] + sorted(ts) + [(1.0, 1.0)]
        L = lambda p, q, t: p + (q - p) * t
        LU = lambda u, v, t: (u[0] + (v[0] - u[0]) * t, u[1] + (v[1] - u[1]) * t)
        return [([L(P[0], P[1], ta), L(P[0], P[1], tc), L(P[3], P[2], td), L(P[3], P[2], tb)],
                 [LU(U[0], U[1], ta), LU(U[0], U[1], tc), LU(U[3], U[2], td), LU(U[3], U[2], tb)]) for (ta, tb), (tc, td) in zip(ts[:-1], ts[1:])]
    return None


def schicht_anlegen():
    M.TUER_H = TUER_H
    M.AUGE = M.AUGE + BLICK_HOEHER
    M.HZ_KAMERA = tuple((a[0], a[1], a[2] + BLICK_HOEHER) for a in M.HZ_KAMERA)
    M.BI_KAMERA = tuple((a[0], a[1], a[2] + BLICK_HOEHER) for a in M.BI_KAMERA)
    R.GRUPPE = umbenennen(R.GRUPPE)
    R.HAUS = umbenennen(R.HAUS)
    R.BLATT = R.BLATT + "_elb"
    alt_uv, alt_exp = R.uv2_und_ao, R.exportiere

    def uv(*a, **kw):
        R.GRUPPE = umbenennen(R.GRUPPE)
        return alt_uv(*a, **kw)
    R.uv2_und_ao = uv
    R.exportiere = lambda objekte, datei: alt_exp(objekte, umbenennen(datei))


if __name__ == "__main__":
    schicht_anlegen()
    opt = R.optionen()
    if "_s1_" in opt.get("haus", ""):
        raise SystemExit("Elben: Raeume erst ab Stufe 2 (Stufe 1 hat keine)")
    import raeume_s23
    raeume_s23.DACH_BIBLIOTHEK = False
    import raum_bibliothek
    alt_raum = raum_bibliothek.baue_raum

    def raum():
        """Wie bei den Menschen, aber die Brueststange der Taefelung an der linken Wand endet an den Pfosten der dorthin verlegten Tuer
        (sie lief durch die Tueroeffnung und lag deckungsgleich auf den Tuerpfosten: Pruefung 12)."""
        r = alt_raum()
        _w, (t0, t1) = M.RAUM_TUER["Bibliothek"]
        a, b = t0 - 0.07, t1 + 0.07                                  # Aussenkanten der Tuerpfosten
        neu, geteilt = [], 0
        for pts, uv, st, fa, gl in r.flaechen:
            ys = [p.y for p in pts]
            if (len(pts) == 4 and all(-M.IX - 0.01 <= p.x <= -M.IX + 0.05 for p in pts) and all(M.Z_BODEN + 0.6 <= p.z <= M.Z_BODEN + 1.2 for p in pts)
                    and min(ys) < a and max(ys) > b):
                stuecke = teile_y(pts, uv, (a, b))
                if stuecke:
                    neu += [(q, u, st, fa, gl) for q, u in stuecke if not a < sum(p.y for p in q) / 4 < b]
                    geteilt += 1
                    continue
            neu.append((pts, uv, st, fa, gl))
        r.flaechen = neu
        print(f"RAUM Brueststange an der Tuer geteilt: {geteilt} Flaechen")
        return r
    raum_bibliothek.baue_raum = raum
    raeume_s23.main()
