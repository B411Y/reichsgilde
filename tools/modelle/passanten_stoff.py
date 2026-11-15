"""Stoff mit Knochenketten: Rock bzw. Kleid und Schürze der vier Frauenfiguren (Frau, Elbin, Zwergin, Halblingin).

Auftrag 15.11.2026 („Stoff wie empfohlen“). Das Skript arbeitet direkt an der .glb (wie passanten_netze_anpassen.py, kein Blender-Export):

1. **Ketten:** 8 Bahnen x 3 Glieder als Knochen `Stoff_<Bahn>_<Glied>` unter `Hips`, dazu je Bahn ein Endknochen `Stoff_<Bahn>_3` am
   Saum (Bahn 0/1 vorn links/rechts über dem Oberschenkel, 2/3 schräg vorn, 4/5 Seite, 6/7 hinten). Die Gelenke liegen auf der
   Rockfläche: die Wurzel 3 cm über dem Hüftgelenk, das zweite Gelenk je Bahn so tief, dass es im Sitzen am Knie ankommt (vorn an der
   Kniekante, an der Seite neben dem Knie, hinten in der Kniekehle), das Ende am Saum.
2. **Netz:** Der Rock hatte zwischen Hüfte und Saum keine Ecken (lange Dreiecke). Er wird an sechs Ringen geschnitten, deren Höhe mit den
   Gelenkhöhen der Bahnen um den Körper wandert (`Nah_*`; `Fern_*` nur an den zwei Gelenkringen), und unterhalb der Wurzeln an die Ketten
   gebunden (zwei Nachbarbahnen x zwei Glieder). Jede Ecke von `Zub_schuerze` übernimmt die Bindung des nächsten Punkts der Kleidfläche.
   Die Zielformen „Sitzen*“ des Kleides werden null (die Namen bleiben für das Spiel); die der Schürze werden neu gerechnet, damit sie in
   jedem Sitzclip den Abstand zum Kleid aus dem Stehen hält.
3. **Vorberechnung je Clip:** Federrechnung der Ketten (Zug zur Zielform, Dämpfung, feste Gliedlängen) mit Kapseln an Oberschenkel und
   Schienbein, Sitzbrett (Sitzclips, Maße aus sitzpruefung.py) und Boden. Zielform: aufrecht hängend wie in der Ruhelage; hebt sich der
   Oberschenkel (ab 35°, ganz ab 75°), liegen die oberen Glieder an ihm an – vorn auf ihm (Schoß), hinten unter ihm –, und das letzte hängt
   senkrecht vom Knie. Einlauf aus dem Stehen, zwei Schleifen, die zweite wird als Drehspuren der Kettenknochen in den Clip geschrieben
   (10 Bilder/s, Naht verteilt, ruhige Spuren mit zwei Bildern). Mittel spielt diese Spuren, die VAT (Fern) backt sie mit, Nah legt
   `SpringBoneSimulator3D` darüber (Stadtansicht.Stoff.cs).

Aufruf (Blenders Python hat numpy):
    ~/werkzeuge/blender/blender-5.2.2-linux-x64/5.2/python/bin/python3.13 tools/modelle/passanten_stoff.py \
        [--ordner=...] [--aus=...] [--nur=frau,zwergin] [--gegenprobe=1] [--clips=Sitzen,Gehen] [--bericht=datei.json]
`--gegenprobe=1` rechnet ohne Beinkapseln und Sitz und lässt das letzte Glied starr dem Oberschenkel folgen (der Fehler „Rock folgt dem
Knie“): rock_pruefung.py muss an einer solchen Ausgabe Fehler melden. Nur in einen eigenen Ordner (`--aus`), nie ins Spiel.
Reihenfolge nach einem Neubau der Figuren: zuletzt, nach passanten_sitzen.py. Eine Datei, die schon Kettenknochen hat, wird abgelehnt.
"""
import json
import math
import os
import struct
import sys

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import sitzpruefung as sp  # noqa: E402

DATEIEN = {"frau": "passant_probe_frau", "elbin": "passant_probe_elbin", "zwergin": "passant_probe_zwergin", "halblingin": "passant_probe_halblingin"}

BAHNEN = 8
GLIEDER = 3
# Bahn: (Ursprung x als Vielfaches des Hüftgelenk-Abstands, Richtung x, Richtung z) für die linke Seite (+x); rechts gespiegelt.
# Vorn folgt die Bahn der Linie Hüftgelenk -> Knie (sie liegt im Sitzen auf dem Oberschenkel), hinten spiegelbildlich.
BAHN_LINKS = [(1.0, 0.108, 0.994), (0.0, 0.80, 0.60), (0.0, 1.0, -0.10), (1.0, 0.25, -0.97)]
ARM_KNOCHEN = ("Shoulder", "Arm", "Hand", "Thumb", "Index", "Middle", "Ring", "Little")
FUSS_KNOCHEN = ("Foot", "Toes")
SITZ_CLIPS = set(sp.SITZ_CLIPS) | {"SitzenBock"}

R_SCHENKEL = 0.022      # Kapsel Oberschenkel (m, Modellraum); dieselben Werte stehen in FigurenBaukasten.Stoff*
R_SCHIENBEIN = 0.055    # Kapsel Schienbein
R_GELENK = 0.008        # Halbmesser der Kettengelenke
FEST_BAND = 0.015       # Höhe unter der Hüftlinie, auf der die Bindung von Hips auf das erste Glied wechselt
# Zugabe zum Weg von der Wurzel bis zum Knie je Bahnpaar (vorn, schräg vorn, Seite, hinten): negativ = das zweite Gelenk liegt knapp
# davor (vorn fällt das letzte Glied dicht am Knie, hinten hängt es aus der Kniekehle)
KNIE_ZUGABE = (-0.02, -0.02, -0.04, -0.06)
# Ringe, an denen der Rock geschnitten wird (Wurzelhöhe y0, Gelenkhöhen y1 und y2 an der Stelle): an den Gelenken, dazwischen und je einer
# knapp unter der Wurzel und unter dem zweiten Gelenk (runder Knick, die Schürze liegt enger am Kleid)
RINGE = (lambda y0, y1, y2: y0 - 0.025, lambda y0, y1, y2: 0.5 * (y0 + y1), lambda y0, y1, y2: y1, lambda y0, y1, y2: 0.5 * (y1 + y2),
         lambda y0, y1, y2: y2, lambda y0, y1, y2: y2 - 0.10)
RINGE_FERN = (2, 4)     # das ferne Netz bekommt nur die Ringe an den Gelenken
BILDER = 10             # Bilder je Sekunde der gebackenen Spuren
SCHUERZE_NAEHER = 0.0   # im Sitzen liegt die Schürze um so viel näher am Kleid als im Stehen (vor dem Knie zählt jeder Millimeter)
SCHUERZE_MAX = 0.12     # größte Verschiebung einer Schürzenecke durch ihre Zielform (m, Bindungsraum)
RUHIG = 0.0005          # ändert sich ein Quaternion im Clip um weniger, bekommt die Spur nur zwei Bilder
SCHRITT = 1.0 / 60.0    # Zeitschritt der Rechnung
ZUG = 0.10              # Anteil je Schritt zur Ruheform (aufrecht hängend)
DAEMPFUNG = 0.25        # Verlust der Geschwindigkeit je Schritt
EBENE = 0.5             # Anteil je Schritt, um den die Bahn in ihre senkrechte Ebene zurückkehrt
SCHENKEL_AB = 35.0      # Grad aus der Senkrechten, ab denen die oberen Glieder dem Oberschenkel folgen (Gehen bleibt darunter)
SCHENKEL_VOLL = 75.0    # ab hier ganz (Sitzen)
SAUM_NEIGUNG = 0.02    # das frei hängende letzte Glied neigt sich so viel nach außen (Überlänge legt sich nach außen auf den Boden)
GLAETTEN = 4            # so viele Rechenschritte mittelt die Aufnahme
KANTE_LUFT = 0.035      # so weit vor der Sitzkante hängt das Saumende (die Ecken um das Gelenk bleiben vor dem Sitzbrett)
LUFT = 0.004            # die Auflage liegt um so viel über der Schenkelkapsel (kein ständiger Stoß, kein Zittern)
BODEN_TIEFE = 0.05      # so tief darf ein Gelenk unter den Boden
EINLAUF = 1.0           # Sekunden vom Stehen in das erste Bild des Clips
TEIL = []               # Knoten der laufenden Datei, die die Rechnung braucht (Hüfte, Beine und ihre Eltern)


