"""Formen, Maße, Materialien und Zeiten der drei Buecher. buch_bau.py setzt daraus Netze, Gerueest und Clips zusammen.
Alle Maße in Metern; "Offenkoordinaten": Ruecken-Achse bei x = 0 (aufgeschlagen im Ursprung), Block rechts (+x), der Vorderdeckel dreht um (x = 0, z = z_h)."""
import math

from mathutils import Vector

import buch_texturen as tx

SEGMENTE = 6   # Abschnitte des umgeblaetterten Blatts
# 1/Meter fuer die dreiplanare UV-Abbildung von Flaechen ohne eigene Abbildung
SKALA = {"Buch_Leder": 10.0, "Buch_Riemen": 12.0, "Buch_Schnitt": 33.0, "Buch_Pergament": 10.0}

STILE = {
    # Personalbuch (Lesepult der Werkstatt): dicker Block, derbes dunkles Leder, heller Riemen mit Messingschnalle, Eisenbeschlaege
    "personal": dict(
        lagen=9, W=0.20, D=0.29, c=0.008, a=0.030, s=0.012, g=0.003, o=0.010, inhalt_rand=0.012,
        rand_mat="Buch_Leder", anhang=("Riemen",),
        auf_ang=[(0, 0.0), (18, 180.0), (22, 175.0), (26, 180.0), (28, 179.0), (31, 180.0)], zu_ang=[(0, 180.0), (26, 0.0)],
        blatt_bilder=22, blatt_biegung=0.15, schwelle_grad=172.0),
    # Familienbuch (Stubentisch des Wohnhauses): dunkelrotes Leder, Goldrahmen, Wappenfeld, Goldschnitt, zwei Messing-Schliessen
    "familie": dict(
        lagen=8, W=0.19, D=0.27, c=0.007, a=0.020, s=0.010, g=0.003, o=0.007, inhalt_rand=0.012,
        rand_mat="Buch_Leder", anhang=("Schliesse",), wappen=(0.085, 0.105),
        auf_ang=[(0, 0.0), (15, 180.0), (19, 174.0), (23, 180.0), (25, 178.5), (28, 180.0)], zu_ang=[(0, 180.0), (24, 0.0)],
        blatt_bilder=22, blatt_biegung=0.16, schwelle_grad=172.0),
    # Kontorbuch (Schreibpult der Werkstatt): schlankes Rechnungsbuch, Pergamentdeckel, Halbledereinband mit Ruecken-Schnueren, Daumenregister
    "kontor": dict(
        lagen=14, W=0.23, D=0.31, c=0.004, a=0.013, s=0.008, g=0.003, o=0.006, inhalt_rand=0.010,
        rand_mat="Buch_Pergament", anhang=(),
        auf_ang=[(0, 0.0), (12, 180.0), (15, 176.0), (18, 180.0), (20, 179.2), (22, 180.0)], zu_ang=[(0, 180.0), (20, 0.0)],
        blatt_bilder=18, blatt_biegung=0.18, schwelle_grad=172.0),
}


