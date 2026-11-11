"""Sucht die Parameter der Haltung Text: flache, gerade Hand, hochkant, schmalste Silhouette von der Kante (Welt-X-Ausdehnung) bei wenig Durchdringung.
blender -b handschuh_pixal3d_v6.blend --python v6_opt_text.py -- <params_aus.json> [Runden] [Seed]   (Umgebung TEXT = Startparameter)"""
import sys, os, json, random, copy
HIER = os.path.dirname(os.path.abspath(__file__)); sys.path.insert(0, HIER)
import numpy as np
from mathutils import Vector
import v6_text_lib as T
args = sys.argv[sys.argv.index("--") + 1:]
aus = args[0]; runden = int(args[1]) if len(args) > 1 else 6; random.seed(int(args[2]) if len(args) > 2 else 1)
FREEZE = os.environ.get("FREEZE", "0") == "1"   # nur der Daumen (Indizes 18-24) wird gesucht, alles andere bleibt exakt
GERADE = os.environ.get("GERADE", "1") == "1"   # Finger ganz gestreckt und fest aneinander
GW = float(os.environ.get("GW_DURCH", "1.0"))   # mm je Dreieckspaar

_q = Vector((0, 0.001, -1)).normalized().to_track_quat("-Z", "Y")   # Kamera 'oben' (blickt entlang -Z): _q ist die Kameradrehung; rechts/oben im Bild
RECHTS = -np.array((_q @ Vector((1, 0, 0)))[:]); OBEN = -np.array((_q @ Vector((0, 1, 0)))[:])   # an gerenderten Bildern geprueft: Bild = um 180 Grad gedreht gegenueber der Kameradrehung der Rechnung
def ansicht(q): return np.array([q @ RECHTS, q @ OBEN]) * 1000   # Bildkoordinaten in mm (rechts, oben)

def zu_liste(p):
    f = p["f"]; dm = p["daumen"]
    return [p["rollen"], p["gegenhand"]] + [f[str(k)][i] for k in (2, 3, 4, 5) for i in range(4)] + [dm["swing"]] + dm["flex"] + dm["dreh"] + [p["wurzel"][1] - 180.0] + list(p.get("mc", [0, 0, 0, 0]))
def aus_liste(x, basis):
    p = copy.deepcopy(basis); p["rollen"], p["gegenhand"] = x[0], x[1]
    for n, k in enumerate((2, 3, 4, 5)):
        p["f"][str(k)] = [0.0, 0.0, 0.0, x[5 + 4 * n]] if GERADE else [x[2 + 4 * n + i] for i in range(4)]   # GERADE: alle Glieder gestreckt, je Finger nur die Neigung (Spreizung) frei, damit sie sich aneinanderlegen
    p["daumen"] = {"swing": x[18], "flex": x[19:22], "dreh": x[22:25]}
    p["wurzel"] = [p["wurzel"][0], 180.0 + x[25], p["wurzel"][2]]; p["mc"] = list(x[26:30])   # Drehung der ganzen Hand in der Bildebene (Neigung der Fingerachsen gegen die Senkrechte)
    return p
