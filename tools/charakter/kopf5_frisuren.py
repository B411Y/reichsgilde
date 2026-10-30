"""Charakter Phase 5: zehn Frisuren je Geschlecht und zehn Bärte als Leitsträhnen (eigener Code, MIT; Blenders Python).

Vorbild sind die Porträt-Vorlagen der Bild-KI (lokal unter ~/werkzeuge/charakter/vorlagen-haar/, nicht im Repository;
nur angesehen, nichts übernommen): Spätmittelalter um 1400. Die Werkzeuge (Kämmen, Wellen und Klumpen, Zöpfe, Knoten)
kommen aus kopf4_frisuren.py; hier stehen nur die Stile. Reihenfolge = Werte der Gene „frisur“ und „bartform“
(Wert × Anzahl ÷ 256; Bart 0 = rasiert).

Männer: m01 Topfschnitt, m02 Pagenkopf (mit Stirnfransen), m03 Haarrolle, m04 schulterlang mit Mittelscheitel,
m05 kurz geschoren, m06 Stirnfransen, m07 Krauskopf, m08 Tonsur, m09 zerzaust, m10 Halbglatze mit Kranz.
Frauen: f01 zwei Zöpfe, f02 ein Zopf, f03 offen, f04 Flechtkranz, f05 Schnecken, f06 Zopfschlaufen, f07 Nackenknoten,
f08 Haarnetz (nur der große Knoten; das Netz ist Kopfputz, später), f09 hohe Stirn, f10 krause Locken.
Bärte: b01 rasiert (keine Strähnen), b02 Stoppeln, b03 kurzer Vollbart, b04 langer Vollbart, b05 Gabelbart, b06 Kinnbart,
b07 Schnurr- und Kinnbart, b08 Schnurrbart, b09 Backenbart, b10 wilder Vollbart.

Achsen: Blender (z oben, Gesicht nach -y, +x = linke Seite der Figur), Meter.
"""
import math

import numpy as np
from mathutils import Vector

import kopf4_frisuren as k4
from kopf4_frisuren import HINTEN, UNTEN, VORN, Stil, _norm, glatt, knoten_straehnen, umtasten, zopf_bahn, zopf_straehnen


def _streu(w, a=0, b=1):
    """Fester Zufall je Wurzel (0..1), für ungleich geschnittene Enden."""
    return (hash((round(float(w[a]), 5), round(float(w[b]), 5))) % 1000) / 1000


def _schnitt(h, q, stuetzen):
    """Höhe der Schnittlinie an der Richtung von q (0 vorn, 180 hinten); stuetzen: [(Richtung, Höhe), …]."""
    r = float(h.richtung(np.asarray(q))[0])
    return float(np.interp(r, [s[0] for s in stuetzen], [s[1] for s in stuetzen]))


def _vom_wirbel(h, q, vorn=0.3, unten=0.5):
    weg = _norm(Vector(np.asarray(q) - h.wirbel))
    return _norm(weg + VORN * vorn + UNTEN * unten)


def _scheitel(h, q, w, M):
    """Mittelscheitel: zur Seite und über die Ohren nach hinten (wie die Frauenfrisuren der Phase 4)."""
    sx = 1.0 if w[0] >= 0 else -1.0
    hinten = glatt(w[1] - h.kopfmitte[1], 0.0, 0.05 * M)
    return Vector((sx * (1.0 - hinten), 0.35 + 0.4 * hinten, -0.35 - 0.4 * hinten))


def bahn_auf_kopf(h, stuetz, M, abstand=0.012, n=200, glaetten=6):
    """Glatte Bahn (Catmull-Rom) durch Stützpunkte, auf `abstand` über die Haut gelegt (Zöpfe um den Kopf)."""
    stuetz = np.asarray(stuetz, dtype=np.float64)
    p = np.vstack([2 * stuetz[0] - stuetz[1], stuetz, 2 * stuetz[-1] - stuetz[-2]])
    fein = []
    for i in range(1, len(p) - 2):
        for t in np.linspace(0, 1, 40, endpoint=False):
            a0, b, c, d = p[i - 1], p[i], p[i + 1], p[i + 2]
            fein.append(0.5 * ((2 * b) + (-a0 + c) * t + (2 * a0 - 5 * b + 4 * c - d) * t * t + (-a0 + 3 * b - 3 * c + d) * t ** 3))
    fein.append(p[-2])
    fein = np.array(fein)
    for _ in range(3):
        for j in range(len(fein)):
            l, nn, _, _ = h.naechste(fein[j])
            if l is not None:
                fein[j] = l + nn * abstand * M
        for _ in range(glaetten):
            fein[1:-1] = 0.25 * fein[:-2] + 0.5 * fein[1:-1] + 0.25 * fein[2:]
    return umtasten(fein, n)


def _auf_haut(h, q, M, ueber=0.0):
    l, n, _, _ = h.naechste(np.asarray(q, dtype=np.float64))
    return (l + n * ueber * M) if l is not None else np.asarray(q, dtype=np.float64)


class _Objekt:
    """Kleiner Träger für knoten_straehnen (braucht .anker, .anker_n, .M)."""

    def __init__(self, anker, anker_n, M):
        self.anker, self.anker_n, self.M = anker, anker_n, M


# ================================================================= Männer
class Topfschnitt(Stil):
    """m01: Topfschnitt (1420–1460) – vom Wirbel strahlenförmig gekämmt, oben 4–6 cm; scharfe waagrechte Kante rundum
    2–3 cm über den Ohrspitzen, kurze gerade Fransen auf der oberen Stirn; unter der Kante (Schläfen, Seiten, Hinterkopf,
    Nacken) glatt rasiert, Ohren frei."""
    name, geschlecht, anzahl, punkte = "topfschnitt", "m", 5000, 16
    schritt, volumen, volumen_weg, hub, steif = 0.003, (0.0012, 0.006), 0.03, 0.25, 0.32
    einwaerts = 0.5
    wellen, klumpen = (0.0012, 0.04), (14, 0.3)

    def kante(self, r):
        h, M = self.h, self.M
        return np.interp(r, [0, 30, 55, 80, 180], [h.brauen_oben + 0.028 * M, h.brauen_oben + 0.026 * M, h.auge_z + 0.04 * M,
                                                   h.auge_z + 0.045 * M, h.auge_z + 0.045 * M])

    def dichte(self, q):
        # unter der Kante glatt rasiert (nur die Fransen vorn reichen darüber hinaus)
        q = np.atleast_2d(q)
        r = self.h.richtung(q)
        frei = glatt(q[:, 2] - (self.kante(r) - 0.004 * self.M), 0.0, 0.004 * self.M)
        return self.h.kopfhaut(q, ohr_frei=(0.026, 0.036)) * np.where(r < 30, 1.0, frei)

    def kamm(self, q, n, s, w):
        return _vom_wirbel(self.h, q, vorn=0.35, unten=0.55)

    def schwere(self, s, w):
        return 0.55 * glatt(s, 0.01, 0.05)

    def laenge(self, w, n):
        return 0.2

    def ende(self, q, s, w):
        r = float(self.h.richtung(np.asarray(q))[0])
        return q[2] < float(self.kante(r)) + _streu(w) * 0.002 * self.M


