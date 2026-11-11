"""Meldungstöne der Oberfläche: Siegel, Brief, Entscheidung, Fehler, Mahnung (Stab-Glockenspiel, weiche Partiale, ein Hall).
Rein rechnerisch (numpy), keine Samples, nichts aus dem Original-Spiel. Zufall nur aus festen Startwerten: derselbe Lauf liefert dieselben Samples.

Herkunft: Das Skript fasst die Läufe der Klangrunden 1 bis 4 zusammen (11.11.2026), nur die gewählten Töne:
  Runde 1  Synthese (Partiale, Arpeggio, Glissando, Faltungshall, Messung)         -> Bausteine unten
  Runde 2  siegel_a, eine Quinte tiefer, weiche Partiale (siegel_a_tief2_weich)    -> meldung_siegel
  Runde 3  Tonlisten brief_2, entscheidung_3, fehler_2                             -> meldung_brief, _entscheidung, _fehler
  Runde 4  mahnung_neu_1 (ein Schlag F4 mit kurzem Glitzer)                        -> meldung_mahnung
Aufruf (vom Wurzelordner des Repos; braucht numpy und ffmpeg mit libvorbis):
  python -I tools/ton/meldungston_erzeugen.py game/assets/audio/geraeusche/oberflaeche [--ffmpeg PFAD] [--vergleich ORDNER_MIT_WAV]
Lautheit: Verstärkung auf -20,0 LUFS (Spitze der 400-ms-Lautheit, K-gewichtet nach BS.1770, ohne Gate), kein Begrenzer; 3 ms Einblenden, Kosinus-Ausblenden.
OGG: libvorbis q6, 48 kHz Stereo. Der Ogg-Container ist nicht bitgleich über Läufe und Versionen (Seriennummer); die WAV vor dem Kodieren sind es
(Kurzhash wird ausgegeben, --vergleich prüft ihn gegen die WAV der Klangrunden).
"""
import argparse, hashlib, os, subprocess, tempfile, wave
import numpy as np

SR = 48000
ZIEL_LUFS = -20.0


# ---------------- Messen (BS.1770-K-Gewichtung, ohne Gate) ----------------
def _biquad(x, b, a):
    y = np.zeros_like(x); x1 = x2 = y1 = y2 = 0.0
    for i, v in enumerate(x):
        o = b[0] * v + b[1] * x1 + b[2] * x2 - a[1] * y1 - a[2] * y2
        x2, x1, y2, y1 = x1, v, y1, o
        y[i] = o
    return y


def _kfilter(x):
    s = _biquad(x, [1.53512485958697, -2.69169618940638, 1.19839281085285], [1.0, -1.69065929318241, 0.73248077421585])
    return _biquad(s, [1.0, -2.0, 1.0], [1.0, -1.99004745483398, 0.99007225036621])


def messen(x):
    """Rückgabe: (LUFS ganze Datei ohne Gate, Spitze der 400-ms-Lautheit, True-Peak dBTP)."""
    x = x if x.ndim == 2 else x[:, None]
    z = sum(_kfilter(x[:, c].astype(float)) ** 2 for c in range(x.shape[1]))
    ganz = -0.691 + 10 * np.log10(max(z.mean(), 1e-12))
    f = int(0.4 * SR)
    zp = np.concatenate([np.zeros(f - 1), z, np.zeros(f - 1)])
    w = np.convolve(zp, np.ones(f) / f, mode="valid")
    mom = -0.691 + 10 * np.log10(max(w.max(), 1e-12))
    tp = 0.0
    for c in range(x.shape[1]):
        n = len(x); X = np.fft.rfft(x[:, c])
        U = np.fft.irfft(np.concatenate([X, np.zeros(3 * len(X))]), 4 * n)[:4 * n] * 4
        tp = max(tp, np.abs(U).max())
    return ganz, mom, 20 * np.log10(max(tp, 1e-9))


def wav_schreiben(pfad, x):
    with wave.open(pfad, "wb") as w:
        w.setnchannels(x.shape[1]); w.setsampwidth(2); w.setframerate(SR)
        w.writeframes((np.clip(x, -1, 1) * 32767).round().astype("<i2").tobytes())


# ---------------- Synthese ----------------
# Partiale: (Frequenzverhältnis, Anfangspegel, Abklingfaktor relativ zum Grundton)
WEICH = [(1.0, 1.0, 1.0), (2.76, 0.28, 0.45), (5.40, 0.08, 0.22)]   # freier Stab, Obertöne leise (Siegel, Brief, Entscheidung, Mahnung)
DUMPF = [(1.0, 1.0, 1.0), (2.76, 0.10, 0.35)]                         # fast nur Grundton (Fehler)


