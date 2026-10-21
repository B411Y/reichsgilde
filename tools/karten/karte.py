#!/usr/bin/env python3
"""
Werkzeug für die Stadtkarten von Reichsgilde (Bereich Leveldesign).

Eine Stadt ist ein eigenes Skript (z. B. koeln.py), das mit den Bausteinen hier eine Karte beschreibt.
Dieses Werkzeug
  • rastet alle Punkte ein (Bauzonen auf das 2-m-Raster),
  • prüft die Kartenregeln aus docs/LEVELDESIGN.md (z. B. „jede Bauzone liegt an einer Karrenstraße“)
    und die Karte gegen den Gebäudekatalog game/daten/katalog.json,
  • schreibt die Kartendatei game/karten/<kennung>.json im Kartenformat 2
  • und ein Vorschaubild docs/karten/<kennung>.svg, das nur aus der JSON-Datei (und dem Katalog) gezeichnet wird.

Kartenformat 2 (seit 21.10.2026, docs/LEVELDESIGN.md, „Kartenformat 2“): Die Karte sagt nur, WO etwas steht – je Gebäude die
Kennung des Bausteins im Katalog, Name, Mitte der Grundfläche und Drehung. WAS ein Gebäude ist (Modell, Grundfläche), steht im
Katalog. Dazu Mauern mit ihren Toren, Wege, Bauzonen, Natur (Wald- und Streuflächen), Wasser, Belegung am Start und Kamera.

Koordinaten: Meter auf dem Boden, x = Osten, z = Süden (−z = Norden), wie im Spiel (Godot).
Der Ursprung liegt am Marktplatz der Stadt. Drehung in Grad wie Godot Rotation.Y: Die Vorderseite eines Gebäudes zeigt bei 0 nach
Süden (+z), bei 90 nach Osten (+x), bei 180 nach Norden, bei 270 nach Westen. Die Kartendatei kennt kein Godot – Kern und
Darstellung lesen sie gleich.
"""
import json
import math
import os
import re

FORMAT = 2
RASTER = 2.0                       # Rasterfeld in Metern (entschieden 16.10.2026)
KARRENSTRASSEN = {"land", "haupt", "ring", "wall"}
WEGKLASSEN = KARRENSTRASSEN | {"gasse", "leinpfad"}
ZONENARTEN = {"kern", "mauerring", "vorstadt", "umland"}
WURZEL = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
KATALOGDATEI = os.path.join(WURZEL, "game", "daten", "katalog.json")

# Bausteine dieser Arten stehen unter `gebaeude`; `mauer` steht unter `mauern`, `natur` unter `natur`. Werkstatt, Wohnhaus und
# Einwohnerhaus wählt das Spiel nach Beruf, Stufe und Hausart – sie stehen nie in einer Karte.
GEBAEUDEARTEN = {"werkstattplatz", "wohnhausplatz", "einwohnerplatz", "kulisse", "sonderbau", "umlandbetrieb", "umlandstelle", "berg"}
VORPLATZ = 2.0                     # Meter Vorplatz an der Vorderseite jeder Grundfläche (LEVELDESIGN „Gebäudegrößen“)
STRASSENNAH = 7.0                  # Eingangsregel des Kerns: Vorplatz höchstens 7 m von der Straßenmitte
TORNAH = 1.0                       # so weit darf ein Tor neben seiner Mauerlinie liegen


def katalog_laden(pfad=KATALOGDATEI):
    """Der Gebäudekatalog als {Kennung: Baustein}."""
    with open(pfad, encoding="utf-8") as f:
        return {b["kennung"]: b for b in json.load(f)["bausteine"]}


# ── Geometrie ────────────────────────────────────────────────────────────────
def runden(p, schritt=0.5):
    return [round(round(p[0] / schritt) * schritt, 4), round(round(p[1] / schritt) * schritt, 4)]

def aufs_raster(p):
    return runden(p, RASTER)

def abstand(a, b):
    return math.hypot(a[0] - b[0], a[1] - b[1])

def abstand_strecke(p, a, b):
    """Kürzester Abstand von Punkt p zur Strecke a–b."""
    dx, dz = b[0] - a[0], b[1] - a[1]
    l2 = dx * dx + dz * dz
    t = 0 if l2 == 0 else max(0, min(1, ((p[0] - a[0]) * dx + (p[1] - a[1]) * dz) / l2))
    return abstand(p, (a[0] + t * dx, a[1] + t * dz))

def abstand_linie(p, linie):
    if len(linie) == 1:
        return abstand(p, linie[0])
    return min(abstand_strecke(p, linie[i], linie[i + 1]) for i in range(len(linie) - 1))

def schnitt(a, b, c, d):
    """Schnittpunkt der Strecken a–b und c–d oder None."""
    r = (b[0] - a[0], b[1] - a[1]); s = (d[0] - c[0], d[1] - c[1])
    n = r[0] * s[1] - r[1] * s[0]
    if abs(n) < 1e-9:
        return None
    t = ((c[0] - a[0]) * s[1] - (c[1] - a[1]) * s[0]) / n
    u = ((c[0] - a[0]) * r[1] - (c[1] - a[1]) * r[0]) / n
    if 0 <= t <= 1 and 0 <= u <= 1:
        return (a[0] + t * r[0], a[1] + t * r[1])
    return None

def flaeche(poly):
    """Fläche eines Vielecks in m² (Gaußsche Trapezformel)."""
    return abs(sum(poly[i][0] * poly[(i + 1) % len(poly)][1] - poly[(i + 1) % len(poly)][0] * poly[i][1]
                   for i in range(len(poly)))) / 2

def ohne_doppelte(punkte):
    aus = []
    for p in punkte:
        if not aus or p != aus[-1]:
            aus.append(p)
    if len(aus) > 1 and aus[0] == aus[-1]:
        aus.pop()
    return aus

