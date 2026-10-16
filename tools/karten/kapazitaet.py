"""
Kapazitätsprüfung für Stadtkarten (Bereich Leveldesign).

Frage: Passen die Gebäude, die eine Partie mit 8 Häusern braucht, wirklich in die Bauzonen einer Stadt?
Ein Überschlag mit Flächen reicht nicht – Gebäude sind Rechtecke, Zonen sind schräg, und jedes Gebäude
braucht eine Seite an einer Straße. Deshalb setzt dieses Modul die Gebäude probeweise auf das 2-m-Raster:

  1. Jede Bauzone in der Stadt wird in Rasterfelder zerlegt (Mittelpunkt liegt in der Zone).
  2. Für jedes Feld steht fest, wie weit die nächste Karrenstraße und die nächste Gasse entfernt sind.
  3. Feste Bauten (Startgrundstücke, Dom, Kirchen, Leerstand) belegen ihre Felder zuerst.
  4. Dann setzt das Modul die Gebäude der Reihe nach, größte zuerst. Jedes nimmt den ersten freien Platz,
     der möglichst nah an der Straße liegt („gierig“). Werkstätten brauchen eine Karrenstraße, Wohnhäuser
     dürfen auch an einer Gasse stehen (Entscheidung 16.10.2026).

Ein gieriges Verfahren findet nicht immer die dichteste Packung. Passt alles hinein, ist die Antwort sicher
„ja“; passt etwas nicht, könnte ein geschickterer Spieler es vielleicht doch unterbringen – die Prüfung ist
also eher streng, und das ist gewollt.
"""
import math

from karte import RASTER, KARRENSTRASSEN, abstand_strecke

# Gebäudegrößen in Rasterfeldern (Breite, Tiefe), entschieden 16.10.2026 – docs/LEVELDESIGN.md, „Gebäudegrößen“
GEBAEUDE = {
    "Werkstatt": (8, 7),         # alle Stufen, 16 × 14 m
    "Wohnhaus": (4, 5),          # Fachwerkhaus → Bürgerhaus → Steinhaus, 8 × 10 m
    "Stadtpalais": (6, 5),       # 12 × 10 m, eigener Bau
    "Hof": (8, 7),               # 16 × 14 m, eigener Bau, nur Kernlage
    "Einwohner-Wohnhaus": (3, 3),        # 6 × 6 m
    "Einwohner-Bürgerhaus": (4, 4),      # 7 × 7 m, belegt 8 × 8 m
    "Ratsherrenhaus": (4, 4),            # 8 × 8 m
}
FARBE = {"Werkstatt": "#8C5A2B", "Wohnhaus": "#A3262A", "Stadtpalais": "#6B2A7A", "Hof": "#2F4F7A",
         "Einwohner-Wohnhaus": "#7A8A6A", "Einwohner-Bürgerhaus": "#5E7A4A", "Ratsherrenhaus": "#3E5A34",
         "fest": "#1F2326"}
STADTZONEN = {"kern", "mauerring", "vorstadt"}
STRASSENNAH = 7.0      # Meter: so weit darf ein Randfeld höchstens von der Straßenmitte weg sein


def bedarf_8_haeuser(einwohner_je_haus=25, haushalt=5, ratsherren=13, buergerhaeuser=3):
    """Was eine Partie mit 8 Häusern in der Stadt braucht (ohne Startgrundstücke, die sind fest).

    Pflicht: je Haus zwei weitere Werkstätten (KI-Häuser führen bis zu 3 Betriebe, ab Patrizier 4; einer
    davon ist die Start-Werkstatt, einer liegt oft im Umland) und die Häuser der Einwohner
    (Werte aus wirtschaft.json, Abschnitt buerger: 25 je Haus, Haushalte 4–6, im Mittel 5).
    Reserve: was wachsende Häuser später bauen könnten – jedes Haus ein Stadtpalais, die Hälfte einen Hof.
    """
    pflicht = [("Werkstatt", 16), ("Ratsherrenhaus", ratsherren), ("Einwohner-Bürgerhaus", buergerhaeuser),
               ("Einwohner-Wohnhaus", math.ceil(8 * einwohner_je_haus / haushalt))]
    reserve = [("Hof", 4), ("Stadtpalais", 8)]
    return pflicht, reserve


def _drin(p, poly):
    x, z = p; drin = False
    for i in range(len(poly)):
        (x1, z1), (x2, z2) = poly[i], poly[i - 1]
        if (z1 > z) != (z2 > z) and x < (x2 - x1) * (z - z1) / (z2 - z1) + x1:
            drin = not drin
    return drin