# ───────────────────────── glb lesen und schreiben ─────────────────────────

class Datei(sp.Glb):
    """Glb der Sitzprüfung, dazu Schreiben: neue und ersetzte Accessoren hängen hinten an, am Ende wird der Puffer verdichtet."""

    def __init__(self, pfad):
        super().__init__(pfad)
        self.bin = bytearray(self.bin)

    KOMP = {5126: np.float32, 5123: np.uint16, 5121: np.uint8, 5125: np.uint32, 5122: np.int16}

    def _roh(self, daten, comp, norm):
        typ = self.KOMP[comp]
        d = np.asarray(daten, dtype=np.float64)
        if norm:
            d = np.round(d * {np.uint8: 255.0, np.uint16: 65535.0, np.int16: 32767.0}[typ])
        elif comp != 5126:
            d = np.round(d)
        return d.astype(typ).tobytes()

    def _sicht(self, roh, ziel=None):
        self.bin += b"\0" * (-len(self.bin) % 4)
        sicht = {"buffer": 0, "byteOffset": len(self.bin), "byteLength": len(roh)}
        if ziel:
            sicht["target"] = ziel
        self.bin += roh
        self.j["bufferViews"].append(sicht)
        return len(self.j["bufferViews"]) - 1

    def ersetzen(self, idx, daten):
        """Accessor idx bekommt neue Daten (Typ und Normierung bleiben, Anzahl und min/max neu)."""
        a = self.j["accessors"][idx]
        d = np.asarray(daten, dtype=np.float64).reshape(len(daten), -1)
        ziel = self.j["bufferViews"][a["bufferView"]].get("target")
        a["bufferView"] = self._sicht(self._roh(d, a["componentType"], a.get("normalized")), ziel)
        a.pop("byteOffset", None)
        a["count"] = len(d)
        if "min" in a:
            a["min"], a["max"] = [float(np.float32(x)) for x in d.min(0)], [float(np.float32(x)) for x in d.max(0)]

    def neu(self, daten, comp, typ, norm=False, minmax=False):
        d = np.asarray(daten, dtype=np.float64).reshape(len(daten), -1)
        a = {"bufferView": self._sicht(self._roh(d, comp, norm)), "componentType": comp, "count": len(d), "type": typ}
        if norm:
            a["normalized"] = True
        if minmax:
            a["min"], a["max"] = [float(np.float32(x)) for x in d.min(0)], [float(np.float32(x)) for x in d.max(0)]
        self.j["accessors"].append(a)
        return len(self.j["accessors"]) - 1

    def teil_vorbereiten(self, namen):
        """Knoten, deren Weltlage die Federrechnung braucht (mit allen Eltern), in Reihenfolge Eltern vor Kind."""
        folge = []
        for n in namen:
            kette, i = [], self.knoten(n)
            while True:
                kette.append(i)
                if i not in self.eltern:
                    break
                i = self.eltern[i]
            for i in reversed(kette):
                if i not in folge:
                    folge.append(i)
        self.teil = folge

    def welt_teil(self, lokal):
        aus = {}
        for i in self.teil:
            m = self.matrix(*lokal[i])
            aus[i] = aus[self.eltern[i]] @ m if i in self.eltern else m
        return aus

    def schreiben(self, pfad):
        # verdichten: nur Sichten, auf die ein Accessor zeigt
        benutzt = sorted({a["bufferView"] for a in self.j["accessors"]})
        neu_bin, neu_sichten, platz = bytearray(), [], {}
        for s in benutzt:
            v = dict(self.j["bufferViews"][s])
            ab = v.get("byteOffset", 0)
            neu_bin += b"\0" * (-len(neu_bin) % 4)
            roh = self.bin[ab:ab + v["byteLength"]]
            v["byteOffset"] = len(neu_bin)
            neu_bin += roh
            platz[s] = len(neu_sichten)
            neu_sichten.append(v)
        for a in self.j["accessors"]:
            a["bufferView"] = platz[a["bufferView"]]
        self.j["bufferViews"] = neu_sichten
        b = bytes(neu_bin) + b"\0" * (-len(neu_bin) % 4)
        self.bin = bytearray(b)
        self.j["buffers"][0]["byteLength"] = len(b)
        text = json.dumps(self.j, separators=(",", ":"), ensure_ascii=False).encode("utf-8")
        text += b" " * (-len(text) % 4)
        with open(pfad, "wb") as f:
            f.write(struct.pack("<4sII", b"glTF", 2, 12 + 8 + len(text) + 8 + len(b)))
            f.write(struct.pack("<II", len(text), 0x4E4F534A) + text)
            f.write(struct.pack("<II", len(b), 0x004E4942) + b)


# ───────────────────────── kleine Rechenhelfer ─────────────────────────

def einheit(v):
    return v / max(np.linalg.norm(v), 1e-12)


def quat_aus_matrix(m):
    """Drehmatrix 3x3 -> Quaternion (x, y, z, w)."""
    t = m[0, 0] + m[1, 1] + m[2, 2]
    if t > 0:
        s = math.sqrt(t + 1.0) * 2
        q = [(m[2, 1] - m[1, 2]) / s, (m[0, 2] - m[2, 0]) / s, (m[1, 0] - m[0, 1]) / s, 0.25 * s]
    elif m[0, 0] > m[1, 1] and m[0, 0] > m[2, 2]:
        s = math.sqrt(1.0 + m[0, 0] - m[1, 1] - m[2, 2]) * 2
        q = [0.25 * s, (m[0, 1] + m[1, 0]) / s, (m[0, 2] + m[2, 0]) / s, (m[2, 1] - m[1, 2]) / s]
    elif m[1, 1] > m[2, 2]:
        s = math.sqrt(1.0 + m[1, 1] - m[0, 0] - m[2, 2]) * 2
        q = [(m[0, 1] + m[1, 0]) / s, 0.25 * s, (m[1, 2] + m[2, 1]) / s, (m[0, 2] - m[2, 0]) / s]
    else:
        s = math.sqrt(1.0 + m[2, 2] - m[0, 0] - m[1, 1]) * 2
        q = [(m[0, 2] + m[2, 0]) / s, (m[1, 2] + m[2, 1]) / s, 0.25 * s, (m[1, 0] - m[0, 1]) / s]
    q = np.array(q)
    return q / np.linalg.norm(q)


def bogen(a, b):
    """Kürzeste Drehung (3x3), die den Einheitsvektor a auf b legt."""
    v, c = np.cross(a, b), float(a @ b)
    if c < -0.999999:
        achse = einheit(np.cross(a, [1.0, 0, 0]) if abs(a[0]) < 0.9 else np.cross(a, [0, 1.0, 0]))
        return 2 * np.outer(achse, achse) - np.eye(3)
    k = np.array([[0, -v[2], v[1]], [v[2], 0, -v[0]], [-v[1], v[0], 0]])
    return np.eye(3) + k + k @ k / (1 + c)


