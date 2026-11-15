"""Sitz- und Blickprüfung der Baukasten-Figuren (Darstellung, ohne Godot, nur Rechnung).

Setzt die Figuren mit Passanten-Skelett (game/assets/modelle/figuren/probe/*.glb) in der Pose „Sitzen“ an jeden Sitzplatz der Innenräume –
genau nach den Regeln des Spiels (Stadtansicht.Baukasten.cs: InnenFigurBauen, BaukastenFigurEinsetzen; Schänke, Kirche, Ratssitzung über
ihre Anker) – und misst:
  * Eindringtiefe der Figur in die Möbel in Zentimetern (Ecken der Netze, CPU-Skinning, jede zweite Ecke, Bilder alle 0,4 s; Möbel der Räume aus
    Grundkörpern als Kisten aus dem Code, Möbel der Raummodelle als Hüllkisten ihrer Teile aus der glb),
  * Abstand der Füße zum Boden in Zentimetern (positiv: die Füße hängen),
  * Blickwinkel: Winkel zwischen Vorwärts der Figur (Modell +Z) und der Richtung zum Ziel des Platzes (Tisch, Pult, Altar, Kanzel, Redner),
    Soll unter 45°.

Aufruf:  python tools/modelle/sitzpruefung.py [--regel=alt|neu] [--gegenprobe] [--arten=Mann,Frau] [--figuren=<Ordner mit den .glb>]
  --regel=alt   so wie vor der Sitzregel des Baukastens (Modellursprung am Sitzpunkt)
  --regel=neu   Hüftgelenk des Sitzen-Clips über dem Sitzpunkt (Stadtansicht.Baukasten.cs, SitzVersatz) – Vorgabe
  --gegenprobe  dreht jede Figur um 180° und lässt den Versatz weg: dann müssen Blick und Eindringen anschlagen (Prüfung der Prüfung)
Rückgabe 0, wenn mit --regel=neu überall Eindringen ≤ 0,5 cm und Blick < 45° (Menschen, Mann und Frau); sonst 1.
Die Maße der Möbel stehen unten in RAEUME und müssen zu Stadtansicht.Raeume.cs / Stadtansicht.Sonderbauten.cs passen.
"""
import json, math, os, struct, sys
import numpy as np

REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
FIGUREN = os.path.join(REPO, 'game', 'assets', 'modelle', 'figuren', 'probe')
for _a in sys.argv:
    if _a.startswith('--figuren='):   # andere Figurdateien prüfen (z. B. neu gebaute Clips vor dem Einchecken)
        FIGUREN = os.path.abspath(_a.split('=', 1)[1])
INNEN = os.path.join(REPO, 'game', 'assets', 'modelle', 'innen')

# ═════════════════════════════════════════════════════════════════════════
#  glb lesen (Knoten, Animation, CPU-Skinning)
# ═════════════════════════════════════════════════════════════════════════