GRENZ = [(0, 0), (-6, 28)] + [(-12, 12), (-12, 12), (-12, 12), (-40, 40)] * 4 + [(-50, 40), (-45, 45), (-45, 60), (-60, 60), (-60, 60), (-60, 60), (-60, 60), (-35, 35)] + [(-15, 15)] * 4   # Daumen nahezu gestreckt (Foto der Nutzerhand), kein Beugen zum Handruecken
def kosten(x, basis):
    """Kosten: Silhouettenbreite in der Betrachter-Ansicht (Welt-X, Neigung der Handachse herausgerechnet), Versatz der Fingergelenke in X
    (Finger decken sich hintereinander), Finger parallel (Richtungsunterschied), Daumen anliegend, Handachse leicht geneigt (Foto), Durchdringung."""
    p = aus_liste(x, basis); d = T.text_pose(p)
    V = T.hand_punkte(d)
    vz = -1.0 if abs(basis['wurzel'][1]) > 90 else 1.0   # Hand liegt bei 180 Grad Drehung bei negativem y
    pb = T.rig.pose.bones; W = T.rig.matrix_world
    P_ = lambda b, a: np.array((W @ getattr(pb[b], a))[:])
    lg = P_("finger3-3.R", "tail") - P_("finger3-1.R", "head"); lean = lg[0] / abs(lg[1])
    hand = V[vz * V[:, 1] > 0.045]; xh = hand[:, 0] - lean * vz * hand[:, 1]   # Hoehe entlang der Handachse = vz * y
    lo, hi = np.percentile(xh, [1, 99]); breite = (hi - lo) * 1000
    inter, zz = T.messen_pose(d)
    ff = sum(n for (a_, b_), n in zz.items() if a_ != b_ and a_.startswith('F') and b_.startswith('F')); inter = (inter - ff) + 0.1 * ff   # Finger/Finger kaum bestraft (im Spiel unsichtbar)
    punkte = [P_(f"finger{k}-{g}.R", a) for k in (2, 3, 4, 5) for g, a in ((1, "head"), (2, "head"), (3, "head"), (3, "tail"))]
    xv = np.array([q[0] - lean * vz * q[1] for q in punkte]); stapel = (xv.max() - xv.min()) * 1000   # Versatz der Fingergelenke in X (mm)
    dirs = [(P_(f"finger{k}-3.R", "tail") - P_(f"finger{k}-1.R", "head")) for k in (2, 3, 4, 5)]
    dirs = [v / np.linalg.norm(v) for v in dirs]; parallel = sum(np.linalg.norm(v - dirs[1]) for v in dirs) * 100   # 0,1 = ca. 6 Grad Abweichung
    tip = P_("finger1-3.R", "tail"); a2 = P_("finger2-2.R", "head"); b2 = P_("finger2-2.R", "tail"); m2 = (a2 + b2) / 2; ax = (P_("finger2-3.R", "tail") - P_("finger2-1.R", "head")); ax /= np.linalg.norm(ax)
    axial = abs(float((tip - m2) @ ax)) * 1000; seit = np.linalg.norm((tip - m2) - ((tip - m2) @ ax) * ax) * 1000
    daumen = 0.5 * axial   # Daumenspitze am Mittelglied des Zeigefingers, seitlich anliegend
    neig = 200.0 * abs(lean - 0.035)   # Fingerachsen etwa 2 Grad gegen die Senkrechte (Foto); Handgelenk dagegen bis ~25 Grad abgeknickt (Manschette neigt sich)
    neig += 0.15 * abs(x[1] - 25.0)
    # Foto: Daumenprofil ">": Knick (Grundgelenk) nach aussen zum Handruecken (Bild rechts), Spitze zurueck zum Betrachter (links)
    t0 = ansicht(P_("finger1-1.R", "head")); t1 = ansicht(P_("finger1-1.R", "tail")); t3 = ansicht(P_("finger1-3.R", "tail"))
    knick = t1[0] - (t0[0] + t3[0]) / 2   # mm, Soll etwa +10 (Foto: rund 12)
    daumen += 3.0 * abs(knick - 10.0) + 120.0 * max(0.0, 7.0 - knick)   # Knick darf auf >= +4 mm sinken (Auftraggeber), Vorzeichen positiv
    d1 = ansicht(P_("finger1-2.R", "tail")) - t1; d2 = t3 - ansicht(P_("finger1-2.R", "tail"))   # zweiter Daumenknick (nahe der Spitze) soll weg
    daumen += 20.0 * abs(np.degrees(np.arctan2(d2[0], d2[1])) - np.degrees(np.arctan2(d1[0], d1[1])))
    achsen = []
    for k in (2, 3, 4, 5):
        v = ansicht(P_(f"finger{k}-3.R", "tail")) - ansicht(P_(f"finger{k}-1.R", "head")); achsen.append(np.degrees(np.arctan2(v[0], v[1])))
    winkel = max(achsen) - min(achsen)   # Fingerachsen zueinander in der Betrachter-Ansicht, Grad (Ziel < 3)
    parallel += 20.0 * max(0.0, winkel - 1.5)
    fan = max(np.degrees(np.arccos(np.clip(dirs[i] @ dirs[j], -1, 1))) for i in range(4) for j in range(i + 1, 4))   # Faecherwinkel der vier Finger zueinander (3D, Grad)
    td = P_("finger1-3.R", "tail") - P_("finger1-1.R", "head"); td /= np.linalg.norm(td)
    dw = float(np.degrees(np.arccos(np.clip(td @ dirs[0], -1, 1))))   # Daumenachse zur Zeigefingerachse (3D, Grad), Foto ~ 15-20
    daumen += 90.0 * max(0.0, dw - float(os.environ.get('DWMAX', '12')))   # harte Ziele: Daumenachse zur Zeigefingerachse <= 8 Grad
    lat = np.linalg.norm((P_('finger1-3.R', 'tail') - P_('finger2-1.R', 'head')) - ((P_('finger1-3.R', 'tail') - P_('finger2-1.R', 'head')) @ dirs[0]) * dirs[0]) * 1000
    daumen += 10.0 * max(0.0, lat - float(os.environ.get('LATMAX', '28')))   # Spitzenabstand zur Zeigefingerachse <= 30 mm
    daumen += 5.0 * max(0.0, breite - 70.0)   # Silhouette <= 73 mm halten
    e1 = P_("finger1-2.R", "head"); e2 = P_("finger1-2.R", "tail"); e3 = P_("finger1-3.R", "tail")
    u_, v_ = (e2 - e1) / np.linalg.norm(e2 - e1), (e3 - e2) / np.linalg.norm(e3 - e2)
    knick2 = float(np.degrees(np.arccos(np.clip(u_ @ v_, -1, 1))))   # zweiter Knick (Grundglied -> Endglied) 3D, Grad: verboten
    daumen += 40.0 * max(0.0, knick2 - 3.0)
    FANK = 90.0 * max(0.0, fan - 3.0)   # Finger dicht zusammen und parallel (Nutzer): Faecherwinkel moeglichst unter 3 Grad
    return breite + GW * inter + 0.0 * knick2 + 5.0 * stapel + FANK + 0.1 * parallel + 0.5 * daumen + neig, breite, inter, stapel, winkel, knick, fan