def _rechteck_felder(mitte, breite, tiefe, richtung):
    """Felder unter einem gedrehten Rechteck (Breite quer zur Richtung, Tiefe entlang)."""
    ux, uz = richtung; vx, vz = -uz, ux
    r = math.hypot(breite, tiefe) / 2 + RASTER
    felder = set()
    i0, i1 = int((mitte[0] - r) // RASTER), int((mitte[0] + r) // RASTER)
    j0, j1 = int((mitte[1] - r) // RASTER), int((mitte[1] + r) // RASTER)
    for i in range(i0, i1 + 1):
        for j in range(j0, j1 + 1):
            cx, cz = (i + 0.5) * RASTER - mitte[0], (j + 0.5) * RASTER - mitte[1]
            if abs(cx * vx + cz * vz) <= breite / 2 and abs(cx * ux + cz * uz) <= tiefe / 2:
                felder.add((i, j))
    return felder


class Belegung:
    def __init__(self, karte):
        self.k = karte
        self.zone = {}                      # Feld → Zonenart
        for z in karte["bauzonen"]:
            if z["art"] not in STADTZONEN:
                continue
            xs = [p[0] for p in z["flaeche"]]; zs = [p[1] for p in z["flaeche"]]
            for i in range(int(min(xs) // RASTER), int(max(xs) // RASTER) + 1):
                for j in range(int(min(zs) // RASTER), int(max(zs) // RASTER) + 1):
                    if _drin(((i + 0.5) * RASTER, (j + 0.5) * RASTER), z["flaeche"]):
                        self.zone[(i, j)] = z["art"]
        karren = [(l[n], l[n + 1]) for w in karte["wege"] if w["klasse"] in KARRENSTRASSEN
                  for l in [w["linie"]] for n in range(len(l) - 1)]
        alle = [(l[n], l[n + 1]) for w in karte["wege"] for l in [w["linie"]] for n in range(len(l) - 1)]
        self.d_karren, self.d_alle = {}, {}
        for f in self.zone:
            p = ((f[0] + 0.5) * RASTER, (f[1] + 0.5) * RASTER)
            self.d_karren[f] = min(abstand_strecke(p, a, b) for a, b in karren)
            self.d_alle[f] = min(abstand_strecke(p, a, b) for a, b in alle)
        self.frei = set(self.zone)
        self.gesetzt = []                    # (Art, Felder)

    def fest_belegen(self, markt=(0, 0)):
        """Feste Bauten nehmen ihre Felder zuerst weg."""
        for f in self.k["feste"]:
            x, z = f["punkt"]
            if f["art"] == "start":
                ex, ez = f["eingang"]; d = math.hypot(x - ex, z - ez) or 1
                breite, tiefe = f["groesse"]
                felder = _rechteck_felder((x, z), breite, tiefe, ((x - ex) / d, (z - ez) / d))
            elif f["kennung"] == "Dom":
                felder = _rechteck_felder((x, z + 3), 12, 28, (0, 1))
            elif f["art"] in ("kirche", "leerstand"):
                felder = _rechteck_felder((x, z), 16, 14, (0, 1))
            else:
                continue                      # Marktviertel und Hafen liegen außerhalb der Bauzonen
            felder &= set(self.zone)
            self.frei -= felder
            self.gesetzt.append(("fest", felder))

    def setzen(self, art, nur_kern=False):
        b, t = GEBAEUDE[art]
        dist = self.d_karren if art == "Werkstatt" else self.d_alle
        kandidaten = sorted(self.frei, key=lambda f: (dist[f], f))
        for (i, j) in kandidaten:
            for (w, h) in ((b, t), (t, b)):
                for (oi, oj) in ((0, 0), (-(w - 1), 0), (0, -(h - 1)), (-(w - 1), -(h - 1))):
                    felder = {(i + oi + a, j + oj + c) for a in range(w) for c in range(h)}
                    if not felder <= self.frei:
                        continue
                    if nur_kern and any(self.zone[f] != "kern" for f in felder):
                        continue
                    if sum(1 for f in felder if dist[f] <= STRASSENNAH) < 2:
                        continue
                    self.frei -= felder
                    self.gesetzt.append((art, felder))
                    return True
        return False

    def pruefen(self):
        """Gibt (Fehler, Hinweise) zurück: Pflicht muss passen, Reserve wird nur berichtet."""
        self.fest_belegen()
        pflicht, reserve = bedarf_8_haeuser()
        fehler, hinweise = [], []
        gesamt = len(self.zone)
        hinweise.append(f"Stadt-Bauzonen: {gesamt} Felder, davon fest belegt {gesamt - len(self.frei)}")
        for art, anzahl in pflicht:
            gesetzt = sum(self.setzen(art) for _ in range(anzahl))
            (hinweise if gesetzt == anzahl else fehler).append(
                f"Pflicht {art}: {gesetzt} von {anzahl} passen")
        nach_pflicht = len(self.frei)
        for art, anzahl in reserve:
            gesetzt = sum(self.setzen(art, nur_kern=(art == "Hof")) for _ in range(anzahl))
            hinweise.append(f"Reserve {art}: {gesetzt} von {anzahl} passen")
        hinweise.append(f"Frei nach der Pflicht: {nach_pflicht} Felder ({nach_pflicht / gesamt:.0%}), "
                        f"nach der Reserve: {len(self.frei)} Felder")
        return fehler, hinweise

    def svg_ebene(self):
        teile = []
        for art, felder in self.gesetzt:
            for (i, j) in felder:
                teile.append(f'<rect x="{i * RASTER:g}" y="{j * RASTER:g}" width="{RASTER:g}" height="{RASTER:g}" '
                             f'fill="{FARBE[art]}" opacity="{0.35 if art == "fest" else 0.75}"/>')
        return "\n".join(teile)
