"""Bewegungen der Zugtiere (Pferd, Ochse): eigener Vierbeiner-Schritt statt der Bewegungen der Vorlage.

Wird von zugtiere.py benutzt (läuft in Blender). Alles ist Eigenbau; von Quaternius stammen nur Netz und Skelett.

Gangart (Gehen, Ziehen, Anfahren_*, Anhalten_*): Eine Planung setzt für jeden Huf die Aufsetz- und Abhebepunkte in der Welt
(Fußfolge links hinten, links vorn, rechts hinten, rechts vorn, je eine Viertelschleife auseinander). Im Stand liegt der Huf fest
auf einem Punkt der Welt (er rutscht nicht), im Schwung läuft er auf einem Bogen zum nächsten Punkt. Die Hufziele des Skeletts
(IK der Vorlage, drei Knochen je Bein) folgen dieser Bahn, der Rumpf senkt sich so weit, dass jedes Bein erreichbar bleibt (die Bahn
des Rumpfes ergibt sich daraus, nicht aus einer Sinuskurve), Hals und Kopf nicken, der Schweif schwingt nach. Danach wird die
fertige Pose jedes Bildes (IK eingerechnet) in Knochendrehungen übertragen; die IK-Bedingungen fallen weg.

Zeit: Das Weltmaß ist der Weg X(t) des Rumpfes (Meter). Bei Tempo v und Anlaufkurve A(t) (0 bis 1) ist X' = v · A.
Das Spiel bewegt den Karren mit derselben Kurve (siehe game/daten/zugtiere.json, „anlauf“).
"""

import math

import numpy as np
from mathutils import Quaternion, Vector

FPS = 24

# Fußfolge: Name des Hufziels, Phase (Anteil der Schleife, in dem der Huf abhebt)
FUESSE = {"LH": ("BackFoot.L", 0.00), "LF": ("FrontFoot.L", 0.25), "RH": ("BackFoot.R", 0.50), "RF": ("FrontFoot.R", 0.75)}
VORN = {"LF", "RF"}

# T: Dauer einer Schleife (zwei Schritte je Seite = ein voller Zyklus) in Sekunden, v: Tempo (m/s bei Tempo 1 des Karrens),
# beta: Anteil der Schleife, in dem ein Huf am Boden steht, hub: Höhe des Hufs im Schwung (Meter, vorn, hinten),
# kniebeuge: Rumpf bleibt so viel tiefer als gerade nötig (Meter, im Gehen), nick: Kopfnicken (Grad), rollen: Wiegen des Rumpfs (Grad),
# schlaengeln: Biegung der Wirbelsäule (Grad), schweif: Schwung des Schweifs (Grad je Knochen 1 bis 4)
PARAMETER = {
    "pferd": dict(T=1.25, v=1.3, beta=0.62, hub=(0.115, 0.095), kniebeuge=0.020, nick=3.6, rollen=1.5, schlaengeln=1.6,
                  schweif=(5.0, 7.0, 9.0, 11.0), schweif_phase=0.16,
                  zug=dict(kopf=18.0, hals=26.0, rumpf=1.6, schulter=1.6, nick=3.5, ausgleich=(0.037, 0.046))),
    "ochse": dict(T=1.5, v=0.8, beta=0.70, hinten_vor=0.05, hub=(0.065, 0.055), kniebeuge=0.016, nick=1.6, rollen=1.1, schlaengeln=1.0,
                  schweif=(3.0, 4.5, 6.0, 8.0), schweif_phase=0.2,
                  zug=dict(kopf=17.0, hals=22.0, rumpf=1.6, schulter=2.0, nick=2.4, ausgleich=(0.062, 0.045)), reichweite=0.965),
}


def _glatt(x):
    x = max(0.0, min(1.0, x))
    return x * x * (3 - 2 * x)


def _glatter(x):
    x = max(0.0, min(1.0, x))
    return x * x * x * (x * (6 * x - 15) + 10)


def buckel(t, a, b):
    """0 außerhalb von [a, b], dazwischen eine weiche Erhebung 0 → 1 → 0."""
    if t <= a or t >= b:
        return 0.0
    return math.sin(math.pi * (t - a) / (b - a)) ** 2


