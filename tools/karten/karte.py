#!/usr/bin/env python3
"""
Werkzeug für die Stadtkarten von Reichsgilde (Bereich Leveldesign).

Eine Stadt ist ein eigenes Skript (z. B. koeln.py), das mit den Bausteinen hier eine Karte beschreibt.
Dieses Werkzeug
  • rastet alle Punkte ein (Bauzonen auf das 2-m-Raster),
  • prüft die Kartenregeln aus docs/LEVELDESIGN.md (z. B. „jede Bauzone liegt an einer Karrenstraße“),
  • schreibt die Kartendatei game/data/karten/<kennung>.json
  • und ein Vorschaubild docs/karten/<kennung>.svg, das nur aus der JSON-Datei gezeichnet wird.

Koordinaten: Meter auf dem Boden, x = Osten, z = Süden (−z = Norden), wie im Spiel (Godot).
Der Ursprung liegt am Marktplatz der Stadt. Die Kartendatei kennt kein Godot – der Kern kann sie
später genauso lesen wie die Darstellung.
"""
import json
import math
import os

FORMAT = 1
RASTER = 2.0                       # Rasterfeld in Metern (entschieden 16.10.2026)
KARRENSTRASSEN = {"land", "haupt", "ring", "wall"}
WEGKLASSEN = KARRENSTRASSEN | {"gasse", "leinpfad"}
ZONENARTEN = {"kern", "mauerring", "vorstadt", "umland"}
WURZEL = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))


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