class Glb:
    def __init__(self, pfad):
        b = open(pfad, 'rb').read()
        n = struct.unpack('<I', b[12:16])[0]
        self.j = json.loads(b[20:20 + n])
        off = 20 + n
        m = struct.unpack('<I', b[off:off + 4])[0]
        self.bin = b[off + 8:off + 8 + m]
        self.eltern = {c: i for i, nd in enumerate(self.j['nodes']) for c in nd.get('children', [])}

    def accessor(self, i):
        a = self.j['accessors'][i]
        bv = self.j['bufferViews'][a['bufferView']]
        typ = {5126: np.float32, 5123: np.uint16, 5121: np.uint8, 5125: np.uint32, 5122: np.int16}[a['componentType']]
        anz = {'SCALAR': 1, 'VEC2': 2, 'VEC3': 3, 'VEC4': 4, 'MAT4': 16}[a['type']]
        start = bv.get('byteOffset', 0) + a.get('byteOffset', 0)
        schritt, groesse = bv.get('byteStride', 0), np.dtype(typ).itemsize * anz
        if schritt and schritt != groesse:
            roh = np.frombuffer(self.bin, dtype=np.uint8, count=schritt * (a['count'] - 1) + groesse, offset=start)
            zeilen = np.lib.stride_tricks.as_strided(roh, shape=(a['count'], groesse), strides=(schritt, 1))
            daten = np.frombuffer(zeilen.copy().tobytes(), dtype=typ).reshape(a['count'], anz)
        else:
            daten = np.frombuffer(self.bin, dtype=typ, count=a['count'] * anz, offset=start).reshape(a['count'], anz)
        daten = daten.astype(np.float64)
        if a.get('normalized'):
            daten /= {np.uint8: 255.0, np.uint16: 65535.0}[typ]
        return daten

    @staticmethod
    def matrix(t=None, r=None, s=None):
        m = np.eye(4)
        if s is not None:
            m = np.diag([*s, 1]) @ m
        if r is not None:
            x, y, z, w = r
            r4 = np.eye(4)
            r4[:3, :3] = [[1 - 2 * (y * y + z * z), 2 * (x * y - z * w), 2 * (x * z + y * w)],
                          [2 * (x * y + z * w), 1 - 2 * (x * x + z * z), 2 * (y * z - x * w)],
                          [2 * (x * z - y * w), 2 * (y * z + x * w), 1 - 2 * (x * x + y * y)]]
            m = r4 @ m
        if t is not None:
            t4 = np.eye(4)
            t4[:3, 3] = t
            m = t4 @ m
        return m

    def ruhe(self):
        return {i: (nd.get('translation'), nd.get('rotation'), nd.get('scale')) for i, nd in enumerate(self.j['nodes'])}

    def welt(self, lokal=None):
        lokal = lokal or self.ruhe()
        cache = {}

        def w(i):
            if i not in cache:
                m = self.matrix(*lokal[i])
                cache[i] = w(self.eltern[i]) @ m if i in self.eltern else m
            return cache[i]
        return [w(i) for i in range(len(self.j['nodes']))]

    def pose(self, clip, zeit):
        lokal = self.ruhe()
        self.formgewichte = {}   # Knoten -> Gewichte der Zielformen: wie FigurenBaukasten.SitzSchluessel 1 für „Sitzen“ im Clip „Sitzen“
        for i, nd in enumerate(self.j['nodes']):
            namen = self.j['meshes'][nd['mesh']].get('extras', {}).get('targetNames', []) if 'mesh' in nd else []
            if namen:
                self.formgewichte[i] = [1.0 if (clip in SITZ_CLIPS and n == ('Sitzen' if clip == 'Sitzen' else 'Sitzen_' + clip)) else 0.0 for n in namen]
        anim = next(a for a in self.j['animations'] if a['name'] == clip)
        for kanal in anim['channels']:
            s = anim['samplers'][kanal['sampler']]
            ein, aus = self.accessor(s['input'])[:, 0], self.accessor(s['output'])
            k = int(np.clip(np.searchsorted(ein, zeit) - 1, 0, len(ein) - 1))
            k2 = min(k + 1, len(ein) - 1)
            f = 0 if k2 == k else float(np.clip((zeit - ein[k]) / (ein[k2] - ein[k]), 0, 1))
            wert = aus[k] * (1 - f) + aus[k2] * f
            t, r, sk = lokal[kanal['target']['node']]
            pfad = kanal['target']['path']
            if pfad == 'weights':
                n = len(aus) // len(ein) if len(aus) > len(ein) else 1
                werte = aus.reshape(len(ein), -1)
                self.formgewichte[kanal['target']['node']] = werte[k] * (1 - f) + werte[k2] * f
                continue
            if pfad == 'rotation':
                r = list(wert / np.linalg.norm(wert))
            elif pfad == 'translation':
                t = list(wert)
            elif pfad == 'scale':
                sk = list(wert)
            lokal[kanal['target']['node']] = (t, r, sk)
        return lokal

    def clipdauer(self, clip):
        anim = next(a for a in self.j['animations'] if a['name'] == clip)
        return max(float(self.accessor(s['input'])[-1, 0]) for s in anim['samplers'])

    def knoten(self, name):
        return next(i for i, nd in enumerate(self.j['nodes']) if nd.get('name') == name)

    def netz_ecken(self, lokal, namen):
        """Ecken (Modellraum) aller Netzknoten, deren Name (ohne .001) in namen liegt; mit Skin über CPU-Skinning."""
        welt = self.welt(lokal)
        teile = []
        for i, nd in enumerate(self.j['nodes']):
            if 'mesh' not in nd:
                continue
            name = nd.get('name', '').split('.')[0]
            mname = self.j['meshes'][nd['mesh']].get('name', '').split('.')[0]
            if name not in namen and mname not in namen:
                continue
            for prim in self.j['meshes'][nd['mesh']]['primitives']:
                pos = self.accessor(prim['attributes']['POSITION'])
                gewichte = getattr(self, 'formgewichte', {}).get(i, self.j['meshes'][nd['mesh']].get('weights', []))
                for w, ziel in zip(gewichte, prim.get('targets', [])):
                    if w and 'POSITION' in ziel:
                        pos = pos + float(w) * self.accessor(ziel['POSITION'])
                p4 = np.c_[pos, np.ones(len(pos))]
                if 'skin' in nd and 'JOINTS_0' in prim['attributes']:
                    skin = self.j['skins'][nd['skin']]
                    inv = self.accessor(skin['inverseBindMatrices']).reshape(-1, 4, 4).transpose(0, 2, 1)
                    gel = np.array([welt[g] @ inv[k] for k, g in enumerate(skin['joints'])])
                    jo = self.accessor(prim['attributes']['JOINTS_0']).astype(int)
                    ge = self.accessor(prim['attributes']['WEIGHTS_0'])
                    erg = sum(ge[:, s:s + 1] * np.einsum('nij,nj->ni', gel[jo[:, s]], p4) for s in range(4))
                    teile.append(erg[:, :3])
                else:
                    teile.append((welt[i] @ p4.T).T[:, :3])
        return np.vstack(teile)

    def dreiecke(self, namen):
        """Dreiecke (Welt der glb) aller Netzknoten mit diesen Namen (ohne Skin)."""
        welt = self.welt()
        aus = []
        for i, nd in enumerate(self.j['nodes']):
            if 'mesh' not in nd or nd.get('name', '').split('.')[0] not in namen:
                continue
            for prim in self.j['meshes'][nd['mesh']]['primitives']:
                pos = self.accessor(prim['attributes']['POSITION'])
                p = (welt[i] @ np.c_[pos, np.ones(len(pos))].T).T[:, :3]
                idx = self.accessor(prim['indices']).astype(int).reshape(-1, 3) if 'indices' in prim else np.arange(len(p)).reshape(-1, 3)
                aus.append(p[idx])
        return np.vstack(aus) if aus else np.zeros((0, 3, 3))


