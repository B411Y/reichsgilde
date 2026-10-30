"""Kamera aus einer Begleitdatei der Bögen (charakter_schau.gd, Phase 6): Punkte des Kopfes ins Bild projizieren.

Godot: Transform3D = Basis (Spalten x, y, z) + Ursprung; die Kamera blickt entlang −Z; keep_aspect 1 = KEEP_HEIGHT
(fov und size gelten senkrecht). Bildpunkte: x nach rechts, y nach unten, Mitte des Bildpunkts bei +0,5.
"""
import json
import os

import numpy as np


def _transform(liste):
    t = np.asarray(liste, dtype=np.float64)
    B = np.stack([t[0:3], t[3:6], t[6:9]], axis=1)   # Spalten
    return B, t[9:12]


class Kamera:
    def __init__(self, daten):
        if isinstance(daten, str):
            daten = json.load(open(os.path.expanduser(daten), encoding="utf-8"))
        self.daten = daten
        self.B, self.o = _transform(daten["transform"])
        kt = daten.get("kopf_transform") or [1, 0, 0, 0, 1, 0, 0, 0, 1, 0, 0, 0]
        self.KB, self.ko = _transform(kt)
        self.w, self.h = daten["bild"]
        self.ortho = daten.get("projektion") == "ortho"
        self.fov = float(daten.get("fov", 20.0))
        self.size = float(daten.get("size", 1.0))
        if int(daten.get("keep_aspect", 1)) != 1:
            raise ValueError("nur KEEP_HEIGHT")

    def projizieren(self, punkte_mm):
        """Punkte des Kopfes (lokal, mm) -> Bildpunkte (N, 2) und Abstand vor der Kamera (N,) in m."""
        p = np.atleast_2d(np.asarray(punkte_mm, dtype=np.float64)) / 1000.0
        welt = p @ self.KB.T + self.ko
        c = (welt - self.o) @ self.B          # = B^T (welt − o)
        tiefe = -c[:, 2]
        if self.ortho:
            s = self.h / self.size
            x = self.w / 2 + c[:, 0] * s
            y = self.h / 2 - c[:, 1] * s
        else:
            f = (self.h / 2) / np.tan(np.radians(self.fov) / 2)
            x = self.w / 2 + f * c[:, 0] / tiefe
            y = self.h / 2 - f * c[:, 1] / tiefe
        return np.stack([x, y], axis=1), tiefe

    def blickrichtung_kopf(self):
        """Richtung von der Kamera weg, im Kopfsystem (für „zeigt zur Kamera“)."""
        v = -self.B[:, 2]
        return self.KB.T @ v

    def kamerapunkt_kopf(self):
        return (self.KB.T @ (self.o - self.ko)) * 1000.0