# ── Die Karte ────────────────────────────────────────────────────────────────
class Karte:
    def __init__(self, kennung, stadt, vorlage, schritt=0.5):
        self.schritt = schritt            # Rundung aller Punkte außer den Bauzonen (die rasten auf das 2-m-Raster)
        self.d = {"format": FORMAT, "kennung": kennung, "stadt": stadt, "vorlage": vorlage,
                  "einheit": "Meter", "achsen": "x = Osten, z = Süden (−z = Norden), Ursprung am Marktplatz",
                  "raster": RASTER, "mauer": {}, "tore": [], "wasser": [], "gelaende": [], "wege": [],
                  "uebergaenge": [], "bauzonen": [], "feste": [], "vorkommen": [], "nebenmauern": [], "haefen": [],
                  "fiktion": []}

    def mauer(self, linie):
        self.d["mauer"] = {"linie": [runden(p, self.schritt) for p in linie]}

    def nebenmauer(self, name, linie):
        """Weitere Mauer, z. B. um eine befestigte Vorstadt. Ihre Tore stehen wie alle Tore in `tore`."""
        self.d["nebenmauern"].append({"name": name, "linie": [runden(p, self.schritt) for p in linie]})

    def hafen(self, name, kante, anleger, kran):
        """Hafen am Fluss: Kaimauer als Linie, Anlegestellen als Punkte (dort liegen Schiffe), Kran als Punkt."""
        self.d["haefen"].append({"name": name, "kante": [runden(p, self.schritt) for p in kante],
                                 "anleger": [runden(p, self.schritt) for p in anleger], "kran": runden(kran, self.schritt)})

    def tor(self, name, punkt, schliesst_nachts=True):
        self.d["tore"].append({"name": name, "punkt": runden(punkt, self.schritt), "schliesstNachts": schliesst_nachts})

    def wasser(self, art, name, linie=None, breite=None, flaeche=None):
        e = {"art": art, "name": name}
        if linie: e["linie"] = [runden(p, self.schritt) for p in linie]; e["breite"] = breite
        if flaeche: e["flaeche"] = [runden(p, self.schritt) for p in flaeche]
        self.d["wasser"].append(e)

    def gelaende(self, art, name, flaeche):
        self.d["gelaende"].append({"art": art, "name": name, "flaeche": [runden(p, self.schritt) for p in flaeche]})

    def weg(self, name, klasse, linie, gefahr=1.0, breite=None):
        """`breite` (Meter, optional) ist die gezeichnete Breite des Weges – die Greybox-Karte gibt sie an, Köln noch nicht."""
        assert klasse in WEGKLASSEN, klasse
        e = {"name": name, "klasse": klasse, "linie": [runden(p, self.schritt) for p in linie], "gefahr": gefahr}
        if breite is not None: e["breite"] = breite
        self.d["wege"].append(e)

    def uebergang(self, art, name, von, bis):
        self.d["uebergaenge"].append({"art": art, "name": name, "von": runden(von, self.schritt), "bis": runden(bis, self.schritt)})

    def bauzone(self, kennung, art, poly):
        assert art in ZONENARTEN, art
        pts = ohne_doppelte([aufs_raster(p) for p in poly])
        self.d["bauzonen"].append({"kennung": kennung, "art": art, "flaeche": pts})

    def fest(self, kennung, art, name, punkt, eingang=None, groesse=None, **mehr):
        e = {"kennung": kennung, "art": art, "name": name, "punkt": runden(punkt, self.schritt)}
        if eingang is not None: e["eingang"] = runden(eingang, self.schritt)
        if groesse is not None: e["groesse"] = groesse
        e.update(mehr)
        self.d["feste"].append(e)

    def vorkommen(self, kennung, betrieb, punkt):
        """Fester Ort für einen Rohstoffbetrieb (Mine, Steinbruch, Holzschlag) – nur dort darf er stehen."""
        self.d["vorkommen"].append({"kennung": kennung, "betrieb": betrieb, "punkt": runden(punkt, self.schritt)})

    def fiktion(self, text):
        self.d["fiktion"].append(text)

    # ── Prüfungen nach den Kartenregeln ──────────────────────────────────────
    def pruefen(self, markt=(0, 0), startabweichung=0.10, anschluss=8.0, startzahl=8):
        fehler, hinweise = [], []
        wege = self.d["wege"]
        karren = [w["linie"] for w in wege if w["klasse"] in KARRENSTRASSEN]
        alle = [w["linie"] for w in wege]

        # 1. Jede Bauzone hat eine Kante an einer Karrenstraße (Werkstätten brauchen sie).
        for z in self.d["bauzonen"]:
            n = min(abstand_linie(p, l) for p in z["flaeche"] for l in karren)
            if n > anschluss:
                fehler.append(f"Bauzone {z['kennung']}: nächste Karrenstraße {n:.1f} m entfernt (höchstens {anschluss} m)")

        # 2. Startgrundstücke: gleich weit vom Markt (Fairness bei 8 Häusern), Eingang an einer Karrenstraße.
        #    (Die Greybox-Karte hat keine Startgrundstücke, sondern Wohnhausplätze – sie prüft sie selbst: startzahl=0.)
        starts = [f for f in self.d["feste"] if f["art"] == "start"] if startzahl else []
        if startzahl and len(starts) != startzahl:
            fehler.append(f"{len(starts)} Startgrundstücke statt {startzahl}")
        if starts:
            ds = [abstand(f["punkt"], markt) for f in starts]
            m = sum(ds) / len(ds)
            for f, d in zip(starts, ds):
                if abs(d - m) > startabweichung * m:
                    fehler.append(f"{f['kennung']}: {d:.0f} m zum Markt, Mittel {m:.0f} m (±{startabweichung:.0%})")
                if min(abstand_linie(f["eingang"], l) for l in karren) > 1.5:
                    fehler.append(f"{f['kennung']}: Eingang liegt nicht an einer Karrenstraße")
            hinweise.append(f"Startgrundstücke: {min(ds):.0f}–{max(ds):.0f} m Luftlinie zum Markt")

        # 3. Jedes Vorkommen ist über einen Weg erreichbar.
        for v in self.d["vorkommen"]:
            n = min(abstand_linie(v["punkt"], l) for l in alle)
            if n > 2:
                fehler.append(f"Vorkommen {v['kennung']}: {n:.1f} m vom nächsten Weg")

        # 4. Wege kreuzen eine Mauer nur an einem Tor.
        mauern = ([("Stadtmauer", self.d["mauer"]["linie"])] if self.d["mauer"] else []) + \
                 [(m["name"], m["linie"]) for m in self.d["nebenmauern"]]
        tore = [t["punkt"] for t in self.d["tore"]]
        for name, ml_ in mauern:
            for w in wege:
                l = w["linie"]
                for i in range(len(l) - 1):
                    for j in range(len(ml_) - 1):
                        s = schnitt(l[i], l[i + 1], ml_[j], ml_[j + 1])
                        if s and min(abstand(s, t) for t in tore) > 3:
                            fehler.append(f"{w['name']} kreuzt {name} ohne Tor bei ({s[0]:.0f}, {s[1]:.0f})")

        # 5. Anleger liegen an der Kaimauer.
        for h in self.d["haefen"]:
            for p in h["anleger"] + [h["kran"]]:
                if abstand_linie(p, h["kante"]) > 2:
                    fehler.append(f"{h['name']}: Anleger oder Kran {p} liegt nicht an der Kaimauer")

        # 6. Das Wegenetz hängt zusammen (Kreuzungen und Berührungen verbinden, Brücken und Fähren auch).
        teile = self._netzteile(alle + [[u["von"], u["bis"]] for u in self.d["uebergaenge"]])
        if teile != 1:
            fehler.append(f"Wegenetz zerfällt in {teile} Teile")
        hinweise.append(f"Wegenetz: {len(wege)} Wege, {teile} zusammenhängender Teil" + ("" if teile == 1 else "e"))
        return fehler, hinweise

    @staticmethod
    def _netzteile(linien, fang=1.0):
        strecken = [(l[i], l[i + 1]) for l in linien for i in range(len(l) - 1)]
        eltern = list(range(len(strecken)))
        def wurzel(i):
            while eltern[i] != i:
                eltern[i] = eltern[eltern[i]]; i = eltern[i]
            return i
        for i, (a, b) in enumerate(strecken):
            for j in range(i + 1, len(strecken)):
                c, d = strecken[j]
                if (schnitt(a, b, c, d) or min(abstand_strecke(c, a, b), abstand_strecke(d, a, b),
                                               abstand_strecke(a, c, d), abstand_strecke(b, c, d)) <= fang):
                    eltern[wurzel(i)] = wurzel(j)
        return len({wurzel(i) for i in range(len(strecken))})

    def bilanz(self):
        zeilen = {}
        for z in self.d["bauzonen"]:
            a = flaeche(z["flaeche"])
            zz = zeilen.setdefault(z["art"], [0, 0.0]); zz[0] += 1; zz[1] += a
        return {art: (n, a, int(a / (RASTER * RASTER))) for art, (n, a) in zeilen.items()}

    # ── Ausgabe ──────────────────────────────────────────────────────────────
    def schreiben(self, pruefung=None, startzahl=8):
        """Prüft und schreibt die Karte. `pruefung` ist eine Funktion Karte → (Fehler, Hinweise, SVG-Ebene) für die
        Kapazität einer Stadt mit eigenen Regeln (Greybox); ohne sie gilt die Kölner Prüfung für 8 Häuser."""
        fehler, hinweise = self.pruefen(startzahl=startzahl)
        if pruefung is None:
            from kapazitaet import Belegung       # erst hier laden: kapazitaet.py braucht dieses Modul
            belegung = Belegung(self.d)
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
        pfad = os.path.join(WURZEL, "game", "data", "karten", f"{kennung}.json")
        os.makedirs(os.path.dirname(pfad), exist_ok=True)
        with open(pfad, "w", encoding="utf-8") as f:
            f.write(json_kompakt(self.d) + "\n")
        svg = os.path.join(WURZEL, "docs", "karten", f"{kennung}.svg")
        os.makedirs(os.path.dirname(svg), exist_ok=True)
        with open(svg, "w", encoding="utf-8") as f:
            f.write(vorschau(pfad))
        with open(svg.replace(".svg", "-belegung.svg"), "w", encoding="utf-8") as f:
            f.write(vorschau(pfad).replace("</svg>", ebene + "\n</svg>"))
        print(f"   geschrieben: game/data/karten/{kennung}.json, docs/karten/{kennung}.svg, {kennung}-belegung.svg")


