"""Charakter Phase 4: Frisuren und Bärte als Leitsträhnen (eigener Code, MIT; läuft in Blenders Python).

Vorbild sind die 2D-Porträts der Menschen (game/assets/oberflaeche/portraets/mensch): Männer kurz, Pagenkopf
(kinnlang, Scheitel in der Mitte), zurückgestrichen (nackenlang, wellig); Frauen mit Mittelscheitel glatt zum Knoten im
Nacken, offen über die Schultern, zwei Zöpfe nach vorn über die Schultern. Bärte: Schnurrbart, Kinnbart (Schnurrbart
und Spitzbart), Vollbart (gestutzt), langer Bart.

Jede Strähne wird von der Wurzel aus „gekämmt“: Schrittweise folgt sie der Kammrichtung des Stils (auf die Kopfhaut
gelegt), der Schwere und einem Anker (Knoten, Zopf), bleibt um ihre Lagenhöhe über der Haut (Volumen: jede Strähne
liegt in einer eigenen Höhe, innen flach, außen auf den anderen) und endet nach ihrer Länge oder an der Schnittlinie.
Danach Wellen und Klumpen (Spitzen laufen zur Mittelsträhne ihres Büschels). Zöpfe und Knoten sind eigene Strähnen
(geflochtene Bahnen bzw. eine Spirale).

Achsen: Blender (z oben, Gesicht nach -y, +x = linke Seite der Figur), Meter.
"""
import math

import numpy as np
from mathutils import Vector

from kopf4_daten import glatt

UNTEN = Vector((0.0, 0.0, -1.0))
HINTEN = Vector((0.0, 1.0, 0.0))
VORN = Vector((0.0, -1.0, 0.0))


def _tangential(c, n):
    return c - n * c.dot(n)


def _norm(v):
    l = v.length
    return v / l if l > 1e-9 else v


# ---------------------------------------------------------------- Wurzeln
def wurzeln(h, dichte, anzahl, rng, mindest=0.12):
    """Wurzeln auf der Haut: nach Fläche × Dichte gezogen, dann ausgedünnt (Poisson-Scheibe, Radius nach der Dichte:
    am Rand stehen die Haare lichter – weicher Haaransatz statt Helmkante)."""
    p = h.p
    dv = np.clip(dichte(p), 0, 1)
    dv[~h.haut & ~h.k.ist_bueste] = 0.0
    t = h.tri
    a = 0.5 * np.linalg.norm(np.cross(p[t[:, 1]] - p[t[:, 0]], p[t[:, 2]] - p[t[:, 0]]), axis=1)
    w = a * dv[t].mean(1)
    if w.sum() <= 0:
        return np.zeros((0, 3)), np.zeros((0, 3)), np.zeros(0)
    flaeche = w.sum()
    r0 = math.sqrt(flaeche / anzahl) * 0.72
    kand = anzahl * 4
    ti = rng.choice(len(t), kand, p=w / flaeche)
    u = rng.random((kand, 2))
    um = u.sum(1) > 1
    u[um] = 1 - u[um]
    bc = np.stack([1 - u[:, 0] - u[:, 1], u[:, 0], u[:, 1]], 1)
    q = (p[t[ti]] * bc[..., None]).sum(1)
    dq = (dv[t[ti]] * bc).sum(1)
    nq = (h.vn[t[ti]] * bc[..., None]).sum(1)
    nq /= np.linalg.norm(nq, axis=1, keepdims=True)
    # ausdünnen: Radius r0 / sqrt(Dichte)
    zelle = r0
    gitter = {}
    behalten = []
    for i in rng.permutation(kand):
        if dq[i] < mindest * rng.random():
            continue
        r = r0 / math.sqrt(max(dq[i], mindest))
        c = tuple(np.floor(q[i] / zelle).astype(int))
        weit = int(math.ceil(r / zelle))
        frei = True
        for dx in range(-weit, weit + 1):
            for dy in range(-weit, weit + 1):
                for dz in range(-weit, weit + 1):
                    for j in gitter.get((c[0] + dx, c[1] + dy, c[2] + dz), ()):
                        if np.sum((q[j] - q[i]) ** 2) < r * r:
                            frei = False
                            break
                    if not frei:
                        break
                if not frei:
                    break
            if not frei:
                break
        if frei:
            gitter.setdefault(c, []).append(i)
            behalten.append(i)
            if len(behalten) >= anzahl:
                break
    b = np.array(behalten, np.int64)
    return q[b], nq[b], dq[b]