def im_vieleck(p, poly):
    """Liegt p im Vieleck? (Strahlverfahren, Rand zählt mal so, mal so.)"""
    x, z = p; drin = False
    for i in range(len(poly)):
        (x1, z1), (x2, z2) = poly[i], poly[i - 1]
        if (z1 > z) != (z2 > z) and x < (x2 - x1) * (z - z1) / (z2 - z1) + x1:
            drin = not drin
    return drin

def dreh(lx, lz, grad):
    """Ein lokaler Punkt (x nach rechts, z zur Vorderseite) in Weltrichtung gedreht – wie Godot Rotation.Y in Grad."""
    t = math.radians(grad)
    return (lx * math.cos(t) + lz * math.sin(t), -lx * math.sin(t) + lz * math.cos(t))

def drehung_zu(dx, dz):
    """Drehung in Grad (−180 … 180), bei der die Vorderseite in Richtung (dx, dz) zeigt."""
    return math.degrees(math.atan2(dx, dz))

def strecken(poly, geschlossen=True):
    n = len(poly)
    return [(poly[i], poly[(i + 1) % n]) for i in range(n if geschlossen else n - 1)]

def abstand_strecken(a, b, c, d):
    if schnitt(a, b, c, d):
        return 0.0
    return min(abstand_strecke(a, c, d), abstand_strecke(b, c, d), abstand_strecke(c, a, b), abstand_strecke(d, a, b))

def abstand_vielecke(p, q, q_geschlossen=True):
    """Kürzester Abstand zwischen dem Vieleck p und dem Vieleck (oder der Linie) q – 0, wenn sie sich überdecken."""
    if any(im_vieleck(e, p) for e in q) or (q_geschlossen and any(im_vieleck(e, q) for e in p)):
        return 0.0
    if len(q) == 1:
        return min(abstand_strecke(q[0], a, b) for a, b in strecken(p))
    return min(abstand_strecken(a, b, c, d) for a, b in strecken(p) for c, d in strecken(q, q_geschlossen))

def ueberlappen(p, q, rand=0.01):
    """Überdecken sich zwei konvexe Vielecke um mehr als `rand` Meter? (Trennachsen; Berühren an einer Kante zählt nicht.)"""
    for poly in (p, q):
        for a, b in strecken(poly):
            nx, nz = b[1] - a[1], a[0] - b[0]
            laenge = math.hypot(nx, nz) or 1
            nx, nz = nx / laenge, nz / laenge
            sp = [e[0] * nx + e[1] * nz for e in p]
            sq = [e[0] * nx + e[1] * nz for e in q]
            if min(max(sp), max(sq)) - max(min(sp), min(sq)) <= rand:
                return False
    return True


# ── Gebäude und Katalog ──────────────────────────────────────────────────────
def groesse_von(g, katalog):
    """[Breite, Tiefe] der Grundfläche eines Gebäudes: Größe des Bausteins mal `massstab` – oder None."""
    b = katalog.get(g["katalog"], {})
    if "groesse" not in b:
        return None
    s = g.get("massstab", 1)
    return (b["groesse"][0] * s, b["groesse"][1] * s)

def ecken_von(g, katalog, ohne_vorplatz=False):
    """Die vier Ecken der Grundfläche (gedreht), ab hinten links. `ohne_vorplatz` lässt die 2 m an der Vorderseite weg."""
    gr = groesse_von(g, katalog)
    if gr is None:
        return None
    w, t = gr
    vorn = t / 2 - (VORPLATZ if ohne_vorplatz else 0)
    x, z = g["punkt"]
    return [(x + dx, z + dz) for dx, dz in (dreh(lx, lz, g["drehung"]) for lx, lz in
                                             ((-w / 2, -t / 2), (w / 2, -t / 2), (w / 2, vorn), (-w / 2, vorn)))]

def lokal(g, lx, lz):
    """Ein Punkt im Gebäude (lokal: x nach rechts, z zur Vorderseite) in Stadtkoordinaten."""
    dx, dz = dreh(lx, lz, g["drehung"])
    return (g["punkt"][0] + dx, g["punkt"][1] + dz)

HOF_HINTER_KANTE = 0.5             # der Hof eines Werkstattplatzes liegt im Vorplatz, 0,5 m hinter der Vorderkante (wie in der Stadtansicht)

def hof_von(g, katalog):
    """Der Hof eines Werkstattplatzes (Karren, Gäste, Anfang des Weges zum Markt) in Stadtkoordinaten."""
    return lokal(g, 0, groesse_von(g, katalog)[1] / 2 - HOF_HINTER_KANTE)

def kennung_von(name):
    """Kennung aus einem Namen: nur Buchstaben und Ziffern, Umlaute umschrieben."""
    t = name
    for a, b in (("ä", "ae"), ("ö", "oe"), ("ü", "ue"), ("ß", "ss"), ("Ä", "Ae"), ("Ö", "Oe"), ("Ü", "Ue")):
        t = t.replace(a, b)
    return "".join(c if c.isalnum() else "_" for c in t).strip("_")


def _zahl(x):
    """Ganze Zahlen ohne „.0“ schreiben (50 statt 50.0)."""
    if isinstance(x, float) and x == int(x):
        return int(x)
    return x


