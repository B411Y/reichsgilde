"""Elbisches Schnitzwerk fuer die Holzbaender der Trim-Sheets (Nutzer 18.11.2026): eine geschnitzte Ranke mit wechselnden Blaettern,
eingetieft als Relief in Normalmap und Albedo. Eigene, prozedurale Zeichnung (keine Fremdtextur, keine Bild-KI).

    ranke_auftragen(albedo, normal, orm, a, e, meter_u, periode_m=0.6, saat=1)

Das Band (Zeilen a..e) liegt im Spiel quer ueber der Flaeche eines Balkens, Rahmens oder Moebelteils; u laeuft entlang des Holzes
(`meter_u` Meter je Blattbreite). Die Ranke ist bewusst grob (Periode 0,6 m, Stiel ein Zehntel, Blaetter ein Drittel der Bandhoehe),
damit sie im Pixel-Look als Musterung stehen bleibt: Rand des Bandes (12 %) bleibt frei fuer die Fase.
"""
import math

import numpy as np

GRUND = 0.18         # so viel dunkler der ausgehobene Grund um die Ranke
TIEFE = 0.5         # Staerke des Reliefs in der Normalmap
DUNKEL = 0.45        # so viel dunkler die Kerben (Rand der Schnitzerei) in der Albedo
HELL = 0.12          # so viel heller die erhabenen Teile (abgegriffen)


def _srgb_lin(c):
    return np.where(c <= 0.04045, c / 12.92, ((c + 0.055) / 1.055) ** 2.4)


def _lin_srgb(c):
    c = np.clip(c, 0, 1)
    return np.where(c <= 0.0031308, c * 12.92, 1.055 * c ** (1 / 2.4) - 0.055)


def ranke_hoehe(h, breite, meter_u, periode_m=0.6, saat=1):
    """Hoehenfeld 0..1 der Ranke (h Zeilen, breite Spalten): Stiel als Welle, je Halbwelle ein Blatt nach aussen, an jedem
    zweiten Blatt eine kleine Ranke (Kringel). Kachelt waagerecht nahtlos (ganze Perioden je Blattbreite)."""
    px_m = breite / meter_u
    perioden = max(1, round(meter_u / periode_m))
    P = breite / perioden                                   # Bildpunkte je Periode (ganzzahlig kachelnd)
    rand = 0.12 * h
    mitte, amp = h / 2, (h / 2 - rand) * 0.45
    yy, xx = np.mgrid[0:h, 0:breite].astype(np.float32)
    phase = 2 * math.pi * xx / P
    stiel_y = mitte + amp * np.sin(phase)
    dicke = max(2.0, 0.10 * h)
    hoehe = np.clip(1 - np.abs(yy - stiel_y) / dicke, 0, 1) ** 0.6
    rng = np.random.default_rng(saat)
    for k in range(perioden * 2):
        x0 = (k + 0.5) * P / 2 + rng.uniform(-0.05, 0.05) * P
        seite = 1 if k % 2 == 0 else -1                     # wechselnd oben und unten
        y0 = mitte + amp * math.sin(2 * math.pi * x0 / P)
        laenge, breit = 0.50 * h, 0.22 * h
        w = math.radians(55)
        dxr, dyr = math.cos(w), -seite * math.sin(w)        # Blattachse: schraeg nach vorn, nach oben (seite 1) bzw. unten
        cx, cy = x0 + dxr * laenge * 0.5, y0 + dyr * laenge * 0.5
        for dx in (-breite, 0, breite):                     # nahtlos
            u = (xx - cx - dx) * dxr + (yy - cy) * dyr
            v = -(xx - cx - dx) * dyr + (yy - cy) * dxr
            r = (u / (laenge / 2)) ** 2 + (v / (breit / 2)) ** 2
            blatt = np.clip(1 - r, 0, 1) ** 0.5
            ader = np.clip(1 - np.abs(v) / max(1.0, 0.08 * breit), 0, 1) * (r < 1)   # Mittelader eingekerbt
            hoehe = np.maximum(hoehe, blatt * 0.9 - ader * 0.35)
        if k % 2 == 1:                                      # Kringel: kleiner Ring neben dem Blatt
            kx, ky = x0 - 0.18 * P, mitte - seite * (amp + 0.05 * h)
            for dx in (-breite, 0, breite):
                d = np.hypot(xx - kx - dx, yy - ky)
                ring = np.clip(1 - np.abs(d - 0.09 * h) / max(1.5, 0.035 * h), 0, 1)
                hoehe = np.maximum(hoehe, ring * 0.8)
    hoehe[yy < rand * 0.6] = 0
    hoehe[yy > h - rand * 0.6] = 0
    return hoehe


def ranke_auftragen(albedo, normal, orm, a, e, meter_u, periode_m=0.6, saat=1, x0=0, x1=None):
    """Traegt die Ranke in die Zeilen a..e (Spalten x0..x1) ein; albedo sRGB (3 oder 4 Kanaele), normal (OpenGL), orm; 0..1."""
    x1 = albedo.shape[1] if x1 is None else x1
    h, breite = e - a, x1 - x0
    hoehe = ranke_hoehe(h, breite, meter_u * breite / albedo.shape[1], periode_m, saat)
    gy, gx = np.gradient(hoehe)
    kante = np.clip(np.hypot(gx, gy) * 3.0, 0, 1)            # Kerbe am Rand der Schnitzerei
    lin = _srgb_lin(albedo[a:e, x0:x1, :3])
    # Grund um die Ranke ausgehoben (etwas dunkler), Ranke erhaben und abgegriffen (heller), Kerben am Rand dunkel: so bleibt die
    # Musterung auch in groben Mip-Stufen (Pixel-Look) als Hell-Dunkel stehen
    grund = np.zeros_like(hoehe); grund[int(0.12 * h * 0.6):h - int(0.12 * h * 0.6)] = 1.0
    f = (1 - GRUND * grund * (1 - np.clip(hoehe * 2, 0, 1))) * (1 - DUNKEL * kante) * (1 + HELL * hoehe)
    # mittlere Helligkeit des Bandes bleibt: das Schnitzwerk verteilt nur Hell und Dunkel (der Holzton gilt im Ganzen)
    f = f / max(float((lin.mean(axis=2) * f).mean() / max(lin.mean(), 1e-6)), 1e-6)
    albedo[a:e, x0:x1, :3] = _lin_srgb(lin * f[..., None])
    normal[a:e, x0:x1, 0] = np.clip(normal[a:e, x0:x1, 0] - gx * TIEFE * 2, 0, 1)
    normal[a:e, x0:x1, 1] = np.clip(normal[a:e, x0:x1, 1] + gy * TIEFE * 2, 0, 1)
    orm[a:e, x0:x1, 0] = orm[a:e, x0:x1, 0] * (1 - 0.35 * kante)  # Verdeckung in den Kerben
    return float((hoehe > 0.3).mean())