def strahl_treffer(ursprung, richtung, ecken, dreiecke):
    """Größter Abstand, in dem der Strahl ein Dreieck trifft (Möller-Trumbore), oder None."""
    a, b, c = ecken[dreiecke[:, 0]], ecken[dreiecke[:, 1]], ecken[dreiecke[:, 2]]
    e1, e2 = b - a, c - a
    p = np.cross(richtung, e2)
    det = np.einsum("ij,ij->i", e1, p)
    gut = np.abs(det) > 1e-12
    inv = np.where(gut, 1.0 / np.where(gut, det, 1.0), 0.0)
    t = ursprung - a
    u = np.einsum("ij,ij->i", t, p) * inv
    q = np.cross(t, e1)
    v = (q @ richtung) * inv
    s = np.einsum("ij,ij->i", e2, q) * inv
    treffer = gut & (u >= -1e-9) & (v >= -1e-9) & (u + v <= 1 + 1e-9) & (s > 1e-6)
    return float(s[treffer].max()) if treffer.any() else None


# ───────────────────────── Netz: Rock erkennen, schneiden, binden ─────────────────────────

class Netz:
    """Ein Primitive eines Netzknotens als Felder; schreibt sich nach Schnitt und Bindung zurück."""

    def __init__(self, g, knoten):
        self.g, self.knoten = g, knoten
        nd = g.j["nodes"][knoten]
        self.netz = g.j["meshes"][nd["mesh"]]
        assert len(self.netz["primitives"]) == 1, nd["name"]
        self.prim = self.netz["primitives"][0]
        at = self.prim["attributes"]
        self.feld = {k: g.accessor(v) for k, v in at.items()}
        self.ziele = [{k: g.accessor(v) for k, v in z.items()} for z in self.prim.get("targets", [])]
        self.dreiecke = g.accessor(self.prim["indices"]).reshape(-1, 3).astype(np.int64)
        skin = g.j["skins"][nd["skin"]]
        self.gelenknamen = [g.j["nodes"][k]["name"] for k in skin["joints"]]

    def anteil(self, worte):
        wahl = np.array([any(t in n for t in worte) for n in self.gelenknamen])
        jo, ge = self.feld["JOINTS_0"].astype(int), self.feld["WEIGHTS_0"]
        return sum(ge[:, s] * wahl[jo[:, s]] for s in range(4))

    def rock(self, y_oben):
        """Rock-Ecken: unter der Hüfthöhe, nicht Arm oder Hand, nicht Fuß."""
        return (self.feld["POSITION"][:, 1] < y_oben) & (self.anteil(ARM_KNOCHEN) <= 0.3) & (self.anteil(FUSS_KNOCHEN) <= 0.5)

    def stoffdreiecke(self):
        arm = self.anteil(ARM_KNOCHEN) > 0.3
        return self.dreiecke[~arm[self.dreiecke].any(axis=1)]

    def schneiden(self, ketten, ring):
        """Schneidet alle Rock-Dreiecke am Ring Nummer ring (RINGE); seine Höhe wandert mit den Gelenkhöhen der Bahnen um den Körper.
        Neue Ecken liegen auf den Kanten (alle Felder linear gemischt)."""
        pos = self.feld["POSITION"]
        stoff = (self.anteil(ARM_KNOCHEN) <= 0.3) & (self.anteil(FUSS_KNOCHEN) <= 0.5)
        y_oben = ketten.hoehen[0, 0]
        # Abstand zur Ringhöhe an der Stelle jeder Ecke (nur dort gebraucht, wo Stoff unter der Wurzelhöhe in der Nähe ist)
        ueber = np.full(len(pos), 1.0)
        for e in np.nonzero(stoff & (pos[:, 1] < y_oben + 0.05))[0]:
            _, _, _, y1, y2 = ketten.lokal(*pos[e])
            ueber[e] = pos[e, 1] - RINGE[ring](y_oben, y1, y2)
        oben = ueber > 0
        kanten, neu_i, neu_j, neu_t = {}, [], [], []
        n = len(pos)

        def kante(i, j):
            key = (min(i, j), max(i, j))
            if key not in kanten:
                a, b = key
                kanten[key] = n + len(neu_i)
                neu_i.append(a)
                neu_j.append(b)
                neu_t.append(ueber[a] / (ueber[a] - ueber[b]))
            return kanten[key]

        fertig = []
        for tri in self.dreiecke:
            o = oben[tri]
            if o.all() or not o.any() or not stoff[tri].all():
                fertig.append(tri)
                continue
            # die einzelne Ecke nach vorn drehen (Umlaufsinn bleibt)
            k = int(np.argmax(o)) if o.sum() == 1 else int(np.argmin(o))
            a, b, c = tri[k], tri[(k + 1) % 3], tri[(k + 2) % 3]
            ab, ac = kante(a, b), kante(a, c)
            fertig += [[a, ab, ac], [ab, b, c], [ab, c, ac]]
        if not neu_i:
            return 0
        i, j, t = np.array(neu_i), np.array(neu_j), np.clip(np.array(neu_t), 0.02, 0.98)[:, None]

        def mischen(f):
            return np.vstack([f, f[i] * (1 - t) + f[j] * t])
        for name in list(self.feld):
            if name in ("JOINTS_0", "WEIGHTS_0"):
                self.feld[name] = np.vstack([self.feld[name], self.feld[name][i]])   # wird für Rock-Ecken ohnehin neu gesetzt
            else:
                self.feld[name] = mischen(self.feld[name])
        nrm = self.feld["NORMAL"]
        self.feld["NORMAL"] = nrm / np.maximum(np.linalg.norm(nrm, axis=1, keepdims=True), 1e-12)
        for z in self.ziele:
            for name in z:
                z[name] = mischen(z[name])
        self.dreiecke = np.array(fertig, dtype=np.int64)
        return len(i)

    def binden(self, ketten, y_oben, stoffgelenke):
        """Rock-Ecken an die Ketten: zwei Nachbarbahnen (nach dem Winkel um die Körperachse) x Glieder nach der Höhe; oben weich an Hips."""
        pos = self.feld["POSITION"]
        rock = self.rock(y_oben)
        jo, ge = self.feld["JOINTS_0"].astype(int).copy(), self.feld["WEIGHTS_0"].copy()
        hips = self.gelenknamen.index("Hips")
        y0 = ketten.hoehen[0, 0]
        for e in np.nonzero(rock)[0]:
            x, y, z = pos[e]
            a, b, fb, y1, y2 = ketten.lokal(x, y, z)             # Nachbarbahnen nach dem Winkel, Ringhöhen an dieser Stelle
            # Glieder nach der Höhe (Hutfunktionen mit Knoten an den Ringen)
            if y >= y1:
                f = (y0 - y) / (y0 - y1)
                g0, g1, g2 = 1 - 0.5 * f, 0.5 * f, 0.0
            elif y >= y2:
                f = (y1 - y) / (y1 - y2)
                g0, g1, g2 = 0.5 * (1 - f), 0.5, 0.5 * f
            else:
                f = min((y2 - y) / 0.10, 1.0)
                g0, g1, g2 = 0.0, 0.5 * (1 - f), 0.5 + 0.5 * f
            fest = max(0.0, 1 - (y0 - y) / FEST_BAND)           # Anteil Hips: 1 an der Hüfte, darunter rasch 0 (im Sitzen knickt der Stoff hier um 90°)
            teile = [(hips, fest)]
            for bahn, fbahn in ((a, 1 - fb), (b, fb)):
                for glied, fg in enumerate((g0, g1, g2)):
                    teile.append((stoffgelenke[bahn][glied], (1 - fest) * fbahn * fg))
            teile = sorted((t for t in teile if t[1] > 1e-4), key=lambda t: -t[1])[:4]
            summe = sum(t[1] for t in teile)
            jo[e], ge[e] = 0, 0
            for s, (gel, gew) in enumerate(teile):
                jo[e, s], ge[e, s] = gel, gew / summe
        self.feld["JOINTS_0"], self.feld["WEIGHTS_0"] = jo, ge
        for z in self.ziele:
            for name in z:
                z[name][rock] = 0.0
        return int(rock.sum())

    def an_flaeche_binden(self, kleid):
        """Schürze: Jede Ecke übernimmt die Bindung des nächsten Punkts der Kleidfläche (Gewichte der drei Dreiecksecken nach ihrer
        Lage gemischt, die vier größten). So liegt sie in jeder Haltung wie das Kleid darunter und faltet sich nicht für sich."""
        pos = self.feld["POSITION"]
        idx, bary = sp.naechste_dreiecke(pos, kleid.feld["POSITION"], kleid.stoffdreiecke())
        dreiecke = kleid.stoffdreiecke()[idx]
        kj, kg = kleid.feld["JOINTS_0"].astype(int), kleid.feld["WEIGHTS_0"]
        jo, ge = np.zeros((len(pos), 4)), np.zeros((len(pos), 4))
        for e in range(len(pos)):
            summe = {}
            for ecke, anteil in zip(dreiecke[e], bary[e]):
                for s in range(4):
                    if kg[ecke, s] > 0:
                        summe[kj[ecke, s]] = summe.get(kj[ecke, s], 0.0) + float(anteil) * kg[ecke, s]
            teile = sorted(summe.items(), key=lambda t: -t[1])[:4]
            gesamt = sum(t[1] for t in teile)
            for s, (gel, gew) in enumerate(teile):
                jo[e, s], ge[e, s] = gel, gew / gesamt
        self.feld["JOINTS_0"], self.feld["WEIGHTS_0"] = jo, ge
        for z in self.ziele:
            for name in z:
                z[name][:] = 0.0
        return len(pos)

    def schreiben(self):
        g = self.g
        for name, idx in self.prim["attributes"].items():
            g.ersetzen(idx, self.feld[name])
        for z, zf in zip(self.prim.get("targets", []), self.ziele):
            for name, idx in z.items():
                g.ersetzen(idx, zf[name])
        a = g.j["accessors"][self.prim["indices"]]
        if len(self.feld["POSITION"]) > 65535:
            a["componentType"] = 5125
        g.ersetzen(self.prim["indices"], self.dreiecke.reshape(-1, 1))