# ---------------------------------------------------------------- Kämmen
class Stil:
    """Grundstil: Unterklassen setzen dichte(), kamm(), laenge(), ende() und die Zahlen."""
    name = ""
    teil = "frisur"            # frisur | bart | zusatz
    geschlecht = "m"
    anzahl = 3000
    punkte = 16                # Punkte je Strähne (TressFX: 4, 8, 16, 32, 64)
    schritt = 0.003
    volumen = (0.002, 0.008)   # Lagenhöhe über der Haut (min, max je Strähne)
    volumen_weg = 0.03         # bis zu dieser Bogenlänge steigt die Strähne auf ihre Lagenhöhe
    hub = 0.35                 # wie steil die Wurzel aus der Haut kommt
    steif = 0.35               # wie schnell die Richtung der Kammrichtung folgt (je Schritt)
    wellen = (0.0, 0.04)       # Höhe (m), Länge (m)
    klumpen = (14, 0.35)       # Strähnen je Büschel, Zug der Spitzen zur Büschelmitte
    kraus = 0.0                # Zufallsrichtung je Schritt (Bart)
    frei_faellt = 1.0          # 1: frei hängende Strähnen folgen nur der Schwere (0: Anker, Zöpfe)
    einwaerts = 0.0            # frei hängende Enden zum Hals hin

    def __init__(self, h, rng):
        self.h = h
        self.rng = rng
        self.M = h.k.mass

    def dichte(self, q):
        return self.h.kopfhaut(q)

    def kamm(self, q, n, s, w):
        return UNTEN

    def schwere(self, s, w):
        return 0.0

    def laenge(self, w, n):
        return 0.1

    def ende(self, q, s, w):
        return False


def kaemmen(stil, wurzel, normal, rng):
    """Eine Strähne von der Wurzel aus: Liste der Punkte (feine Schritte)."""
    h = stil.h
    w = Vector(wurzel)
    n = Vector(normal)
    L = stil.laenge(wurzel, normal)
    lage = rng.uniform(*stil.volumen)
    stil.letzte_lage = (lage - stil.volumen[0]) / max(stil.volumen[1] - stil.volumen[0], 1e-9)
    if L < 0.002:
        return None
    p = w + n * 0.0004
    d = _norm(_tangential(stil.kamm(p, n, 0.0, wurzel), n) + n * stil.hub)
    pkt = [Vector(w), Vector(p)]
    s = 0.0004
    schritt = min(stil.schritt, L / 6)
    while s < L:
        abst, l, nn = h.abstand(np.array(p))
        nn = Vector(nn) if nn is not None else n
        soll0 = lage * glatt(s, 0.0, stil.volumen_weg)
        # frei hängend (weiter als 4 mm über der Lagenhöhe): kein Kämmen mehr, nur Schwere und Schwung
        frei = glatt(abst - soll0, 0.004, 0.015) * stil.frei_faellt
        if stil.frei_faellt == 0 and abst > soll0 + 0.004:
            # Anker (Knoten, Zopf): abseits der Haut nicht auf die Haut legen – sonst verschwindet der Zug zum Anker,
            # sobald die Strähne hinter ihm liegt, und sie fliegt geradeaus weiter
            c = stil.kamm(p, nn, s, wurzel)
        else:
            c = _tangential(stil.kamm(p, nn, s, wurzel), nn) * (1.0 - frei)
        g = stil.schwere(s, wurzel) + frei * 0.6
        if stil.einwaerts and frei > 0:
            # frei hängende Enden laufen zum Hals hin ein (Pagenkopf, Nacken)
            zur = Vector((-p[0], h.kopfmitte[1] - p[1], 0.0))
            c = c + _norm(zur) * (stil.einwaerts * frei)
        hoch = max(0.0, soll0 - max(abst, 0.0))
        ziel = _norm(c + UNTEN * g + nn * (hoch * 40.0))
        if stil.kraus:
            ziel = _norm(ziel + Vector(rng.normal(0, stil.kraus, 3)))
        d = _norm(d + (ziel - d) * stil.steif)
        q = p + d * schritt
        abst2, l2, n2 = h.abstand(np.array(q))
        soll = lage * glatt(s, 0.0, stil.volumen_weg) + 0.0006
        if l2 is not None and abst2 < soll:
            q = Vector(l2) + Vector(n2) * soll
        d = _norm(q - p)
        s += (q - p).length
        p = q
        pkt.append(Vector(p))
        if stil.ende(p, s, wurzel) or len(pkt) > 600:
            break
        # Haupthaar fällt nicht über das Gesicht (Augen, Wangen): dort endet die Strähne
        if stil.teil == "frisur" and abs(p[0]) < 0.052 * h.k.mass and p[1] < h.auge_y + 0.025 * h.k.mass \
                and p[2] < h.brauen_oben + 0.004 * h.k.mass and s > 0.01:
            break
    return np.array([tuple(v) for v in pkt])


def umtasten(pts, n):
    d = np.concatenate([[0.0], np.cumsum(np.linalg.norm(np.diff(pts, axis=0), axis=1))])
    if d[-1] <= 1e-6:
        return None
    t = np.linspace(0.0, d[-1], n)
    return np.stack([np.interp(t, d, pts[:, k]) for k in range(3)], 1)