class Pagenkopf(Topfschnitt):
    """m02: Pagenkopf – Stirnfransen bis an die Brauen, seitlich glatt bis zum Kiefer, hinten bis in den Nacken; die
    Enden laufen leicht nach innen (weniger glockig als Phase 4)."""
    name, anzahl = "mann_pagen", 5200
    volumen, hub = (0.0012, 0.0055), 0.22
    einwaerts = 1.1

    def dichte(self, q):
        return self.h.kopfhaut(q, ohr_frei=(0.018, 0.026))

    def schwere(self, s, w):
        return 0.9 * glatt(s, 0.015, 0.06)

    def ende(self, q, s, w):
        h, M = self.h, self.M
        z = _schnitt(h, q, [(0, h.brauen_oben + 0.002 * M), (25, h.brauen_oben + 0.001 * M), (45, h.auge_z),
                            (70, h.mund_z - 0.005 * M), (110, h.kinn[2] + 0.004 * M), (180, h.kinn[2] - 0.02 * M)])
        return q[2] < z + _streu(w) * 0.006 * M


class Haarrolle(Stil):
    """m03: Haarrolle (spätes 14. Jh.) – Mittelscheitel, oben glatt anliegend, ohrlang; der untere Rand ist rundum zu
    einer dicken Rolle nach außen eingedreht."""
    name, geschlecht, anzahl, punkte = "haarrolle", "m", 4800, 32
    schritt, volumen, volumen_weg, hub, steif = 0.0035, (0.001, 0.0045), 0.04, 0.2, 0.32
    wellen, klumpen = (0.0008, 0.05), (12, 0.25)
    frei_faellt = 0.6

    def dichte(self, q):
        return self.h.kopfhaut(q, ohr_frei=(0.012, 0.018))

    def kamm(self, q, n, s, w):
        return _norm(_scheitel(self.h, q, w, self.M) + UNTEN * 0.8)

    def schwere(self, s, w):
        return 0.6 * glatt(s, 0.02, 0.06)

    def laenge(self, w, n):
        return 0.25

    def ende(self, q, s, w):
        h, M = self.h, self.M
        # Nutzer 30.10.: das Ohr liegt hinter bzw. in der Rolle (Kante auf Höhe des Ohrläppchens)
        z = _schnitt(h, q, [(0, h.brauen_oben + 0.02 * M), (35, h.auge_z + 0.01 * M), (60, h.ohr_z - 0.024 * M),
                            (120, h.ohr_z - 0.03 * M), (180, h.ohr_z - 0.034 * M)])
        return q[2] < z

    def nach(self, pkt, w, rng):
        """Ende der Strähne nach außen und oben zur Rolle eindrehen (¾ Kreis, Halbmesser 1,0–1,3 cm)."""
        h, M = self.h, self.M
        p_e = pkt[-1]
        t = _norm(Vector(pkt[-1] - pkt[-3]))
        raus = Vector((p_e[0], p_e[1] - h.kopfmitte[1], 0.0))
        raus = _norm(raus - t * raus.dot(t))
        R = rng.uniform(0.012, 0.016) * M
        bogen = rng.uniform(1.25, 1.55) * math.pi
        neu = [Vector(p_e) + (t * math.sin(f) + raus * (1.0 - math.cos(f))) * R for f in np.linspace(0, bogen, 18)[1:]]
        return np.vstack([pkt, np.array([tuple(v) for v in neu])])


class Schulterlang(Stil):
    """m04: schulterlang mit Mittelscheitel, leicht gewellt."""
    name, geschlecht, anzahl, punkte = "schulterlang", "m", 4600, 32
    schritt, volumen, volumen_weg, hub, steif = 0.005, (0.0015, 0.01), 0.05, 0.3, 0.28
    wellen, klumpen = (0.005, 0.06), (12, 0.45)
    einwaerts = 0.3

    def dichte(self, q):
        return self.h.kopfhaut(q, ohr_frei=(0.016, 0.024))

    def kamm(self, q, n, s, w):
        return _norm(_scheitel(self.h, q, w, self.M) + HINTEN * 0.15 * glatt(s, 0.04, 0.12))

    def schwere(self, s, w):
        return 1.0 * glatt(s, 0.02, 0.1)

    def laenge(self, w, n):
        return 0.4

    def ende(self, q, s, w):
        h, M = self.h, self.M
        schulter = (h.schulter["l"][2] + h.schulter["r"][2]) / 2
        z = _schnitt(h, q, [(0, schulter + 0.035 * M), (90, schulter + 0.02 * M), (180, schulter + 0.01 * M)])
        return q[2] < z + _streu(w, 0, 2) * 0.03 * M


class Geschoren(Stil):
    """m05: kurz geschoren – rundum mit der Schere auf etwa 5 mm (etwas ungleich), Kopfform sichtbar, Ohren frei."""
    name, geschlecht, anzahl, punkte = "mann_kurz", "m", 8000, 4
    schritt, volumen, volumen_weg, hub, steif = 0.001, (0.0003, 0.001), 0.003, 0.15, 0.6
    wellen, klumpen = (0.0006, 0.02), (8, 0.15)

    def dichte(self, q):
        return self.h.kopfhaut(q, ohr_frei=(0.026, 0.036))

    def kamm(self, q, n, s, w):
        return _vom_wirbel(self.h, q, vorn=0.3, unten=0.4)

    def schwere(self, s, w):
        return 0.1

    def laenge(self, w, n):
        oben = glatt(w[2] - (self.h.auge_z + 0.05 * self.M), 0, 0.04 * self.M)
        return (0.005 + 0.002 * oben) * self.rng.uniform(0.75, 1.2) * self.M