# ═════════════════════════════════════════════════════════════════════════
#  Figuren (wie ProbeArten und ProbefigurEinsetzen)
# ═════════════════════════════════════════════════════════════════════════

# Datei, Nahnetze (ohne Bettler: der Bettler sitzt nicht, er bettelt), Sitzhöhe (ProbeArtDaten.Sitzhoehe), Maßstab im Spiel
FIGUREN_ARTEN = {
    'Mann': ('passant_probe', ['cotte', 'gugel', 'rock', 'kutte'], 0.446, 1.0),
    'Frau': ('passant_probe_frau', ['haube', 'gebende'], 0.389, 1.0),
    'Ork': ('passant_probe_ork', ['wams'], 0.439, 1.1),
    'Oger': ('passant_probe_oger', ['sack'], 0.449, 1.35),
    'Zwerg': ('passant_probe_zwerg', ['cotte', 'gugel', 'rock', 'kutte'], 0.371, 1.35 / 1.573),
    'Zwergin': ('passant_probe_zwergin', ['haube', 'gebende'], 0.307, 1.35 * 0.95 / 1.479),
    'Elb': ('passant_probe_elb', ['cotte', 'gugel', 'rock', 'kutte'], 0.496, 1.85 / 1.826),
    'Elbin': ('passant_probe_elbin', ['haube', 'gebende', 'offen'], 0.445, 1.85 * 0.95 / 1.635),
    'Halbling': ('passant_probe_halbling', ['cotte', 'gugel', 'rock', 'kutte'], 0.373, 1.10 / 1.675),
    'Halblingin': ('passant_probe_halblingin', ['haube', 'gebende'], 0.313, 1.10 * 0.95 / 1.532),
}


# Knochenstrecken des Körpers mit Halbmesser (m): was weiter weg liegt, ist Stoff (Saum, Rock), nicht Körper
KOERPER = [(s + a, s + b, 0.10) for s in ('Left', 'Right') for a, b in
           (('UpperLeg', 'LowerLeg'), ('LowerLeg', 'Foot'), ('Foot', 'Toes'), ('Shoulder', 'UpperArm'), ('UpperArm', 'LowerArm'), ('LowerArm', 'Hand'))]
KOERPER += [('Hips', 'Spine', 0.17), ('Spine', 'Chest', 0.17), ('Chest', 'UpperChest', 0.17), ('UpperChest', 'Neck', 0.15), ('Neck', 'Head', 0.13)]