def ton(f0, partiale, dauer, abkling, pegel, zufall):
    """Ein Glockenton: Summe gedämpfter Sinus mit zufälliger Phase, 2 ms Anstieg."""
    n = int(dauer * SR); t = np.arange(n) / SR; y = np.zeros(n)
    for r, a, d in partiale:
        f = f0 * r
        if f > 18000:
            continue
        y += a * np.exp(-t / (abkling * d)) * np.sin(2 * np.pi * f * t + zufall.uniform(0, 2 * np.pi))
    an = int(0.002 * SR); y[:an] *= np.linspace(0, 1, an)
    return pegel * y


def glissando(f_von, f_bis, dauer, pegel):
    """Schneller Aufwärtsstreifen (Sinus, logarithmischer Lauf), ausgeblendet: der Glitzer."""
    n = int(dauer * SR); t = np.arange(n) / SR
    f = f_von * (f_bis / f_von) ** (t / dauer)
    return pegel * np.sin(np.pi * t / dauer) ** 2 * np.sin(2 * np.pi * np.cumsum(f) / SR)


def hall(x, rt60, mischung, vorlauf_ms, daempf, seed):
    """Stereo-Hall aus abklingendem Rauschen (Faltung per FFT), mono -> (n,2)."""
    z = np.random.default_rng(seed)
    n = int(rt60 * 1.1 * SR); t = np.arange(n) / SR
    kan = []
    for _ in range(2):
        ir = z.standard_normal(n) * 10 ** (-3 * t / rt60)
        a = np.exp(-t * daempf)
        glatt = np.convolve(ir, np.ones(4) / 4, mode="same")
        ir = a * ir + (1 - a) * glatt
        ir[:int(vorlauf_ms / 1000 * SR)] = 0
        ir = np.diff(np.concatenate([[0.0], ir]))
        ir /= np.sqrt((ir ** 2).sum())
        L = len(x) + n; N = 1 << int(np.ceil(np.log2(L)))
        kan.append(np.fft.irfft(np.fft.rfft(x, N) * np.fft.rfft(ir, N), N)[:L])
    nass = np.stack(kan, axis=1)
    trocken = np.concatenate([x, np.zeros(len(nass) - len(x))])[:, None] * np.ones((1, 2))
    return trocken * (1 - mischung) + nass * mischung * np.sqrt((trocken ** 2).sum() / max((nass ** 2).sum(), 1e-12))


def faden(y, ein_ms, aus_ms):
    a, b = int(ein_ms / 1000 * SR), int(aus_ms / 1000 * SR)
    y = y.copy(); y[:a] *= np.linspace(0, 1, a)[:, None]
    y[-b:] *= (0.5 + 0.5 * np.cos(np.linspace(0, np.pi, b)))[:, None]
    return y


def noten_hz(namen, halbtoene=0):
    """Notennamen (C D E F G A H B, # = Kreuz, Zahl = Oktave) in Hz, um `halbtoene` nach unten verschoben."""
    tab = {"C": 0, "D": 2, "E": 4, "F": 5, "G": 7, "A": 9, "H": 11, "B": 11}
    out = []
    for s in namen:
        k = tab[s[0]]; i = 1
        if s[i] == "#":
            k += 1; i += 1
        out.append(440 * 2 ** ((k - 9 + 12 * (int(s[i:]) - 4)) / 12) * 2 ** (-halbtoene / 12))
    return out


# ---------------- Die fünf Töne ----------------
def siegel():
    """Runde 2, siegel_a_tief2_weich: C6 E6 G6 C7 eine Quinte (7 Halbtöne) tiefer = F5 A5 C6 F6, Abstand 55 ms, Glitzer 0,22 s, Hall RT60 0,9 s, 0,80 s."""
    s = 7; z = np.random.default_rng(11); dauer = 0.80; x = np.zeros(int((dauer + 1.0) * SR)); n = len(x)
    for i, f in enumerate(noten_hz(["C6", "E6", "G6", "C7"], s)):
        t0 = max(0, int((i * 0.055 + z.uniform(-0.004, 0.004)) * SR))
        y = ton(f, WEICH, 0.18 * 5, 0.18 * (1 + 0.15 * i), 0.88 ** i, z)
        x[t0:t0 + len(y)] += y[:n - t0]
    k = 2 ** (-s / 12); g = glissando(1500 * k, 6000 * k, 0.22, 0.05 * 0.35); x[:len(g)] += g
    return faden(hall(x, 0.9, 0.28, 8, 2.0, 12)[:int(dauer * SR)], 3, 260)


GL = (1000, 4000, 0.22, 0.0175)   # weicher Glitzer


def lauf(namen, t0=0.0, abstand=0.055, fall=0.88, ab=0.18, pg=1.0):
    return [(t0 + i * abstand, n, pg * fall ** i, ab) for i, n in enumerate(namen)]