# ── Die Karte ────────────────────────────────────────────────────────────────
class Karte:
    def __init__(self, kennung, stadt, vorlage, schritt=0.5):
        self.schritt = schritt            # Rundung aller Punkte außer den Bauzonen (die rasten auf das 2-m-Raster)
        self.katalog = katalog_laden()
        self.d = {"format": FORMAT, "kennung": kennung, "stadt": stadt, "vorlage": vorlage,
                  "einheit": "Meter", "achsen": "x = Osten, z = Süden (−z = Norden), Ursprung am Marktplatz",
                  "raster": RASTER, "gebaeude": [], "mauern": [], "wege": [], "bauzonen": [],
                  "natur": {"flaechen": [], "pflanzen": []}, "wasser": [], "gelaende": [], "haefen": [], "uebergaenge": [],
                  "start": None, "kamera": None, "vorkommen": [], "fiktion": []}

    def _p(self, p):
        return runden(p, self.schritt)

    # ── Gebäude ──────────────────────────────────────────────────────────────
    def gebaeude(self, kennung, katalog, name, punkt, drehung=0, massstab=None, betrieb=None, lage=None, konfession=None):
        """Ein Gebäude: Baustein `katalog` mit der Mitte seiner Grundfläche bei `punkt`, gedreht um `drehung` Grad (auf 0,1° gerundet)."""
        e = {"kennung": kennung, "katalog": katalog, "name": name, "punkt": self._p(punkt), "drehung": round(drehung, 1)}
        if massstab is not None and massstab != 1: e["massstab"] = massstab
        if betrieb is not None: e["betrieb"] = betrieb
        if lage is not None: e["lage"] = lage
        if konfession is not None: e["konfession"] = konfession
        self.d["gebaeude"].append(e)
        return e

    # ── Mauern ───────────────────────────────────────────────────────────────
    def mauer(self, name, linie, geschlossen=False, katalog="stadtmauer"):
        """Eine Mauer als Linie (jeder Punkt ein Knick oder ein Ende, dort setzt das Spiel einen Turm). Tore kommen mit `tor` dazu."""
        self.d["mauern"].append({"name": name, "linie": [self._p(p) for p in linie], "geschlossen": geschlossen,
                                 "katalog": katalog, "tore": []})

    def tor(self, mauer, name, punkt, schliesst_nachts=True):
        """Ein Tor in der Mauer `mauer` (Name); es sitzt auf der Mauerlinie, das Spiel setzt eine Torburg."""
        ziel = next(m for m in self.d["mauern"] if m["name"] == mauer)
        ziel["tore"].append({"name": name, "punkt": self._p(punkt), "schliesstNachts": schliesst_nachts})

    # ── Wege, Wasser, Gelände ────────────────────────────────────────────────
    def hafen(self, name, kante, anleger, kran):
        """Hafen am Fluss: Kaimauer als Linie, Anlegestellen als Punkte (dort liegen Schiffe), Kran als Punkt."""
        self.d["haefen"].append({"name": name, "kante": [self._p(p) for p in kante],
                                 "anleger": [self._p(p) for p in anleger], "kran": self._p(kran)})

    def wasser(self, art, name, linie=None, breite=None, flaeche=None):
        e = {"art": art, "name": name}
        if linie: e["linie"] = [self._p(p) for p in linie]; e["breite"] = breite
        if flaeche: e["flaeche"] = [self._p(p) for p in flaeche]
        self.d["wasser"].append(e)

    def gelaende(self, art, name, flaeche):
        self.d["gelaende"].append({"art": art, "name": name, "flaeche": [self._p(p) for p in flaeche]})

    def weg(self, name, klasse, linie, gefahr=1.0, breite=None):
        """`breite` (Meter, optional) ist die gezeichnete Breite des Weges; fehlt sie, wählt das Spiel nach der Klasse."""
        assert klasse in WEGKLASSEN, klasse
        e = {"name": name, "klasse": klasse, "linie": [self._p(p) for p in linie], "gefahr": gefahr}
        if breite is not None: e["breite"] = breite
        self.d["wege"].append(e)

    def uebergang(self, art, name, von, bis):
        self.d["uebergaenge"].append({"art": art, "name": name, "von": self._p(von), "bis": self._p(bis)})

    def bauzone(self, kennung, art, poly):
        assert art in ZONENARTEN, art
        pts = ohne_doppelte([aufs_raster(p) for p in poly])
        self.d["bauzonen"].append({"kennung": kennung, "art": art, "flaeche": pts})

    # ── Natur ────────────────────────────────────────────────────────────────
    def naturflaeche(self, name, art, flaeche, dichte, startwert, gruppen, aussparungen=()):
        """Eine Wald- oder Streufläche: Vieleck mit Löchern, `dichte` Streupunkte je 1.000 m², an jedem Punkt eine Gruppe nach Gewicht.
        `gruppen`: Liste von (Gewicht, Radius, [({Art: Gewicht}, von, bis), …])."""
        self.d["natur"]["flaechen"].append({
            "name": name, "art": art, "flaeche": [self._p(p) for p in flaeche],
            **({"aussparungen": [[self._p(p) for p in a] for a in aussparungen]} if aussparungen else {}),
            "dichte": dichte, "startwert": startwert,
            "gruppen": [{"gewicht": gewicht, "radius": radius,
                         "teile": [{"arten": arten, "anzahl": [von, bis]} for arten, von, bis in teile]}
                        for gewicht, radius, teile in gruppen]})

    def pflanze(self, katalog, punkt, drehung=0, massstab=1, variante=0):
        self.d["natur"]["pflanzen"].append({"katalog": katalog, "punkt": self._p(punkt), "drehung": round(drehung, 1),
                                            "massstab": massstab, "variante": variante})

    # ── Start, Kamera, Sonstiges ─────────────────────────────────────────────
    def start(self, spieler, haeuser):
        """Belegung am Start (Spieler und KI-Häuser in Konto-Reihenfolge), Felder wie in LEVELDESIGN „Kartenformat 2“, „Start“."""
        self.d["start"] = {"spieler": spieler, "haeuser": haeuser}

    def kamera(self, punkt, zoom, drehung=0):
        """Blick beim Start: Punkt der Stadt, Zoom (Größe des Bildausschnitts in Metern) und Drehung (0 = wie heute)."""
        self.d["kamera"] = {"punkt": self._p(punkt), "zoom": zoom, "drehung": drehung}

    def vorkommen(self, kennung, betrieb, punkt):
        """Fester Ort für einen Rohstoffbetrieb (Mine, Steinbruch, Holzschlag) – nur dort darf er stehen."""
        self.d["vorkommen"].append({"kennung": kennung, "betrieb": betrieb, "punkt": self._p(punkt)})

    def fiktion(self, text):
        self.d["fiktion"].append(text)

    # ── Prüfungen nach den Kartenregeln ──────────────────────────────────────
    def pruefen(self, markt=(0, 0), startabweichung=0.10, anschluss=8.0, startzahl=8, mauer_geschlossen=None):
        """Allgemeine Regeln für jede Karte. `startzahl`: so viele Startgrundstücke (Werkstattplatz und Wohnhausplatz, Kennung S<n>_…)
        müssen gleich weit vom Markt liegen (0 = die Karte prüft ihre Wohnhausplätze selbst). `mauer_geschlossen`: True/False verlangt
        eine geschlossene bzw. offene Stadtmauer (erste Mauer)."""
        fehler, hinweise = [], []
        d, kat = self.d, self.katalog
        wege = d["wege"]
        karren = [w["linie"] for w in wege if w["klasse"] in KARRENSTRASSEN]
        alle = [w["linie"] for w in wege]
        gebaeude = d["gebaeude"]

        # 1. Gebäude gegen den Katalog: Baustein bekannt und unter `gebaeude` erlaubt, Kennungen eindeutig, Wohnhausplätze rechtwinklig,
        #    Umlandbetriebe tragen ihren Betrieb, jeder Betriebsname steht höchstens an einem Platz.
        kennungen, betriebe = {}, {}
        for g in gebaeude:
            b = kat.get(g["katalog"])
            if b is None:
                fehler.append(f"Gebäude {g['kennung']}: den Baustein „{g['katalog']}“ gibt es im Katalog nicht")
                continue
            if b["art"] not in GEBAEUDEARTEN:
                fehler.append(f"Gebäude {g['kennung']}: Baustein „{g['katalog']}“ ({b['art']}) steht nicht unter `gebaeude`")
            if "groesse" not in b:
                fehler.append(f"Gebäude {g['kennung']}: Baustein „{g['katalog']}“ hat keine Größe")
            if g["kennung"] in kennungen:
                fehler.append(f"Kennung {g['kennung']} gibt es zweimal")
            kennungen[g["kennung"]] = g
            if b["art"] == "wohnhausplatz" and g["drehung"] % 90:
                fehler.append(f"Wohnhausplatz {g['kennung']}: Drehung {g['drehung']}° ist nicht rechtwinklig")
            if b["art"] == "umlandbetrieb" and not g.get("betrieb"):
                fehler.append(f"Umlandbetrieb {g['kennung']}: `betrieb` fehlt")
            if g.get("betrieb"):
                if g["betrieb"] in betriebe:
                    fehler.append(f"Betrieb „{g['betrieb']}“ steht an zwei Plätzen ({betriebe[g['betrieb']]}, {g['kennung']})")
                betriebe[g["betrieb"]] = g["kennung"]

        # 2. Nichts überlappt: die Grundflächen aus dem Katalog (gedreht, mal Maßstab). Ein Felsberg „Berg_X“ läuft hinter dem Felshang
        #    seiner Grube X aus und überdeckt deren Rückseite; seine Fläche im Katalog hat Rand um das Modell, deshalb ist sein Überlappen
        #    mit anderen Gebäuden nur ein Hinweis.
        flaechen = [(g, ecken_von(g, kat)) for g in gebaeude if "groesse" in kat.get(g["katalog"], {})]
        for i, (a, pa) in enumerate(flaechen):
            for b, pb in flaechen[i + 1:]:
                if "Berg_" + a["kennung"] == b["kennung"] or "Berg_" + b["kennung"] == a["kennung"]:
                    continue
                if ueberlappen(pa, pb):
                    text = f"{a['kennung']} ({a['name']}) und {b['kennung']} ({b['name']}) überlappen"
                    if "berg" in (kat[a["katalog"]]["art"], kat[b["katalog"]]["art"]):
                        hinweise.append("Hinweis: " + text + " (Rand des Felsbergs)")
                    else:
                        fehler.append(text)

        # 3. Werkstattplätze: Der Hof (Mitte des Vorplatzes) liegt an einem Weg.
        for g in gebaeude:
            if kat.get(g["katalog"], {}).get("art") != "werkstattplatz":
                continue
            hof = hof_von(g, kat)
            n = min(abstand_linie(hof, l) for l in alle)
            if n > STRASSENNAH:
                fehler.append(f"Werkstattplatz {g['kennung']}: Hof ({hof[0]:.1f}, {hof[1]:.1f}) liegt {n:.1f} m vom nächsten Weg "
                              f"(höchstens {STRASSENNAH:g} m)")

        # 4. Jede Bauzone hat eine Kante an einer Karrenstraße (Werkstätten brauchen sie).
        for z in d["bauzonen"]:
            n = min(abstand_linie(p, l) for p in z["flaeche"] for l in karren)
            if n > anschluss:
                fehler.append(f"Bauzone {z['kennung']}: nächste Karrenstraße {n:.1f} m entfernt (höchstens {anschluss} m)")

        # 5. Startgrundstücke (Köln): Werkstatt- und Wohnhausplatz „S<n>_…“ gleich weit vom Markt (Fairness bei 8 Häusern).
        if startzahl:
            starts = {}
            for g in gebaeude:
                treffer = re.fullmatch(r"(S\d+)_(Werkstatt|Wohnhaus)", g["kennung"])
                if treffer:
                    starts.setdefault(treffer.group(1), []).append(g)
            if len(starts) != startzahl:
                fehler.append(f"{len(starts)} Startgrundstücke statt {startzahl}")
            mitten = {s: (sum(g["punkt"][0] for g in gs) / len(gs), sum(g["punkt"][1] for g in gs) / len(gs)) for s, gs in starts.items()}
            ds = {s: abstand(p, markt) for s, p in mitten.items()}
            if ds:
                m = sum(ds.values()) / len(ds)
                for s, dd in ds.items():
                    if abs(dd - m) > startabweichung * m:
                        fehler.append(f"{s}: {dd:.0f} m zum Markt, Mittel {m:.0f} m (±{startabweichung:.0%})")
                hinweise.append(f"Startgrundstücke: {min(ds.values()):.0f}–{max(ds.values()):.0f} m Luftlinie zum Markt")

        # 6. Jedes Vorkommen ist über einen Weg erreichbar: Es liegt an einem Weg oder in der Fläche eines Umlandbetriebs (dessen Weg
        #    endet an seinem Halteplatz).
        umland = [ecken_von(g, kat) for g in gebaeude if kat.get(g["katalog"], {}).get("art") == "umlandbetrieb"]
        for v in d["vorkommen"]:
            n = min(abstand_linie(v["punkt"], l) for l in alle)
            if n > 2 and not any(im_vieleck(v["punkt"], e) for e in umland):
                fehler.append(f"Vorkommen {v['kennung']}: {n:.1f} m vom nächsten Weg")

        # 7. Mauern: Baukasten im Katalog, die Stadtmauer geschlossen bzw. offen, jedes Tor auf seiner Mauerlinie, und Wege kreuzen
        #    eine Mauer nur an einem Tor.
        for i, m in enumerate(d["mauern"]):
            if kat.get(m["katalog"], {}).get("art") != "mauer":
                fehler.append(f"Mauer {m['name']}: Baukasten „{m['katalog']}“ ist keine Mauer im Katalog")
            if i == 0 and mauer_geschlossen is not None and m["geschlossen"] != mauer_geschlossen:
                fehler.append(f"Mauer {m['name']}: soll {'geschlossen' if mauer_geschlossen else 'offen'} sein")
            linie = m["linie"] + ([m["linie"][0]] if m["geschlossen"] else [])
            for t in m["tore"]:
                n = abstand_linie(t["punkt"], linie)
                if n > TORNAH:
                    fehler.append(f"Tor {t['name']}: {n:.1f} m neben der Linie der Mauer {m['name']}")
            for w in wege:
                l = w["linie"]
                for a, b in zip(l, l[1:]):
                    for c, e in zip(linie, linie[1:]):
                        s = schnitt(a, b, c, e)
                        if s and min([abstand(s, t["punkt"]) for t in m["tore"]] or [1e9]) > 3:
                            fehler.append(f"{w['name']} kreuzt {m['name']} ohne Tor bei ({s[0]:.0f}, {s[1]:.0f})")

        # 8. Anleger liegen an der Kaimauer.
        for h in d["haefen"]:
            for p in h["anleger"] + [h["kran"]]:
                if abstand_linie(p, h["kante"]) > 2:
                    fehler.append(f"{h['name']}: Anleger oder Kran {p} liegt nicht an der Kaimauer")

        # 9. Natur: jede Art ist ein Natur-Baustein des Katalogs; Flächen haben eine Dichte und Gruppen.
        for fl in d["natur"]["flaechen"]:
            if fl["dichte"] <= 0 or not fl["gruppen"]:
                fehler.append(f"Naturfläche {fl['name']}: Dichte oder Gruppen fehlen")
            for gr in fl["gruppen"]:
                for teil in gr["teile"]:
                    for art in teil["arten"]:
                        if kat.get(art, {}).get("art") != "natur":
                            fehler.append(f"Naturfläche {fl['name']}: „{art}“ ist keine Natur-Art im Katalog")
        for p in d["natur"]["pflanzen"]:
            if kat.get(p["katalog"], {}).get("art") != "natur":
                fehler.append(f"Pflanze bei {p['punkt']}: „{p['katalog']}“ ist keine Natur-Art im Katalog")

        # 10. Startbelegung: alle Plätze gibt es, von der richtigen Art, keiner doppelt.
        fehler += self._start_pruefen(kennungen)

        # 11. Das Wegenetz hängt zusammen (Kreuzungen und Berührungen verbinden, Brücken und Fähren auch).
        teile = self._netzteile(alle + [[u["von"], u["bis"]] for u in d["uebergaenge"]])
        if teile != 1:
            fehler.append(f"Wegenetz zerfällt in {teile} Teile")
        hinweise.append(f"Wegenetz: {len(wege)} Wege, {teile} zusammenhängender Teil" + ("" if teile == 1 else "e"))
        arten = {}
        for g in gebaeude:
            a = kat.get(g["katalog"], {}).get("art", "?")
            arten[a] = arten.get(a, 0) + 1
        hinweise.append("Gebäude: " + ", ".join(f"{n} {a}" for a, n in sorted(arten.items())))
        return fehler, hinweise

    def _start_pruefen(self, kennungen):
        s = self.d["start"]
        if s is None:
            return []
        kat, fehler = self.katalog, []

        def art(kennung):
            return kat.get(kennungen[kennung]["katalog"], {}).get("art") if kennung in kennungen else None

        wohnhaeuser = [("Spieler", s["spieler"]["wohnhaus"])] + [(h["name"], h["wohnhaus"]) for h in s["haeuser"]]
        for wer, w in wohnhaeuser:
            if art(w) != "wohnhausplatz":
                fehler.append(f"Start, {wer}: „{w}“ ist kein Wohnhausplatz der Karte")
        if len({w for _, w in wohnhaeuser}) != len(wohnhaeuser):
            fehler.append("Start: ein Wohnhausplatz ist zweimal vergeben")
        werkstaetten = [s["spieler"]["werkstatt"]] + list(s["spieler"].get("jeBeruf", {}).values())
        for w in werkstaetten:
            if art(w["platz"]) != "werkstattplatz":
                fehler.append(f"Start, Spieler: „{w['platz']}“ ist kein Werkstattplatz der Karte")
        plaetze = [w["platz"] for w in werkstaetten[:1]]
        for h in s["haeuser"]:
            for b in h["betriebe"]:
                plaetze.append(b["platz"])
                a = art(b["platz"])
                if a not in ("werkstattplatz", "umlandbetrieb"):
                    fehler.append(f"Start, {h['name']}: „{b['platz']}“ ist weder Werkstattplatz noch Umlandbetrieb der Karte")
                elif not kennungen[b["platz"]].get("betrieb"):
                    fehler.append(f"Start, {h['name']}: am Platz „{b['platz']}“ steht kein `betrieb`")
                elif a == "umlandbetrieb" and kat[kennungen[b["platz"]]["katalog"]].get("betriebsart") != b["art"]:
                    fehler.append(f"Start, {h['name']}: „{b['platz']}“ ist kein Platz für {b['art']}")
        doppelt = {p for p in plaetze if plaetze.count(p) > 1}
        if doppelt:
            fehler.append("Start: Plätze doppelt vergeben: " + ", ".join(sorted(doppelt)))
        namen = [h["name"] for h in s["haeuser"]]
        if len(set(namen)) != len(namen):
            fehler.append("Start: ein Hausname kommt zweimal vor")
        wappen = [json.dumps(h["wappen"], sort_keys=True) for h in s["haeuser"]]
        if len(set(wappen)) != len(wappen):
            fehler.append("Start: zwei Häuser haben dasselbe Wappen")
        return fehler

    @staticmethod
    def _netzteile(linien, fang=1.0):
        strecken_ = [(l[i], l[i + 1]) for l in linien for i in range(len(l) - 1)]
        eltern = list(range(len(strecken_)))
        def wurzel(i):
            while eltern[i] != i:
                eltern[i] = eltern[eltern[i]]; i = eltern[i]
            return i
        for i, (a, b) in enumerate(strecken_):
            for j in range(i + 1, len(strecken_)):
                c, d = strecken_[j]
                if (schnitt(a, b, c, d) or min(abstand_strecke(c, a, b), abstand_strecke(d, a, b),
                                               abstand_strecke(a, c, d), abstand_strecke(b, c, d)) <= fang):
                    eltern[wurzel(i)] = wurzel(j)
        return len({wurzel(i) for i in range(len(strecken_))})

    def bilanz(self):
        zeilen = {}
        for z in self.d["bauzonen"]:
            a = flaeche(z["flaeche"])
            zz = zeilen.setdefault(z["art"], [0, 0.0]); zz[0] += 1; zz[1] += a
        return {art: (n, a, int(a / (RASTER * RASTER))) for art, (n, a) in zeilen.items()}

    # ── Ausgabe ──────────────────────────────────────────────────────────────
    def daten(self):
        """Die Kartendatei als Wörterbuch: leere freiwillige Abschnitte fallen weg, ganze Zahlen ohne „.0“."""
        freiwillig = {"wasser", "gelaende", "haefen", "uebergaenge", "start", "kamera", "vorkommen", "fiktion", "probeplan"}
        aus = {}
        for k, v in self.d.items():
            if k in freiwillig and not v:
                continue
            if k == "natur" and not v["flaechen"] and not v["pflanzen"]:
                continue
            aus[k] = v

        def sauber(x):
            if isinstance(x, dict):
                return {k: sauber(v) for k, v in x.items()}
            if isinstance(x, list):
                return [sauber(v) for v in x]
            return _zahl(x)
        return sauber(aus)

    def schreiben(self, pruefung=None, startzahl=8, mauer_geschlossen=None):
        """Prüft und schreibt die Karte. `pruefung` ist eine Funktion Karte → (Fehler, Hinweise, SVG-Ebene) für die
        Kapazität einer Stadt mit eigenen Regeln (Greybox); ohne sie gilt die Kölner Prüfung für 8 Häuser."""
        fehler, hinweise = self.pruefen(startzahl=startzahl, mauer_geschlossen=mauer_geschlossen)
        if pruefung is None:
            from kapazitaet import Belegung       # erst hier laden: kapazitaet.py braucht dieses Modul
            belegung = Belegung(self.d, self.katalog)
            f2, h2 = belegung.pruefen()
            ebene = belegung.svg_ebene()
        else:
            f2, h2, ebene = pruefung(self)
        fehler += f2; hinweise += h2
        kennung = self.d["kennung"]
        for h in hinweise:
            print("  ", h)
        for art, (n, a, felder) in sorted(self.bilanz().items()):
            tsd = lambda z: f"{z:,.0f}".replace(",", ".")
            print(f"   Bauzonen {art}: {n}, {tsd(a)} m², {tsd(felder)} Rasterfelder")
        if fehler:
            for f in fehler:
                print("FEHLER:", f)
            raise SystemExit(f"{kennung}: {len(fehler)} Regelverstöße, nichts geschrieben")
        pfad = os.path.join(WURZEL, "game", "karten", f"{kennung}.json")
        os.makedirs(os.path.dirname(pfad), exist_ok=True)
        with open(pfad, "w", encoding="utf-8") as f:
            f.write(json_kompakt(self.daten()) + "\n")
        svg = os.path.join(WURZEL, "docs", "karten", f"{kennung}.svg")
        os.makedirs(os.path.dirname(svg), exist_ok=True)
        with open(svg, "w", encoding="utf-8") as f:
            f.write(vorschau(pfad, self.katalog))
        with open(svg.replace(".svg", "-belegung.svg"), "w", encoding="utf-8") as f:
            f.write(vorschau(pfad, self.katalog).replace("</svg>", ebene + "\n</svg>"))
        print(f"   geschrieben: game/karten/{kennung}.json, docs/karten/{kennung}.svg, {kennung}-belegung.svg")