class Stirnfransen(Stil):
    """m06: kurz und etwas zerzaust (4–6 cm), Fransen fallen nach vorn in die Stirn."""
    name, geschlecht, anzahl, punkte = "stirnfransen", "m", 5200, 16
    schritt, volumen, volumen_weg, hub, steif = 0.003, (0.0015, 0.007), 0.02, 0.35, 0.35
    wellen, klumpen, kraus = (0.002, 0.03), (10, 0.4), 0.12

    def dichte(self, q):
        return self.h.kopfhaut(q, ohr_frei=(0.024, 0.034))

    def kamm(self, q, n, s, w):
        vorn = glatt(-(w[1] - self.h.kopfmitte[1]), 0.0, 0.05 * self.M)
        return _norm(_vom_wirbel(self.h, q, vorn=0.3 + 0.8 * vorn, unten=0.4))

    def schwere(self, s, w):
        return 0.35 * glatt(s, 0.02, 0.05)

    def laenge(self, w, n):
        r = float(self.h.richtung(w)[0])
        return np.interp(r, [0, 60, 120, 180], [0.06, 0.05, 0.04, 0.038]) * self.rng.uniform(0.75, 1.1) * self.M

    def ende(self, q, s, w):
        h, M = self.h, self.M
        return q[2] < h.brauen_oben + (0.004 + 0.012 * _streu(w)) * M and float(h.richtung(np.asarray(q))[0]) < 45


class Krauskopf(Stil):
    """m07: Krauskopf – dichte, kräftige Locken, etwa 8 cm, viel Volumen."""
    name, geschlecht, anzahl, punkte = "krauskopf", "m", 4600, 32
    schritt, volumen, volumen_weg, hub, steif = 0.003, (0.008, 0.038), 0.035, 0.9, 0.3
    wellen, klumpen, kraus = (0.006, 0.022), (8, 0.35), 0.3

    def dichte(self, q):
        return self.h.kopfhaut(q, ohr_frei=(0.018, 0.026))

    def kamm(self, q, n, s, w):
        return _norm(_vom_wirbel(self.h, q, vorn=0.2, unten=0.25) + Vector(n) * 0.8)

    def schwere(self, s, w):
        return 0.3 * glatt(s, 0.03, 0.08)

    def laenge(self, w, n):
        return 0.12 * self.rng.uniform(0.8, 1.15) * self.M

    def ende(self, q, s, w):
        h, M = self.h, self.M
        return q[2] < _schnitt(h, q, [(0, h.brauen_oben + 0.008 * M), (60, h.auge_z), (110, h.kinn[2] + 0.02 * M),
                                      (180, h.kinn[2] - 0.01 * M)])

    def nach(self, pkt, w, rng):
        return _locken(pkt, self.h, rng, 0.0055 * self.M, 0.022 * self.M)


def _locken(pkt, h, rng, hoehe, wl):
    """Schraubenlocken um die gekämmte Bahn (fest in den Stil gelegt; das Gen „haarstruktur“ legt im Spiel noch dazu)."""
    pkt = np.asarray(pkt)
    if len(pkt) < 4:
        return pkt
    bogen = np.concatenate([[0], np.cumsum(np.linalg.norm(np.diff(pkt, axis=0), axis=1))])
    fein = umtasten(pkt, max(len(pkt), int(bogen[-1] / 0.0015) + 2))
    bogen = np.concatenate([[0], np.cumsum(np.linalg.norm(np.diff(fein, axis=0), axis=1))])
    tang = np.gradient(fein, axis=0)
    tang /= np.maximum(np.linalg.norm(tang, axis=1, keepdims=True), 1e-9)
    raus = fein - h.kopfmitte
    raus -= tang * (raus * tang).sum(1, keepdims=True)
    raus /= np.maximum(np.linalg.norm(raus, axis=1, keepdims=True), 1e-9)
    quer = np.cross(tang, raus)
    ph = rng.random() * 2 * np.pi
    th = 2 * np.pi * bogen / wl * rng.uniform(0.85, 1.15) + ph
    an = glatt(bogen, 0.006, 0.03)[:, None]
    return fein + (quer * np.sin(th)[:, None] + raus * np.cos(th)[:, None]) * hoehe * an


class Tonsur(Stil):
    """m08: Mönchstonsur (Corona) – auf dem Scheitel eine runde kahle Stelle (Ø etwa 9 cm, etwas hinter der Mitte), der
    breite Kranz darum kurz (2–3 cm) vom Scheitel weg gekämmt und gerade geschnitten, Ohren frei. Nutzer 30.10.: die
    kahle Stelle der Vorlage war übertrieben groß, der Kranz zu schmal."""
    name, geschlecht, anzahl, punkte = "tonsur", "m", 4200, 8
    schritt, volumen, volumen_weg, hub, steif = 0.002, (0.001, 0.004), 0.01, 0.25, 0.4
    wellen, klumpen = (0.0008, 0.02), (10, 0.25)

    def dichte(self, q):
        h, M = self.h, self.M
        q = np.atleast_2d(q)
        kahl = glatt(np.linalg.norm(q - self.mitte, axis=1), 0.043 * M, 0.049 * M)
        return h.kopfhaut(q, ohr_frei=(0.024, 0.034)) * kahl

    @property
    def mitte(self):
        if not hasattr(self, "_mitte"):
            h = self.h
            l, _, _, _ = h.naechste(np.array([0.0, h.scheitel[1] + 0.025 * self.M, h.scheitel[2] + 0.02 * self.M]))
            self._mitte = np.asarray(l)
        return self._mitte

    def kamm(self, q, n, s, w):
        weg = _norm(Vector(np.asarray(q) - self.mitte))
        return _norm(weg + UNTEN * 0.8)

    def schwere(self, s, w):
        return 0.4

    def laenge(self, w, n):
        return 0.03 * self.rng.uniform(0.9, 1.05) * self.M

    def ende(self, q, s, w):
        h, M = self.h, self.M
        z = _schnitt(h, q, [(0, h.brauen_oben + 0.024 * M), (60, h.auge_z + 0.016 * M), (90, h.ohr_z + 0.016 * M),
                            (180, h.ohr_z - 0.01 * M)])
        return q[2] < z


class Zerzaust(Stil):
    """m09: zerzaust, halblang (bis Kinn und Nacken), unregelmäßig gestuft, Fransen in der Stirn."""
    name, geschlecht, anzahl, punkte = "zerzaust", "m", 4800, 16
    schritt, volumen, volumen_weg, hub, steif = 0.004, (0.002, 0.012), 0.04, 0.4, 0.3
    wellen, klumpen, kraus = (0.004, 0.045), (10, 0.5), 0.22
    einwaerts = 0.3

    def dichte(self, q):
        return self.h.kopfhaut(q, ohr_frei=(0.016, 0.024))

    def kamm(self, q, n, s, w):
        vorn = glatt(-(w[1] - self.h.kopfmitte[1]), 0.0, 0.05 * self.M)
        return _vom_wirbel(self.h, q, vorn=0.2 + 0.5 * vorn, unten=0.6)

    def schwere(self, s, w):
        return 0.7 * glatt(s, 0.02, 0.07)

    def laenge(self, w, n):
        return 0.16 * self.rng.uniform(0.55, 1.05) * self.M

    def ende(self, q, s, w):
        h, M = self.h, self.M
        z = _schnitt(h, q, [(0, h.brauen_oben - 0.004 * M), (40, h.auge_z), (80, h.mund_z), (130, h.kinn[2]),
                            (180, h.kinn[2] - 0.025 * M)])
        return q[2] < z + _streu(w) * 0.02 * M