def tonliste(p):
    """Runde 3: Liste von Tönen (Zeit s, Note, Pegel, Abklingzeit s), optional ein Glitzer, Hall."""
    z = np.random.default_rng(p["seed"]); n = int((p["dauer"] + 1.0) * SR); x = np.zeros(n)
    tab = p.get("klang", WEICH)
    for t, nm, pg, ab in p["ev"]:
        y = ton(noten_hz([nm])[0], tab, ab * 5, ab, pg, z); i = int((t + z.uniform(-0.003, 0.003) * (t > 0)) * SR); x[i:i + len(y)] += y[:n - i]
    if p.get("gliss"):
        (v, b, d, g), t0 = p["gliss"]; gl = glissando(v, b, d, g); i = int(t0 * SR); x[i:i + len(gl)] += gl
    return faden(hall(x, p["rt60"], p["hall"], 8, 2.0, p["seed"] + 1)[:int(p["dauer"] * SR)], 3, p["aus"])


def brief():
    """Runde 3, brief_2: langer Lauf F5 G5 A5 C6 D6 F6 (Abstand 50 ms) mit Glitzer, 1,15 s."""
    return tonliste(dict(ev=lauf(["F5", "G5", "A5", "C6", "D6", "F6"], 0.0, 0.05, fall=0.9), dauer=1.15, rt60=1.1, hall=0.30, aus=420, gliss=(GL, 0.0), seed=42))


def entscheidung():
    """Runde 3, entscheidung_3: Lauf auf und ab F5 A5 C6 F6 C6 A5 (Abstand 75 ms) und ein F5 dazu, kein Glitzer, 1,40 s."""
    return tonliste(dict(ev=lauf(["F5", "A5", "C6", "F6", "C6", "A5"], 0.0, 0.075, fall=0.95, ab=0.22) + [(0.45, "F5", 0.8, 0.4)], dauer=1.4, rt60=1.3, hall=0.30, aus=520, gliss=None, seed=53))


def fehler():
    """Runde 3, fehler_2: zwei gedämpfte Töne abwärts F5, C5 (fast nur Grundton), kurzer Hall, 0,65 s."""
    return tonliste(dict(ev=[(0.0, "F5", 1.0, 0.12), (0.13, "C5", 0.9, 0.16)], klang=DUMPF, dauer=0.65, rt60=0.5, hall=0.15, aus=240, gliss=None, seed=62))


def mahnung():
    """Runde 4, mahnung_neu_1: ein ernster Schlag F4 mit kurzem, sehr leisem Glitzer, kein zweiter Anschlag, 0,95 s."""
    z = np.random.default_rng(81)
    x = np.zeros(int(2.0 * SR)); y = ton(noten_hz(["F4"])[0], WEICH, 2.5, 0.45, 1.0, z); x[:len(y)] += y[:len(x)]
    g = glissando(900, 3600, 0.20, 0.0175); i = int(0.01 * SR); x[i:i + len(g)] += g
    return faden(hall(x, 1.1, 0.30, 8, 2.0, 82)[:int(0.95 * SR)], 3, 380)


TOENE = {"meldung_siegel": siegel, "meldung_brief": brief, "meldung_entscheidung": entscheidung, "meldung_fehler": fehler, "meldung_mahnung": mahnung}
FRUEHERE = {"meldung_siegel": "siegel_a_tief2_weich", "meldung_brief": "brief_2", "meldung_entscheidung": "entscheidung_3", "meldung_fehler": "fehler_2", "meldung_mahnung": "mahnung_neu_1"}


def kurzhash(pfad):
    with open(pfad, "rb") as f:
        return hashlib.sha256(f.read()).hexdigest()[:16]


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("ziel", help="Zielordner für die OGG (game/assets/audio/geraeusche/oberflaeche)")
    ap.add_argument("--ffmpeg", default="ffmpeg")
    ap.add_argument("--vergleich", default=None, help="Ordner mit den WAV der Klangrunden (siegel_a_tief2_weich.wav, brief_2.wav, ...) für den Hash-Vergleich")
    a = ap.parse_args()
    os.makedirs(a.ziel, exist_ok=True)
    for name, fn in TOENE.items():
        y = fn(); mom = messen(y)[1]; y = y * 10 ** ((ZIEL_LUFS - mom) / 20)   # nur Verstärkung, kein Begrenzer
        ganz, mom, tp = messen(y); assert np.abs(y).max() < 1.0, "Übersteuerung " + name
        with tempfile.TemporaryDirectory() as d:
            w = os.path.join(d, name + ".wav"); wav_schreiben(w, y)
            h = kurzhash(w); vergleich = ""
            if a.vergleich:
                alt = os.path.join(a.vergleich, FRUEHERE[name] + ".wav")
                vergleich = (" == " + FRUEHERE[name] + ".wav") if os.path.exists(alt) and kurzhash(alt) == h else " (WAV weicht ab oder fehlt)"
            subprocess.run([a.ffmpeg, "-v", "error", "-y", "-i", w, "-c:a", "libvorbis", "-q:a", "6", os.path.join(a.ziel, name + ".ogg")], check=True)
        print(f"{name}: {len(y)/SR:.2f} s, ganz {ganz:.1f} LUFS, Spitze 400 ms {mom:.1f} LUFS, TP {tp:.1f} dBTP, WAV {h}{vergleich}")


if __name__ == "__main__":
    main()