def lade_texturen(B, bild_laden, mat_bild, mat_flaeche, mat_durchsichtig):
    """Legt B.mats an: Namen Buch_* (Einband, Leder, Seiten, Schnitt, ...), Buch_Inhalt (durchsichtig) und, wenn das Buch ein Wappenfeld hat, Buch_Wappenfeld."""
    art, aus, P = B.art, B.aus, B.P
    M = B.mats = {}

    def bild(n, arr):
        return bild_laden(aus, n, arr, f"{art}_{n}")

    M["Buch_Inhalt"] = mat_durchsichtig("Buch_Inhalt")
    if art == "personal":
        lk = tx.leder(4001, (0.30, 0.19, 0.11), falten=0.6)
        M["Buch_Leder"] = mat_bild("Buch_Leder", bild("leder", lk), 0.85)
        M["Buch_Einband"] = mat_bild("Buch_Einband", bild("einband", tx.einband_personal(lk)), 0.85)
        M["Buch_Riemen"] = mat_bild("Buch_Riemen", bild("riemen", tx.riemen()), 0.8)
        M["Buch_Seiten"] = mat_bild("Buch_Seiten", bild("seiten", tx.seiten_personal()), 0.95)
        M["Buch_Schnitt"] = mat_bild("Buch_Schnitt", bild("schnitt", tx.schnitt_personal()), 0.95)
        M["Buch_Messing"] = mat_flaeche("Buch_Messing", (0.74, 0.58, 0.26), 0.45)
        M["Buch_Eisen"] = mat_flaeche("Buch_Eisen", (0.17, 0.17, 0.18), 0.6)
    elif art == "familie":
        lk = tx.leder(4002, (0.42, 0.10, 0.12), falten=0.4, narbung=0.12)
        fu = P["wappen"][0] / 2.0 / B.Wc
        fv = P["wappen"][1] / 2.0 / B.Dc
        M["Buch_Leder"] = mat_bild("Buch_Leder", bild("leder", lk), 0.7)
        M["Buch_Einband"] = mat_bild("Buch_Einband", bild("einband", tx.einband_familie(lk, fu, fv)), 0.7)
        M["Buch_Seiten"] = mat_bild("Buch_Seiten", bild("seiten", tx.seiten_familie()), 0.9)
        M["Buch_Schnitt"] = mat_bild("Buch_Schnitt", bild("schnitt", tx.schnitt_familie()), 0.6)
        M["Buch_Gold"] = mat_flaeche("Buch_Gold", (0.88, 0.70, 0.30), 0.4)
        M["Buch_Wappenfeld"] = mat_flaeche("Buch_Wappenfeld", (0.20, 0.30, 0.60), 0.8)
    else:
        lk = tx.leder(4003, (0.33, 0.22, 0.13), falten=0.8)
        M["Buch_Leder"] = mat_bild("Buch_Leder", bild("leder", lk), 0.85)
        M["Buch_Einband"] = mat_bild("Buch_Einband", bild("einband", tx.einband_kontor()), 0.9)
        M["Buch_Pergament"] = mat_bild("Buch_Pergament", bild("pergamentkante", tx.pergamentkante()), 0.9)
        M["Buch_Reiter"] = mat_flaeche("Buch_Reiter", (0.58, 0.20, 0.15), 0.85)
        M["Buch_Seiten"] = mat_bild("Buch_Seiten", bild("seiten", tx.seiten_kontor()), 0.95)
        M["Buch_Schnitt"] = mat_bild("Buch_Schnitt", bild("schnitt", tx.schnitt_kontor()), 0.95)


def wappenfeld(B):
    """Ecken (xa, xb, ya, yb, z) der Wappenfeld-Flaeche auf dem Vorderdeckel (Ruhelage zu) oder None."""
    w = B.P.get("wappen")
    if not w:
        return None
    xc = B.s + B.Wc / 2.0
    return (xc - w[0] / 2, xc + w[0] / 2, -w[1] / 2, w[1] / 2, B.H + 0.0008)