def json_kompakt(d, breite=190):
    """JSON mit Einrückung; was in eine Zeile von höchstens `breite` Zeichen passt (ein Gebäude, ein Tor, ein Weg), steht in einer
    Zeile, lange Punktfolgen mit mehreren Punkten je Zeile – so bleibt die Datei lesbar und Git-Änderungen klein."""
    def ist_punkt(v):
        return isinstance(v, list) and len(v) == 2 and all(isinstance(z, (int, float)) and not isinstance(z, bool) for z in v)

    def dump(x, tiefe):
        flach = json.dumps(x, ensure_ascii=False, separators=(", ", ": "))
        if len(flach) + tiefe <= breite or not isinstance(x, (dict, list)) or not x:
            return flach
        innen = " " * (tiefe + 1)
        if isinstance(x, dict):
            teile = [f"{innen}{json.dumps(k, ensure_ascii=False)}: {dump(v, tiefe + 1)}" for k, v in x.items()]
            return "{\n" + ",\n".join(teile) + "\n" + " " * tiefe + "}"
        if all(ist_punkt(v) for v in x):
            zeilen, zeile = [], []
            for v in x:
                zeile.append(json.dumps(v))
                if len(", ".join(zeile)) + tiefe > breite - 20:
                    zeilen.append(", ".join(zeile)); zeile = []
            if zeile:
                zeilen.append(", ".join(zeile))
            return "[\n" + ",\n".join(innen + z for z in zeilen) + "\n" + " " * tiefe + "]"
        return "[\n" + ",\n".join(innen + dump(v, tiefe + 1) for v in x) + "\n" + " " * tiefe + "]"
    return dump(d, 0)


