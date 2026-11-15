"""Rock der Sitzenden: folgt er dem Knie? (ohne Blender; CPU-Skinning und Formschlüssel wie im Spiel, tools/modelle/sitzpruefung.py)

    python tools/modelle/rock_pruefung.py [--ordner=game/assets/modelle/figuren/probe] [--arten=Frau,Elbin] [--clips=Sitzen,Feinarbeit]

Je Figur mit Rock (Frau, Elbin, Zwergin, Halblingin; alle Nah-Kleider), je sitzendem Clip (FigurenBaukasten.SitzSchluessel: Formschlüssel = 1)
und je Zeitpunkt der Schleife (13 Zeitpunkte gleich verteilt):
  (a) VOR DER KNIEFRONT: wie weit liegt Stoff vor dem Knie? Kniefront = Kniegelenk (LowerLeg) + KNIE_HALB nach vorn (+Z), je Seite;
      unter der Kniehöhe die Kniefront oder – wo das Schienbein weiter vorn liegt – dessen Front auf derselben Höhe (Knochenstrecke
      Knie–Knöchel + SCHIEN_HALB). Gezählt wird Stoff im Bereich des Beins (seitlich ±12 cm um das Knie, unter Hüfte + 12 cm). Grenzwert VOR_MAX (Spielmeter).
  (b) SENKRECHT UNTER DEM KNIE: die vorderste Stoffkontur unterhalb der Kniehöhe in fünf Höhenstreifen (je Streifen der vorderste Punkt,
      Streifen mit mindestens zwei Ecken); Neigung der Ausgleichsgeraden durch diese Punkte gegen die Senkrechte. Grenzwert NEIGUNG_MAX (Grad).
  (c) LAGEN: Die Schürze liegt auf dem Kleid wie im Stehen – je Schürzenecke der Abstand zur nächsten Kleidecke (ohne Arme) entlang deren
      Normale, verglichen mit demselben Abstand in der Ruhe; weicht er um mehr als LAGE_TOL ab (in den Körper bzw. heraus), ist es ein Fehler.
      Reihenfolge Körper < Kleid < Schürze (Körper und Kleid sind ein Netz und werden nicht getrennt geprüft).
Ausgabe je Figur und Clip der schlechteste Wert über alle Zeitpunkte und Kleider; Rückgabewert 1, wenn ein Grenzwert überschritten ist.
Die Durchdringung prüft weiter tools/modelle/sitzpruefung.py.
"""
import math
import os
import sys

import numpy as np

HIER = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HIER)
import sitzpruefung as sp  # noqa: E402

SCHIEN_HALB = 0.05      # halbe Dicke des Schienbeins nach vorn (Modellmeter)
KNIE_HALB = 0.055       # halbe Kniedicke nach vorn (Modellmeter; die Figuren sind in Menschengröße gebaut und im Spiel skaliert)
VOR_MAX = 0.04          # Stoff höchstens so weit vor der Kniefront (Spielmeter)
NEIGUNG_MAX = 20.0      # Neigung der vorderen Kontur unter dem Knie gegen die Senkrechte (Grad)
LAGE_TOL = 0.005        # Schürze höchstens so weit näher am bzw. weiter vom Kleid als im Stehen (Spielmeter)
ROCK_ARTEN = ("Frau", "Elbin", "Zwergin", "Halblingin")
SITZ_CLIPS = ("Sitzen", "Feinarbeit", "Trinken", "Schreiben", "Beten", "Zaehlen", "Putzen", "Rechnen", "Kutschieren")


def messen(g, clip, zeit, varianten, m):
    lok = g.pose(clip, zeit)
    welt = g.welt(lok)
    k = {nd.get("name"): welt[i][:3, 3] for i, nd in enumerate(g.j["nodes"])}
    vor, neigung = -1.0, 0.0
    for v in varianten:
        e = g.netz_ecken(lok, {f"Nah_{v}", "Zub_schuerze"})
        stoff = sp.stoff_maske(e, k)
        # Hände und Finger (liegen oft auf dem Schoß vor dem Knie) zählen nicht als Rock
        hand = np.zeros(len(e), dtype=bool)
        for name, p in k.items():
            if name and any(t in name for t in ("Hand", "Thumb", "Index", "Middle", "Ring", "Little")):
                hand |= np.linalg.norm(e - p, axis=1) < 0.12
        s = e[stoff & ~hand]
        for seite in ("Left", "Right"):
            knie = k[f"{seite}LowerLeg"]
            front = knie[2] + KNIE_HALB
            bein = s[(np.abs(s[:, 0] - knie[0]) < 0.12) & (s[:, 1] < k['Hips'][1] + 0.12)]   # Rock: unterhalb Hüfte + 12 cm (nicht Arme, Hände)
            if not len(bein):
                continue
            linie = np.full(len(bein), front)
            fussg = k[f"{seite}Foot"]
            unter = bein[:, 1] < knie[1]
            anteil = np.clip((knie[1] - bein[unter, 1]) / max(knie[1] - fussg[1], 1e-6), 0, 1)
            linie[unter] = np.maximum(front, knie[2] + anteil * (fussg[2] - knie[2]) + SCHIEN_HALB)
            vor = max(vor, float((bein[:, 2] - linie).max()) * m)
            unten = bein[(bein[:, 1] < knie[1]) & (bein[:, 2] > front - 0.10)]
            if len(unten) < 5:
                continue
            boden = unten[:, 1].min()
            grenzen = np.linspace(knie[1], boden, 6)
            vorne = []
            for o, u in zip(grenzen[:-1], grenzen[1:]):
                streifen = unten[(unten[:, 1] <= o) & (unten[:, 1] >= u)]
                if len(streifen) >= 2:
                    vorne.append(((o + u) / 2, streifen[:, 2].max()))
            if len(vorne) >= 3:
                ys, zs = np.array([v[0] for v in vorne]), np.array([v[1] for v in vorne])
                steigung = np.polyfit(ys, zs, 1)[0]
                neigung = max(neigung, math.degrees(math.atan(abs(steigung))))
    return vor, neigung