def blatt(B, T):
    """Das umgeblaetterte Blatt: SEGMENTE Abschnitte, Gelenkzeilen mit je 0,5 auf beiden Nachbarn (Blatt biegt sich), Ruhelage rechts auf dem Block."""
    seg = SEGMENTE
    W, D = B.W, B.D
    L = W / seg
    xs = [B.xb + j * L for j in range(seg + 1)]
    top, bot = B.z_h + 0.0006, B.z_h - 0.0006
    y0, y1 = -D / 2, D / 2

    def gw(j):
        if j == 0:
            return {"Blatt_1": 1.0}
        if j == seg:
            return {f"Blatt_{seg}": 1.0}
        return {f"Blatt_{j}": 0.5, f"Blatt_{j + 1}": 0.5}
    for i in range(seg):
        a, b = xs[i], xs[i + 1]
        spec_o = ("box", B.xb, B.xb + W, y0, y1, False)
        spec_u = ("box", B.xb, B.xb + W, y0, y1, True)
        for z, n, sp in ((top, (0, 0, 1), spec_o), (bot, (0, 0, -1), spec_u)):
            pts = [(a, y0, z), (b, y0, z), (b, y1, z), (a, y1, z)]
            T.flaeche(pts, n, "Buch_Seiten", [T.uv_von(Vector(p), Vector(n), sp, "Buch_Seiten") for p in pts], [gw(i), gw(i + 1), gw(i + 1), gw(i)])
        for y, n in ((y0, (0, -1, 0)), (y1, (0, 1, 0))):
            pts = [(a, y, bot), (b, y, bot), (b, y, top), (a, y, top)]
            T.flaeche(pts, n, "Buch_Schnitt", [T.uv_von(Vector(p), Vector(n), None, "Buch_Schnitt") for p in pts], [gw(i), gw(i + 1), gw(i + 1), gw(i)])
    x = xs[-1]
    pts = [(x, y0, bot), (x, y1, bot), (x, y1, top), (x, y0, top)]
    T.flaeche(pts, (1, 0, 0), "Buch_Schnitt", [T.uv_von(Vector(p), Vector((1, 0, 0)), None, "Buch_Schnitt") for p in pts], [gw(seg)] * 4)


def _lagen_block(B, T, z0, z1, bone, flaeche_oben, lagen, saat):
    """Seitenblock aus `lagen` Stapeln, deren Kanten um Zehntelmillimeter versetzt sind (unregelmaessige Blattkante). Die Lage an der Blattflaeche ist voll gross.
    flaeche_oben True: Blattflaeche liegt oben (rechter Block), sonst unten (linker Block in 'zu')."""
    W, D = B.W, B.D
    dz = (z1 - z0) / lagen
    spec = ("box", B.xb, B.xb + W, -D / 2, D / 2, not flaeche_oben)
    for k in range(lagen):
        # k = 0 ist die Lage an der Blattflaeche
        off = 0.0 if k == 0 else (((k * 37 + saat * 11) % 5) + 1) * 0.00032
        if flaeche_oben:
            za, zb = z1 - (k + 1) * dz, z1 - k * dz
        else:
            za, zb = z0 + k * dz, z0 + (k + 1) * dz
        sei = {"+z": "Buch_Seiten", "-z": None, "*": "Buch_Schnitt"} if flaeche_oben else {"-z": "Buch_Seiten", "+z": None, "*": "Buch_Schnitt"}
        T.box(B.xb, B.xb + W - off, -D / 2 + off, D / 2 - off, za, zb, bone, sei, {"+z": spec, "-z": spec})


def _deckel(B, T, bone, z0, z1, oben_mat, rm, aussen_oben=True):
    """Deckel als Brett mit aufgesetzter, eingerueckter Platte (Stufe an der Kante: Dicke und Rand lesen sich auch im Pixel-Look)."""
    s, Wc, Dc = B.s, B.Wc, B.Dc
    ins = 0.0022
    dick = z1 - z0
    plat = dick * 0.42
    if aussen_oben:        # Platte oben (Vorderdeckel in 'zu'): Aussenflaeche mit Einband
        T.box(s, s + Wc, -Dc / 2, Dc / 2, z0, z1 - plat, bone, {"*": rm})
        T.box(s + ins, s + Wc - ins, -Dc / 2 + ins, Dc / 2 - ins, z1 - plat, z1, bone, {"+z": oben_mat, "-z": None, "*": rm},
              {"+z": ("box", s + ins, s + Wc - ins, -Dc / 2 + ins, Dc / 2 - ins, False)})
    else:                  # Rueckdeckel: Platte innen oben, Unterseite nie sichtbar
        T.box(s, s + Wc, -Dc / 2, Dc / 2, z0, z1 - plat, bone, {"*": rm, "-z": None})
        T.box(s + ins, s + Wc - ins, -Dc / 2 + ins, Dc / 2 - ins, z1 - plat, z1, bone, {"-z": None, "*": rm})