# ── Vorschau: zeichnet nur aus der JSON-Datei und dem Katalog ────────────────
FARBEN = {
    "boden": "#E6E8E1", "feld": "#DCD5A6", "wald": "#9DB287", "huegel": "#C9BFA6", "wildnis": "#D9B79A",
    "wasser": "#AFC7D5", "wasserlinie": "#4E7A96", "mauer": "#1F2326", "kern": "#EBD3CB", "mauerring": "#E7E0CC",
    "vorstadt": "#E2DCC6", "umland": "#E1E5C0", "zonenrand": "#6B6E66", "start": "#A3262A", "fest": "#1F2326",
    "leer": "#6B6E66", "vorkommen": "#FFFFFF", "text": "#1F2326",
}
# Gebäude: Füllfarbe je Art des Bausteins
GEBAEUDEFARBEN = {"sonderbau": "#8E8F88", "werkstattplatz": "#B58A5A", "umlandbetrieb": "#C9B88A", "umlandstelle": "#B7C79A",
                  "kulisse": "#9DB287", "einwohnerplatz": "#CFE0C0", "wohnhausplatz": "#A3262A", "berg": "#BDB3A0"}
NATURFARBEN = {"wald": "#7F9B6A", "streu": "#C4CFA8"}
STRICH = {"land": (2.0, "6 3", "#4B5258"), "haupt": (2.6, None, "#1F2326"), "ring": (2.2, None, "#1F2326"),
          "wall": (1.6, None, "#4B5258"), "gasse": (1.0, "3 2", "#4B5258"), "leinpfad": (0.8, "1 2", "#4E7A96")}