def naechste_dreiecke(punkte, ecken, dreiecke):
    """Je Punkt das nächste Dreieck (Index) und seine baryzentrischen Koordinaten des nächsten Punktes darauf (Ericson, Real-Time Collision
    Detection 5.1.5). Für Stofflagen: Die Schürze merkt sich so ihren Platz auf dem Kleid und folgt ihm (passanten_sitzen.py, rock_pruefung.py)."""
    a, b, c = ecken[dreiecke[:, 0]], ecken[dreiecke[:, 1]], ecken[dreiecke[:, 2]]
    ab, ac = b - a, c - a
    erg_i, erg_b = [], []
    for p in punkte:
        ap, bp, cp = p - a, p - b, p - c
        d1, d2 = np.einsum('ij,ij->i', ab, ap), np.einsum('ij,ij->i', ac, ap)
        d3, d4 = np.einsum('ij,ij->i', ab, bp), np.einsum('ij,ij->i', ac, bp)
        d5, d6 = np.einsum('ij,ij->i', ab, cp), np.einsum('ij,ij->i', ac, cp)
        va, vb, vc = d3 * d6 - d5 * d4, d5 * d2 - d1 * d6, d1 * d4 - d3 * d2
        bary = np.zeros((len(a), 3))
        # innen
        nenner = va + vb + vc
        nenner = np.where(np.abs(nenner) < 1e-18, 1e-18, nenner)
        v, w = vb / nenner, vc / nenner
        bary[:] = np.stack([1 - v - w, v, w], axis=1)
        # Ecken und Kanten (in umgekehrter Reihenfolge, damit die vorderen Regeln gewinnen)
        m = (va <= 0) & ((d4 - d3) >= 0) & ((d5 - d6) >= 0)
        w_bc = np.where(m, (d4 - d3) / np.where(((d4 - d3) + (d5 - d6)) == 0, 1e-18, (d4 - d3) + (d5 - d6)), 0)
        bary[m] = np.stack([np.zeros(m.sum()), 1 - w_bc[m], w_bc[m]], axis=1)
        m = (vb <= 0) & (d2 >= 0) & (d6 <= 0)
        w_ac = np.where(m, d2 / np.where((d2 - d6) == 0, 1e-18, d2 - d6), 0)
        bary[m] = np.stack([1 - w_ac[m], np.zeros(m.sum()), w_ac[m]], axis=1)
        m = (vc <= 0) & (d1 >= 0) & (d3 <= 0)
        v_ab = np.where(m, d1 / np.where((d1 - d3) == 0, 1e-18, d1 - d3), 0)
        bary[m] = np.stack([1 - v_ab[m], v_ab[m], np.zeros(m.sum())], axis=1)
        m = (d6 >= 0) & (d5 <= d6)
        bary[m] = [0, 0, 1]
        m = (d3 >= 0) & (d4 <= d3)
        bary[m] = [0, 1, 0]
        m = (d1 <= 0) & (d2 <= 0)
        bary[m] = [1, 0, 0]
        q = bary[:, :1] * a + bary[:, 1:2] * b + bary[:, 2:] * c
        i = int(np.argmin(np.linalg.norm(q - p, axis=1)))
        erg_i.append(i)
        erg_b.append(bary[i])
    return np.array(erg_i), np.array(erg_b)


def dreieck_lage(ecken, dreiecke, idx, bary):
    """Punkt und Normale (Einheitsvektor, Windung der Dreiecke) zu nächste_dreiecke in einer anderen Lage derselben Ecken."""
    a, b, c = ecken[dreiecke[idx, 0]], ecken[dreiecke[idx, 1]], ecken[dreiecke[idx, 2]]
    q = bary[:, :1] * a + bary[:, 1:2] * b + bary[:, 2:] * c
    n = np.cross(b - a, c - a)
    return q, n / np.maximum(np.linalg.norm(n, axis=1, keepdims=True), 1e-12)


def stoff_maske(ecken, knochen):
    """True für Ecken, die von keiner Knochenstrecke des Körpers in ihrem Halbmesser liegen: Stoff, der vom Körper absteht (Rocksaum)."""
    nah = np.zeros(len(ecken), dtype=bool)
    for a, b, rad in KOERPER:
        if a not in knochen or b not in knochen:
            continue
        pa, pb = knochen[a], knochen[b]
        ab = pb - pa
        tt = np.clip((ecken - pa) @ ab / max(ab @ ab, 1e-12), 0, 1)
        nah |= np.linalg.norm(ecken - (pa + tt[:, None] * ab), axis=1) < rad
    return ~nah


class Sitzfigur:
    """Netze einer Art in der Pose Sitzen (größte Ausdehnung über die Schleife: Bilder alle 0,2 s), Modellraum ohne Maßstab."""

    def __init__(self, art):
        datei, varianten, self.sitzhoehe, self.massstab = FIGUREN_ARTEN[art]
        self.datei = datei
        g = Glb(os.path.join(FIGUREN, datei + '.glb'))
        dauer = g.clipdauer('Sitzen')
        self.posen = []
        for t in np.arange(0, dauer, 0.4):
            lok = g.pose('Sitzen', float(t))
            welt = g.welt(lok)
            knochen = {nd.get('name'): welt[i][:3, 3] for i, nd in enumerate(g.j['nodes'])}
            ecken = {}
            for v in varianten:
                e = g.netz_ecken(lok, {f'Nah_{v}', 'Fuss_halb', 'Fuss_holz'})[::2]
                ecken[v] = (e, stoff_maske(e, knochen))
            self.posen.append(ecken)
        lok = g.pose('Sitzen', 0.0)
        self.huefte = g.welt(lok)[g.knoten('Hips')][:3, 3]   # Hüftgelenk im Modellraum (Sitzen, Bild 0)
        fuesse = g.netz_ecken(lok, {'Fuss_halb'})
        self.fuss_unten = fuesse[:, 1].min()