def teile_gemeinsam(B, T):
    """Deckel, Bloecke (in Lagen), Ruecken (zwei Haelften, abgeschraegt), Blatt - in der Anordnung 'zu'."""
    s, c, a = B.s, B.c, B.a
    Dc, zh, H = B.Dc, B.z_h, B.H
    rm = B.P["rand_mat"]
    lagen = B.P.get("lagen", 5)
    _deckel(B, T, "Deckel_hinten", 0.0, c, rm, rm, aussen_oben=False)
    _lagen_block(B, T, c, B.z_t, "Deckel_hinten", True, lagen, 1)
    _deckel(B, T, "Deckel_vorn", H - c, H, "Buch_Einband", rm, aussen_oben=True)
    _lagen_block(B, T, zh + 0.0006, zh + 0.0006 + a, "Deckel_vorn", False, lagen, 2)
    # Ruecken: untere Haelfte am Rueckdeckel, obere am Vorderdeckel (nach dem Aufklappen liegen beide als Falz zwischen den Bloecken). Aussenkanten abgeschraegt.
    ch = min(0.0045, s * 0.5, zh * 0.4)
    T.prisma([(ch, 0.0), (s, 0.0), (s, zh), (0.0, zh), (0.0, ch)], -Dc / 2, Dc / 2, "Deckel_hinten",
             lambda n: None if (n[0] > 0.9 or n[2] < -0.9) else "Buch_Leder")
    T.prisma([(0.0, zh), (s, zh), (s, H), (ch, H), (0.0, H - ch)], -Dc / 2, Dc / 2, "Deckel_vorn",
             lambda n: None if n[0] > 0.9 else "Buch_Leder")
    blatt(B, T)


def _baender(B, T, ys, breit, dick, mat):
    """Ruecken-Baender (Schnuere): je ein Stueck auf beiden Haelften, quer ueber den Ruecken."""
    ch = min(0.0045, B.s * 0.5, B.z_h * 0.4)
    for y in ys:
        T.box(-dick, 0.0, y - breit / 2, y + breit / 2, ch, B.z_h, "Deckel_hinten", {"+z": None, "-z": None, "+x": None, "*": mat})
        T.box(-dick, 0.0, y - breit / 2, y + breit / 2, B.z_h, B.H - ch, "Deckel_vorn", {"-z": None, "+x": None, "+z": None, "*": mat})


def _niete(T, cx, cy, z, bone, r=0.0065, h=0.0035, mat="Buch_Eisen"):
    T.zylinder(cx, cy, r, z, z + h, bone, mat, n=8)


def _wappen_rahmen(B, T, mat):
    xa, xb, ya, yb, z = wappenfeld(B)
    r = 0.0065
    z0 = B.H
    h = 0.0016
    T.box(xa - r, xb + r, ya - r, ya, z0, z0 + h, "Deckel_vorn", {"-z": None, "*": mat})
    T.box(xa - r, xb + r, yb, yb + r, z0, z0 + h, "Deckel_vorn", {"-z": None, "*": mat})
    T.box(xa - r, xa, ya, yb, z0, z0 + h, "Deckel_vorn", {"-z": None, "*": mat})
    T.box(xb, xb + r, ya, yb, z0, z0 + h, "Deckel_vorn", {"-z": None, "*": mat})
    for x in (xa - r, xb + r):
        for y in (ya - r, yb + r):
            T.zylinder(x, y, 0.0048, z0, z0 + 0.0034, "Deckel_vorn", mat, n=8)