def vorschau(pfad, katalog=None):
    k = json.load(open(pfad, encoding="utf-8"))
    kat = katalog or katalog_laden()
    xs, zs = [], []
    def sammeln(pts):
        for p in pts:
            xs.append(p[0]); zs.append(p[1])
    for g in k.get("gelaende", []): sammeln(g["flaeche"])
    for w in k["wege"]: sammeln(w["linie"])
    for w in k.get("wasser", []): sammeln(w.get("linie", []) + w.get("flaeche", []))
    for g in k["gebaeude"]: sammeln(ecken_von(g, kat) or [g["punkt"]])
    x0, x1, z0, z1 = min(xs) - 10, max(xs) + 10, min(zs) - 10, max(zs) + 10
    pts = lambda l: " ".join(f"{p[0]:g},{p[1]:g}" for p in l)
    o = [f'<svg xmlns="http://www.w3.org/2000/svg" viewBox="{x0:g} {z0:g} {x1-x0:g} {z1-z0:g}" '
         f'font-family="sans-serif">',
         f'<title>{k["stadt"]} – Kartendatei Format {k["format"]} (Vorschau)</title>',
         f'<rect x="{x0:g}" y="{z0:g}" width="{x1-x0:g}" height="{z1-z0:g}" fill="{FARBEN["boden"]}"/>']
    for g in k.get("gelaende", []):
        o.append(f'<polygon points="{pts(g["flaeche"])}" fill="{FARBEN.get(g["art"], "#ccc")}" opacity="0.8"/>')
    # Natur: jede Fläche mit ihren Aussparungen als Maske, Wald kräftiger als Streu
    for i, fl in enumerate(k.get("natur", {}).get("flaechen", [])):
        o.append(f'<mask id="natur{i}"><rect x="{x0:g}" y="{z0:g}" width="{x1-x0:g}" height="{z1-z0:g}" fill="#fff"/>'
                 + "".join(f'<polygon points="{pts(a)}" fill="#000"/>' for a in fl.get("aussparungen", [])) + "</mask>")
        o.append(f'<polygon points="{pts(fl["flaeche"])}" fill="{NATURFARBEN.get(fl["art"], "#9DB287")}" '
                 f'opacity="{0.55 if fl["art"] == "wald" else 0.35}" mask="url(#natur{i})"/>')
    for z in k["bauzonen"]:
        o.append(f'<polygon points="{pts(z["flaeche"])}" fill="{FARBEN[z["art"]]}" stroke="{FARBEN["zonenrand"]}" stroke-width="0.4"/>')
    for w in k.get("wasser", []):
        if "flaeche" in w:
            o.append(f'<polygon points="{pts(w["flaeche"])}" fill="{FARBEN["wasser"]}" stroke="{FARBEN["wasserlinie"]}" stroke-width="0.6"/>')
        else:
            farbe = FARBEN["wasser"] if w["breite"] > 4 else FARBEN["wasserlinie"]
            o.append(f'<polyline points="{pts(w["linie"])}" fill="none" stroke="{farbe}" stroke-width="{w["breite"]:g}" stroke-linecap="round"/>')
    for w in k["wege"]:
        b, dash, farbe = STRICH[w["klasse"]]
        if "breite" in w:                 # gezeichnete Breite des Weges
            b, dash = w["breite"], None
        if w["gefahr"] > 1:
            o.append(f'<polyline points="{pts(w["linie"])}" fill="none" stroke="#B5651D" stroke-width="6" opacity="0.35"/>')
        o.append(f'<polyline points="{pts(w["linie"])}" fill="none" stroke="{farbe}" stroke-width="{b}"'
                 + (f' stroke-dasharray="{dash}"' if dash else "") + ' stroke-linecap="round"/>')
    for i, m in enumerate(k["mauern"]):
        form = "polygon" if m["geschlossen"] else "polyline"
        o.append(f'<{form} points="{pts(m["linie"])}" fill="none" stroke="{FARBEN["mauer"]}" stroke-width="{3 if i == 0 else 2.2}"/>')
    for h in k.get("haefen", []):
        o.append(f'<polyline points="{pts(h["kante"])}" fill="none" stroke="#5A5148" stroke-width="2"/>')
        for x, z in h["anleger"]:
            o.append(f'<rect x="{x:g}" y="{z-1:g}" width="7" height="2" fill="#6B4E31"/>')
        x, z = h["kran"]
        o.append(f'<circle cx="{x:g}" cy="{z:g}" r="2.5" fill="none" stroke="#1F2326" stroke-width="1"/>')
    for u in k.get("uebergaenge", []):
        dash = ' stroke-dasharray="4 3"' if u["art"] == "faehre" else ""
        o.append(f'<polyline points="{pts([u["von"], u["bis"]])}" fill="none" stroke="#1F2326" stroke-width="{4 if u["art"]=="bruecke" else 1.2}"{dash}/>')
    for m in k["mauern"]:
        for t in m["tore"]:
            x, z = t["punkt"]
            o.append(f'<rect x="{x-3:g}" y="{z-3:g}" width="6" height="6" fill="{FARBEN["mauer"]}"/>')
            o.append(f'<text x="{x:g}" y="{z-5:g}" font-size="6" text-anchor="middle" fill="{FARBEN["text"]}">{t["name"]}</text>')
    # Gebäude: Grundfläche aus dem Katalog (gedreht), ein Punkt an der Mitte der Vorderseite; Felsberge zuerst (sie liegen hinter den Gruben)
    for g in sorted(k["gebaeude"], key=lambda g: kat.get(g["katalog"], {}).get("art") != "berg"):
        art = kat.get(g["katalog"], {}).get("art", "?")
        ecken = ecken_von(g, kat)
        x, z = g["punkt"]
        if ecken is None:
            o.append(f'<circle cx="{x:g}" cy="{z:g}" r="3.5" fill="{FARBEN["fest"]}"/>')
            continue
        o.append(f'<polygon points="{" ".join(f"{a:.2f},{b:.2f}" for a, b in ecken)}" fill="{GEBAEUDEFARBEN.get(art, "#999")}" '
                 f'stroke="{FARBEN["fest"]}" stroke-width="0.3"' + (' opacity="0.6"' if art == "berg" else "") + "/>")
        if art != "berg":
            vx, vz = lokal(g, 0, groesse_von(g, kat)[1] / 2)
            o.append(f'<circle cx="{vx:.2f}" cy="{vz:.2f}" r="0.9" fill="{FARBEN["start"] if art != "wohnhausplatz" else "#fff"}"/>')
        if art in ("sonderbau", "werkstattplatz", "umlandbetrieb", "umlandstelle"):
            o.append(f'<text x="{x:g}" y="{z + 1:g}" font-size="3" text-anchor="middle" fill="{FARBEN["text"]}">{g["name"]}</text>')
        elif art == "wohnhausplatz":
            o.append(f'<text x="{x:g}" y="{z + 1.5:g}" font-size="4" text-anchor="middle" fill="#fff" font-weight="bold">{g["kennung"]}</text>')
    for v in k.get("vorkommen", []):
        x, z = v["punkt"]
        o.append(f'<polygon points="{x:g},{z-5:g} {x+5:g},{z:g} {x:g},{z+5:g} {x-5:g},{z:g}" fill="{FARBEN["vorkommen"]}" stroke="#1F2326" opacity="0.8"/>'
                 f'<text x="{x+7:g}" y="{z+2:g}" font-size="6" fill="{FARBEN["text"]}">{v["betrieb"]}</text>')
    for z in k["bauzonen"]:
        cx = sum(p[0] for p in z["flaeche"]) / len(z["flaeche"]); cz = sum(p[1] for p in z["flaeche"]) / len(z["flaeche"])
        o.append(f'<text x="{cx:.0f}" y="{cz:.0f}" font-size="6" text-anchor="middle" fill="{FARBEN["zonenrand"]}">{z["kennung"]}</text>')
    o.append("</svg>\n")
    return "\n".join(o)