def json_kompakt(d):
    """JSON mit Einrückung, aber jeder Punkt [x, z] in einer Zeile – so bleibt die Datei lesbar und Git-Änderungen klein."""
    import re
    text = json.dumps(d, ensure_ascii=False, indent=1)
    return re.sub(r"\[\s*(-?[\d.]+),\s*(-?[\d.]+)\s*\]", r"[\1, \2]", text)


# ── Vorschau: zeichnet nur aus der JSON-Datei ───────────────────────────────
FARBEN = {
    "boden": "#E6E8E1", "feld": "#DCD5A6", "wald": "#9DB287", "huegel": "#C9BFA6", "wildnis": "#D9B79A",
    "wasser": "#AFC7D5", "wasserlinie": "#4E7A96", "mauer": "#1F2326", "kern": "#EBD3CB", "mauerring": "#E7E0CC",
    "vorstadt": "#E2DCC6", "umland": "#E1E5C0", "zonenrand": "#6B6E66", "start": "#A3262A", "fest": "#1F2326",
    "leer": "#6B6E66", "vorkommen": "#FFFFFF", "text": "#1F2326",
}
# Feste Gebäude mit Maßen und Drehung (Greybox-Karte): Füllfarbe je Art
GEBAEUDEFARBEN = {"sonderbau": "#8E8F88", "werkstatt": "#B58A5A", "umland": "#C9B88A", "umlandstelle": "#B7C79A",
                  "einwohnerhaus": "#9DB287", "einwohnerplatz": "#CFE0C0", "wohnhausplatz": "#A3262A", "turm": "#4B5258"}