ARM_KNOCHEN = ("Shoulder", "Arm", "Hand", "Thumb", "Index", "Middle", "Ring", "Little")


def _kleid(g, lok, name):
    """Ecken des Kleides in der Pose und seine Dreiecke ohne Arme (Anteil der Arm-, Hand- und Fingerknochen über 0,3 wie passanten_sitzen.py)."""
    e = g.netz_ecken(lok, {name})
    nd = next(nd for nd in g.j["nodes"] if nd.get("name") == name)
    skin = g.j["skins"][nd["skin"]]
    wahl = np.array([any(t in g.j["nodes"][k]["name"] for t in ARM_KNOCHEN) for k in skin["joints"]])
    dr, arm, ab = [], [], 0
    for prim in g.j["meshes"][nd["mesh"]]["primitives"]:
        dr.append(g.accessor(prim["indices"]).reshape(-1, 3).astype(np.int64) + ab)
        jo = g.accessor(prim["attributes"]["JOINTS_0"]).astype(int)
        ge = g.accessor(prim["attributes"]["WEIGHTS_0"])
        arm.append(sum(ge[:, s] * wahl[jo[:, s]] for s in range(4)) > 0.3)
        ab += len(jo)
    dr, arm = np.concatenate(dr), np.concatenate(arm)
    return e, dr[~arm[dr].any(axis=1)]


def lage(g, clip, zeit, varianten, m):
    """Größte Abweichung (Spielmeter) des Abstands der Schürze zur Kleidfläche (nächster Punkt, entlang der Normale) gegenüber dem Stehen;
    None ohne Schürze. Arme (Dreiecke an Arm-, Hand- und Fingerknochen) zählen nicht zum Kleid."""
    if not any(nd.get("name") == "Zub_schuerze" for nd in g.j["nodes"]):
        return None
    stehen = g.pose("Stehen", 0.0)
    k_r, dr = _kleid(g, stehen, f"Nah_{varianten[0]}")
    s_r = g.netz_ecken(stehen, {"Zub_schuerze"})
    idx, bary = sp.naechste_dreiecke(s_r, k_r, dr)
    q, n = sp.dreieck_lage(k_r, dr, idx, bary)
    vorher = np.einsum("ij,ij->i", s_r - q, n)
    lok = g.pose(clip, zeit)
    k_s, _ = _kleid(g, lok, f"Nah_{varianten[0]}")
    s_s = g.netz_ecken(lok, {"Zub_schuerze"})
    q, n = sp.dreieck_lage(k_s, dr, idx, bary)
    nachher = np.einsum("ij,ij->i", s_s - q, n)
    # die Zielform hält mindestens SCHUERZE_LUFT (1,2 cm) Abstand nach außen: so viel Abstand ist gewollt
    soll = np.sign(vorher + 1e-12) * np.maximum(np.abs(vorher), 0.012)
    return float(np.abs(nachher - soll).max()) * m


def main():
    opt = {a.split("=")[0][2:]: a.split("=", 1)[1] for a in sys.argv[1:] if a.startswith("--") and "=" in a}
    ordner = opt.get("ordner", sp.FIGUREN)
    arten = opt["arten"].split(",") if "arten" in opt else ROCK_ARTEN
    clips = opt["clips"].split(",") if "clips" in opt else SITZ_CLIPS
    schlecht = 0
    print(f"Rockprüfung (Grenzwerte: vor der Kniefront {VOR_MAX * 100:.0f} cm, Neigung unter dem Knie {NEIGUNG_MAX:.0f}°, Lage Schürze {LAGE_TOL * 100:.1f} cm), Ordner {ordner}")
    print(f"{'Art':11} {'Clip':12} {'vor Knie cm':>11} {'Neigung °':>10} {'Lage cm':>8}  Urteil")
    for art in arten:
        datei, varianten, _, m = sp.FIGUREN_ARTEN[art]
        g = sp.Glb(os.path.join(ordner, datei + ".glb"))
        namen = [a["name"] for a in g.j.get("animations", [])]
        for clip in clips:
            if clip not in namen:
                print(f"{art:11} {clip:12} {'fehlt':>11}")
                continue
            dauer = g.clipdauer(clip)
            vor, neigung, lg = -1.0, 0.0, 0.0
            for zeit in np.linspace(0, dauer, 13, endpoint=False):
                v, n = messen(g, clip, float(zeit), varianten, m)
                vor, neigung = max(vor, v), max(neigung, n)
            for zeit in np.linspace(0, dauer, 4, endpoint=False):   # Lagen: 4 Zeitpunkte (die Schürze folgt dem Kleid ohne eigene Bewegung)
                w = lage(g, clip, float(zeit), varianten, m)
                lg = max(lg, w or 0.0)
            gut = vor <= VOR_MAX and neigung <= NEIGUNG_MAX and lg <= LAGE_TOL
            schlecht += not gut
            print(f"{art:11} {clip:12} {vor * 100:11.1f} {neigung:10.0f} {lg * 100:8.1f}  {'gut' if gut else 'FEHLER'}", flush=True)
    print(f"Fehler: {schlecht}")
    return 1 if schlecht else 0


if __name__ == "__main__":
    sys.exit(main())