def buschel_merkmal(st):
    P = st.shape[1]
    return np.concatenate([st[:, 0], st[:, P // 2] * 1.5, st[:, -1]], 1)


def buschel(merkmal, nk, rng):
    """Büschelmitten (am weitesten auseinander) und Zuordnung jeder Strähne zur nächsten Mitte."""
    S = len(merkmal)
    mitte = [int(rng.integers(S))]
    d = np.linalg.norm(merkmal - merkmal[mitte[0]], axis=1)
    for _ in range(nk - 1):
        i = int(np.argmax(d))
        mitte.append(i)
        d = np.minimum(d, np.linalg.norm(merkmal - merkmal[i], axis=1))
    mitte = np.array(mitte)
    zu = np.empty(S, np.int64)
    for a in range(0, S, 2000):
        zu[a:a + 2000] = np.argmin(np.linalg.norm(merkmal[a:a + 2000, None] - merkmal[mitte][None], axis=2), axis=1)
    return mitte, zu


def wellen_und_klumpen(st, wurzeln_, stil, rng, aussen=None):
    """Wellen quer zur Strähne (je Büschel gleiche Phase) und Klumpen: Spitzen laufen zur Mittelsträhne."""
    S, P, _ = st.shape
    if S == 0:
        return st, np.zeros(S, np.int64)
    je, zug = stil.klumpen
    nk = max(1, S // je)
    # Büschel nach dem Weg der Strähne (Wurzel, Mitte, Spitze), nicht nur nach der Wurzel: sonst landen am Scheitel
    # Strähnen beider Seiten in einem Büschel und ihre Spitzen werden quer über Stirn und Gesicht gezogen
    merkmal = buschel_merkmal(st)
    mitte, zu = buschel(merkmal, nk, rng)
    t = np.linspace(0, 1, P)[None, :, None]
    neu = st.copy()
    hoehe, wl = stil.wellen
    if hoehe > 0:
        phase = rng.random(nk) * 2 * np.pi
        bogen = np.concatenate([np.zeros((S, 1)), np.cumsum(np.linalg.norm(np.diff(st, axis=1), axis=2), axis=1)], 1)
        tang = np.gradient(st, axis=1)
        tang /= np.maximum(np.linalg.norm(tang, axis=2, keepdims=True), 1e-9)
        aus = st - (stil.h.kopfmitte if aussen is None else aussen)
        quer = np.cross(tang, aus)
        quer /= np.maximum(np.linalg.norm(quer, axis=2, keepdims=True), 1e-9)
        amp = hoehe * glatt(bogen, 0.01, 0.05) * rng.uniform(0.6, 1.2, (S, 1))
        neu += quer * (amp * np.sin(2 * np.pi * bogen / wl + phase[zu][:, None]))[..., None]
    m = neu[mitte][zu]
    # nur, wenn die Spitzen nah beieinander liegen (sonst kein Zug)
    nah = (np.linalg.norm(st[:, -1] - st[mitte][zu][:, -1], axis=1) < 0.03 * stil.M)[:, None, None]
    k = zug * t ** 1.6 * rng.uniform(0.6, 1.0, (S, 1, 1)) * nah
    neu = neu * (1 - k) + m * k
    return neu, zu


def nach_aussen(st, h, mindest=0.0008, ab=2):
    """Punkte ab dem dritten wieder aus der Haut schieben (nach Wellen und Klumpen)."""
    for i in range(len(st)):
        for j in range(ab, st.shape[1]):
            abst, l, n = h.abstand(st[i, j])
            if l is not None and abst < mindest:
                st[i, j] = l + n * mindest
    return st


def erzeugen(stil, rng, log=print):
    """Alle Strähnen eines Stils: (Strähnen (S,P,3), Wurzeln, Normalen, Büschel, Lage 0..1 je Strähne). Die Lage (Phase 5)
    sagt, wie hoch die Strähne über der Haut liegt (0 innen, 1 außen); der Shader verschattet innere Strähnen."""
    w, n, dq = wurzeln(stil.h, stil.dichte, stil.anzahl, rng)
    st, ww, nn, lg = [], [], [], []
    for i in range(len(w)):
        pkt = kaemmen(stil, w[i], n[i], rng)
        if pkt is None or len(pkt) < 3:
            continue
        if hasattr(stil, "nach"):                  # Phase 5: Nachbearbeitung je Strähne (z. B. Haarrolle)
            pkt = stil.nach(pkt, w[i], rng)
            if pkt is None or len(pkt) < 3:
                continue
        u = umtasten(pkt, stil.punkte)
        if u is None:
            continue
        st.append(u)
        ww.append(w[i])
        nn.append(n[i])
        lg.append(stil.letzte_lage)
    st = np.array(st)
    ww = np.array(ww)
    st, buschel = wellen_und_klumpen(st, ww, stil, rng)
    st = nach_aussen(st, stil.h)
    laenge = np.linalg.norm(np.diff(st, axis=1), axis=2).sum(1)
    log(f"Stil {stil.name}: {len(st)} Strähnen × {stil.punkte} Punkte, Länge Mittel {laenge.mean() * 100:.1f} cm, "
        f"max {laenge.max() * 100:.1f} cm")
    return st, ww, np.array(nn), buschel, np.array(lg)


# ================================================================= Frisuren der Männer
class MannKurz(Stil):
    """Kurz geschnitten (3–5 cm), vom Wirbel aus, oben leicht nach vorn, Ohren frei."""
    name, geschlecht, anzahl, punkte = "mann_kurz", "m", 5200, 8
    schritt, volumen, volumen_weg, hub, steif = 0.0025, (0.0012, 0.0042), 0.012, 0.22, 0.4
    wellen, klumpen = (0.0012, 0.025), (10, 0.25)

    def dichte(self, q):
        return self.h.kopfhaut(q, ohr_frei=(0.026, 0.036))

    def kamm(self, q, n, s, w):
        h = self.h
        weg = _norm(Vector(np.asarray(q) - h.wirbel))
        return _norm(weg + VORN * 0.35 + UNTEN * 0.5)

    def schwere(self, s, w):
        return 0.15

    def laenge(self, w, n):
        r = float(self.h.richtung(w)[0])
        oben = glatt(w[2] - (self.h.auge_z + 0.04 * self.M), 0, 0.04 * self.M)
        L = np.interp(r, [0, 60, 110, 180], [0.048, 0.038, 0.026, 0.024]) * (0.6 + 0.4 * oben)
        return L * self.rng.uniform(0.85, 1.1) * self.M


class MannPagen(Stil):
    """Pagenkopf: Mittelscheitel, Haar fällt seitlich über die Ohren bis zum Kiefer, Stirnhaar zur Seite."""
    name, geschlecht, anzahl, punkte = "mann_pagen", "m", 4600, 16
    # Phase 5: weniger glockig – flachere Lagen, die freien Enden laufen stärker zu Hals und Kiefer ein
    schritt, volumen, volumen_weg, hub, steif = 0.004, (0.0012, 0.0055), 0.05, 0.25, 0.3
    einwaerts = 1.1
    wellen, klumpen = (0.002, 0.05), (12, 0.4)

    def kamm(self, q, n, s, w):
        sx = 1.0 if w[0] >= 0 else -1.0
        vorn = glatt(-(w[1] - self.h.auge_y - 0.03 * self.M), 0, 0.03 * self.M)   # Stirnhaar
        hinten = glatt(w[1] - self.h.kopfmitte[1], 0.0, 0.04 * self.M)
        c = Vector((sx * (1.0 - 0.6 * hinten), 0.25 * hinten - 0.25 * vorn, -0.45 - 0.5 * hinten))
        return _norm(c)

    def schwere(self, s, w):
        return 0.9 * glatt(s, 0.015, 0.06)

    def laenge(self, w, n):
        return 0.3

    def ende(self, q, s, w):
        h = self.h
        r = float(h.richtung(np.asarray(q))[0])
        schnitt = np.interp(r, [0, 25, 45, 70, 110, 180], [h.brauen_oben + 0.004 * self.M, h.auge_z, h.mund_z + 0.01 * self.M,
                                                       h.mund_z - 0.012 * self.M, h.kinn[2] - 0.005 * self.M, h.kinn[2] - 0.035 * self.M])
        return q[2] < schnitt + (hash((round(w[0], 5), round(w[1], 5))) % 1000) / 1000 * 0.012 * self.M


class MannZurueck(Stil):
    """Zurückgestrichen, nackenlang, wellig; hinter die Ohren."""
    name, geschlecht, anzahl, punkte = "mann_zurueck", "m", 4200, 16
    schritt, volumen, volumen_weg, hub, steif = 0.004, (0.002, 0.01), 0.05, 0.4, 0.3
    einwaerts = 0.5
    wellen, klumpen = (0.004, 0.045), (12, 0.45)

    def kamm(self, q, n, s, w):
        h = self.h
        c = HINTEN * 1.0 + UNTEN * 0.2
        for sname in ("l", "r"):
            d = np.linalg.norm(np.asarray(q) - h.ohr[sname])
            nah = 1 - glatt(d, 0.03 * self.M, 0.06 * self.M)
            c = c + HINTEN * (1.5 * nah) + Vector((0, 0, 0.6 * nah))
        return _norm(c)

    def schwere(self, s, w):
        return 0.6 * glatt(s, 0.05, 0.12)

    def laenge(self, w, n):
        return 0.32

    def ende(self, q, s, w):
        h = self.h
        r = float(h.richtung(np.asarray(q))[0])
        if r < 100:
            return False
        schnitt = h.ohr_z - (0.045 + 0.03 * glatt(r, 120, 180)) * self.M
        return q[2] < schnitt + (hash((round(w[1], 5), round(w[2], 5))) % 1000) / 1000 * 0.015 * self.M


# ================================================================= Frisuren der Frauen
class FrauScheitel(Stil):
    """Grundlage der Frauenfrisuren: Mittelscheitel, Haar glatt zur Seite und über die Ohren nach hinten."""
    geschlecht = "w"

    def dichte(self, q):
        # Frauen: Ansatz etwas tiefer und runder, Schläfen voller
        return self.h.kopfhaut(q, tiefer=0.002 * self.M, ohr_frei=(0.018, 0.028))

    def scheitel(self, q, w):
        # Phase 5: hinter dem Wirbel ohne Seitenzug (sonst läuft der Scheitel als heller Strich bis in den Nacken)
        sx = 1.0 if w[0] >= 0 else -1.0
        hinten = glatt(w[1] - self.h.kopfmitte[1], 0.0, 0.05 * self.M)
        return Vector((sx * (1.0 - hinten), 0.35 + 0.4 * hinten, -0.35 - 0.4 * hinten))


class FrauKnoten(FrauScheitel):
    """Mittelscheitel, glatt über die Ohren zum Knoten im Nacken."""
    name, anzahl, punkte = "frau_knoten", 4600, 16
    schritt, volumen, volumen_weg, hub, steif = 0.004, (0.0015, 0.007), 0.04, 0.3, 0.3
    wellen, klumpen = (0.0012, 0.05), (14, 0.25)

    def __init__(self, h, rng):
        super().__init__(h, rng)
        M = self.M
        # Knoten: hinten am Kopf etwa auf Höhe des Ohrläppchens
        l, n, _, _ = h.naechste(np.array([0.0, h.kopfmitte[1] + 0.12 * M, h.ohr_z - 0.028 * M]))
        n = np.array([0.0, 1.0, 0.0])           # Knoten schaut nach hinten (nicht schräg nach unten wie der Hinterkopf)
        self.anker = Vector(l) + Vector(n) * 0.012 * M
        self.anker_n = Vector(n)

    frei_faellt = 0.0

    def kamm(self, q, n, s, w):
        zum = _norm(self.anker - q)
        # Nacken (unter dem Knoten): gleich hinauf zum Knoten
        k = 1.0 if w[2] < self.anker[2] + 0.01 * self.M else glatt(s, 0.02, 0.09)
        return _norm(self.scheitel(q, w) * (1 - k) + zum * (2.0 * k))

    def schwere(self, s, w):
        return 0.0 if w[2] < self.anker[2] + 0.01 * self.M else 0.2

    def laenge(self, w, n):
        return 0.4

    def ende(self, q, s, w):
        return (q - self.anker).length < 0.024 * self.M


class FrauOffen(FrauScheitel):
    """Mittelscheitel, offen, leicht gewellt, hinter die Schultern."""
    name, anzahl, punkte = "frau_offen", 4600, 32
    schritt, volumen, volumen_weg, hub, steif = 0.005, (0.002, 0.014), 0.05, 0.3, 0.28
    wellen, klumpen = (0.005, 0.055), (12, 0.45)

    def kamm(self, q, n, s, w):
        return _norm(self.scheitel(q, w) + HINTEN * 0.35 * glatt(s, 0.04, 0.12))

    def schwere(self, s, w):
        return 1.0 * glatt(s, 0.02, 0.1)

    def laenge(self, w, n):
        return 0.55

    def ende(self, q, s, w):
        h = self.h
        schulter = (h.schulter["l"][2] + h.schulter["r"][2]) / 2
        r = float(h.richtung(np.asarray(q))[0])
        schnitt = schulter - (0.06 + 0.08 * glatt(r, 60, 180)) * self.M
        return q[2] < schnitt + (hash((round(w[0], 5), round(w[2], 5))) % 1000) / 1000 * 0.05 * self.M


class FrauZoepfe(FrauScheitel):
    """Mittelscheitel, Haar hinter den Ohren zu zwei Zöpfen, die über die Schultern nach vorn hängen."""
    name, anzahl, punkte = "frau_zoepfe", 4600, 16
    schritt, volumen, volumen_weg, hub, steif = 0.004, (0.0015, 0.007), 0.04, 0.3, 0.32
    wellen, klumpen = (0.0012, 0.05), (14, 0.25)

    def __init__(self, h, rng):
        super().__init__(h, rng)
        M = self.M
        self.anker = {}
        for s, sx in (("l", 1.0), ("r", -1.0)):
            q = h.ohr[s] + np.array([-sx * 0.004 * M, 0.03 * M, -0.03 * M])
            l, n, _, _ = h.naechste(q)
            self.anker[s] = Vector(l) + Vector(n) * 0.012 * M

    def _seite(self, w):
        return "l" if w[0] >= 0 else "r"

    frei_faellt = 0.0

    def kamm(self, q, n, s, w):
        zum = _norm(self.anker[self._seite(w)] - q)
        k = 1.0 if w[2] < self.anker[self._seite(w)][2] + 0.01 * self.M else glatt(s, 0.02, 0.08)
        return _norm(self.scheitel(q, w) * (1 - k) + zum * (2.0 * k))

    def schwere(self, s, w):
        return 0.25

    def laenge(self, w, n):
        return 0.4

    def ende(self, q, s, w):
        return (q - self.anker[self._seite(w)]).length < 0.018 * self.M


def zopf_bahn(h, anker, seite, M, laenge=0.27):
    """Mittellinie eines Zopfs: vom Anker hinter dem Ohr über die Schulter nach vorn hinab."""
    sx = 1.0 if seite == "l" else -1.0
    a = np.array(anker)
    sch = h.schulter[seite]
    stuetz = [a,
              a + np.array([sx * 0.02, -0.005, -0.045]) * M,
              np.array([sch[0] - sx * 0.035 * M, sch[1] - 0.035 * M, sch[2] + 0.03 * M]),
              np.array([sch[0] - sx * 0.05 * M, sch[1] - 0.09 * M, sch[2] - 0.04 * M]),
              np.array([sch[0] - sx * 0.055 * M, sch[1] - 0.11 * M, sch[2] - 0.14 * M])]
    stuetz = np.array(stuetz)
    # glatte Kurve (Catmull-Rom), dann aus der Haut schieben
    p = np.vstack([2 * stuetz[0] - stuetz[1], stuetz, 2 * stuetz[-1] - stuetz[-2]])
    fein = []
    for i in range(1, len(p) - 2):
        for t in np.linspace(0, 1, 40, endpoint=False):
            a0, b, c, d = p[i - 1], p[i], p[i + 1], p[i + 2]
            fein.append(0.5 * ((2 * b) + (-a0 + c) * t + (2 * a0 - 5 * b + 4 * c - d) * t * t + (-a0 + 3 * b - 3 * c + d) * t ** 3))
    fein.append(p[-2])
    fein = np.array(fein)
    for j in range(len(fein)):
        abst, l, n = h.abstand(fein[j])
        if l is not None and abst < 0.016 * M:
            fein[j] = l + n * 0.016 * M
    for _ in range(6):
        fein[1:-1] = 0.25 * fein[:-2] + 0.5 * fein[1:-1] + 0.25 * fein[2:]
    u = umtasten(fein, 200)
    seg = np.concatenate([[0], np.cumsum(np.linalg.norm(np.diff(u, axis=0), axis=1))])
    ende = np.searchsorted(seg, laenge * M)
    return u[:max(ende, 10)]


def zopf_straehnen(h, bahn, rng, M, anzahl=420, punkte=64, dicke=0.011, schwanz=0.045):
    """Dreistrang-Zopf entlang der Bahn: drei Bündel kreuzen sich (Achterbahn), am Ende Band und lose Spitze."""
    u = umtasten(bahn, 400)
    seg = np.concatenate([[0], np.cumsum(np.linalg.norm(np.diff(u, axis=0), axis=1))])
    L = seg[-1]
    tang = np.gradient(u, axis=0)
    tang /= np.linalg.norm(tang, axis=1, keepdims=True)
    # Querachsen: „vorn“ = vom Körper weg (Haut-Normale), „seitlich“ = quer dazu
    vorn = np.zeros_like(u)
    for j in range(len(u)):
        _, l, n = h.abstand(u[j])
        vorn[j] = n if n is not None else np.array([0, -1.0, 0])
    for _ in range(10):
        vorn[1:-1] = 0.25 * vorn[:-2] + 0.5 * vorn[1:-1] + 0.25 * vorn[2:]
    vorn -= tang * (vorn * tang).sum(1, keepdims=True)
    vorn /= np.linalg.norm(vorn, axis=1, keepdims=True)
    seit = np.cross(tang, vorn)
    periode = 0.038 * M
    ende_zopf = L - schwanz * M
    st = []
    for k in range(anzahl):
        b = k % 3
        r = dicke * M * 0.32 * math.sqrt(rng.random())
        phi = rng.random() * 2 * np.pi
        versatz = rng.normal(0, 0.003)
        pts = []
        for j in range(len(u)):
            s = seg[j]
            t = 2 * np.pi * (s / periode) + b * 2 * np.pi / 3
            verjuengt = 1.0 - 0.45 * glatt(s, ende_zopf - 0.06 * M, ende_zopf) + 0.25 * glatt(s, ende_zopf + 0.005, L)
            ax = dicke * M * 0.55 * math.sin(t) * verjuengt
            az = dicke * M * 0.28 * math.sin(2 * t) * verjuengt
            if s > ende_zopf:          # nach dem Band: lose Spitze, leicht gefächert
                f = (s - ende_zopf) / max(L - ende_zopf, 1e-6)
                ax = 0.004 * M * math.cos(phi) * (0.4 + 1.6 * f)
                az = 0.004 * M * math.sin(phi) * (0.4 + 1.6 * f)
                rr = 0
            else:
                rr = r * verjuengt
            pts.append(u[j] + seit[j] * (ax + rr * math.cos(phi) + versatz) + vorn[j] * (az + rr * math.sin(phi)))
        pts = np.array(pts)
        st.append(umtasten(pts, punkte))
    return np.array(st)


def knoten_straehnen(stil, rng, anzahl=900, punkte=32):
    """Haarknoten: eine flache Spirale aus Strähnen um den Anker (Scheibe, vom Kopf weg gewölbt)."""
    M = stil.M
    a = np.array(stil.anker)
    n = np.array(stil.anker_n)
    e1 = np.cross(n, [0, 0, 1.0])
    e1 /= np.linalg.norm(e1)
    e2 = np.cross(n, e1)
    st = []
    for k in range(anzahl):
        r0 = 0.004 * M + 0.024 * M * math.sqrt(rng.random())
        phi0 = rng.random() * 2 * np.pi
        umlauf = rng.uniform(1.2, 2.2)
        pts = []
        for j in range(punkte * 3):
            t = j / (punkte * 3 - 1)
            phi = phi0 + umlauf * 2 * np.pi * t
            r = r0 * (1 - 0.25 * t) + 0.002 * math.sin(7 * phi)
            hoehe = 0.016 * M * math.sqrt(max(0.0, 1 - (r / (0.03 * M)) ** 2)) - 0.004 * M
            pts.append(a + e1 * r * math.cos(phi) + e2 * r * math.sin(phi) + n * hoehe)
        st.append(umtasten(np.array(pts), punkte))
    return np.array(st)


# ================================================================= Bärte
class Bart(Stil):
    teil, geschlecht, punkte = "bart", "m", 8
    schritt, volumen, volumen_weg, hub, steif = 0.0015, (0.001, 0.006), 0.012, 0.6, 0.45
    wellen, klumpen, kraus = (0.0015, 0.012), (8, 0.25), 0.25

    def __init__(self, h, rng):
        super().__init__(h, rng)
        k = h.k
        self.m68 = h.m68
        lip = k.lippe + (h._knn_versatz(k.lippe) if h.p is not k.p0 else 0)
        self.lippe_oben = lip[:, 2].max()
        self.lippe_unten = lip[:, 2].min()
        self.mundbreite = abs(self.m68[54, 0] - self.m68[48, 0]) / 2
        self.lip = lip

    def flaechen(self, q):
        """Bartgegenden (je 0..1): Schnurrbart (Oberlippe), Kinn (unter der Unterlippe bis unters Kinn), Wangen
        (unter der Linie Ohr – Nasenflügel, Kiefer), Hals (unter dem Kinn bis zum Kehlkopf)."""
        q = np.atleast_2d(q)
        h, M, m = self.h, self.M, self.m68
        X, Y, Z = q[:, 0], q[:, 1], q[:, 2]
        vorn = glatt(-(Y - (h.ohr["l"][1] - 0.006 * M)), 0.0, 0.012 * M)
        nicht_lippe = np.ones(len(q))
        dl = np.min(np.linalg.norm(q[:, None] - self.lip[None], axis=2), axis=1)
        nicht_lippe = glatt(dl, 0.0025 * M, 0.0045 * M)
        # Lippenrot (zwischen den Lippennähten) frei lassen
        im_mund = (np.abs(X) < self.mundbreite * 0.95) & (Z < self.lippe_oben + 0.001) & (Z > self.lippe_unten - 0.001) & (Y < m[51, 1] + 0.01)
        nicht_lippe *= ~im_mund
        schnurr = (glatt(m[33, 2] - 0.002 * M - Z, 0.0, 0.003 * M) * glatt(Z - (self.lippe_oben - 0.004 * M), 0.0, 0.003 * M)
                   * glatt(self.mundbreite + 0.009 * M - np.abs(X), 0.0, 0.006 * M) * glatt(-(Y - m[33, 1] - 0.025 * M), 0, 0.01))
        kinn = (glatt(self.lippe_unten - 0.003 * M - Z, 0.0, 0.004 * M) * glatt(self.mundbreite + 0.006 * M - np.abs(X), 0.0, 0.008 * M)
                * glatt(Z - (m[8, 2] - 0.035 * M), 0.0, 0.01 * M))
        wange = np.zeros(len(q))
        for s, sx, ohr_m, nasen_m in (("l", 1.0, 15, 35), ("r", -1.0, 1, 31)):
            # Linie vom Kieferrand unter dem Ohr zum Mundwinkel über der Lippe (nicht bis zum Wangenknochen)
            a = m[ohr_m] + np.array([0, 0, -0.008 * M])
            b = m[nasen_m] + np.array([sx * 0.014 * M, 0, -0.016 * M])
            t = np.clip((X * sx - a[0] * sx) / (b[0] * sx - a[0] * sx), 0, 1)
            linie = a[2] + (b[2] - a[2]) * t
            sel = np.sign(X) == sx
            wange[sel] = glatt(linie[sel] - Z[sel], -0.002 * M, 0.01 * M)
            # vor dem Ohr nur ein schmaler Streifen, das Ohr selbst frei
            wange[sel] *= glatt(np.linalg.norm(q[sel] - h.ohr[s], axis=1), 0.022 * M, 0.034 * M)
        wange *= glatt(Z - (m[8, 2] - 0.03 * M), 0.0, 0.01 * M)
        hals_a = (q - self.h.k.kragen_mitte) @ self.h.k.hals_achse
        hals = glatt(m[8, 2] + 0.005 * M - Z, 0.0, 0.006 * M) * glatt(hals_a, 0.045 * M, 0.06 * M)
        gemein = vorn * nicht_lippe * (~h.k.ist_bueste[:len(q)] if len(q) == len(h.p) else 1.0)
        return {"schnurr": schnurr * gemein, "kinn": kinn * gemein, "wange": wange * gemein, "hals": hals * gemein}

    def zone(self, w):
        f = {k: float(v[0]) for k, v in self.flaechen(np.asarray(w)[None]).items()}
        return max(f, key=f.get)


class Schnurrbart(Bart):
    name, anzahl = "schnurrbart", 1400

    def dichte(self, q):
        return self.flaechen(q)["schnurr"]

    def kamm(self, q, n, s, w):
        sx = 1.0 if w[0] >= 0 else -1.0
        aussen = glatt(abs(w[0]), 0.0, self.mundbreite)
        return _norm(Vector((sx * (0.4 + 0.5 * aussen), -0.1, -1.0)))

    def schwere(self, s, w):
        return 0.4

    def laenge(self, w, n):
        aussen = glatt(abs(w[0]), 0.0, self.mundbreite + 0.006 * self.M)
        return (0.012 + 0.014 * aussen) * self.rng.uniform(0.8, 1.15) * self.M


class Kinnbart(Bart):
    """Schnurrbart und Spitzbart am Kinn (Spitzen laufen unter dem Kinn zusammen)."""
    name, anzahl = "kinnbart", 2700

    def dichte(self, q):
        f = self.flaechen(q)
        return np.maximum(f["schnurr"], f["kinn"])

    def kamm(self, q, n, s, w):
        if w[2] > self.lippe_oben - 0.004 * self.M:
            return Schnurrbart.kamm(self, q, n, s, w)
        spitze = Vector((0.0, self.m68[8, 1] - 0.012 * self.M, self.m68[8, 2] - 0.035 * self.M))
        return _norm(UNTEN + (spitze - q) * 20.0)

    def schwere(self, s, w):
        return 0.5

    def laenge(self, w, n):
        if w[2] > self.lippe_oben - 0.004 * self.M:
            return Schnurrbart.laenge(self, w, n)
        return (0.03 + 0.018 * glatt(self.mundbreite - abs(w[0]), 0, self.mundbreite)) * self.rng.uniform(0.85, 1.1) * self.M


class Vollbart(Bart):
    """Gestutzter Vollbart: Wangen, Kiefer, Kinn, Oberlippe, unter dem Kinn."""
    name, anzahl = "vollbart", 6000
    volumen = (0.001, 0.006)

    def dichte(self, q):
        f = self.flaechen(q)
        return np.clip(np.maximum.reduce([f["schnurr"], f["kinn"], f["wange"], f["hals"] * 0.9]), 0, 1)

    def kamm(self, q, n, s, w):
        if self.zone(w) == "schnurr":
            return Schnurrbart.kamm(self, q, n, s, w)
        zur_mitte = Vector((-w[0] * 6.0, -0.15, 0.0))
        return _norm(UNTEN + zur_mitte * 0.35 + VORN * 0.15)

    def schwere(self, s, w):
        return 0.6

    def laenge(self, w, n):
        z = self.zone(w)
        if z == "schnurr":
            return Schnurrbart.laenge(self, w, n)
        unten = glatt(self.lippe_unten - w[2], 0.0, 0.04 * self.M)
        L = {"kinn": 0.04, "wange": 0.013 + 0.02 * unten, "hals": 0.025}[z]
        return L * self.rng.uniform(0.85, 1.1) * self.M


class Langbart(Vollbart):
    """Langer Vollbart: am Kinn und Kiefer lang, läuft vor der Brust spitz zu."""
    name, anzahl, punkte = "langbart", 5600, 16
    schritt, volumen = 0.003, (0.002, 0.012)
    klumpen = (10, 0.4)

    def kamm(self, q, n, s, w):
        if self.zone(w) == "schnurr":
            return Schnurrbart.kamm(self, q, n, s, w)
        M = self.M
        spitze = Vector((0.0, self.m68[8, 1] - 0.02 * M, self.m68[8, 2] - 0.15 * M))
        k = glatt(s, 0.02, 0.06)
        return _norm(UNTEN * (1 - k) + Vector((-w[0] * 6.0, -0.15, 0)) * 0.3 * (1 - k) + _norm(spitze - q) * 1.5 * k)

    def schwere(self, s, w):
        return 0.8

    def laenge(self, w, n):
        z = self.zone(w)
        if z == "schnurr":
            return Schnurrbart.laenge(self, w, n) * 1.3
        unten = glatt(self.lippe_unten - w[2], 0.0, 0.04 * self.M)
        L = {"kinn": 0.15, "wange": 0.025 + 0.11 * unten ** 1.5, "hals": 0.1}[z]
        return L * self.rng.uniform(0.85, 1.1) * self.M


FRISUREN = {"m": [MannKurz, MannPagen, MannZurueck], "w": [FrauKnoten, FrauOffen, FrauZoepfe]}
BAERTE = [Schnurrbart, Kinnbart, Vollbart, Langbart]
