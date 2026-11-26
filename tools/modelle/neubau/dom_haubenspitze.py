"""Spitze der Turmhaube (Dom evangelisch): Turmkugel und Wetterhahn, als reine Zahlen (ohne Blender), damit das Bauskript des Doms und der
Export der Haube als eigenes Modell dieselbe Form benutzen. Alle Masse in Metern, relativ zu Z_KNAUF (Mittelpunkt der Kugel)."""
import math

KUGEL_R = 0.27                      # Kugel 0,54 m Durchmesser
STANGE_BIS = 0.20                   # kurze Stange ueber der Kugel: von Kugelmitte + 0,20 bis zum Fuss des Hahns
HAHN_FUSS = 0.40                    # Fuss des Hahns ueber der Kugelmitte
HAHN_DICKE = 0.03                   # Blech: zwei Flaechen im Abstand von 3 cm
# Umriss des Hahns (x nach vorn zum Schnabel, z nach oben), gegen den Uhrzeigersinn, Fuss bei z = 0; Hoehe 0,88 m
HAHN = [(-0.05, 0.00), (0.12, 0.00), (0.10, 0.18), (0.22, 0.28), (0.28, 0.45), (0.34, 0.60), (0.44, 0.58), (0.35, 0.66), (0.34, 0.76),
        (0.30, 0.72), (0.27, 0.80), (0.23, 0.72), (0.20, 0.78), (0.17, 0.62), (0.12, 0.45), (0.05, 0.55), (-0.10, 0.80), (-0.26, 0.88),
        (-0.30, 0.70), (-0.34, 0.62), (-0.22, 0.50), (-0.26, 0.38), (-0.20, 0.25), (-0.05, 0.18)]
HAHN_HOEHE = max(z for _, z in HAHN)
SPITZE_HOCH = HAHN_FUSS + HAHN_HOEHE        # hoechster Punkt ueber der Kugelmitte


def kugel_profil(zc, r=KUGEL_R, ringe=6):
    """Halbkreis (Radius, Hoehe) von unten nach oben fuer einen Drehkoerper."""
    return [(0.0, zc - r)] + [(r * math.sin(math.pi * i / (ringe + 1)), zc - r * math.cos(math.pi * i / (ringe + 1))) for i in range(1, ringe + 1)] + [(0.0, zc + r)]


def _flaeche2(p):
    return sum(p[i][0] * p[(i + 1) % len(p)][1] - p[(i + 1) % len(p)][0] * p[i][1] for i in range(len(p))) / 2


def dreiecke(punkte=HAHN):
    """Zerlegt den (nicht konvexen) Umriss in Dreiecke (Ohrenschnitt); jedes Dreieck als drei Indizes, gegen den Uhrzeigersinn."""
    idx = list(range(len(punkte)))
    out = []
    def innen(a, b, c, p):
        d1 = (b[0] - a[0]) * (p[1] - a[1]) - (b[1] - a[1]) * (p[0] - a[0])
        d2 = (c[0] - b[0]) * (p[1] - b[1]) - (c[1] - b[1]) * (p[0] - b[0])
        d3 = (a[0] - c[0]) * (p[1] - c[1]) - (a[1] - c[1]) * (p[0] - c[0])
        return d1 >= 0 and d2 >= 0 and d3 >= 0
    while len(idx) > 3:
        for k in range(len(idx)):
            i0, i1, i2 = idx[k - 1], idx[k], idx[(k + 1) % len(idx)]
            a, b, c = punkte[i0], punkte[i1], punkte[i2]
            if (b[0] - a[0]) * (c[1] - a[1]) - (b[1] - a[1]) * (c[0] - a[0]) <= 1e-12: continue      # nach innen geknickt
            if any(innen(a, b, c, punkte[j]) for j in idx if j not in (i0, i1, i2)): continue
            out.append((i0, i1, i2)); idx.pop(k); break
        else:
            raise ValueError("Umriss nicht zerlegbar")
    out.append(tuple(idx))
    return out


assert _flaeche2(HAHN) > 0