def teile_besonders(B, T):
    art = B.art
    s, c, Wc, Dc, H, zh = B.s, B.c, B.Wc, B.Dc, B.H, B.z_h
    xe = s + Wc
    hz = H - c / 2
    if art == "personal":
        _baender(B, T, (-0.085, -0.03, 0.03, 0.085), 0.013, 0.004, "Buch_Leder")
        # Riemen: Platte auf dem Deckel (am Deckel), Lasche ueber die Kante nach unten (am Knochen Riemen), Messingschnalle als Rahmen darauf
        T.box(xe - 0.105, xe + 0.0035, -0.022, 0.022, H, H + 0.003, "Deckel_vorn", {"-z": None, "*": "Buch_Riemen"})
        T.box(xe, xe + 0.0035, -0.022, 0.022, hz - 0.070, H, "Riemen", {"-z": None, "-x": None, "*": "Buch_Riemen"})
        bx0, bx1 = xe + 0.0035, xe + 0.0072
        zb0, zb1 = hz - 0.062, hz - 0.030
        T.box(bx0, bx1, -0.028, -0.021, zb0, zb1, "Riemen", {"-x": None, "*": "Buch_Messing"})
        T.box(bx0, bx1, 0.021, 0.028, zb0, zb1, "Riemen", {"-x": None, "*": "Buch_Messing"})
        T.box(bx0, bx1, -0.021, 0.021, zb0, zb0 + 0.006, "Riemen", {"-x": None, "*": "Buch_Messing"})
        T.box(bx0, bx1, -0.021, 0.021, zb1 - 0.006, zb1, "Riemen", {"-x": None, "*": "Buch_Messing"})
        T.box(bx0, bx1 + 0.0012, -0.0025, 0.0025, zb0 + 0.004, zb1 - 0.004, "Riemen", {"-x": None, "*": "Buch_Eisen"})      # Dorn
        T.box(xe, xe + 0.006, -0.007, 0.007, 0.002, 0.006, "Deckel_hinten", {"-z": None, "*": "Buch_Eisen"})              # Haken am Rueckdeckel
        # Eisenbeschlaege: Eckplatten mit Nieten auf dem Vorderdeckel
        for sx in (-1, 1):
            for sy in (-1, 1):
                x0 = xe - 0.034 if sx > 0 else s
                y0 = (Dc / 2 - 0.034) if sy > 0 else -Dc / 2
                T.box(x0, x0 + 0.034, y0, y0 + 0.034, H, H + 0.0015, "Deckel_vorn", {"-z": None, "*": "Buch_Eisen"})
                _niete(T, x0 + 0.017, y0 + 0.017, H + 0.0015, "Deckel_vorn", r=0.0055, h=0.003, mat="Buch_Messing")
        for y in (-0.06, 0.06):
            _niete(T, s + 0.012, y, H, "Deckel_vorn", r=0.0045, h=0.0028)
        # Nietenreihen entlang Kopf- und Fussrand und an der Vorderkante (Arbeitsbuch: zusaetzlich vernietet)
        for k in range(5):
            x = s + 0.045 + k * 0.03
            for sy in (-1, 1):
                _niete(T, x, sy * (Dc / 2 - 0.009), H, "Deckel_vorn", r=0.0032, h=0.0022)
        for k in range(5):
            y = -0.08 + k * 0.04
            if abs(y) > 0.03:
                _niete(T, xe - 0.009, y, H, "Deckel_vorn", r=0.0032, h=0.0022)
    elif art == "familie":
        _baender(B, T, (-0.09, -0.03, 0.03, 0.09), 0.007, 0.0025, "Buch_Gold")
        ecke = 0.036
        for sx in (-1, 1):
            for sy in (-1, 1):
                x0 = xe - ecke if sx > 0 else s
                y0 = (Dc / 2 - ecke) if sy > 0 else -Dc / 2
                T.box(x0, x0 + ecke, y0, y0 + ecke, H, H + 0.0016, "Deckel_vorn", {"-z": None, "*": "Buch_Gold"})
                T.zylinder(x0 + ecke / 2, y0 + ecke / 2, 0.0065, H + 0.0016, H + 0.0034, "Deckel_vorn", "Buch_Gold", n=8)
        _wappen_rahmen(B, T, "Buch_Gold")
        # Zierstifte in Reihen am Rand zwischen den Eckstuecken
        for k in range(1, 5):
            x = s + 0.012 + k * (Wc - 0.024) / 5
            for sy in (-1, 1):
                T.zylinder(x, sy * (Dc / 2 - 0.009), 0.0024, H, H + 0.0016, "Deckel_vorn", "Buch_Gold", n=6)
        for k in range(1, 8):
            y = -Dc / 2 + k * Dc / 8
            if abs(y) < 0.1:
                for xx in (s + 0.009, xe - 0.009):
                    T.zylinder(xx, y, 0.0024, H, H + 0.0016, "Deckel_vorn", "Buch_Gold", n=6)
        for y in (-0.07, 0.07):
            T.box(xe - 0.026, xe + 0.0025, y - 0.012, y + 0.012, H, H + 0.0016, "Deckel_vorn", {"-z": None, "*": "Buch_Gold"})
            T.box(xe, xe + 0.0025, y - 0.0115, y + 0.0115, hz - 0.046, H, "Schliesse", {"-z": None, "-x": None, "*": "Buch_Gold"})
            T.box(xe + 0.0025, xe + 0.0048, y - 0.0085, y + 0.0085, hz - 0.046, hz - 0.034, "Schliesse", {"-x": None, "*": "Buch_Gold"})
            T.box(xe, xe + 0.005, y - 0.006, y + 0.006, 0.0015, 0.0055, "Deckel_hinten", {"-z": None, "*": "Buch_Gold"})
    else:
        ys = [-0.11 + 0.055 * k for k in range(5)]
        _baender(B, T, ys, 0.009, 0.003, "Buch_Leder")
        T.box(s, s + 0.05, -Dc / 2, Dc / 2, H, H + 0.0013, "Deckel_vorn", {"-z": None, "*": "Buch_Leder"})
        for sy in (-1, 1):
            y0 = (Dc / 2 - 0.058) if sy > 0 else -Dc / 2
            T.box(xe - 0.058, xe, y0, y0 + 0.058, H, H + 0.0013, "Deckel_vorn", {"-z": None, "*": "Buch_Leder"})
        # Schnuere mit schmalem Wulst obenauf, Kapitalbaender an Kopf und Fuss des Rueckens, ein Lesebaendchen am Fussende
        _baender(B, T, ys, 0.0045, 0.0045, "Buch_Leder")
        for z0, bn, z1 in ((0.0, "Deckel_hinten", B.z_h), (B.z_h, "Deckel_vorn", B.H)):
            for y in (-Dc / 2 + 0.003, Dc / 2 - 0.003):
                T.box(-0.0025, s, y - 0.0025, y + 0.0025, z0 + 0.001, z1 - 0.001, bn, {"+z": None, "-z": None, "+x": None, "*": "Buch_Reiter"})
        T.box(0.0, 0.010, -Dc / 2 - 0.05, -Dc / 2 + 0.004, 0.0015, 0.0035, "Deckel_hinten", {"-z": None, "*": "Buch_Reiter"})
        # Schnuere laufen ueber die Deckelkanten (Heftbuende): kleine Stege auf dem Vorderdeckel am Ruecken
        for y in ys:
            T.box(s, s + 0.05, y - 0.0045, y + 0.0045, H + 0.0013, H + 0.0021, "Deckel_vorn", {"-z": None, "*": "Buch_Leder"})
        # Daumenregister: vier Reiter am Blattrand beider Bloecke (nach dem Aufklappen liegen links und rechts gleich)
        xr = B.xb + B.W
        for k in range(6):
            y = -0.125 + 0.05 * k
            T.box(xr - 0.001, xr + 0.012, y - 0.0155, y + 0.0155, B.z_t - 0.0042, B.z_t - 0.0026, "Deckel_hinten", {"*": "Buch_Reiter", "-x": None})
            T.box(xr - 0.001, xr + 0.012, y - 0.0155, y + 0.0155, zh + 0.0006 + 0.0026, zh + 0.0006 + 0.0042, "Deckel_vorn", {"*": "Buch_Reiter", "-x": None})