class Kranz(Stil):
    """m10: Halbglatze mit Kranz – Platte kahl, an den Seiten und hinten glatt bis auf die Schultern."""
    name, geschlecht, anzahl, punkte = "kranz", "m", 3800, 32
    schritt, volumen, volumen_weg, hub, steif = 0.005, (0.001, 0.005), 0.05, 0.15, 0.3
    wellen, klumpen = (0.003, 0.06), (12, 0.45)
    einwaerts = 0.3

    def dichte(self, q):
        h = self.h
        # Nutzer 30.10.: die Glatze hält sich in Grenzen (Platte und Geheimratsecken, kein kahler Oberkopf bis zum Ohr)
        return h.kopfhaut(q, ohr_frei=(0.016, 0.024)) * glatt(h.rang(q), 0.42, 0.6)

    def kamm(self, q, n, s, w):
        sx = 1.0 if w[0] >= 0 else -1.0
        return _norm(Vector((0.35 * sx, 0.25, -1.0)))

    def schwere(self, s, w):
        return 1.0 * glatt(s, 0.01, 0.06)

    def laenge(self, w, n):
        return 0.35

    def ende(self, q, s, w):
        h, M = self.h, self.M
        z = _schnitt(h, q, [(0, h.kinn[2] + 0.01 * M), (90, h.kinn[2] - 0.005 * M), (180, h.kinn[2] - 0.02 * M)])
        return q[2] < z + _streu(w, 1, 2) * 0.015 * M


# ================================================================= Frauen
class _Hochgesteckt(k4.FrauScheitel):
    """Grundlage der hochgesteckten Frisuren: Mittelscheitel, glatt; jede Strähne läuft zu ihrem Anker (Knoten, Zopf),
    nichts hängt herab. Unterklassen setzen anker_fuer(w) und den Endabstand."""
    schritt, volumen, volumen_weg, hub, steif = 0.004, (0.0012, 0.006), 0.04, 0.28, 0.32
    wellen, klumpen = (0.001, 0.05), (14, 0.25)
    frei_faellt = 0.0
    ende_abstand = 0.022

    def anker_fuer(self, w):
        raise NotImplementedError

    def kamm(self, q, n, s, w):
        # Nutzer 30.10.: nichts hängt herab – die Strähnen biegen früh zum Anker (vorher erst nach 2–9 cm)
        a = self.anker_fuer(w)
        zum = _norm(a - q)
        k = 1.0 if w[2] < a[2] + 0.01 * self.M else glatt(s, 0.005, 0.04)
        return _norm(self.scheitel(q, w) * (1 - k) + zum * (2.0 * k))

    def schwere(self, s, w):
        return 0.0

    def laenge(self, w, n):
        # höchstens 1,8 × der Abstand zum Anker, damit keine Strähne am Anker vorbei weiterläuft und herabhängt
        return min(0.4, 1.8 * float(np.linalg.norm(np.asarray(self.anker_fuer(w)) - w)) + 0.03 * self.M)

    def ende(self, q, s, w):
        return (q - self.anker_fuer(w)).length < self.ende_abstand * self.M


class Zoepfe(k4.FrauZoepfe):
    """f01: zwei Zöpfe vor den Schultern (wie Phase 4, die Zöpfe länger)."""
    zopf_laenge = 0.44

    def zusatz(self, rng, faktor):
        h = self.h
        return np.concatenate([zopf_straehnen(h, zopf_bahn(h, self.anker[s], s, h.k.mass, laenge=self.zopf_laenge), rng,
                                              h.k.mass, anzahl=int(360 * faktor)) for s in ("l", "r")]), "zopf"


class EinZopf(_Hochgesteckt):
    """f02: ein Zopf – Mittelscheitel, glatt nach hinten zum Nacken, ein langer Zopf über den Rücken."""
    name, anzahl, punkte = "ein_zopf", 4600, 16

    def __init__(self, h, rng):
        super().__init__(h, rng)
        M = self.M
        l, n, _, _ = h.naechste(np.array([0.0, h.kopfmitte[1] + 0.12 * M, h.ohr_z - 0.045 * M]))
        self.anker = Vector(l) + Vector(n) * 0.012 * M

    def anker_fuer(self, w):
        return self.anker

    def zusatz(self, rng, faktor):
        h, M = self.h, self.M
        a = np.array(self.anker)
        stuetz = [a, a + np.array([0, 0.012, -0.05]) * M, a + np.array([0, 0.02, -0.16]) * M,
                  a + np.array([0, 0.03, -0.32]) * M, a + np.array([0, 0.035, -0.46]) * M]
        bahn = bahn_auf_kopf(h, stuetz, M, abstand=0.016, glaetten=4)
        return zopf_straehnen(h, bahn, rng, M, anzahl=int(520 * faktor), dicke=0.013), "zopf"


class Offen(k4.FrauOffen):
    """f03: offen, lang und glatt (wie Phase 4)."""


class Flechtkranz(_Hochgesteckt):
    """f04: Flechtkranz – Mittelscheitel, glatt; zwei Zöpfe liegen wie eine Krone über den Kopf (etwa 4 cm hinter dem
    Haaransatz), von einem Ohr zum anderen."""
    name, anzahl, punkte = "flechtkranz", 4600, 16

    def __init__(self, h, rng):
        super().__init__(h, rng)
        M = self.M
        self.anker = {}
        for s, sx in (("l", 1.0), ("r", -1.0)):
            q = h.ohr[s] + np.array([-sx * 0.004 * M, 0.025 * M, -0.01 * M])
            self.anker[s] = Vector(_auf_haut(h, q, M, 0.012))

    def anker_fuer(self, w):
        return self.anker["l" if w[0] >= 0 else "r"]

    def zusatz(self, rng, faktor):
        h, M = self.h, self.M
        teile = []
        for s, sx, vor in (("l", 1.0, 0.0), ("r", -1.0, 0.028)):
            a = np.array(self.anker[s])
            b = np.array(self.anker["r" if s == "l" else "l"])
            mitte_oben = np.array([0.0, h.scheitel[1] + (0.012 + vor) * M, h.scheitel[2] + 0.02 * M])
            stuetz = [a, a + np.array([0, -0.01, 0.045]) * M,
                      np.array([sx * 0.055 * M, mitte_oben[1] + 0.005 * M, h.scheitel[2] - 0.02 * M]), mitte_oben,
                      np.array([-sx * 0.055 * M, mitte_oben[1] + 0.005 * M, h.scheitel[2] - 0.02 * M]),
                      b + np.array([0, 0.005, 0.04]) * M]
            bahn = bahn_auf_kopf(h, stuetz, M, abstand=0.011)
            teile.append(zopf_straehnen(h, bahn, rng, M, anzahl=int(330 * faktor), dicke=0.012, schwanz=0.004))
        return np.concatenate(teile), "zopf"