def plateau(t, a, e1, e2, b):
    """0 vor a, weich auf 1 bis e1, 1 bis e2, weich auf 0 bis b."""
    if t <= a or t >= b:
        return 0.0
    if t < e1:
        return _glatt((t - a) / (e1 - a))
    if t <= e2:
        return 1.0
    return _glatt((b - t) / (b - e2))


# ─────────────────────────────────────────────────────────────────────────────
#  Gangplanung
# ─────────────────────────────────────────────────────────────────────────────

class Gang:
    """Plant die Hufe. A(t) = Anlaufkurve (0 bis 1), start = Zeit, vor der das Tier stand (None: der Gang läuft schon ewig).
    Zeiten in Sekunden, t = 0 ist der erste Bildpunkt des Clips."""

    DT = 1.0 / 480.0

    def __init__(self, par, A, start=None, t_min=-4.0, t_max=6.0, zug=False, ende_stand=False):
        self.par, self.A, self.start = par, A, start
        self.T, self.v, self.beta = par["T"], par["v"], par["beta"]
        self.t_min, self.t_max = t_min, t_max
        n = int(round((t_max - t_min) / self.DT)) + 1
        self.zeiten = t_min + np.arange(n) * self.DT
        geschw = np.array([self.v * A(t) for t in self.zeiten])
        # X(t): Weg des Rumpfes; X(0) = 0
        x = np.concatenate([[0.0], np.cumsum((geschw[1:] + geschw[:-1]) * 0.5 * self.DT)])
        i0 = int(round(-t_min / self.DT))
        self.x = x - x[i0]
        self.zug = zug
        self._ereignisse = {k: self._plane(k) for k in FUESSE}

    def X(self, t):
        return float(np.interp(t, self.zeiten, self.x))

    def _plane(self, name):
        phi = FUESSE[name][1]
        T, beta = self.T, self.beta
        schwung = (1.0 - beta) * T
        ereignisse = []
        vorher = 0.0 if self.start is not None else None
        k0 = int(math.floor(self.t_min / T)) - 1
        k1 = int(math.ceil(self.t_max / T)) + 1
        for k in range(k0, k1 + 1):
            tau = (k + phi) * T
            td = tau + schwung
            if self.start is not None and tau < self.start:
                continue
            if self.start is None and tau < 0.0:
                ziel = self.v * (td + beta * T * 0.5)             # schon begonnene Schritte werden zu Ende gegangen wie geplant
            else:
                ziel = self.X(td + beta * T * 0.5)
            if vorher is None:
                ereignisse.append((tau, td, ziel, ziel, False))
                vorher = ziel
                continue
            if name not in VORN:                                   # Hinterhufe stehen etwas weiter vorn unter dem Rumpf (kürzere Reichweite nach hinten)
                ziel += self.par.get("hinten_vor", 0.0) * self.A(td + beta * T * 0.5)
            if abs(ziel - vorher) < 0.003:                       # nichts zu tun: der Huf bleibt stehen
                ereignisse.append((tau, td, vorher, vorher, False))
                continue
            ereignisse.append((tau, td, vorher, ziel, True))
            vorher = ziel
        return ereignisse

    def huf(self, name, t):
        """(Versatz nach vorn gegenüber der Grundstellung in Metern, Höhe in Metern, am Boden?) des Hufs zur Zeit t."""
        phi = FUESSE[name][1]
        ev = self._ereignisse[name]
        letzter = None
        for e in ev:
            if e[0] <= t:
                letzter = e
            else:
                break
        hub = self.par["hub"][0 if name in VORN else 1]
        if letzter is None:
            return -self.X(t), 0.0, True
        tau, td, vor, ziel, schritt = letzter
        if schritt and t < td:
            w = (t - tau) / (td - tau)
            amp = 0.35 + 0.65 * min(1.0, self.A(tau + (td - tau) * 0.5))
            pos = vor + (ziel - vor) * _glatt(w)
            hoehe = hub * amp * math.sin(math.pi * w ** 0.9)
            return pos - self.X(t), hoehe, False
        return ziel - self.X(t), 0.0, True


