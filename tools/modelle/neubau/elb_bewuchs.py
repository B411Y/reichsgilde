"""Efeu der Elbenhaeuser als Textur (nur numpy, ohne Blender): fuer das Blatt trim_s3_elb (trimsheet_elb.py) und fuer die Steinebene
elb_s3 im Haeuser-Array (trimsheet_ebenen.py), die den Stein der Mauer (K2) erst beim Packen einsetzt und den Efeu danach wieder
auftraegt. Gleiches Verfahren wie trimsheet_elb.efeu (Stufe 1, im Putzband): Ranken vom unteren Bandrand nach oben, daran Blaetter;
Farbe EFEU (Kunstwerk), Normale leicht erhaben, Rauheit hoeher. Die Felder sind sRGB-Werte 0..1 [Zeile, Spalte, Kanal].
"""
import numpy as np

EFEU = (0.20, 0.30, 0.13)          # wie trimsheet_elb.EFEU: Farbe, die in der gespeicherten PNG steht (linear gerechnet, sRGB abgelegt)
EFEU_S3 = dict(ranken=1, saat=61, blatt_px=1.5)   # Mauer des Steinhauses (Band "bruchstein"), sehr sparsam: das Band liegt in Lagen von 0,4 m


def _linear(c):
    return np.where(c <= 0.04045, c / 12.92, ((c + 0.055) / 1.055) ** 2.4)


def _srgb(c):
    c = np.clip(c, 0.0, 1.0)
    return np.where(c <= 0.0031308, c * 12.92, 1.055 * c ** (1 / 2.4) - 0.055)


def feld_rauschen(hoehe, breite, zellen_y, zellen_x, saat):
    """Weiches 2D-Rauschen 0..1, waagerecht nahtlos (wie trimsheet_elb.feld_rauschen)."""
    z = np.random.default_rng(saat).random((zellen_y + 1, zellen_x)).astype(np.float32)
    y = np.linspace(0, zellen_y, hoehe, dtype=np.float32)[:, None]
    x = np.linspace(0, zellen_x, breite, endpoint=False, dtype=np.float32)[None, :]
    y0, x0 = np.floor(y).astype(int), np.floor(x).astype(int)
    fy, fx = y - y0, x - x0
    y1, x1 = np.minimum(y0 + 1, zellen_y), (x0 + 1) % zellen_x
    x0 = x0 % zellen_x
    oben = z[y0, x0] * (1 - fx) + z[y0, x1] * fx
    unten = z[y1, x0] * (1 - fx) + z[y1, x1] * fx
    return oben * (1 - fy) + unten * fy


def efeu_maske(hoehe, breite, ranken, saat, blatt_px=1.0):
    """Maske 0/1 der Efeublaetter in einem Band (hoehe x breite Bildpunkte). `blatt_px`: Groesse der Blaetter relativ zum Putzband der
    Stufe 1 (dort 4 mm je Bildpunkt; im Bruchsteinband 2 mm je Bildpunkt: 2,0, damit die Blaetter gleich gross wirken)."""
    z = np.random.default_rng(saat)
    maske = np.zeros((hoehe, breite), dtype=np.float32)
    for k in range(ranken):
        x = (k + 0.25 + 0.4 * z.random()) * breite / ranken
        y = hoehe - 1.0
        oben = hoehe * (0.10 + 0.35 * z.random())
        while y > oben:
            x += z.normal(0, 3.0 * blatt_px); y -= 6.0 * blatt_px
            for _ in range(2):
                bx, by = x + z.normal(0, 14 * blatt_px), y + z.normal(0, 8 * blatt_px)
                rx, ry = (7 + 4 * z.random()) * blatt_px, (5 + 3 * z.random()) * blatt_px
                x0, x1 = int(bx - rx - 1), int(bx + rx + 2); y0, y1 = max(int(by - ry - 1), 0), min(int(by + ry + 2), hoehe)
                if y0 >= y1: continue
                xs = np.arange(x0, x1) % breite
                d = ((np.arange(x0, x1)[None, :] - bx) / rx) ** 2 + ((np.arange(y0, y1)[:, None] - by) / ry) ** 2
                maske[y0:y1][:, xs] = np.maximum(maske[y0:y1][:, xs], (d < 1).astype(np.float32))
    return maske


def efeu_auftragen(albedo, normal, orm, a, e, ranken=4, saat=61, blatt_px=2.0):
    """Traegt Efeu in die Zeilen a..e der Felder ein (albedo sRGB mit 3 oder 4 Kanaelen, normal, orm; alle 0..1, werden veraendert)."""
    h, breite = e - a, albedo.shape[1]
    maske = efeu_maske(h, breite, ranken, saat, blatt_px)
    w = maske[..., None]
    lin = _linear(albedo[a:e, :, :3])
    farbe = np.array(EFEU, dtype=np.float32) * (0.7 + 0.6 * feld_rauschen(h, breite, 40, 400, saat + 1))[..., None]
    albedo[a:e, :, :3] = _srgb(lin * (1 - w) + farbe * w)
    gy, gx = np.gradient(maske * 3.0)
    normal[a:e, :, 0] = np.clip(normal[a:e, :, 0] - gx * 0.5, 0, 1)
    normal[a:e, :, 1] = np.clip(normal[a:e, :, 1] + gy * 0.5, 0, 1)
    orm[a:e, :, 0] = orm[a:e, :, 0] * (1 - 0.25 * maske)
    orm[a:e, :, 1] = orm[a:e, :, 1] * (1 - maske) + 0.7 * maske
    return float(maske.mean())