class Schnecken(_Hochgesteckt):
    """f05: Schnecken – Mittelscheitel, oben glatt; das Haar jeder Seite ist zu einer flachen runden Schnecke über dem
    Ohr gedreht (bedeckt das Ohr)."""
    name, anzahl, punkte = "schnecken", 4600, 16
    ende_abstand = 0.026

    def __init__(self, h, rng):
        super().__init__(h, rng)
        M = self.M
        self.anker, self.anker_n = {}, {}
        for s, sx in (("l", 1.0), ("r", -1.0)):
            q = h.ohr[s] + np.array([0.0, -0.004 * M, -0.004 * M])
            self.anker[s] = Vector(q + np.array([sx * 0.018 * M, 0, 0]))
            self.anker_n[s] = np.array([sx, -0.1, 0.0]) / math.hypot(1.0, 0.1)

    def anker_fuer(self, w):
        return self.anker["l" if w[0] >= 0 else "r"]

    def zusatz(self, rng, faktor):
        teile = []
        for s in ("l", "r"):
            o = _Objekt(self.anker[s], self.anker_n[s], self.M * 1.55)   # Schnecke etwa 8–9 cm, deckt das Ohr
            teile.append(knoten_straehnen(o, rng, anzahl=int(600 * faktor)))
        return np.concatenate(teile), "knoten"


class Zopfschlaufen(_Hochgesteckt):
    """f06: Zopfschlaufen (14. Jh.) – Mittelscheitel, oben glatt; je Seite ein dicker Zopf von der Schläfe vor dem Ohr
    bis zur Kieferhöhe, dann nach hinten zum Nacken, dort zu einem flachen Knoten gesteckt; nichts hängt herab."""
    name, anzahl, punkte = "zopfschlaufen", 4600, 16

    def __init__(self, h, rng):
        super().__init__(h, rng)
        M = self.M
        self.anker = {}
        for s, sx in (("l", 1.0), ("r", -1.0)):
            self.anker[s] = Vector(_auf_haut(h, h.ohr[s] + np.array([-sx * 0.004, -0.018, 0.035]) * M, M, 0.012))

    def anker_fuer(self, w):
        # Hinterkopf (hinter den Ohren) zum Nackenknoten, sonst zur Schläfe (Anfang des Zopfs)
        if w[1] > self.h.ohr["l"][1] + 0.01 * self.M:
            if not hasattr(self, "_nacken"):
                h, M = self.h, self.M
                l, n, _, _ = h.naechste(np.array([0.0, h.kopfmitte[1] + 0.11 * M, h.ohr_z - 0.04 * M]))
                self._nacken = Vector(l) + Vector(n) * 0.012 * M
            return self._nacken
        return self.anker["l" if w[0] >= 0 else "r"]

    def zusatz(self, rng, faktor):
        h, M = self.h, self.M
        teile = []
        for s, sx in (("l", 1.0), ("r", -1.0)):
            o = h.ohr[s]
            nacken = np.array([sx * 0.012 * M, h.kopfmitte[1] + 0.1 * M, h.ohr_z - 0.04 * M])
            stuetz = [np.array(self.anker[s]), o + np.array([sx * 0.012, -0.02, 0.0]) * M,
                      o + np.array([sx * 0.012, -0.012, -0.04]) * M, o + np.array([sx * 0.006, 0.03, -0.052]) * M,
                      nacken + np.array([sx * 0.03, -0.02, 0.0]) * M, nacken]
            bahn = bahn_auf_kopf(h, stuetz, M, abstand=0.013, glaetten=3)
            teile.append(zopf_straehnen(h, bahn, rng, M, anzahl=int(360 * faktor), dicke=0.014, schwanz=0.004))
        # flacher Knoten im Nacken, wo die Zöpfe enden (64 Punkte wie die Zöpfe)
        l, n, _, _ = h.naechste(np.array([0.0, h.kopfmitte[1] + 0.11 * M, h.ohr_z - 0.04 * M]))
        o = _Objekt(Vector(l) + Vector(n) * 0.01 * M, n, M * 0.9)
        teile.append(knoten_straehnen(o, rng, anzahl=int(300 * faktor), punkte=64))
        return np.concatenate(teile), "zopf"


class Nackenknoten(k4.FrauKnoten):
    """f07: Nackenknoten (wie Phase 4)."""

    def zusatz(self, rng, faktor):
        return knoten_straehnen(self, rng, anzahl=int(700 * faktor)), "knoten"


def polster_straehnen(mitte, achse, aussen, R, H, rng, anzahl=700, punkte=32, windungen=(1.5, 2.6)):
    """Haarpolster: Strähnen als Schrauben um eine Achse in einem Ellipsoid (Halbmesser R quer, H/2 längs), außen
    etwas abgeflacht zur Haut hin (aussen = Richtung weg vom Kopf). Für die Seitenpolster unter der Crespine."""
    a = np.asarray(achse, dtype=np.float64)
    a /= np.linalg.norm(a)
    e1 = np.asarray(aussen, dtype=np.float64)
    e1 = e1 - a * (e1 @ a)
    e1 /= np.linalg.norm(e1)
    e2 = np.cross(a, e1)
    st = []
    for _ in range(anzahl):
        rho = math.sqrt(rng.uniform(0.25, 1.0))
        z0, z1 = sorted(rng.uniform(-1, 1, 2))
        if z1 - z0 < 0.5:
            z1 = min(1.0, z0 + 0.5)
        phi0 = rng.random() * 2 * np.pi
        w = rng.uniform(*windungen) * (1 if rng.random() < 0.5 else -1)
        t = np.linspace(0, 1, punkte * 3)
        z = z0 + (z1 - z0) * t
        phi = phi0 + 2 * np.pi * w * t
        r = R * rho * np.sqrt(np.clip(1 - z ** 2, 0.02, 1))
        # zur Haut hin (cos phi < 0) flacher
        r_e1 = r * np.where(np.cos(phi) < 0, 0.55, 1.0)
        pts = (np.asarray(mitte)[None] + np.outer(z * H / 2, a) + np.outer(r_e1 * np.cos(phi), e1)
               + np.outer(r * np.sin(phi), e2))
        st.append(umtasten(pts, punkte))
    return np.array(st)