# ─────────────────────────────────────────────────────────────────────────────
#  Skelett ansteuern
# ─────────────────────────────────────────────────────────────────────────────

class Rumpf:
    """Hilfen am Skelett: Achsen der Knochen, Pose zurücksetzen, Pose lesen."""

    def __init__(self, ske, art, sohle=None):
        self.ske, self.art = ske, art
        self.sohle = sohle or {}
        self.rest = {b.name: b.matrix_local.to_3x3().inverted() for b in ske.data.bones}
        self.kopf = {b.name: Vector(b.head_local) for b in ske.data.bones}
        self.schwanz = {b.name: Vector(b.tail_local) for b in ske.data.bones}
        self.namen = [pb.name for pb in ske.pose.bones]

    def zuruecksetzen(self):
        for pb in self.ske.pose.bones:
            pb.rotation_mode = "QUATERNION"
            pb.location = (0, 0, 0)
            pb.rotation_quaternion = (1, 0, 0, 0)
        for fn, z in self.sohle.items():                       # alle Sohlen stehen auf dem Boden (die Vorlage hat sie unterschiedlich hoch)
            self.verschieben(fn, (0, 0, -z))

    def drehen(self, name, achse, grad):
        """Dreht den Knochen zusätzlich um die Weltachse `achse` (Grad; positiv um +X = Nase nach unten bei Blick nach -Y)."""
        if abs(grad) < 1e-9 or name not in self.rest:
            return
        pb = self.ske.pose.bones[name]
        lokal = (self.rest[name] @ Vector(achse)).normalized()
        pb.rotation_quaternion = Quaternion(lokal, math.radians(grad)) @ pb.rotation_quaternion

    def verschieben(self, name, delta):
        pb = self.ske.pose.bones[name]
        pb.location = pb.location + self.rest[name] @ Vector(delta)

    def huf_luecke(self):
        """Größte Entfernung (Meter) zwischen dem Ende eines Unterschenkels und dem Kopf seines Hufziels: wo die IK das Ziel nicht erreicht."""
        g = 0.0
        for fn in ("FrontFoot.L", "FrontFoot.R", "BackFoot.L", "BackFoot.R"):
            unten = fn.replace("Foot", "LowLeg")
            g = max(g, (self.ske.pose.bones[unten].tail - self.ske.pose.bones[fn].head).length)
        return g

    def lesen(self):
        import bpy
        bpy.context.view_layer.update()
        r = {}
        for pb in self.ske.pose.bones:
            m = self.ske.convert_space(pose_bone=pb, matrix=pb.matrix, from_space="POSE", to_space="LOCAL")
            loc, q, _ = m.decompose()
            r[pb.name] = (loc.copy(), q.copy())
        return r


X_ACHSE, Y_ACHSE, Z_ACHSE = (1, 0, 0), (0, 1, 0), (0, 0, 1)


def _nachlauf(t, T, phase, i):
    return math.sin(2 * math.pi * (t / T - phase * (i + 1)))


def _glaetten(werte, sigma_bilder):
    n = int(sigma_bilder * 4)
    k = np.exp(-0.5 * (np.arange(-n, n + 1) / sigma_bilder) ** 2)
    k /= k.sum()
    return np.convolve(np.pad(werte, n, mode="edge"), k, mode="valid")