# Vorrücken der Figur nach vorn (Spielmeter) wie FigurenBaukasten.VorrueckenFuer: Schienbein je Datei (aus passanten_sitzen.py) gegen die Vorderkante des Sitzes
SCHIENBEIN = {'passant_probe_ork': 0.301, 'passant_probe_zwerg': 0.087, 'passant_probe_zwergin': 0.116, 'passant_probe_halbling': 0.127,
              'passant_probe_halblingin': 0.111}
# Sitzvorderkante vor der Sitzmitte je Szene (Meter), wie die Aufrufe im Spiel (vorderkante)
KANTE = {'Wohnstube': 0.175, 'Kontor': 0.30, 'Ratssaal': 0.275, 'Dom': 0.225, 'Schänke': 0.16, 'Kirche': 0.16, 'Ratssitzung': 0.18}
_kante = [0.30]


def vorruecken(datei):
    return max(0.0, _kante[0] + 0.01 - SCHIENBEIN[datei]) if datei in SCHIENBEIN else 0.0


def platzieren(ecken, fig, ursprung, drehung, y_ursprung, regel, gegenprobe):
    """Ecken der Figur in den Raum setzen: Drehung um Y (Modell +Z = Blick), Maßstab, Versatz nach Regel."""
    m = fig.massstab
    p = ecken * m
    if regel == 'neu' and not gegenprobe:
        p = p - np.array([fig.huefte[0], 0, fig.huefte[2]]) * m  # Hüftgelenk über den Sitzpunkt (SitzVersatz)
        p = p + np.array([0, 0, vorruecken(fig.datei)])   # kleine Völker weiter vorn (FigurenBaukasten.VorrueckenFuer)
    if gegenprobe:
        drehung += math.pi
    c, s = math.cos(drehung), math.sin(drehung)
    rot = np.array([[c, 0, s], [0, 1, 0], [-s, 0, c]])
    return p @ rot.T + np.array([ursprung[0], y_ursprung, ursprung[2]])


def vorwaerts(drehung, gegenprobe):
    d = drehung + (math.pi if gegenprobe else 0)
    return np.array([math.sin(d), 0, math.cos(d)])  # Modell +Z nach Drehung um Y


# ═════════════════════════════════════════════════════════════════════════
#  Eindringen
# ═════════════════════════════════════════════════════════════════════════

def tiefe_kisten(p, kisten):
    """Größte Eindringtiefe (m) der Punkte in Kisten: aus dem Code (Mitte, Größe, Drehung um Y) oder Teile eines Raummodells
    (Hülle und Flächenebenen: innen, wenn innerhalb der Hülle und hinter allen Ebenen; Tiefe = Abstand zur nächsten Ebene oder Hüllfläche)."""
    beste, wo = 0.0, ''
    for kiste in kisten:
        if len(kiste) == 5:
            name, lo, hi, n, d = kiste
            innen = np.all((p > lo) & (p < hi), axis=1)
            if not innen.any():
                continue
            q = p[innen]
            abst = d[None, :] - q @ n.T            # positiv: hinter der Ebene (innen)
            drin = np.all(abst > 0, axis=1)
            if not drin.any():
                continue
            t = np.minimum(abst[drin].min(axis=1), np.minimum(q[drin] - lo, hi - q[drin]).min(axis=1)).max()
            if t > beste:
                beste, wo = t, name
            continue
        name, mitte, groesse, dreh = kiste
        c, s = math.cos(-dreh), math.sin(-dreh)
        q = p - np.array(mitte)
        q = np.c_[q[:, 0] * c + q[:, 2] * s, q[:, 1], -q[:, 0] * s + q[:, 2] * c]
        h = np.array(groesse) / 2
        innen = np.all(np.abs(q) < h, axis=1)
        if innen.any():
            t = np.min(h - np.abs(q[innen]), axis=1).max()
            if t > beste:
                beste, wo = t, name
    return beste, wo


def moebel_kisten(tri):
    """Möbel eines Raummodells als Kisten: zusammenhängende Teile des Netzes (gemeinsame Ecken), je Teil die achsparallele Hülle.
    Wände, Decke und Boden fallen weg (über 3 m oder ohne Dicke). Schräg stehende Teile (Würfeltisch) werden dadurch etwas zu groß."""
    schluessel, eltern = {}, list(range(len(tri)))

    def f(i):
        while eltern[i] != i:
            eltern[i] = eltern[eltern[i]]
            i = eltern[i]
        return i
    for i, t in enumerate(tri):
        for v in t:
            k = tuple(np.round(v, 4))
            if k in schluessel:
                eltern[f(i)] = f(schluessel[k])
            else:
                schluessel[k] = i
    gruppen = {}
    for i in range(len(tri)):
        gruppen.setdefault(f(i), []).append(i)
    kisten = []
    for g in gruppen.values():
        p = tri[g].reshape(-1, 3)
        lo, hi = p.min(0), p.max(0)
        groesse = hi - lo
        if groesse.max() > 3.0 or groesse.min() < 0.005:
            continue
        # Ebenen der Flächen, nach außen gerichtet (vom Schwerpunkt weg): Teile sind Kisten, auch schräge (Beine der Tische)
        ecken = tri[g]
        n = np.cross(ecken[:, 1] - ecken[:, 0], ecken[:, 2] - ecken[:, 0])
        laenge = np.linalg.norm(n, axis=1)
        gut = laenge > 1e-10
        n = n[gut] / laenge[gut, None]
        punkt = ecken[gut, 0]
        mitte = p.mean(0)
        n *= np.sign(np.einsum('ij,ij->i', n, punkt - mitte) + 1e-12)[:, None]
        kisten.append(('Möbel', lo, hi, n, np.einsum('ij,ij->i', n, punkt)))
    return kisten