basis = T.PARAM; x = zu_liste(basis); GRENZ[0] = (x[0] - 25, x[0] + 25)   # Rollwinkel um den Startwert suchen
best = kosten(x, basis)
if os.environ.get("SCAN"):   # Anzeige: Parameter Nr. SCAN ueber seinen Bereich abtasten (Kosten, Breite, Durchdringung, Fingerversatz, Fingerwinkel, Daumenknick)
    n_ = int(os.environ["SCAN"]); lo_, hi_ = GRENZ[n_]
    for k_ in range(11):
        y = list(x); y[n_] = lo_ + (hi_ - lo_) * k_ / 10; print("SCAN", n_, round(y[n_], 1), [round(float(v), 1) for v in kosten(y, basis)], flush=True)
    sys.exit(0)
if os.environ.get("GRID"):   # Zwei Parameter gemeinsam abtasten (gekoppelt), bestes Paar uebernehmen und Feinsuche weiter
    n1, n2 = [int(v) for v in os.environ["GRID"].split(",")]
    for u in range(16):
        for v in range(16):
            y = list(x); y[n1] = GRENZ[n1][0] + (GRENZ[n1][1] - GRENZ[n1][0]) * u / 15; y[n2] = GRENZ[n2][0] + (GRENZ[n2][1] - GRENZ[n2][0]) * v / 15
            c = kosten(y, basis)
            if c[0] < best[0]: x, best = y, c; print("GRID", round(y[n1], 1), round(y[n2], 1), [round(float(w), 1) for w in best], flush=True)
N0 = int(os.environ.get("ZUFALL", "0"))   # Zufallsstarts nur fuer den Daumen (Indizes 18-24), danach Feinsuche
for _ in range(N0):
    y = list(x)
    for i in range(18, 25): y[i] = random.uniform(*GRENZ[i])
    c = kosten(y, basis)
    if c[0] < best[0]: x, best = y, c
print("START", [round(v, 1) for v in best], flush=True)
schritt = 8.0
for r in range(runden):
    for i in range(len(x)):
        if FREEZE and not 18 <= i <= 24: continue
        if GERADE and 2 <= i <= 17 and (i - 5) % 4 != 0: continue
        for sgn in (1, -1):
            for _ in range(2):
                y = list(x); y[i] = min(GRENZ[i][1], max(GRENZ[i][0], y[i] + sgn * schritt * random.uniform(0.3, 1.0)))
                c = kosten(y, basis)
                if c[0] < best[0] - 1e-6: x, best = y, c
    schritt *= 0.65
    print("RUNDE", r, [round(v, 1) for v in best], flush=True)
    json.dump(aus_liste(x, basis), open(aus, "w"), indent=1)
# (1+1)-Evolutionsstrategie auf allen freien Parametern gemeinsam (gekoppelte Parameter wie Mittelhand und Fingerneigung laufen mit)
ES = int(os.environ.get("ES", "0")); sig = 3.0
aktiv = [i for i in range(len(x)) if (not FREEZE or 18 <= i <= 24) and not (GERADE and 2 <= i <= 17 and (i - 5) % 4 != 0) and GRENZ[i][0] != GRENZ[i][1]]
for it in range(ES):
    y = list(x)
    for i in aktiv:
        if random.random() < 0.5: y[i] = min(GRENZ[i][1], max(GRENZ[i][0], y[i] + random.gauss(0, sig)))
    c = kosten(y, basis)
    if c[0] < best[0] - 1e-9: x, best = y, c
    if it % 150 == 149:
        sig = max(0.4, sig * 0.8); print("ES", it, [round(float(v), 1) for v in best], flush=True); json.dump(aus_liste(x, basis), open(aus, "w"), indent=1)
print("ENDE", [round(v, 2) for v in best], json.dumps(aus_liste(x, basis)))
