def gang_rahmen(rumpf, art, zug, modus, anzahl=None):
    """Clip „schleife“, „anfahren“ oder „anhalten“ als Bilderliste (Knochen -> (Verschiebung, Drehung)).
    Gibt (Bilder, Daten) zurück; Daten enthält die Planung (für die Messung) und den Wegverlauf X(t) je Bild."""
    par = PARAMETER[art]
    T, v = par["T"], par["v"]
    z = par["zug"]
    if modus == "schleife":
        A = lambda t: 1.0
        start = None
        n = int(round(T * FPS))
        stopp = None
    elif modus == "anfahren":
        dauer = 2.0 * T
        rampe = 0.95 * T
        A = lambda t: 0.0 if t <= 0 else _glatter(t / rampe)
        start = 0.0
        n = int(round(dauer * FPS))
    else:
        dauer = 2.0 * T
        rampe = 0.95 * T
        A = lambda t: 1.0 if t <= 0 else 1.0 - _glatter(t / rampe)
        start = None
        n = int(round(dauer * FPS))
    gang = Gang(par, A, start=start, zug=zug, t_min=-4.0, t_max=n / FPS + 3.0)
    ske = rumpf.ske
    d = ske.data.bones

    # Ruhelage der Hufe und Hüftpunkte (Meter, Skelett schon im Maß der Welt)
    huf0 = {n_: Vector(d[FUESSE[n_][0].replace("Foot", "LowLeg")].tail_local) for n_ in FUESSE}
    wurzel = {n_: Vector(d[FUESSE[n_][0].replace("Foot", "Leg")].head_local) for n_ in FUESSE}
    laenge = {}
    for n_, (fn, _) in FUESSE.items():
        teile = [fn.replace("Foot", p) for p in ("Leg", "UpLeg", "LowLeg")]
        laenge[n_] = sum((Vector(d[x_].tail_local) - Vector(d[x_].head_local)).length for x_ in teile)
    # Zeitreihen: Rumpfsenkung (aus der Erreichbarkeit), Stütze links/rechts, vorn/hinten – dicht, dann geglättet
    zd = np.arange(-1.0, n / FPS + 1.0 + 1e-9, 1.0 / 240.0)
    senk = np.zeros(len(zd))
    st_l = np.zeros(len(zd))
    st_vorn = np.zeros(len(zd))
    for i, t in enumerate(zd):
        brauch = 0.0
        for n_ in FUESSE:
            vor, hoehe, boden = gang.huf(n_, t)
            p = huf0[n_] + Vector((0, -vor, hoehe - rumpf.sohle.get(FUESSE[n_][0], 0.0)))
            w = wurzel[n_]
            dx, dy = p.x - w.x, p.y - w.y
            maxhoehe = math.sqrt(max(0.0, (par.get("reichweite", 0.995) * laenge[n_]) ** 2 - dx * dx - dy * dy))
            frei = (p.z + maxhoehe) - w.z                 # so viel höher dürfte die Wurzel liegen (negativ: muss tiefer)
            brauch = min(brauch, frei)
            fest = 1.0 if boden else 0.0
            if n_ in ("LH", "LF"):
                st_l[i] += fest
            if n_ in VORN:
                st_vorn[i] += fest
        senk[i] = brauch
    sigma = 0.05 * 240
    senk_g = _glaetten(senk, sigma)
    rollen_signal = _glaetten((st_l - (2 - st_l - 0.0)) / 2.0, 0.10 * 240)   # + links stärker gestützt
    nick_signal = _glaetten(st_vorn - (2 - st_vorn), 0.10 * 240)

    def lesen(arr, t):
        return float(np.interp(t, zd, arr))

    bilder = []
    daten = {"X": [], "huf": [], "A": [], "t": [], "boden": []}
    for i in range(n + 1):
        t = i / FPS
        a = A(t)
        extra = 0.0
        for versuch in range(40):                   # der Rumpf sinkt, bis jeder Unterschenkel sein Hufziel erreicht (keine Lücke zwischen Bein und Huf)
            rumpf.zuruecksetzen()
            # Hufe
            info = {}
            for n_, (fn, phi) in FUESSE.items():
                vor, hoehe, boden = gang.huf(n_, t)
                rumpf.verschieben(fn, (0, -vor, hoehe))
                info[n_] = (vor, hoehe, boden)
            # Rumpf: Senkung aus der Erreichbarkeit plus Kniebeuge im Gehen
            dz = lesen(senk_g, t) - par["kniebeuge"] * a + extra
            rumpf.verschieben("Body", (0, 0, dz))
            # Wiegen und Pendeln der Wirbelsäule
            roll = par["rollen"] * a * lesen(rollen_signal, t) * 0.5
            rumpf.drehen("Body", Y_ACHSE, roll)
            rumpf.drehen("Torso", Z_ACHSE, par["schlaengeln"] * a * math.sin(2 * math.pi * (t / T + 0.1)))
            rumpf.drehen("Hips", Z_ACHSE, -0.6 * par["schlaengeln"] * a * math.sin(2 * math.pi * (t / T + 0.1)))
            rumpf.drehen("Hips", Y_ACHSE, -0.5 * roll)
            # Neigung des Rumpfs: leicht vorn beim Ziehen, kleines Nicken im Takt
            neig = (z["rumpf"] * a if zug else 0.0) + 0.5 * a * lesen(nick_signal, t) * (1.2 if zug else 0.7)
            rumpf.drehen("Body", X_ACHSE, neig)
            # Hals und Kopf
            nick = par["nick"] * a * math.sin(2 * math.pi * (t / T + 0.15))
            if zug:
                nick = z["nick"] * a * math.sin(2 * math.pi * (t / T + 0.15))
                rumpf.drehen("Shoulders", X_ACHSE, (z["schulter"] + 0.9 * z["schulter"] * math.sin(2 * math.pi * (t / T + 0.35))) * a)
                rumpf.verschieben("Shoulders", (0, -0.022 * a * (0.5 + 0.5 * math.sin(2 * math.pi * (t / T + 0.35))), 0))
                rumpf.drehen("Neck", X_ACHSE, z["hals"] * a)
                rumpf.drehen("Head", X_ACHSE, z["kopf"] * a * 0.6)
            # das Geschirr (Anker_Geschirr) bleibt beim Ziehen etwa dort, wo es im Stehen war; im Gehen ohne Zug zu 60 % (Kniebeuge und Rumpfsenkung)
            rumpf.verschieben("Body", (0, z["ausgleich"][0] * a * (1.0 if zug else 0.6), z["ausgleich"][1] * a * (1.0 if zug else 0.6)))
            rumpf.drehen("Neck", X_ACHSE, nick)
            rumpf.drehen("Head", X_ACHSE, -0.45 * nick)
            rumpf.drehen("Neck", Z_ACHSE, 1.2 * a * math.sin(2 * math.pi * (t / T + 0.1)))
            rumpf.drehen("Ohr_L", Y_ACHSE, 4.0 * a * math.sin(2 * math.pi * (t / T + 0.2)))
            rumpf.drehen("Ohr_R", Y_ACHSE, -4.0 * a * math.sin(2 * math.pi * (t / T + 0.45)))
            # Schweif schwingt nach
            for j, amp in enumerate(par["schweif"]):
                rumpf.drehen(f"Tail{j + 1}", Z_ACHSE, amp * a * _nachlauf(t, T, par["schweif_phase"], j))
                rumpf.drehen(f"Tail{j + 1}", X_ACHSE, 0.35 * amp * a * _nachlauf(t, T / 2.0, par["schweif_phase"], j))
            r_pose = rumpf.lesen()
            luecke = rumpf.huf_luecke()
            if luecke < 0.002:
                break
            extra -= 1.0 * min(luecke, 0.08)
        bilder.append(r_pose)
        daten["t"].append(t)
        daten["X"].append(gang.X(t))
        daten["A"].append(a)
        daten["huf"].append(info)
    return bilder, daten


