# Charakterformen: kantige Gesichtszüge als räumliches Feld (Gauß-Beulen) auf alle Netze des Gesichts rechnen.
# Gleiches Feld für Haut, Brauen und Wimpern, damit alles zusammenpasst; wirkt auf Basis UND alle Shape Keys
# (Deltas bleiben erhalten). Koordinaten relativ zur Nasenspitze (Blender: -Y ist vorn).
import math
import numpy as np
from mathutils import Vector, kdtree

# (Name, x, dz, Sigma x, Sigma z, Stärke in Metern entlang der Normalen, gespiegelt)
FORMEN = [
    ("brauenwulst",    0.028, +0.062, 0.022, 0.010, +0.0085, True),
    ("glabella",       0.000, +0.056, 0.014, 0.012, +0.0065, False),
    ("tränensack",     0.031, +0.028, 0.016, 0.008, -0.0035, True),
    ("wangenknochen",  0.056, +0.016, 0.018, 0.016, +0.0048, True),
    ("wangenhöhle",    0.048, -0.034, 0.018, 0.020, -0.0040, True),
    ("nasenrücken",    0.000, +0.032, 0.007, 0.020, +0.0035, False),
    ("nasolabial",     0.032, -0.010, 0.006, 0.018, -0.0030, True),
    ("kinn",           0.000, -0.074, 0.020, 0.012, +0.0060, False),
    ("kieferwinkel",   0.063, -0.054, 0.012, 0.012, +0.0050, True),
    ("schläfe",        0.072, +0.056, 0.014, 0.020, -0.0045, True),
    ("stirn_mitte",    0.000, +0.085, 0.030, 0.014, +0.0030, False),
]


def feld(pos, norm, nase, staerke=1.0):
    """Verschiebung (n x 3) für Punkte pos (n x 3) mit Normalen norm (n x 3)."""
    d = np.zeros(len(pos))
    rel = pos - np.array([nase.x, nase.y, nase.z])
    vorn = np.clip((-norm[:, 1] - 0.15) / 0.5, 0, 1)         # nur nach vorn zeigende Flächen
    nahe = np.clip((0.12 - rel[:, 1] * -1.0 * 0 - (rel[:, 1] + 0.0)) , 0, 1)  # (Platzhalter, Tiefe wird über vorn geregelt)
    for _, x, dz, sx, sz, amp, spiegel in FORMEN:
        zentren = [x, -x] if spiegel and x != 0 else [x]
        for cx in zentren:
            g = np.exp(-0.5 * (((rel[:, 0] - cx) / sx) ** 2 + ((rel[:, 2] - dz) / sz) ** 2))
            d += g * amp
    d *= vorn * staerke
    # nur Punkte in Gesichtsnähe (nicht Hinterkopf): Tiefe relativ zur Nasenspitze < 0.12
    d *= (rel[:, 1] < 0.10)
    return norm * d[:, None]


def anwenden(b, objekte, nase, staerke=1.0):
    """b: Basisnetz; objekte: weitere Netze (Brauen, Wimpern), die dasselbe Feld bekommen."""
    me = b.data
    n = len(me.vertices)
    basis = np.empty(n * 3); me.vertices.foreach_get("co", basis); basis = basis.reshape(-1, 3)
    me.calc_normals() if hasattr(me, "calc_normals") else None
    nor = np.empty(n * 3); me.vertices.foreach_get("normal", nor); nor = nor.reshape(-1, 3)
    kd = kdtree.KDTree(n)
    for i, p in enumerate(basis):
        kd.insert(Vector(p), i)
    kd.balance()
    _verschieben(b, feld(basis, nor, nase, staerke))
    for o in objekte:
        m = len(o.data.vertices)
        p = np.empty(m * 3); o.data.vertices.foreach_get("co", p); p = p.reshape(-1, 3)
        nn = np.array([nor[kd.find(Vector(q))[1]] for q in p])
        _verschieben(o, feld(p, nn, nase, staerke))


def _verschieben(o, dl):
    me = o.data
    if me.shape_keys:
        for k in me.shape_keys.key_blocks:
            c = np.empty(len(me.vertices) * 3); k.data.foreach_get("co", c)
            k.data.foreach_set("co", (c.reshape(-1, 3) + dl).ravel())
    c = np.empty(len(me.vertices) * 3); me.vertices.foreach_get("co", c)
    me.vertices.foreach_set("co", (c.reshape(-1, 3) + dl).ravel())
    me.update()
