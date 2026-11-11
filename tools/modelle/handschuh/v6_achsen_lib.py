"""Fingerachsen der Faust in der Ansicht von der Handflaeche (wie die Skizze des Nutzers): Linie durch die Gelenkreihe PIP-DIP-Spitze je Finger,
Winkel der Linien (zum Zeigefinger), Schnittpunkt (kleinste Abstandsquadrate), Streuung der Schnittpunkte. Rein lesend."""
import bpy, math
import numpy as np
from bpy_extras.object_utils import world_to_camera_view

def gelenke(rig, k):
    """Weltpositionen MCP, PIP, DIP, Spitze von Finger k (2..5)."""
    M = rig.matrix_world
    p = lambda n, was: np.array((M @ getattr(rig.pose.bones[n], was))[:])
    return [p(f"finger{k}-1.R", "head"), p(f"finger{k}-1.R", "tail"), p(f"finger{k}-2.R", "tail"), p(f"finger{k}-3.R", "tail")]

def bildpunkte(cam, punkte, px):
    sc = bpy.context.scene; out = []
    for P in punkte:
        c = world_to_camera_view(sc, cam, __import__("mathutils").Vector(P.tolist()))
        out.append((c.x * px, (1 - c.y) * px))
    return np.array(out)

def linie(pts):
    """Linie durch Punkte (2D): Mittelpunkt, Richtung (Einheitsvektor, zeigt von der Handwurzel weg in Richtung Fingerspitze)."""
    m = pts.mean(axis=0); u, s, vt = np.linalg.svd(pts - m); d = vt[0]
    if np.dot(pts[-1] - pts[0], d) < 0: d = -d
    return m, d

def schnitt(linien):
    """Punkt mit kleinster Summe der Abstandsquadrate zu allen Linien."""
    A = np.zeros((2, 2)); b = np.zeros(2)
    for m, d in linien:
        N = np.eye(2) - np.outer(d, d); A += N; b += N @ m
    return np.linalg.solve(A, b)

def messen(rig, cam, px, reihe="pip"):
    """reihe: 'pip' = Linie durch PIP, DIP, Spitze; 'mcp' = durch MCP, PIP, DIP, Spitze (gesamter Fingerzug, in der Faust verkuerzt)."""
    L = {}; roh = {}
    for k in (2, 3, 4, 5):
        g = gelenke(rig, k); idx = [1, 2, 3] if reihe == "pip" else [0, 1, 2, 3]
        pts = bildpunkte(cam, [g[i] for i in idx], px); roh[k] = bildpunkte(cam, g, px).tolist(); L[k] = linie(pts)
    S = schnitt(list(L.values()))
    # Richtung der Linie in Grad, gemessen von "Richtung Spitze" gegen die Richtung Zeigefinger
    w = {k: math.degrees(math.atan2(L[k][1][1], L[k][1][0])) for k in L}
    rel = {k: ((w[k] - w[2] + 180) % 360) - 180 for k in L}
    # mittlere Abweichung der paarweisen Schnittpunkte vom Gesamtschnittpunkt
    pw = []
    for a in (2, 3, 4, 5):
        for b in (2, 3, 4, 5):
            if a < b:
                (m1, d1), (m2, d2) = L[a], L[b]; M = np.array([d1, -d2]).T
                if abs(np.linalg.det(M)) > 1e-3: t = np.linalg.solve(M, m2 - m1); pw.append(m1 + t[0] * d1)
    return dict(winkel=w, rel_zeige=rel, schnitt=S.tolist(), paarschnitt=[p.tolist() for p in pw], gelenke_px=roh, linien={k: [L[k][0].tolist(), L[k][1].tolist()] for k in L})

def hand_winkel(rig, reihe="pip"):
    """Fan der Fingerreihen im Handraum (Ebene X-Y): Neigung der Linie PIP-DIP-Spitze gegen die Handachse (-Y, zum Handgelenk),
    positiv = wandert Richtung Handwurzel zur Daumenseite (-X). Dazu Schnittpunkt (x, y in m) der vier Linien und sein Abstand."""
    W = {}; Ls = []
    for k in (2, 3, 4, 5):
        g = gelenke(rig, k); idx = [1, 2, 3] if reihe == "pip" else [0, 1, 2, 3]
        P = np.array([g[i][:2] for i in idx]); m = P.mean(axis=0); d = np.linalg.svd(P - m)[2][0]
        if d[1] > 0: d = -d       # Richtung Handwurzel (-Y)
        W[k] = math.degrees(math.atan2(-d[0], -d[1])); Ls.append((m, d))
    S = schnitt(Ls)
    return W, S