def netz_auf_polster(mitte, achse, aussen, R, H, maschen=1.0, punkte=32, abstand=1.06):
    """Crespine: Netz aus zwei Scharen von Schraubenlinien (Rautenmaschen) über ein Haarpolster; dazu die
    Kreuzungspunkte (für die Perlen)."""
    a = np.asarray(achse, dtype=np.float64)
    a /= np.linalg.norm(a)
    e1 = np.asarray(aussen, dtype=np.float64)
    e1 = e1 - a * (e1 @ a)
    e1 /= np.linalg.norm(e1)
    e2 = np.cross(a, e1)

    def punkt(z, phi):
        r = R * abstand * math.sqrt(max(1 - z * z, 0.02)) * (0.55 if math.cos(phi) < 0 else 1.0)
        return np.asarray(mitte) + a * z * H / 2 + e1 * r * math.cos(phi) + e2 * R * abstand * math.sqrt(max(1 - z * z, 0.02)) * math.sin(phi)

    umfang = 2 * np.pi * R
    n = max(8, int(umfang / (0.011 * maschen)))
    steig = 2 * np.pi * 0.85                     # Windung je Höhe: Rauten etwa 1 cm
    linien, kreuz = [], []
    for vz in (1, -1):
        for k in range(n):
            phi0 = 2 * np.pi * k / n
            z = np.linspace(-0.92, 0.92, punkte)
            linien.append(np.array([punkt(zz, phi0 + vz * steig * (zz + 0.92) / 2) for zz in z]))
    for k in range(n):
        for m in range(1, 6):
            zz = -0.92 + 1.84 * m / 6
            kreuz.append(punkt(zz, 2 * np.pi * k / n + (2 * np.pi / n) * 0.5 * (m % 2)))
    return np.array(linien), np.array(kreuz)


def perlen(punkte_, rng, r=0.0016, punkte=8):
    """Perlen als kleine geschlossene Schlaufen (drei je Kreuzung, gegeneinander gedreht)."""
    st = []
    for p in punkte_:
        for _ in range(3):
            a = rng.normal(size=3)
            a /= np.linalg.norm(a)
            b = np.cross(a, rng.normal(size=3))
            b /= np.linalg.norm(b)
            t = np.linspace(0, 2 * np.pi, punkte)
            st.append(p + np.outer(np.cos(t), a) * r + np.outer(np.sin(t), b) * r)
    return np.array(st)


class Haarnetz(_Hochgesteckt):
    """f08: Crespine (um 1380–1420) – Mittelscheitel, oben glatt; das Haar ist an jeder Seite über Schläfe und Ohr zu
    einem Polster gelegt, jedes Polster in einem Netz aus Goldschnur mit Perlen an den Kreuzungen; ein schmales goldenes
    Band (Fillet) läuft über den Scheitel von Polster zu Polster. Quellen in docs/charakter/phase5.md. Nutzer 30.10.:
    nicht ein Knoten am Hinterkopf, kein loses Haar."""
    name, anzahl, punkte = "haarnetz", 4600, 16
    ende_abstand = 0.03

    def __init__(self, h, rng):
        super().__init__(h, rng)
        M = self.M
        self.polster = {}
        for s, sx in (("l", 1.0), ("r", -1.0)):
            o = h.ohr[s]
            mitte = o + np.array([sx * 0.024, -0.008, 0.012]) * M
            self.polster[s] = (mitte, np.array([sx * 0.15, -0.25, 1.0]), np.array([sx, 0.0, 0.0]), 0.027 * M, 0.085 * M)
        self.anker = {s: Vector(self.polster[s][0]) for s in ("l", "r")}

    def anker_fuer(self, w):
        return self.anker["l" if w[0] >= 0 else "r"]

    def zusatz(self, rng, faktor):
        h, M = self.h, self.M
        haar, netz, kreuz = [], [], []
        for s in ("l", "r"):
            mitte, achse, aussen, R, H = self.polster[s]
            haar.append(polster_straehnen(mitte, achse, aussen, R, H, rng, anzahl=int(800 * faktor)))
            li, kr = netz_auf_polster(mitte, achse, aussen, R, H)
            netz.append(li)
            kreuz.append(kr)
        # Fillet: Band aus acht Goldschnüren über den Scheitel, von Polster zu Polster
        a, b = self.polster["l"][0], self.polster["r"][0]
        top = np.array([0.0, h.scheitel[1] + 0.028 * M, h.scheitel[2]])
        band = []
        for k in range(8):
            versatz = (k - 3.5) / 3.5 * 0.0045 * M
            stuetz = [a + np.array([0, versatz, 0.04 * M]), np.array([0.06 * M, top[1] + versatz, top[2] - 0.025 * M]),
                      top + np.array([0, versatz, 0]), np.array([-0.06 * M, top[1] + versatz, top[2] - 0.025 * M]),
                      b + np.array([0, versatz, 0.04 * M])]
            band.append(umtasten(bahn_auf_kopf(h, stuetz, M, abstand=0.0035, n=120, glaetten=3), 32))
        netz = np.concatenate(netz + [np.array(band)])
        return [("zusatz", np.concatenate(haar), "knoten"), ("netz", netz, "netz"),
                ("perlen", perlen(np.concatenate(kreuz), rng, r=0.0016 * M), "perlen")]


class HoheStirn(_Hochgesteckt):
    """f09: hohe Stirn (15. Jh.) – Haaransatz hoch ausgezupft, alles Haar straff zurück zu einem flachen Knoten am
    Hinterkopf."""
    name, anzahl, punkte = "hohe_stirn", 4200, 16
    volumen = (0.0008, 0.003)
    ende_abstand = 0.024

    def __init__(self, h, rng):
        super().__init__(h, rng)
        M = self.M
        l, n, _, _ = h.naechste(np.array([0.0, h.kopfmitte[1] + 0.1 * M, h.ohr_z + 0.05 * M]))
        self.anker = Vector(l) + Vector(n) * 0.012 * M
        self.anker_n = np.array([0.0, 0.7, 0.71])

    def dichte(self, q):
        # Haaransatz vorn 2,5 cm höher (ausgezupft), an den Schläfen weniger
        q = np.atleast_2d(q)
        r = self.h.richtung(q)
        hoeher = np.interp(r, [0, 40, 80, 180], [0.035, 0.026, 0.006, 0.0]) * self.M
        return self.h.kopfhaut(q - np.stack([np.zeros(len(q)), np.zeros(len(q)), hoeher], 1), tiefer=0.002 * self.M,
                               ohr_frei=(0.018, 0.028))

    def scheitel(self, q, w):
        return _norm(Vector((0.15 * (1.0 if w[0] >= 0 else -1.0), 1.0, -0.15)))

    def anker_fuer(self, w):
        return self.anker

    def zusatz(self, rng, faktor):
        o = _Objekt(self.anker, self.anker_n, self.M)
        return knoten_straehnen(o, rng, anzahl=int(600 * faktor)), "knoten"