# ═════════════════════════════════════════════════════════════════════════
#  Räume (Maße aus dem Code der Szenen)
# ═════════════════════════════════════════════════════════════════════════

# Clips mit Formschlüssel „Sitzen“ = 1 wie FigurenBaukasten.SitzSchluessel (Sitzen und die sitzenden Arbeitsclips)
SITZ_CLIPS = {'Sitzen', 'Feinarbeit', 'Trinken', 'Schreiben', 'Beten', 'Zaehlen', 'Putzen', 'Rechnen', 'Kutschieren'}

NORD, WEST, SUED, OST = 0.0, math.pi / 2, math.pi, -math.pi / 2


def platzdrehung_zu_modell(d):
    """Konvention der Räume aus Grundkörpern: Drehung 0 blickt nach −Z; das Modell blickt nach +Z – eine halbe Drehung mehr."""
    return d + math.pi


def raum_wohnstube(sitz):
    kisten = [('Tisch', (0.4, 0.78, 0), (2.6, 0.1, 1.1), 0), ('Tischfuss', (-0.85, 0.38, 0), (0.1, 0.7, 0.3), 0), ('Tischfuss', (1.65, 0.38, 0), (0.1, 0.7, 0.3), 0)]
    kisten += [(f'Bank', (0.4, sitz - 0.05, dz), (2.4, 0.1, 0.35), 0) for dz in (-0.95, 0.95)]
    plaetze = [((0.4, 0, -0.95), SUED), ((-0.3, 0, 0.95), NORD), ((1.1, 0, 0.95), NORD), ((1.0, 0, -0.95), SUED)]
    return kisten, [(o, platzdrehung_zu_modell(d), sitz, (o[0], 0, 0)) for o, d in plaetze]


def raum_kontor(sitz, stuhl_z):
    kisten = [('Pult', (0, 0.5, -1.0), (2.4, 1.0, 0.2), 0), ('Pultplatte', (0, 1.03, -1.4), (2.5, 0.06, 1.0), 0),
              ('Stuhl', (0, sitz - 0.04, stuhl_z), (0.6, 0.08, 0.6), 0), ('Lehne', (0, 0.9, stuhl_z - 0.3), (0.6, 1.0, 0.08), 0)]
    return kisten, [((0, 0, stuhl_z), platzdrehung_zu_modell(SUED), sitz, (0, 0, -1.4))]


def raum_ratssaal(sitz, seite=2.45):
    kisten = [('Tisch', (0, 0.8, -2.4), (4.0, 0.1, 0.8), 0), ('Tischfuss', (-1.9, 0.4, -2.4), (0.1, 0.7, 0.6), 0), ('Tischfuss', (1.9, 0.4, -2.4), (0.1, 0.7, 0.6), 0)]
    for dx in (-1.6, 1.6):
        kisten += [('Seitentisch', (dx, 0.8, -0.4), (0.8, 0.1, 3.2), 0), ('Seitenfuss', (dx, 0.4, -1.9), (0.6, 0.7, 0.1), 0), ('Seitenfuss', (dx, 0.4, 1.1), (0.6, 0.7, 0.1), 0)]
    sitze = [((0, 0, -3.4), SUED), ((-1.2, 0, -3.4), SUED), ((1.2, 0, -3.4), SUED), ((-seite, 0, -1.0), OST),
             ((seite, 0, -1.0), WEST), ((-seite, 0, 0.6), OST), ((seite, 0, 0.6), WEST)]
    plaetze = []
    for i, (o, d) in enumerate(sitze):
        hinten = np.array([math.sin(d), 0, math.cos(d)]) * 0.32
        kisten.append(('Stuhl', (o[0], sitz - 0.04, o[2]), (0.55, 0.08, 0.55), 0))
        kisten.append(('Lehne', (o[0] + hinten[0], 1.2 if i == 0 else 0.85, o[2] + hinten[2]), (0.55, 1.6 if i == 0 else 0.9, 0.08), d))
        ziel = (o[0], 0, -2.4) if i < 3 else (math.copysign(1.6, o[0]), 0, o[2])   # der Tisch vor dem Stuhl
        plaetze.append((o, platzdrehung_zu_modell(d), sitz, ziel))
    return kisten, plaetze