# ───────────────────────── Ketten ─────────────────────────

class Ketten:
    """Ruhelage der 8 Bahnen (Gelenke im Modellraum) und ihre Knochen."""

    def __init__(self, g, kleid):
        w = g.welt()
        k = {nd.get("name"): w[i][:3, 3] for i, nd in enumerate(g.j["nodes"])}
        huefte, knie = k["LeftUpperLeg"], k["LeftLowerLeg"]
        self.achse_z = float(k["Hips"][2])
        pos, dreiecke = kleid.feld["POSITION"], kleid.stoffdreiecke()
        # Wurzeln auf Höhe der Schenkeloberseite (im Sitzen liegt der waagrechte Schenkel dort, wo vorn der Rock am Bauch ansetzt:
        # eine Wurzel auf Gelenkhöhe steckte im Schenkel, und kurze Glieder kleiner Völker kämen nicht heraus)
        y0 = float(huefte[1]) + R_SCHENKEL + R_GELENK
        saum = pos[kleid.rock(y0)]
        y_saum = float(saum[:, 1].min())
        bahnen = []
        for ux, dx, dz in BAHN_LINKS:
            for seite in (1.0, -1.0):
                bahnen.append((np.array([seite * ux * huefte[0], 0.0, self.achse_z]), einheit(np.array([seite * dx, 0.0, dz]))))
        self.richtung = np.array([b[1] for b in bahnen])
        self.normale = np.array([einheit(np.cross([0, 1.0, 0], d)) for d in self.richtung])   # waagrecht, quer zur Bahn

        def flaeche(bahn, y):
            o, d = bahnen[bahn]
            u = o + np.array([0, y, 0])
            s = strahl_treffer(u, d, pos, dreiecke)
            return None if s is None else u + s * d
        # Zweites Gelenk je Bahn: so tief, dass es im Sitzen am Knie ankommt – der Weg an der Schenkelachse entlang von der Wurzel bis
        # zur Kniekante (vorn und schräg vorn auf dem Schenkel), bis neben das Knie (Seite) bzw. bis in die Kniekehle (hinten, unter dem
        # Schenkel). Von dort hängt das letzte Glied frei herab, vor der Sitzkante.
        schenkel = float(np.linalg.norm(knie - huefte))
        self.hoehen = np.zeros((BAHNEN, GLIEDER))
        self.ruhe = np.zeros((BAHNEN, GLIEDER + 1, 3))
        for b in range(BAHNEN):
            o, d = bahnen[b]
            wurzel = flaeche(b, y0)
            assert wurzel is not None, f"Bahn {b}: kein Rock auf Wurzelhöhe"
            weg = schenkel - (wurzel[2] - huefte[2]) + KNIE_ZUGABE[b // 2]
            y2 = max(y0 - weg, y_saum + 0.10)
            self.hoehen[b] = (y0, 0.5 * (y0 + y2), y2)
            for i, y in enumerate(self.hoehen[b]):
                p = flaeche(b, y)
                assert p is not None, f"Bahn {b}: kein Rock auf Höhe {y:.2f}"
                self.ruhe[b, i] = p
            # Ende: äußerste Rockfläche knapp über dem Saum (der unterste Ring kann ein innerer Umschlag sein)
            ende = None
            for y in np.arange(y_saum + 0.01, self.hoehen[b, 2], 0.01):
                ende = flaeche(b, y)
                if ende is not None and (ende - o) @ d >= 0.8 * ((self.ruhe[b, 2] - o) @ d):
                    break
            assert ende is not None, f"Bahn {b}: kein Saum"
            self.ruhe[b, 3] = ende
        self.laenge = np.linalg.norm(self.ruhe[:, 1:] - self.ruhe[:, :-1], axis=2)
        # Ruhe-Drehung der Knochen: +Y entlang des Glieds, +X quer zur Bahn (waagrecht)
        self.basis = np.zeros((BAHNEN, GLIEDER, 3, 3))
        for b in range(BAHNEN):
            for i in range(GLIEDER):
                y = einheit(self.ruhe[b, i + 1] - self.ruhe[b, i])
                x = einheit(self.normale[b] - y * (self.normale[b] @ y))
                self.basis[b, i] = np.column_stack([x, y, np.cross(x, y)])

    def winkel(self, y):
        """Winkel jeder Bahn um die Körperachse auf der Höhe y (entlang ihres Linienzugs)."""
        aus = np.zeros(BAHNEN)
        for b in range(BAHNEN):
            p = self.ruhe[b]
            for i in range(GLIEDER):
                if y >= p[i + 1, 1] or i == GLIEDER - 1:
                    f = np.clip((p[i, 1] - y) / max(p[i, 1] - p[i + 1, 1], 1e-9), 0, 1)
                    q = p[i] * (1 - f) + p[i + 1] * f
                    aus[b] = math.atan2(q[0], q[2] - self.achse_z)
                    break
        return aus

    def lokal(self, x, y, z):
        """Für einen Punkt des Rocks: die zwei Nachbarbahnen (a, b), der Anteil fb von b und die Höhen der beiden Ringe an dieser Stelle
        (zwischen den Gelenkhöhen der Nachbarbahnen gemischt)."""
        w = math.atan2(x, z - self.achse_z)
        wk = self.winkel(y)
        folge = np.argsort(wk)
        k = int(np.searchsorted(wk[folge], w)) % BAHNEN
        a, b = int(folge[k - 1]), int(folge[k])
        spanne = (wk[b] - wk[a]) % (2 * math.pi)
        fb = ((w - wk[a]) % (2 * math.pi)) / max(spanne, 1e-9)
        y1 = (1 - fb) * self.hoehen[a, 1] + fb * self.hoehen[b, 1]
        y2 = (1 - fb) * self.hoehen[a, 2] + fb * self.hoehen[b, 2]
        return a, b, fb, y1, y2

    def knochen_anlegen(self, g):
        """Knoten und Gelenke; gibt je Bahn die Plätze im Skin und die Knotennummern zurück."""
        hips = g.knoten("Hips")
        w = g.welt()
        skin = g.j["skins"][0]
        inv = list(g.accessor(skin["inverseBindMatrices"]).reshape(-1, 4, 4).transpose(0, 2, 1))
        plaetze, knoten = [], []
        for b in range(BAHNEN):
            eltern_welt, eltern = w[hips], hips
            plaetze.append([])
            knoten.append([])
            for i in range(GLIEDER):
                m = np.eye(4)
                m[:3, :3], m[:3, 3] = self.basis[b, i], self.ruhe[b, i]
                lok = np.linalg.inv(eltern_welt) @ m
                nr = len(g.j["nodes"])
                g.j["nodes"].append({"name": f"Stoff_{b}_{i}", "translation": [float(x) for x in lok[:3, 3]],
                                     "rotation": [float(x) for x in quat_aus_matrix(lok[:3, :3])]})
                g.j["nodes"][eltern].setdefault("children", []).append(nr)
                g.eltern[nr] = eltern
                skin["joints"].append(nr)
                inv.append(np.linalg.inv(m))
                plaetze[b].append(len(skin["joints"]) - 1)
                knoten[b].append(nr)
                eltern_welt, eltern = m, nr
            # Endknochen am Saum (ohne Gewichte, ohne Spuren): gibt der Federrechnung im Spiel die Länge des letzten Glieds
            m = np.eye(4)
            m[:3, :3], m[:3, 3] = self.basis[b, GLIEDER - 1], self.ruhe[b, GLIEDER]
            lok = np.linalg.inv(eltern_welt) @ m
            nr = len(g.j["nodes"])
            g.j["nodes"].append({"name": f"Stoff_{b}_{GLIEDER}", "translation": [float(x) for x in lok[:3, 3]]})
            g.j["nodes"][eltern].setdefault("children", []).append(nr)
            g.eltern[nr] = eltern
            skin["joints"].append(nr)
            inv.append(np.linalg.inv(m))
        g.ersetzen(skin["inverseBindMatrices"], np.array([m.T.flatten() for m in inv]))
        return plaetze, knoten


# ───────────────────────── Clips schnell abtasten ─────────────────────────

class Abtaster:
    def __init__(self, g, clip):
        self.g = g
        anim = next(a for a in g.j["animations"] if a["name"] == clip)
        self.spuren = []
        for kanal in anim["channels"]:
            if kanal["target"]["path"] == "weights" or kanal["target"]["node"] not in g.teil:
                continue
            s = anim["samplers"][kanal["sampler"]]
            self.spuren.append((kanal["target"]["node"], kanal["target"]["path"], g.accessor(s["input"])[:, 0], g.accessor(s["output"]),
                                s.get("interpolation", "LINEAR") == "STEP"))
        self.dauer = max(float(s[2][-1]) for s in self.spuren)
        self.ruhe = g.ruhe()

    def lokal(self, zeit):
        lokal = dict(self.ruhe)
        for knoten, pfad, ein, aus, stufe in self.spuren:
            k = int(np.clip(np.searchsorted(ein, zeit, side="right") - 1, 0, len(ein) - 1))
            k2 = min(k + 1, len(ein) - 1)
            f = 0.0 if k2 == k or stufe else float(np.clip((zeit - ein[k]) / (ein[k2] - ein[k]), 0, 1))
            wert = aus[k] * (1 - f) + aus[k2] * f
            t, r, s = lokal[knoten]
            if pfad == "rotation":
                r = list(wert / np.linalg.norm(wert))
            elif pfad == "translation":
                t = list(wert)
            else:
                s = list(wert)
            lokal[knoten] = (t, r, s)
        return lokal


def lokal_mischen(a, b, f):
    aus = dict(a)
    for k in TEIL:
        (ta, ra, sa), (tb, rb, sb) = a[k], b[k]
        t = None if ta is None else list(np.array(ta) * (1 - f) + np.array(tb if tb is not None else ta) * f)
        r = ra
        if ra is not None and rb is not None:
            qa, qb = np.array(ra), np.array(rb)
            if qa @ qb < 0:
                qb = -qb
            r = list(einheit(qa * (1 - f) + qb * f))
        aus[k] = (t, r, sa)
    return aus


# ───────────────────────── Federrechnung ─────────────────────────

def kapsel_stoss(p, a, b, rad, aussen):
    """Schiebt Punkte aus der Kapsel; die Richtung ist um aussen (mal Halbmesser) verschoben: Der Stoff bleibt auf seiner Seite des Beins."""
    ab = b - a
    t = np.clip((p - a) @ ab / max(ab @ ab, 1e-12), 0, 1)
    q = a + t[:, None] * ab
    d = p - q
    n = np.linalg.norm(d, axis=1)
    innen = n < rad
    if innen.any():
        richt = d[innen] + rad * aussen[innen]
        richt /= np.maximum(np.linalg.norm(richt, axis=1), 1e-9)[:, None]
        p[innen] = q[innen] + richt * rad
    return p


class Rechnung:
    def __init__(self, g, ketten, kollision=True):
        self.g, self.k, self.kollision = g, ketten, kollision
        self.nr = {n: g.knoten(n) for n in ("Hips", "LeftUpperLeg", "LeftLowerLeg", "LeftFoot", "RightUpperLeg", "RightLowerLeg", "RightFoot")}
        w = g.welt()
        self.hips_ruhe = w[self.nr["Hips"]]
        self.versatz = ketten.ruhe - self.hips_ruhe[:3, 3]          # Gelenke relativ zum Hüftknochen (Ruhe)
        self.schenkel_ruhe = [w[self.nr[n + "UpperLeg"]][:3, :3] for n in ("Left", "Right")]
        self.frei = np.zeros(BAHNEN)                                   # 1 = das letzte Glied hängt frei (Schenkel gehoben)
        self.auflage = np.zeros((BAHNEN, 3))                           # Seite, auf der die oberen Glieder am Schenkel anliegen
        self.x = None

    def ziel(self, welt):
        """Wurzeln (fest am Hüftknochen) und Zielpunkte je Gelenk; dazu die Ebenennormalen.

        Ruheform: aufrecht hängend (nur die Drehung des Beckens um die Hochachse zählt). Hebt sich der Oberschenkel der Seite über
        SCHENKEL_AB Grad aus der Senkrechten, folgen die beiden oberen Glieder ihm (ab SCHENKEL_VOLL ganz: der Stoff liegt im Sitzen am
        Schenkel), das letzte Glied hängt dann senkrecht vom zweiten Ring. Liegt der Körper, folgt alles dem Becken."""
        h = welt[self.nr["Hips"]]
        q = h[:3, :3] @ self.hips_ruhe[:3, :3].T                       # Drehung des Beckens gegen die Ruhe
        vor = q @ np.array([0, 0, 1.0])
        gier = math.atan2(vor[0], vor[2])
        c, s = math.cos(gier), math.sin(gier)
        ry = np.array([[c, 0, s], [0, 1, 0], [-s, 0, c]])
        kipp = math.degrees(math.acos(float(np.clip((q @ np.array([0, 1.0, 0]))[1], -1, 1))))
        u = float(np.clip((kipp - 45.0) / 30.0, 0, 1))
        tief = float(np.clip((0.45 - h[1, 3] / self.hips_ruhe[1, 3]) / 0.15, 0, 1))
        u = u * u * (3 - 2 * u) * tief                                 # liegend (Becken gekippt und am Boden) folgt der Stoff dem Körper; gebückt hängt er
        self.ry = ry
        wurzel = h[:3, 3] + self.versatz[:, 0] @ q.T
        rel = self.versatz[:, 1:] - self.versatz[:, :1]                # Gelenke 1..3 relativ zur Wurzel (Ruhe)
        ziel = np.zeros((BAHNEN, GLIEDER, 3))
        normale = np.zeros((BAHNEN, 3))
        for seite, name in ((0, "Left"), (1, "Right")):
            sch = welt[self.nr[name + "UpperLeg"]]
            qs = sch[:3, :3] @ self.schenkel_ruhe[seite].T
            richt = einheit(welt[self.nr[name + "LowerLeg"]][:3, 3] - sch[:3, 3])
            winkel = math.degrees(math.acos(float(np.clip(-richt[1], -1, 1))))
            a = float(np.clip((winkel - SCHENKEL_AB) / (SCHENKEL_VOLL - SCHENKEL_AB), 0, 1))
            a = a * a * (3 - 2 * a) * (1 - u)
            # Auflage: Die oberen Glieder liegen am gehobenen Schenkel an – vorn auf seiner Oberseite (der Schoß liegt flach, die Hände ruhen
            # sichtbar darauf), schräg vorn oben außen, an der Seite außen, hinten unter ihm (der Stoff, auf dem man sitzt) –, im Abstand
            # der Kapsel von der Schenkelachse und so weit, wie die Glieder reichen (höchstens bis zum Knie).
            huefte, knie = sch[:3, 3], welt[self.nr[name + "LowerLeg"]][:3, 3]
            hoch = np.array([0, 1.0, 0]) - richt * richt[1]
            hoch = einheit(hoch) if np.linalg.norm(hoch) > 1e-6 else np.array([0, 1.0, 0])
            quer = einheit(np.cross(hoch, richt)) * (1.0 if seite == 0 else -1.0)   # zur Außenseite des Beins
            if quer[0] * (1.0 if seite == 0 else -1.0) < 0:
                quer = -quer
            laenge_schenkel = float(np.linalg.norm(knie - huefte))
            for b in range(seite, BAHNEN, 2):
                n = (hoch, einheit(hoch + quer), quer, -hoch)[b // 2]
                s0 = float((wurzel[b] - huefte) @ richt)
                weg = np.minimum(s0 + np.cumsum(self.k.laenge[b, :2]), laenge_schenkel)
                am_schenkel = np.array([huefte + richt * w + n * (R_SCHENKEL + R_GELENK + LUFT) for w in weg]) - wurzel[b]
                self.auflage[b] = n
                oben = (1 - u - a) * (rel[b, :2] @ ry.T) + a * am_schenkel + u * (rel[b, :2] @ q.T)
                d3 = rel[b, 2] - rel[b, 1]
                haengen = einheit(np.array([0, -1.0, 0]) + SAUM_NEIGUNG * (ry @ self.k.richtung[b])) * self.k.laenge[b, 2]
                if not self.kollision:
                    haengen = qs @ d3                                   # Gegenprobe: das letzte Glied steht starr am Schenkel („Rock folgt dem Knie“)
                unten = (1 - u - a) * (ry @ d3) + a * haengen + u * (q @ d3)
                ziel[b, :2] = wurzel[b] + oben
                ziel[b, 2] = ziel[b, 1] + unten
                n = (1 - u - a) * (ry @ self.k.normale[b]) + a * (qs @ self.k.normale[b]) + u * (q @ self.k.normale[b])
                normale[b] = einheit(n)
                self.frei[b] = a
        return wurzel, ziel, normale, q

    def koerper(self, welt):
        p = {n: welt[i][:3, 3] for n, i in self.nr.items()}
        return [(p[s + "UpperLeg"], p[s + "LowerLeg"], R_SCHENKEL + R_GELENK) for s in ("Left", "Right")] + \
               [(p[s + "LowerLeg"], p[s + "Foot"], R_SCHIENBEIN + R_GELENK) for s in ("Left", "Right")]

    def schritt(self, welt, sitz):
        wurzel, ziel, normale, _ = self.ziel(welt)
        if self.x is None:
            self.x, self.alt = ziel.copy(), ziel.copy()
        x = self.x + (self.x - self.alt) * (1 - DAEMPFUNG)
        self.alt = self.x
        x += ZUG * (ziel - x)
        quer = np.einsum("bgi,bi->bg", x - wurzel[:, None], normale)
        halt = np.full((BAHNEN, GLIEDER), EBENE)
        halt[:, GLIEDER - 1] *= 1 - self.frei
        x -= (halt * quer)[:, :, None] * normale[:, None]
        aussen = self.k.richtung @ self.ry.T                           # waagrecht nach außen (Richtung der Bahn); nur das Saumende wird so geschoben
        aussen = 0.5 * aussen
        # die oberen Glieder weichen am gehobenen Schenkel zu ihrer Auflageseite aus: Der Schoß gerät beim Hinsetzen nicht unter den
        # Schenkel, der Stoff hinten nicht darüber
        auf = 2.0 * self.frei[:, None] * self.auflage
        kapseln = self.koerper(welt) if self.kollision else []
        for _ in range(4):
            for i in range(GLIEDER):
                eltern = wurzel if i == 0 else x[:, i - 1]
                d = x[:, i] - eltern
                x[:, i] = eltern + d / np.maximum(np.linalg.norm(d, axis=1, keepdims=True), 1e-9) * self.k.laenge[:, i:i + 1]
                p = x[:, i].copy()
                for a, b, rad in kapseln:
                    p = kapsel_stoss(p, a, b, rad, aussen if i == GLIEDER - 1 else auf)
                if self.kollision and sitz is not None:
                    y_sitz, z_kante = sitz
                    tief = (y_sitz + R_GELENK) - p[:, 1]
                    vor = z_kante + R_GELENK - p[:, 2]
                    drin = (tief > 0) & (vor > 0)
                    hoch = drin & (tief <= vor)
                    if i == GLIEDER - 1:
                        hoch[:] = False                                 # das Saumende weicht immer nach vorn an die Sitzkante (hinter die Waden), nie auf den Sitz
                    p[hoch, 1] = y_sitz + R_GELENK
                    p[drin & ~hoch, 2] = z_kante + R_GELENK
                p[:, 1] = np.maximum(p[:, 1], -BODEN_TIEFE)            # Boden (etwas tiefer: wenig Überlänge verschwindet im Boden statt sich zu spreizen)
                x[:, i] = p
        for i in range(GLIEDER):                                       # zuletzt feste Längen (Knochen dehnen sich nicht)
            eltern = wurzel if i == 0 else x[:, i - 1]
            d = x[:, i] - eltern
            x[:, i] = eltern + d / np.maximum(np.linalg.norm(d, axis=1, keepdims=True), 1e-9) * self.k.laenge[:, i:i + 1]
        self.x = x
        return wurzel, x

    def drehungen(self, welt, wurzel, x):
        """Lokale Drehungen (Quaternion) der 24 Knochen, die die Gelenke auf wurzel/x legen."""
        _, _, _, q = self.ziel(welt)
        h = welt[self.nr["Hips"]][:3, :3]
        aus = np.zeros((BAHNEN, GLIEDER, 4))
        for b in range(BAHNEN):
            eltern = h
            for i in range(GLIEDER):
                von = wurzel[b] if i == 0 else x[b, i - 1]
                richt = einheit(x[b, i] - von)
                r0 = q @ self.k.basis[b, i]
                r = bogen(r0[:, 1], richt) @ r0
                aus[b, i] = quat_aus_matrix(eltern.T @ r)
                eltern = r
        return aus


def clip_backen(g, ketten, clip, stehen, knoten, kollision, sitzmasse):
    ab = Abtaster(g, clip)
    dauer = ab.dauer
    sitz = None
    proben = [g.welt_teil(ab.lokal(t)) for t in np.linspace(0, dauer, 9)]
    if clip in SITZ_CLIPS:
        # Sitzfläche wie im Spiel: so tief unter dem Hüftgelenk wie im Clip „Sitzen“ (Sitzhöhe der Art), Vorderkante so weit vor dem
        # Hüftgelenk wie beim tiefsten Sitz der Räume (sitzmasse aus sitzpruefung.py) – davor hängt der Stoff frei herab
        tiefe, kante = sitzmasse
        nr = {n: g.knoten(n) for n in ("LeftUpperLeg", "RightUpperLeg")}
        y_sitz = min(w[nr[n]][1, 3] for w in proben for n in nr) - tiefe
        z_kante = float(np.mean([proben[0][nr[n]][2, 3] for n in nr])) + kante
        sitz = (y_sitz, z_kante)
    r = Rechnung(g, ketten, kollision)
    start = ab.lokal(0.0)
    for s in range(int(EINLAUF / SCHRITT)):                            # aus dem Stehen in den Clip (Stoff legt sich über die Schenkel)
        f = (s + 1) / (EINLAUF / SCHRITT)
        r.schritt(g.welt_teil(lokal_mischen(stehen, start, f)), sitz if f > 0.6 else None)
    for s in range(int(0.5 / SCHRITT)):
        r.schritt(g.welt_teil(start), sitz)
    n = max(2, int(math.ceil(dauer * BILDER)) + 1)
    zeiten = np.linspace(0, dauer, n)
    schritte = max(1, int(round(dauer / SCHRITT)))
    dt = dauer / schritte
    aufnahme, letzte = {}, []
    for runde in range(2):
        naechstes = 0
        for s in range(schritte + 1):
            t = s * dt
            welt = g.welt_teil(ab.lokal(min(t, dauer)))
            if s > 0:
                wurzel, x = r.schritt(welt, sitz)
            else:
                wurzel, x = r.ziel(welt)[0], r.x
            letzte.append(x - wurzel[:, None])
            del letzte[:-GLAETTEN]
            # aufgenommen wird das Mittel der letzten Schritte relativ zur Wurzel: Wo sich Länge und Stoß je Schritt abwechseln, zittert
            # ein Gelenk um Millimeter – das soll nicht in die Spuren
            mittel = wurzel[:, None] + np.mean(letzte, axis=0)
            while runde == 1 and naechstes < n and zeiten[naechstes] <= t + 1e-9:
                aufnahme[naechstes] = (welt, wurzel.copy(), mittel.copy())
                naechstes += 1
        if runde == 1:
            for i in range(naechstes, n):
                aufnahme[i] = (welt, wurzel.copy(), mittel.copy())
    # Naht: das letzte Bild soll das erste sein; den Unterschied über die Schleife verteilen, Längen wiederherstellen
    fehler = aufnahme[n - 1][2] - aufnahme[0][2]
    spuren = np.zeros((n, BAHNEN, GLIEDER, 4))
    for i in range(n):
        welt, wurzel, x = aufnahme[i]
        x = x - fehler * (i / (n - 1))
        for gl in range(GLIEDER):
            eltern = wurzel if gl == 0 else x[:, gl - 1]
            d = x[:, gl] - eltern
            x[:, gl] = eltern + d / np.maximum(np.linalg.norm(d, axis=1, keepdims=True), 1e-9) * ketten.laenge[:, gl:gl + 1]
        spuren[i] = r.drehungen(welt, wurzel, x)
    for i in range(1, n):                                              # Vorzeichen stetig halten
        kipp = np.einsum("bgi,bgi->bg", spuren[i], spuren[i - 1]) < 0
        spuren[i][kipp] *= -1
    anim = next(a for a in g.j["animations"] if a["name"] == clip)
    ein, ein_kurz = g.neu(zeiten.reshape(-1, 1), 5126, "SCALAR", minmax=True), None
    for b in range(BAHNEN):
        for gl in range(GLIEDER):
            spur = spuren[:, b, gl]
            if np.abs(spur - spur[0]).max() < RUHIG:                   # Glied steht im ganzen Clip still: zwei Bilder genügen
                if ein_kurz is None:
                    ein_kurz = g.neu(zeiten[[0, -1]].reshape(-1, 1), 5126, "SCALAR", minmax=True)
                aus = g.neu(spur[[0, 0]], 5126, "VEC4")
                anim["samplers"].append({"input": ein_kurz, "output": aus, "interpolation": "LINEAR"})
            else:
                aus = g.neu(spur, 5126, "VEC4")
                anim["samplers"].append({"input": ein, "output": aus, "interpolation": "LINEAR"})
            anim["channels"].append({"sampler": len(anim["samplers"]) - 1, "target": {"node": knoten[b][gl], "path": "rotation"}})
    return n, float(np.abs(fehler).max())


# ───────────────────────── Schürze: Zielform je Sitzclip ─────────────────────────

def schuerze_formen(g, variante):
    """Die Zielformen „Sitzen*“ der Schürze neu: Sie rücken jede Ecke so, dass ihr Abstand zum Kleid im Sitzclip dem im Stehen gleicht
    (mindestens 1,2 cm; Mittel über vier Zeitpunkte, wie rock_pruefung.lage misst). Das Kleid selbst braucht keine Zielform mehr."""
    import rock_pruefung as rp
    if not any(nd.get("name") == "Zub_schuerze" for nd in g.j["nodes"]):
        return {}
    nr = g.knoten("Zub_schuerze")
    nd = g.j["nodes"][nr]
    netz = g.j["meshes"][nd["mesh"]]
    prim = netz["primitives"][0]
    namen = netz.get("extras", {}).get("targetNames", [])
    skin = g.j["skins"][nd["skin"]]
    inv = g.accessor(skin["inverseBindMatrices"]).reshape(-1, 4, 4).transpose(0, 2, 1)
    pos = g.accessor(prim["attributes"]["POSITION"])
    jo, ge = g.accessor(prim["attributes"]["JOINTS_0"]).astype(int), g.accessor(prim["attributes"]["WEIGHTS_0"])
    kleid = f"Nah_{variante}"
    stehen = g.pose("Stehen", 0.0)
    k_r, dr = rp._kleid(g, stehen, kleid)
    s_r = g.netz_ecken(stehen, {"Zub_schuerze"})
    idx, bary = sp.naechste_dreiecke(s_r, k_r, dr)
    q, n = sp.dreieck_lage(k_r, dr, idx, bary)
    vorher = np.einsum("ij,ij->i", s_r - q, n)
    soll = np.sign(vorher + 1e-12) * np.maximum(np.abs(vorher) - SCHUERZE_NAEHER, 0.012)
    bericht = {}
    for z, name in enumerate(namen):
        clip = "Sitzen" if name == "Sitzen" else name[len("Sitzen_"):]
        if not any(a["name"] == clip for a in g.j["animations"]):
            continue
        dauer = g.clipdauer(clip)
        summe = np.zeros_like(pos)
        zeiten = np.linspace(0, dauer, 4, endpoint=False)
        for zeit in zeiten:
            lok = g.pose(clip, float(zeit))
            g.formgewichte = {}                                        # ohne Zielformen messen
            welt = g.welt(lok)
            k_s, _ = rp._kleid(g, lok, kleid)
            s_s = g.netz_ecken(lok, {"Zub_schuerze"})
            q, n = sp.dreieck_lage(k_s, dr, idx, bary)
            nachher = np.einsum("ij,ij->i", s_s - q, n)
            ziel = s_s + n * (soll - nachher)[:, None]
            gel = np.array([welt[gk] @ inv[k] for k, gk in enumerate(skin["joints"])])
            m = sum(ge[:, s, None, None] * gel[jo[:, s]] for s in range(4))
            z4 = np.c_[ziel, np.ones(len(ziel))]
            summe += np.array([np.linalg.solve(m[e], z4[e])[:3] for e in range(len(pos))]) - pos
        delta = summe / len(zeiten)
        betrag = np.linalg.norm(delta, axis=1, keepdims=True)
        delta *= np.minimum(1.0, SCHUERZE_MAX / np.maximum(betrag, 1e-9))   # nie weiter als SCHUERZE_MAX (eine gestauchte Bindung gäbe sonst Ausreißer)
        g.ersetzen(prim["targets"][z]["POSITION"], delta)
        bericht[name] = round(float(np.abs(delta).max()) * 100, 2)
    return bericht


# ───────────────────────── Hauptteil ─────────────────────────

def figur(pfad, ziel, kollision, clips=None):
    g = Datei(pfad)
    if any(nd.get("name", "").startswith("Stoff_") for nd in g.j["nodes"]):
        raise SystemExit(f"{pfad}: hat schon Kettenknochen (vom Stand vor passanten_stoff.py ausgehen)")
    art = next(a for a, v in sp.FIGUREN_ARTEN.items() if v[0] == os.path.splitext(os.path.basename(pfad))[0])
    varianten = sp.FIGUREN_ARTEN[art][1]
    kleid = Netz(g, g.knoten(f"Nah_{varianten[0]}"))
    ketten = Ketten(g, kleid)
    y0 = float(ketten.hoehen[0, 0])
    plaetze, knoten = ketten.knochen_anlegen(g)
    g.teil_vorbereiten(["Hips", "LeftFoot", "RightFoot"])
    TEIL[:] = g.teil
    stehen = Abtaster(g, "Stehen").lokal(0.0)
    bericht = {"hoehen": [[round(float(y), 3) for y in ketten.hoehen[b]] for b in range(0, BAHNEN, 2)],
               "laengen": [[round(float(x), 3) for x in ketten.laenge[b]] for b in range(0, BAHNEN, 2)],
               "ende_y": [round(float(x), 3) for x in ketten.ruhe[:, 3, 1]], "netze": {}}
    gebundene = {}
    for i, nd in sorted(enumerate(list(g.j["nodes"])), key=lambda t: t[1].get("name", "") == "Zub_schuerze"):   # die Schürze zuletzt
        name = nd.get("name", "")
        if "mesh" not in nd or not (name.startswith("Nah_") or name.startswith("Fern_") or name == "Zub_schuerze"):
            continue
        netz = Netz(g, i)
        vorher = len(netz.feld["POSITION"])
        if name == "Zub_schuerze":
            gebunden = netz.an_flaeche_binden(gebundene[f"Nah_{varianten[0]}"])
        else:
            for ring in (RINGE_FERN if name.startswith("Fern_") else range(len(RINGE))):
                netz.schneiden(ketten, ring)
            gebunden = netz.binden(ketten, y0, plaetze)
            gebundene[name] = netz
        netz.schreiben()
        bericht["netze"][name] = {"ecken": [vorher, len(netz.feld["POSITION"])], "gebunden": gebunden}
    # Sitzmaße der Art (Modellmeter): Sitzfläche unter dem Hüftgelenk und Vorderkante vor dem Hüftgelenk
    datei, _, sitzhoehe, massstab = sp.FIGUREN_ARTEN[art]
    hueftgelenk = g.welt(Abtaster(g, "Sitzen").lokal(0.0))[g.knoten("LeftUpperLeg")][1, 3]
    kante_spiel = max(sp.KANTE.values())
    if datei in sp.SCHIENBEIN:
        kante_spiel = min(kante_spiel, sp.SCHIENBEIN[datei] - 0.01)   # kleine Völker rücken vor, bis das Schienbein vor der Kante hängt
    sitzmasse = (float(hueftgelenk - sitzhoehe), kante_spiel / massstab + KANTE_LUFT)
    bericht["sitz"] = [round(x, 3) for x in sitzmasse]
    bericht["clips"] = {}
    for anim in g.j["animations"]:
        if clips and anim["name"] not in clips:
            continue                                                   # nur zum Probieren: die übrigen Clips bleiben ohne Kettenspuren
        n, naht = clip_backen(g, ketten, anim["name"], stehen, knoten, kollision, sitzmasse)
        bericht["clips"][anim["name"]] = {"bilder": n, "naht_cm": round(naht * 100, 2)}
        print(f"  {anim['name']:14} {n:3d} Bilder, Naht {naht * 100:.2f} cm", flush=True)
    bericht["schuerze_cm"] = schuerze_formen(g, varianten[0])
    g.schreiben(ziel)
    bericht["bytes"] = [os.path.getsize(pfad), os.path.getsize(ziel)] if pfad != ziel else os.path.getsize(ziel)
    return bericht


def hauptteil():
    opt = {a.split("=")[0][2:]: a.split("=", 1)[1] for a in sys.argv[1:] if a.startswith("--") and "=" in a}
    ordner = opt.get("ordner", sp.FIGUREN)
    aus = opt.get("aus", ordner)
    os.makedirs(aus, exist_ok=True)
    nur = opt["nur"].split(",") if "nur" in opt else list(DATEIEN)
    bericht = {}
    for art in nur:
        datei = DATEIEN[art] + ".glb"
        print(datei, flush=True)
        vorher = os.path.getsize(os.path.join(ordner, datei))
        bericht[art] = figur(os.path.join(ordner, datei), os.path.join(aus, datei), opt.get("gegenprobe", "0") != "1",
                             opt["clips"].split(",") if "clips" in opt else None)
        bericht[art]["bytes"] = [vorher, os.path.getsize(os.path.join(aus, datei))]
        print(f"  {json.dumps({k: v for k, v in bericht[art].items() if k != 'clips'}, ensure_ascii=False)}", flush=True)
    if "bericht" in opt:
        with open(opt["bericht"], "w", encoding="utf-8") as f:
            json.dump(bericht, f, indent=1, ensure_ascii=False)


if __name__ == "__main__":
    hauptteil()