class KrauseLocken(k4.FrauScheitel):
    """f10: krause Locken, lang (bis zur Brust), viel Volumen."""
    name, anzahl, punkte = "krause_locken", 4200, 64
    schritt, volumen, volumen_weg, hub, steif = 0.005, (0.003, 0.025), 0.05, 0.45, 0.28
    wellen, klumpen, kraus = (0.004, 0.03), (8, 0.35), 0.1

    def kamm(self, q, n, s, w):
        return _norm(self.scheitel(q, w) + Vector(n) * 0.25 + HINTEN * 0.1 * glatt(s, 0.04, 0.12))

    def schwere(self, s, w):
        return 0.9 * glatt(s, 0.03, 0.12)

    def laenge(self, w, n):
        return 0.45

    def ende(self, q, s, w):
        h, M = self.h, self.M
        schulter = (h.schulter["l"][2] + h.schulter["r"][2]) / 2
        z = _schnitt(h, q, [(0, schulter - 0.02 * M), (90, schulter - 0.03 * M), (180, schulter - 0.05 * M)])
        return q[2] < z + _streu(w, 0, 2) * 0.04 * M

    def nach(self, pkt, w, rng):
        # 64 Punkte je Strähne (Höchstwert von TressFX) tragen bei 35 cm etwa acht Windungen: Wellenlänge 4,5 cm
        return _locken(pkt, self.h, rng, 0.008 * self.M, 0.045 * self.M)


# ================================================================= Bärte
class Stoppeln(k4.Bart):
    """b02: Stoppeln (einige Tage, 2–4 mm) überall, wo ein Vollbart wächst."""
    name, anzahl, punkte = "stoppeln", 9000, 4
    schritt, volumen, volumen_weg, hub, steif = 0.001, (0.0003, 0.0008), 0.002, 1.0, 0.6
    wellen, klumpen, kraus = (0.0, 0.01), (6, 0.0), 0.2

    def dichte(self, q):
        f = self.flaechen(q)
        return np.clip(np.maximum.reduce([f["schnurr"], f["kinn"], f["wange"], f["hals"] * 0.8]), 0, 1)

    def kamm(self, q, n, s, w):
        return _norm(UNTEN + VORN * 0.2)

    def schwere(self, s, w):
        return 0.1

    def laenge(self, w, n):
        # kaemmen() verwirft Strähnen unter 2 mm: Stoppeln 2,1–3 mm (die Vorlage nennt 1–2 mm; sichtbar ist kaum ein Unterschied)
        return 0.0024 * self.rng.uniform(0.88, 1.25) * self.M


class Gabelbart(k4.Langbart):
    """b05: Gabelbart – Vollbart etwa 10 cm, unter dem Kinn gescheitelt, zwei Spitzen; Schnurrbart geht in den Bart."""
    name, anzahl, punkte = "gabelbart", 5200, 32
    klumpen = (10, 0.15)

    def kamm(self, q, n, s, w):
        if self.zone(w) == "schnurr":
            return k4.Schnurrbart.kamm(self, q, n, s, w)
        M = self.M
        sx = 1.0 if w[0] >= 0 else -1.0
        spitze = Vector((sx * 0.034 * M, self.m68[8, 1] - 0.02 * M, self.m68[8, 2] - 0.15 * M))   # Nutzer: länger
        k = glatt(s, 0.005, 0.025)
        # Mitte: Scheitel unter dem Kinn (die Strähnen jeder Seite laufen zu ihrer Spitze, nicht zur Mitte)
        return _norm(UNTEN * (1 - k) * 0.6 + Vector((sx * 0.4, -0.1, 0)) * (1 - k) + _norm(spitze - q) * 3.0 * k)

    def laenge(self, w, n):
        z = self.zone(w)
        if z == "schnurr":
            return k4.Schnurrbart.laenge(self, w, n) * 1.2
        unten = glatt(self.lippe_unten - w[2], 0.0, 0.04 * self.M)
        L = {"kinn": 0.155, "wange": 0.02 + 0.12 * unten ** 1.5, "hals": 0.11}[z]
        return L * self.rng.uniform(0.85, 1.1) * self.M


class Spitzbart(k4.Bart):
    """b06: Kinnbart – nur am Kinn, 3–5 cm, läuft zu einer Spitze zusammen; Oberlippe rasiert."""
    name, anzahl = "spitzbart", 1800

    def dichte(self, q):
        q = np.atleast_2d(q)
        return self.flaechen(q)["kinn"] * (1 - glatt(np.abs(q[:, 0]), 0.35 * self.mundbreite, 0.55 * self.mundbreite))

    def kamm(self, q, n, s, w):
        spitze = Vector((0.0, self.m68[8, 1] - 0.012 * self.M, self.m68[8, 2] - 0.03 * self.M))
        return _norm(UNTEN + (spitze - q) * 25.0)

    def schwere(self, s, w):
        return 0.5

    def laenge(self, w, n):
        return (0.035 + 0.018 * glatt(self.mundbreite - abs(w[0]), 0, self.mundbreite)) * self.rng.uniform(0.85, 1.1) * self.M


class KurzerVollbart(k4.Vollbart):
    """b03: kurzer Vollbart – gestutzt, 1,5–2 cm, dicht, Wangen bis zur Linie Ohr – Mundwinkel."""
    name, anzahl, punkte = "vollbart", 6500, 8
    volumen = (0.0008, 0.004)

    def dichte(self, q):
        # Hals rasiert, saubere Linie unter dem Kiefer (sonst hängt der kurze Bart wie ein Vorhang unter den Kiefer)
        f = self.flaechen(q)
        return np.clip(np.maximum.reduce([f["schnurr"], f["kinn"], f["wange"]]), 0, 1)

    def kamm(self, q, n, s, w):
        if self.zone(w) == "hals":
            return _norm(UNTEN * 0.3 + VORN * 0.6)        # am Hals anliegend nach vorn zum Kinn
        return k4.Vollbart.kamm(self, q, n, s, w)

    def laenge(self, w, n):
        z = self.zone(w)
        if z == "schnurr":
            return 0.01 * self.rng.uniform(0.85, 1.1) * self.M
        L = {"kinn": 0.013, "wange": 0.009, "hals": 0.006}[z]
        return L * self.rng.uniform(0.85, 1.1) * self.M