def raum_dom(sitz):
    kisten, plaetze = [], []
    for reihe in range(8):
        z = -4.6 + reihe * 1.55
        for x in (-2.4, 2.4):
            kisten += [('Bank', (x, sitz - 0.04, z), (3.2, 0.08, 0.45), 0), ('Bankfuss', (x, (sitz - 0.08) / 2, z + 0.05), (3.0, sitz - 0.08, 0.2), 0),
                       ('Lehne', (x, 0.8, z + 0.27), (3.2, 0.6, 0.07), 0)]
            for dx in (-1.0, 0.0, 1.0):
                plaetze.append(((x + dx, 0, z), platzdrehung_zu_modell(NORD), sitz, (x + dx, 0, -8.4)))
    return kisten, plaetze


def glb_raum(datei, anker_praefixe, ziel_von, netze, hoehe_von):
    """Raummodell: Anker (−Z blickt zum Tisch), Halter am Anker um 180° gedreht (Modell +Z); Möbel als Dreiecke."""
    g = Glb(os.path.join(INNEN, datei))
    welt = g.welt()
    namen = [nd.get('name', '') for nd in g.j['nodes']]
    plaetze = []
    for i, n in enumerate(namen):
        if not any(n.startswith(p) for p in anker_praefixe):
            continue
        m = welt[i]
        vor = -m[:3, 2] / np.linalg.norm(m[:3, 2])   # −Z des Ankers
        drehung = math.atan2(vor[0], vor[2])         # Modell +Z soll dorthin
        ziel = ziel_von(n, namen, welt, m)
        extras = g.j['nodes'][i].get('extras', {})
        plaetze.append((tuple(m[:3, 3]), drehung, hoehe_von(extras), ziel, n))
    return moebel_kisten(g.dreiecke(set(netze))), plaetze


def schaenke():
    def ziel(n, namen, welt, m):
        krug = 'Krug' + n[4:]
        return tuple(welt[namen.index(krug)][:3, 3]) if krug in namen else tuple(m[:3, 3])
    return glb_raum('wirt_innen.glb', ('Gast_',), ziel, ['Raum_Saal', 'Raum_Werk', 'Wuerfeltisch'], lambda e: None)


def kirche():
    def ziel(n, namen, welt, m):
        return (m[0, 3], 0, -20.0)   # nach vorn zum Chor (−Z des Kirchenraums); die Kanzel steht seitlich
    return glb_raum('prediger_innen.glb', ('Glaeubig_',), ziel, ['Raum_Kirche'], lambda e: None)


def ratssitzung():
    def ziel(n, namen, welt, m):
        if n == 'Anker_Buergermeister':
            return tuple(welt[namen.index('Anker_Redner')][:3, 3])
        return (0.0, 0, m[2, 3])   # die Ratsherren sitzen an der langen Tafel einander gegenüber: zur Mitte der Tafel
    return glb_raum('rathaus_innen.glb', ('Anker_Sitz_', 'Anker_Buergermeister'), ziel, ['Raum_Rat'],
                    lambda e: float(str(e.get('sitzhoehe', 0.45)).replace(',', '.')))


# ═════════════════════════════════════════════════════════════════════════
#  Lauf
# ═════════════════════════════════════════════════════════════════════════

# Sitzhöhen und Stuhllage wie im Spiel (Stadtansicht.Raeume.cs, Sonderbauten.cs); --regel=alt: der Stand vor der Sitzregel
WERTE = {
    'alt': dict(wohnstube=0.47, kontor=0.46, kontor_stuhl=-2.3, kontor_platz=-2.25, ratssaal=0.46, ratssaal_seite=2.45, dom=0.49, modell_sitz=None,
                sitzung_luft=0.04),
    'neu': dict(wohnstube=0.44, kontor=0.44, kontor_stuhl=-2.6, kontor_platz=-2.6, ratssaal=0.44, ratssaal_seite=2.6, dom=0.44, modell_sitz=0.43,
                sitzung_luft=0.01),
}


def winkel(vor, ort, ziel):
    r = np.array(ziel) - np.array(ort)
    r[1] = 0
    if np.linalg.norm(r) < 1e-6:
        return 0.0
    r /= np.linalg.norm(r)
    return math.degrees(math.acos(float(np.clip(vor @ r, -1, 1))))