STRICH = {"land": (2.0, "6 3", "#4B5258"), "haupt": (2.6, None, "#1F2326"), "ring": (2.2, None, "#1F2326"),
          "wall": (1.6, None, "#4B5258"), "gasse": (1.0, "3 2", "#4B5258"), "leinpfad": (0.8, "1 2", "#4E7A96")}

def vorschau(pfad):
    k = json.load(open(pfad, encoding="utf-8"))
    xs, zs = [], []
    def sammeln(pts):
        for p in pts:
            xs.append(p[0]); zs.append(p[1])
    for g in k["gelaende"]: sammeln(g["flaeche"])
    for w in k["wege"]: sammeln(w["linie"])
    for w in k["wasser"]: sammeln(w.get("linie", []) + w.get("flaeche", []))
    x0, x1, z0, z1 = min(xs) - 10, max(xs) + 10, min(zs) - 10, max(zs) + 10
    pts = lambda l: " ".join(f"{p[0]:g},{p[1]:g}" for p in l)
    o = [f'<svg xmlns="http://www.w3.org/2000/svg" viewBox="{x0:g} {z0:g} {x1-x0:g} {z1-z0:g}" '
         f'font-family="sans-serif">',
         f'<title>{k["stadt"]} – Kartendatei Format {k["format"]} (Vorschau)</title>',
         f'<rect x="{x0:g}" y="{z0:g}" width="{x1-x0:g}" height="{z1-z0:g}" fill="{FARBEN["boden"]}"/>']
    for g in k["gelaende"]:
        o.append(f'<polygon points="{pts(g["flaeche"])}" fill="{FARBEN.get(g["art"], "#ccc")}" opacity="0.8"/>')
    for z in k["bauzonen"]:
        o.append(f'<polygon points="{pts(z["flaeche"])}" fill="{FARBEN[z["art"]]}" stroke="{FARBEN["zonenrand"]}" stroke-width="0.4"/>')
    for w in k["wasser"]:
        if "flaeche" in w:
            o.append(f'<polygon points="{pts(w["flaeche"])}" fill="{FARBEN["wasser"]}" stroke="{FARBEN["wasserlinie"]}" stroke-width="0.6"/>')
        else:
            farbe = FARBEN["wasser"] if w["breite"] > 4 else FARBEN["wasserlinie"]
            o.append(f'<polyline points="{pts(w["linie"])}" fill="none" stroke="{farbe}" stroke-width="{w["breite"]:g}" stroke-linecap="round"/>')
    for w in k["wege"]:
        b, dash, farbe = STRICH[w["klasse"]]
        if "breite" in w:                 # gezeichnete Breite des Weges (Greybox-Karte)
            b, dash = w["breite"], None
        if w["gefahr"] > 1:
            o.append(f'<polyline points="{pts(w["linie"])}" fill="none" stroke="#B5651D" stroke-width="6" opacity="0.35"/>')
        o.append(f'<polyline points="{pts(w["linie"])}" fill="none" stroke="{farbe}" stroke-width="{b}"'
                 + (f' stroke-dasharray="{dash}"' if dash else "") + ' stroke-linecap="round"/>')
    if k["mauer"]:
        o.append(f'<polyline points="{pts(k["mauer"]["linie"])}" fill="none" stroke="{FARBEN["mauer"]}" stroke-width="3"/>')
    for m in k.get("nebenmauern", []):
        o.append(f'<polyline points="{pts(m["linie"])}" fill="none" stroke="{FARBEN["mauer"]}" stroke-width="2.2"/>')
    for h in k.get("haefen", []):
        o.append(f'<polyline points="{pts(h["kante"])}" fill="none" stroke="#5A5148" stroke-width="2"/>')
        for x, z in h["anleger"]:
            o.append(f'<rect x="{x:g}" y="{z-1:g}" width="7" height="2" fill="#6B4E31"/>')
        x, z = h["kran"]
        o.append(f'<circle cx="{x:g}" cy="{z:g}" r="2.5" fill="none" stroke="#1F2326" stroke-width="1"/>')
    for u in k["uebergaenge"]:
        dash = ' stroke-dasharray="4 3"' if u["art"] == "faehre" else ""
        o.append(f'<polyline points="{pts([u["von"], u["bis"]])}" fill="none" stroke="#1F2326" stroke-width="{4 if u["art"]=="bruecke" else 1.2}"{dash}/>')
    for t in k["tore"]:
        x, z = t["punkt"]
        o.append(f'<rect x="{x-3:g}" y="{z-3:g}" width="6" height="6" fill="{FARBEN["mauer"]}"/>')
        o.append(f'<text x="{x:g}" y="{z-5:g}" font-size="6" text-anchor="middle" fill="{FARBEN["text"]}">{t["name"]}</text>')
    for f in k["feste"]:
        x, z = f["punkt"]
        if "drehung" in f:                # Gebäude mit Maßen: Breite entlang der Vorderseite, Tiefe dahinter, Vorderseite lokal +z
            breite, tiefe = f["groesse"]
            wx, wz = (breite, tiefe) if f["drehung"] % 180 == 0 else (tiefe, breite)
            o.append(f'<rect x="{x - wx / 2:g}" y="{z - wz / 2:g}" width="{wx:g}" height="{wz:g}" '
                     f'fill="{GEBAEUDEFARBEN.get(f["art"], "#999")}" stroke="{FARBEN["fest"]}" stroke-width="0.3"/>')
            if "eingang" in f:
                ex, ez = f["eingang"]
                o.append(f'<circle cx="{ex:g}" cy="{ez:g}" r="0.9" fill="{FARBEN["start"]}"/>')
            if f["art"] in ("sonderbau", "werkstatt"):
                o.append(f'<text x="{x:g}" y="{z + 1:g}" font-size="3" text-anchor="middle" fill="{FARBEN["text"]}">{f["kennung"]}</text>')
            elif f["art"] == "wohnhausplatz":
                o.append(f'<text x="{x:g}" y="{z + 1.5:g}" font-size="4" text-anchor="middle" fill="#fff" font-weight="bold">{f["kennung"]}</text>')
            continue
        if f["art"] == "start":
            o.append(f'<rect x="{x-5:g}" y="{z-5:g}" width="10" height="10" fill="{FARBEN["start"]}"/>'
                     f'<text x="{x:g}" y="{z+2.5:g}" font-size="7" text-anchor="middle" fill="#fff" font-weight="bold">{f["kennung"][1:]}</text>')
        elif f["art"] == "leerstand":
            o.append(f'<rect x="{x-4:g}" y="{z-4:g}" width="8" height="8" fill="none" stroke="{FARBEN["leer"]}" stroke-dasharray="2 1.5"/>')
        else:
            o.append(f'<circle cx="{x:g}" cy="{z:g}" r="3.5" fill="{FARBEN["fest"]}"/>'
                     f'<text x="{x+5:g}" y="{z+2:g}" font-size="6" fill="{FARBEN["text"]}">{f["name"]}</text>')
    for v in k["vorkommen"]:
        x, z = v["punkt"]
        o.append(f'<polygon points="{x:g},{z-5:g} {x+5:g},{z:g} {x:g},{z+5:g} {x-5:g},{z:g}" fill="{FARBEN["vorkommen"]}" stroke="#1F2326"/>'
                 f'<text x="{x+7:g}" y="{z+2:g}" font-size="6" fill="{FARBEN["text"]}">{v["betrieb"]}</text>')
    for z in k["bauzonen"]:
        cx = sum(p[0] for p in z["flaeche"]) / len(z["flaeche"]); cz = sum(p[1] for p in z["flaeche"]) / len(z["flaeche"])
        o.append(f'<text x="{cx:.0f}" y="{cz:.0f}" font-size="6" text-anchor="middle" fill="{FARBEN["zonenrand"]}">{z["kennung"]}</text>')
    o.append("</svg>\n")
    return "\n".join(o)