class SchnurrKinnbart(k4.Bart):
    """b07: schmaler Schnurrbart (nur über der Lippe, außen kurz eingedreht) und ein kleines Büschel unter der
    Unterlippe."""
    name, anzahl = "kinnbart", 1400

    def dichte(self, q):
        q = np.atleast_2d(q)
        f = self.flaechen(q)
        schmal = glatt(q[:, 2] - (self.lippe_oben + 0.001 * self.M), 0.0, 0.002 * self.M) \
            * (1 - glatt(q[:, 2] - (self.lippe_oben + 0.007 * self.M), 0.0, 0.002 * self.M))
        mitte = 1 - glatt(np.abs(q[:, 0]), 0.25 * self.mundbreite, 0.4 * self.mundbreite)
        unter_lippe = glatt(self.lippe_unten - 0.002 * self.M - q[:, 2], 0.0, 0.002 * self.M) \
            * (1 - glatt(self.lippe_unten - 0.02 * self.M - q[:, 2], 0.0, 0.004 * self.M))
        return np.maximum(f["schnurr"] * schmal, f["kinn"] * mitte * unter_lippe)

    def kamm(self, q, n, s, w):
        if w[2] > self.lippe_oben - 0.002 * self.M:
            sx = 1.0 if w[0] >= 0 else -1.0
            return _norm(Vector((sx * 1.0, -0.1, -0.35)))
        return _norm(UNTEN + VORN * 0.2)

    def schwere(self, s, w):
        return 0.3

    def laenge(self, w, n):
        if w[2] > self.lippe_oben - 0.002 * self.M:
            return (0.008 + 0.012 * glatt(abs(w[0]), 0.0, self.mundbreite)) * self.rng.uniform(0.85, 1.1) * self.M
        return 0.02 * self.rng.uniform(0.8, 1.1) * self.M


class LangerSchnurrbart(k4.Schnurrbart):
    """b08: langer, hängender Schnurrbart – die Enden hängen an den Mundwinkeln bis unter das Kinn."""
    name, anzahl, punkte = "schnurrbart", 1500, 16
    schritt = 0.002

    def kamm(self, q, n, s, w):
        sx = 1.0 if w[0] >= 0 else -1.0
        aussen = glatt(abs(w[0]), 0.0, self.mundbreite)
        k = glatt(s, 0.008, 0.02)
        return _norm(Vector((sx * (0.5 + 0.5 * aussen) * (1 - k) + sx * 0.08 * k, -0.15, -1.0)))

    def schwere(self, s, w):
        return 0.8 * glatt(s, 0.01, 0.03)

    def laenge(self, w, n):
        aussen = glatt(abs(w[0]), 0.2 * self.mundbreite, self.mundbreite + 0.004 * self.M)
        return (0.014 + 0.065 * aussen ** 2) * self.rng.uniform(0.85, 1.1) * self.M


class LangerVollbart(k4.Langbart):
    """b04: langer Vollbart bis zur Brust (etwa 25 cm), voller Schnurrbart, hängt gerade, leicht wellig, unten schmaler."""
    name, anzahl, punkte = "langbart", 5600, 32
    wellen = (0.003, 0.05)

    def kamm(self, q, n, s, w):
        if self.zone(w) == "schnurr":
            return k4.Schnurrbart.kamm(self, q, n, s, w)
        M = self.M
        spitze = Vector((0.0, self.m68[8, 1] - 0.03 * M, self.m68[8, 2] - 0.26 * M))
        k = glatt(s, 0.02, 0.08)
        return _norm(UNTEN * (1 - k) + Vector((-w[0] * 6.0, -0.15, 0)) * 0.3 * (1 - k) + _norm(spitze - q) * 1.2 * k)

    def laenge(self, w, n):
        z = self.zone(w)
        if z == "schnurr":
            return k4.Schnurrbart.laenge(self, w, n) * 1.3
        unten = glatt(self.lippe_unten - w[2], 0.0, 0.04 * self.M)
        L = {"kinn": 0.25, "wange": 0.04 + 0.19 * unten ** 1.5, "hals": 0.18}[z]
        return L * self.rng.uniform(0.85, 1.1) * self.M


class Backenbart(k4.Bart):
    """b09: Backenbart (Kinnbart-Kranz) – kurz (etwa 2 cm) entlang des Kieferrands von Ohr zu Ohr und unter dem Kinn;
    Oberlippe und obere Wangen rasiert."""
    name, anzahl = "backenbart", 3600

    def dichte(self, q):
        q = np.atleast_2d(q)
        f = self.flaechen(q)
        h, M, m = self.h, self.M, self.m68
        # nur ein Streifen von 2 cm über dem Kieferrand (Kieferlinie: Merkpunkte 0–16)
        kiefer = m[0:17]
        d = np.min(np.linalg.norm(q[:, None] - kiefer[None], axis=2), axis=1)
        streifen = 1 - glatt(d, 0.016 * M, 0.024 * M)
        return np.clip(np.maximum(np.maximum(f["wange"], f["kinn"]) * streifen, f["hals"] * 0.9), 0, 1)

    def kamm(self, q, n, s, w):
        return _norm(UNTEN + Vector((-w[0] * 6.0, -0.15, 0.0)) * 0.3 + VORN * 0.15)

    def schwere(self, s, w):
        return 0.5

    def laenge(self, w, n):
        return 0.02 * self.rng.uniform(0.8, 1.15) * self.M


class Wildbart(k4.Vollbart):
    """b10: wilder Vollbart – ungepflegt, 6–9 cm, kraus, die Haare stehen in alle Richtungen."""
    name, anzahl, punkte = "wildbart", 5600, 16
    schritt, volumen = 0.0025, (0.002, 0.014)
    wellen, klumpen, kraus = (0.004, 0.02), (8, 0.35), 0.45

    def laenge(self, w, n):
        z = self.zone(w)
        if z == "schnurr":
            return k4.Schnurrbart.laenge(self, w, n) * 1.4
        unten = glatt(self.lippe_unten - w[2], 0.0, 0.04 * self.M)
        L = {"kinn": 0.12, "wange": 0.04 + 0.07 * unten, "hals": 0.08}[z]
        return L * self.rng.uniform(0.7, 1.15) * self.M


FRISUREN = {"m": [Topfschnitt, Pagenkopf, Haarrolle, Schulterlang, Geschoren, Stirnfransen, Krauskopf, Tonsur, Zerzaust,
                  Kranz],
            "w": [Zoepfe, EinZopf, Offen, Flechtkranz, Schnecken, Zopfschlaufen, Nackenknoten, Haarnetz, HoheStirn,
                  KrauseLocken]}
# Bart 0 (rasiert) hat keine Strähnen; die Liste beginnt mit b02
BAERTE = [Stoppeln, KurzerVollbart, LangerVollbart, Gabelbart, Spitzbart, SchnurrKinnbart, LangerSchnurrbart, Backenbart,
          Wildbart]