def main():
    regel = next((a.split('=')[1] for a in sys.argv if a.startswith('--regel=')), 'neu')
    gegenprobe = '--gegenprobe' in sys.argv
    arten = [a.split('=')[1].split(',') for a in sys.argv if a.startswith('--arten=')]
    arten = arten[0] if arten else list(FIGUREN_ARTEN)
    w = WERTE[regel]
    figuren = {a: Sitzfigur(a) for a in arten}
    szenen = []
    k, p = raum_wohnstube(w['wohnstube'])
    szenen.append(('Wohnstube', 'kisten', k, p))
    k, p = raum_kontor(w['kontor'], w['kontor_stuhl'])
    p = [((0, 0, w['kontor_platz']),) + x[1:] for x in p]
    szenen.append(('Kontor', 'kisten', k, p))
    k, p = raum_ratssaal(w['ratssaal'], w['ratssaal_seite'])
    szenen.append(('Ratssaal', 'kisten', k, p))
    k, p = raum_dom(w['dom'])
    szenen.append(('Dom', 'kisten', k, p))
    for name, fn in (('Schänke', schaenke), ('Kirche', kirche), ('Ratssitzung', ratssitzung)):
        kisten, plaetze = fn()
        szenen.append((name, 'modell', kisten, plaetze))

    schlecht = False
    print(f'Sitzprüfung, Regel {regel}{" (Gegenprobe: gedreht, ohne Versatz)" if gegenprobe else ""}')
    print(f'{"Szene":12} {"Art":11} {"Plätze":>6} {"Körper cm":>10} {"bei":12} {"Saum cm":>8} {"bei":10} {"Füße über Boden cm":>19} {"Blick max °":>11}')
    nur = [a.split('=')[1].split(',') for a in sys.argv if a.startswith('--szenen=')]
    for name, art_m, moebel, plaetze in szenen:
        if nur and name not in nur[0]:
            continue
        _kante[0] = KANTE[name]
        for art, fig in figuren.items():
            tief, wo, fuss, blick, saum, saum_wo = 0.0, '', [], 0.0, 0.0, ''
            for platz in plaetze:
                ort, drehung, sitz, ziel = platz[:4]
                # Ratssitzung: der Thron des Bürgermeisters (auf dem Podest) hat seine Vorderkante 1 cm weiter vorn (Stadtansicht.Ratssitzung.cs)
                _kante[0] = 0.19 if name == 'Ratssitzung' and ort[1] > 0.2 else KANTE[name]
                luft = 0.01 if regel == 'neu' else 0.0   # neu: 1 cm Luft unter dem Gesäß (Stadtansicht.Baukasten.cs, SitzLuft)
                if art_m == 'kisten':
                    y = sitz + luft - fig.sitzhoehe * fig.massstab   # InnenFigurBauen: Sitzhöhe − (Kniehöhe − 0,02)
                elif name == 'Ratssitzung':   # alt: Modell.Position = Sitzhöhe + 0,04 − Kniehöhe; neu: Gesäß auf der Sitzhöhe des Ankers
                    y = ort[1] + sitz + w['sitzung_luft'] - (fig.sitzhoehe + (0.02 if regel == 'alt' else 0)) * fig.massstab
                elif w['modell_sitz'] is None:   # alt: Halter = SitzKniehoehe (0,50) − Kniehöhe
                    y = ort[1] + 0.50 - (fig.sitzhoehe + 0.02) * fig.massstab
                else:                             # neu: Gesäß auf der Sitzfläche der Bänke im Modell
                    y = ort[1] + w['modell_sitz'] + luft - fig.sitzhoehe * fig.massstab
                if regel == 'neu':   # Füße nie unter dem Boden (sitzt jemand auf einer niedrigeren Bank, schwebt das Gesäß knapp darüber)
                    y = max(y, ort[1] if art_m == 'modell' else 0.0)
                blick = max(blick, winkel(vorwaerts(drehung, gegenprobe), ort, ziel))
                for pose in fig.posen:
                    for ecken in pose.values():
                        e, stoff = ecken
                        pp = platzieren(e, fig, ort, drehung, y, regel, gegenprobe)
                        t, o = tiefe_kisten(pp[~stoff], moebel)
                        if t > tief:
                            tief, wo = t, o
                        if stoff.any():
                            ts, os_ = tiefe_kisten(pp[stoff], moebel)
                            if ts > saum:
                                saum, saum_wo = ts, os_
                boden = ort[1] if art_m == 'modell' else 0.0
                fuss.append((y + fig.fuss_unten * fig.massstab - boden) * 100)
            print(f'{name:12} {art:11} {len(plaetze):6d} {tief * 100:10.1f} {wo:12} {saum * 100:8.1f} {saum_wo:10} {min(fuss):9.1f} bis {max(fuss):5.1f} {blick:11.0f}')
            if art in ('Mann', 'Frau') and (tief > 0.005 or blick >= 45):
                schlecht = True
    return 1 if schlecht else 0


if __name__ == '__main__':
    sys.exit(main())