# ─────────────────────────────────────────────────────────────────────────────
#  Stehen, Ruhen, Umschauen, Fressen
# ─────────────────────────────────────────────────────────────────────────────

STEHEN = {
    "pferd": dict(atmen=0.5, schweif=(3.0, 4.5, 6.0, 8.0), umschauen=52.0, hals_hoch=9.0, fressen_hals=40.0, fressen_kopf=22.0,
                  fressen_schulter=14.0, kauen=1.6),
    "ochse": dict(atmen=0.55, schweif=(2.0, 3.0, 4.5, 6.0), umschauen=48.0, hals_hoch=6.0, fressen_hals=34.0, fressen_kopf=22.0,
                  fressen_schulter=13.0, kauen=1.8),
}
STEHEN_DAUER = {"Stehen": 6.0, "Stehen_Ruhen": 9.0, "Stehen_Umschauen": 8.0, "Fressen": 9.0}


def stehen_rahmen(rumpf, art, name):
    """Stehen (Atmen, Schweif, Kopf), Stehen_Ruhen (Gewichtsverlagerung, ein Hinterhuf ruht), Stehen_Umschauen (Kopf hebt und dreht),
    Fressen (Kopf zum Boden, Kauen, Kopf hoch, wieder zum Stehen). Alle beginnen und enden in derselben Grundpose und bestehen aus
    ganzen Wellen: Schleifen und Wechsel ohne Sprung."""
    p = STEHEN[art]
    par = PARAMETER[art]
    dauer = STEHEN_DAUER[name]
    n = int(round(dauer * FPS))
    bilder = []
    for i in range(n + 1):
        t = i / FPS
        u = t / dauer
        rumpf.zuruecksetzen()
        w = 2 * math.pi * u
        # Atmen: eine Welle je Schleife (Stehen) bzw. mehr bei längeren Clips (etwa alle 5 s)
        wellen = max(1, int(round(dauer / 5.0)))
        atem = math.sin(w * wellen)
        rumpf.verschieben("Torso", (0, 0, 0.004 * p["atmen"] * atem))
        rumpf.drehen("Torso", X_ACHSE, 0.45 * p["atmen"] * atem)
        rumpf.drehen("Shoulders", X_ACHSE, 0.3 * atem)
        # Schweif: sanft nachschwingend, ein Wedeln
        for j, amp in enumerate(p["schweif"]):
            rumpf.drehen(f"Tail{j + 1}", Z_ACHSE, amp * 0.6 * (math.sin(w - 0.5 * (j + 1)) + math.sin(0.5 * (j + 1))))
        wedel = buckel(t, 0.55 * dauer, 0.70 * dauer)
        for j, amp in enumerate(p["schweif"]):
            rumpf.drehen(f"Tail{j + 1}", Z_ACHSE, 2.2 * amp * wedel * math.sin(2 * math.pi * 3 * (t - 0.55 * dauer) / dauer * dauer / 1.6 - 0.45 * (j + 1)))
        # ein wenig Kopfspiel
        rumpf.drehen("Neck", X_ACHSE, 1.1 * (math.sin(w + 0.6) - math.sin(0.6)))
        rumpf.drehen("Head", X_ACHSE, 0.8 * (math.sin(2 * w + 1.0) - math.sin(1.0)))
        rumpf.drehen("Neck", Z_ACHSE, 1.4 * math.sin(w))
        zucken = buckel(t, 0.20 * dauer, 0.26 * dauer) + buckel(t, 0.62 * dauer, 0.68 * dauer)
        rumpf.drehen("Ohr_L", Y_ACHSE, 12.0 * zucken * math.sin(2 * math.pi * 4 * t) + 2.0 * math.sin(w))
        rumpf.drehen("Ohr_R", Y_ACHSE, -9.0 * buckel(t, 0.40 * dauer, 0.45 * dauer) * math.sin(2 * math.pi * 4 * t) - 2.0 * math.sin(w + 0.7))
        if name == "Stehen_Ruhen":
            # erst ruht der rechte, dann der linke Hinterhuf (von der Spielkamera sieht man die rechte, von der Seite die linke Seite): Huf angewinkelt
            # unter den Bauch, auf der Spitze, Hüfte gesenkt, Gewicht auf das andere Bein, Kopf hängt, Ohren zurück
            hub = 0.20 if art == "pferd" else 0.16
            gesamt = 0.0
            for fn, vz, ea, eb, ec, ed in (("BackFoot.R", -1.0, 0.5, 1.6, 3.6, 4.5), ("BackFoot.L", 1.0, 4.5, 5.6, 7.7, 8.8)):
                e = plateau(t, ea, eb, ec, ed)
                gesamt += e
                rumpf.verschieben(fn, (-vz * 0.03 * e, 0.16 * e, hub * e))          # Huf unter den Bauch und angehoben
                rumpf.drehen(fn, X_ACHSE, -30.0 * e)                                 # Spitze zeigt nach unten
                rumpf.drehen("Hips", Y_ACHSE, vz * 9.0 * e)                          # Hüfte auf der Seite des ruhenden Beins gesenkt
                rumpf.drehen("Body", Y_ACHSE, -vz * 3.5 * e)
                rumpf.verschieben("Body", (-vz * 0.09 * e, 0, 0))                    # Gewicht auf die andere Seite
            rumpf.verschieben("Body", (0, 0, -0.06 * min(1.0, gesamt)))
            rumpf.drehen("Neck", X_ACHSE, 20.0 * min(1.0, gesamt))                   # Kopf hängt
            rumpf.drehen("Head", X_ACHSE, 12.0 * min(1.0, gesamt))
            rumpf.drehen("Ohr_L", Y_ACHSE, 16.0 * min(1.0, gesamt))
            rumpf.drehen("Ohr_R", Y_ACHSE, -16.0 * min(1.0, gesamt))
            s = buckel(t, 4.6, 6.2)
            for j2, amp in enumerate(p["schweif"]):
                rumpf.drehen(f"Tail{j2 + 1}", Z_ACHSE, 2.4 * amp * s * math.sin(2 * math.pi * 2.5 * (t - 4.6) - 0.5 * (j2 + 1)))
        elif name == "Stehen_Umschauen":
            rumpf.drehen("Ohr_L", Z_ACHSE, 14.0 * plateau(t, 1.5, 2.4, 3.2, 4.0) - 8.0 * plateau(t, 3.9, 4.8, 5.6, 6.5))
            rumpf.drehen("Ohr_R", Z_ACHSE, 8.0 * plateau(t, 1.5, 2.4, 3.2, 4.0) - 14.0 * plateau(t, 3.9, 4.8, 5.6, 6.5))
            hoch = plateau(t, 0.4, 1.6, 6.4, 7.7)
            rumpf.drehen("Neck", X_ACHSE, -p["hals_hoch"] * hoch)
            rumpf.drehen("Head", X_ACHSE, 0.4 * p["hals_hoch"] * hoch)
            rumpf.drehen("Shoulders", X_ACHSE, -2.0 * hoch)
            links = plateau(t, 1.5, 2.4, 3.2, 4.0)
            rechts = plateau(t, 3.9, 4.8, 5.6, 6.5)
            gier = p["umschauen"] * (links - rechts)
            rumpf.drehen("Neck", Z_ACHSE, 0.45 * gier)
            rumpf.drehen("Head", Z_ACHSE, 0.55 * gier)
            rumpf.drehen("Torso", Z_ACHSE, 0.12 * gier)
        elif name == "Fressen":
            runter = plateau(t, 0.4, 1.8, 5.2, 6.4)                 # Kopf zum Boden
            hoch2 = plateau(t, 5.6, 6.4, 7.4, 8.4)                  # Kopf hoch, kauen, wieder herunter zum Stehen
            tief = runter * (1.0 - 0.0)
            rumpf.drehen("Shoulders", X_ACHSE, p["fressen_schulter"] * tief)
            rumpf.drehen("Neck", X_ACHSE, p["fressen_hals"] * tief)
            rumpf.drehen("Head", X_ACHSE, p["fressen_kopf"] * tief)
            rumpf.drehen("Body", X_ACHSE, 2.2 * tief)
            kau = tief * math.sin(2 * math.pi * 3.0 * t)
            rumpf.drehen("Head", X_ACHSE, p["kauen"] * kau)
            rumpf.drehen("Head", Z_ACHSE, 1.2 * tief * math.sin(2 * math.pi * 1.5 * t))
            hk = hoch2 * math.sin(2 * math.pi * 3.0 * t)
            rumpf.drehen("Head", Z_ACHSE, 2.0 * hk)
            # beim Weiden zieht das Tier einen Vorderhuf nach vorn
            e = plateau(t, 2.0, 2.8, 4.4, 5.2)
            rumpf.verschieben("FrontFoot.R", (0, -0.05 * e, 0.0))
        bilder.append(rumpf.lesen())
    return bilder
