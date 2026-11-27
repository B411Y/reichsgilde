#!/usr/bin/env python3
"""Baut die vier Innenraum-Wetterschleifen (Regen und Sturm "von drinnen", Tag und Nacht) aus dem eigenen Bestand.

Eigene, freie Fassung: Quellen sind nur die vorhandenen Schichten unter game/assets/audio/umgebung/schichten
(regen_leicht, regen_stark, regen_dach, regen_traufe, wind_mittel; abgeleitet von CC0-Aufnahmen, siehe HERKUNFT.md)
und eigene Synthese (Zufallssaat fest, byte-gleich wiederholbar). Nichts aus Fremdbibliotheken mit Weitergabeverbot.

Aufruf (Python 3.13 mit numpy, ffmpeg mit libvorbis im Pfad oder über --ffmpeg):
  python -I tools/ton/innen_wetter_bauen.py --ffmpeg <pfad> [--aus game/assets/audio/umgebung/schichten] [--arbeit <ordner>]

Aufbau (alle Rechnungen im Frequenzbereich über genau 60 s, darum exakt periodisch: nahtlos ohne Naht bei der Synthese;
die Regenquellen werden mit Naht gleicher Leistung auf 60 s gelegt):
  innen_regen_*  : Regenbett (regen_leicht, Bandpass 150-900 Hz, wie durch eine Wand), Dachprasseln (regen_dach, 250 Hz-1,5 kHz),
                   Traufe (regen_traufe, leise), Tropfen an der Fensterscheibe (Poisson, 1,6-3,4 kHz, einzelne große Tropfen um 900 Hz),
                   dumpfer Raumton (unter 180 Hz). Nacht: Bett 1-4 kHz um rund 4 dB heller, Raumton leiser, Tropfen etwas deutlicher.
  innen_sturm_*  : Guss (regen_stark, Bandpass 120 Hz-1,6 kHz), Wind (wind_mittel, Tiefpass 700 Hz), Pfeifen in den Ritzen
                   (drei schmale Bänder 380/560/840 Hz mit eigener Böenkurve), Knarzen der Balken (Gleiten 75-150 Hz mit Rucken),
                   kein Donner (der kommt über den Donner-Bus). Nacht: heller (1-4 kHz), Pfeifen etwas stärker.
Lautheit -26 LUFS wie alle Schichten (Grundpegel im Spiel: Tabelle `KlangUmgebungModell.Schichten`), OGG Vorbis q4.
"""
import argparse
import re
import subprocess
import sys
import wave
from pathlib import Path

import numpy as np

FS = 48000
DAUER = 60
N = FS * DAUER
ZIEL_LUFS = -26.0
QUELLEN = Path(__file__).resolve().parents[2] / "game/assets/audio/umgebung/schichten"


def ffmpeg_lesen(ffmpeg, pfad):
    """OGG -> float64 Stereo (n, 2) bei 48 kHz."""
    roh = subprocess.run([ffmpeg, "-v", "error", "-i", str(pfad), "-f", "f32le", "-ac", "2", "-ar", str(FS), "-"],
                         check=True, capture_output=True).stdout
    return np.frombuffer(roh, dtype="<f4").reshape(-1, 2).astype(np.float64)


def auf_60s(x, versatz, blende=4.0):
    """Mono-Quelle (selbst eine Schleife) kreisend auf genau 60 s legen, Naht gleicher Leistung (Kosinusblende) am Ende."""
    b = int(blende * FS)
    idx = (np.arange(N + b) + int(versatz * FS)) % len(x)
    t = x[idx]
    out = t[:N].copy()
    w = np.arange(b) / b
    out[:b] = t[:b] * np.sin(w * np.pi / 2) + t[N:N + b] * np.cos(w * np.pi / 2)
    return out


def spektrum(n, f1=None, f2=None, ordnung=2):
    """Betrag eines Bandpasses (Butterworth-Betrag, nullphasig) im Frequenzbereich; f1 = Hochpassgrenze, f2 = Tiefpassgrenze."""
    f = np.fft.rfftfreq(n, 1.0 / FS)
    f[0] = 1e-3
    h = np.ones_like(f)
    if f1:
        h = h / np.sqrt(1.0 + (f1 / f) ** (2 * ordnung))
    if f2:
        h = h / np.sqrt(1.0 + (f / f2) ** (2 * ordnung))
    return h


def filtern(x, f1=None, f2=None, ordnung=2):
    return np.fft.irfft(np.fft.rfft(x) * spektrum(len(x), f1, f2, ordnung), n=len(x))


def norm(a):
    return a / (np.sqrt(np.mean(a ** 2)) + 1e-12)


def band_schmal(rng, mitte, breite):
    """Periodisches Rauschen in einem schmalen Band (zufällige Phase im Frequenzbereich), Effektivwert 1."""
    f = np.fft.rfftfreq(N, 1.0 / FS)
    s = np.exp(-0.5 * ((f - mitte) / breite) ** 2) * np.exp(2j * np.pi * rng.random(len(f)))
    return norm(np.fft.irfft(s, n=N))


def breit(rng):
    return norm(rng.standard_normal(N))


def rosa(rng):
    f = np.fft.rfftfreq(N, 1.0 / FS)
    f[0] = 1.0
    s = np.fft.rfft(rng.standard_normal(N)) / np.sqrt(f)
    s[0] = 0
    return norm(np.fft.irfft(s, n=N))


def boenkurve(rng, anzahl=5, tief=0.55):
    """Periodische Böenkurve aus ganzzahligen Vielfachen von 1/60 Hz (exakt nahtlos); Wert 1-tief .. 1."""
    t = np.arange(N) / FS
    k = rng.choice(np.arange(1, 13), size=anzahl, replace=False)
    g = sum(rng.uniform(0.4, 1.0) / (1 + 0.25 * kk) * np.cos(2 * np.pi * kk * t / DAUER + rng.uniform(0, 2 * np.pi)) for kk in k)
    g = (g - g.min()) / (g.max() - g.min())
    return (1 - tief) + tief * g ** 1.6


def hinzu_kreisend(ziel, ereignis, start):
    """Ereignis ab Probe start addieren, am Ende kreisend (Schleife)."""
    np.add.at(ziel, (start + np.arange(len(ereignis))) % len(ziel), ereignis)


def tropfen_scheibe(rng, rate, grosse_rate):
    """Tropfen an der Fensterscheibe: kurze helle Klicks (Poisson) und selten ein großer, dumpfer Tropfen."""
    y = np.zeros(N)
    for s in rng.integers(0, N, size=int(rate * DAUER)):
        L = int(0.012 * FS)
        e = np.exp(-(np.arange(L) / FS) / 0.0035) * rng.uniform(0.15, 1.0)
        n = rng.standard_normal(L)
        n = np.fft.irfft(np.fft.rfft(n) * spektrum(L, rng.uniform(1600, 2400), rng.uniform(2800, 3600), 2), n=L)
        hinzu_kreisend(y, n * e, int(s))
    for s in rng.integers(0, N, size=int(grosse_rate * DAUER)):
        L = int(0.09 * FS)
        t = np.arange(L) / FS
        f0 = rng.uniform(780, 1000)
        e = np.exp(-t / 0.028) * rng.uniform(0.5, 1.0)
        ton = np.sin(2 * np.pi * (f0 * t - 200 * t ** 2))  # kurz fallende Tonhöhe, dazu etwas Rauschen
        hinzu_kreisend(y, (0.8 * ton + 0.25 * rng.standard_normal(L)) * e, int(s))
    return norm(y)


def knarzen(rng, anzahl):
    """Knarzen von Balken: gleitender Ton mit Rucken (Haft-Gleit) und dunklem Rauschanteil, kreisend einsortiert."""
    y = np.zeros(N)
    for _ in range(anzahl):
        dauer = rng.uniform(0.7, 1.7)
        L = int(dauer * FS)
        t = np.arange(L) / FS
        f0 = rng.uniform(75, 150)
        phase = 2 * np.pi * np.cumsum(f0 * (1 + rng.uniform(-0.25, 0.35) * t / dauer)) / FS
        ruck = rng.uniform(14, 34)
        rucken = 0.55 + 0.45 * np.sign(np.sin(2 * np.pi * ruck * t + 3 * np.sin(2 * np.pi * 3.1 * t)))
        k = int(0.004 * FS)
        rucken = np.convolve(rucken, np.ones(k) / k, mode="same")
        ton = sum(np.sin(m * phase + rng.uniform(0, 6)) / m ** 1.1 for m in (1, 2, 3, 4, 5, 6))
        rauschen = norm(filtern(rng.standard_normal(L), 90, 700, 2))
        huelle = np.sin(np.pi * np.minimum(t / dauer, 1)) ** 1.4 * np.minimum(t / 0.08, 1)
        hinzu_kreisend(y, (ton * 0.6 + rauschen * 0.5) * rucken * huelle * rng.uniform(0.5, 1.0), int(rng.integers(0, N)))
    return norm(filtern(y, 70, 1200, 2))


def db(x):
    return 10 ** (x / 20.0)


def lautheit(ffmpeg, y):
    """Integrierte Lautheit nach EBU R128 (ffmpeg ebur128) und True Peak."""
    roh = np.clip(y, -1, 1).astype("<f4").tobytes()
    r = subprocess.run([ffmpeg, "-nostats", "-v", "info", "-f", "f32le", "-ar", str(FS), "-ac", "2", "-i", "-",
                        "-af", "ebur128=peak=true", "-f", "null", "-"], input=roh, capture_output=True)
    t = r.stderr.decode(errors="replace")
    return float(re.findall(r"I:\s+(-?[\d.]+) LUFS", t)[-1]), float(re.findall(r"Peak:\s+(-?[\d.]+) dBFS", t)[-1])


def angleichen(ffmpeg, y):
    for _ in range(3):
        y = y * db(ZIEL_LUFS - lautheit(ffmpeg, y)[0])
    return y


def ogg_schreiben(y, ziel, ffmpeg, arbeit):
    wav = Path(arbeit) / (Path(ziel).stem + ".wav")
    with wave.open(str(wav), "wb") as w:
        w.setnchannels(2)
        w.setsampwidth(2)
        w.setframerate(FS)
        w.writeframes((np.clip(y, -1, 1) * 32767).astype("<i2").tobytes())
    subprocess.run([ffmpeg, "-v", "error", "-y", "-i", str(wav), "-map_metadata", "-1", "-fflags", "+bitexact", "-flags:a", "+bitexact",
                    "-c:a", "libvorbis", "-q:a", "4", str(ziel)], check=True)
    wav.unlink()


def zwei_kanaele(bauen, saat):
    """Links und rechts mit eigenem Zufall: ein Raum klingt in beiden Ohren verwandt, aber nicht gleich."""
    return np.stack([bauen(np.random.default_rng(saat)), bauen(np.random.default_rng(saat + 1000))], axis=1)


def regen(quellen, nacht, saat):
    leicht, dach, traufe = quellen

    def kanal(rng):
        bett = filtern(auf_60s(leicht, rng.uniform(0, 50)), 150, 900, 2)
        if nacht:  # etwas heller: 1-4 kHz deutlich mehr als am Tag
            bett = norm(bett) + 0.9 * norm(filtern(auf_60s(leicht, rng.uniform(0, 50)), 1000, 4000, 2))
        d = filtern(auf_60s(dach, rng.uniform(0, 80)), 250, 1500, 2)
        tr = filtern(auf_60s(traufe, rng.uniform(0, 15), 3.0), 400, 2500, 2)
        scheibe = tropfen_scheibe(rng, 4.5 if nacht else 3.5, 0.45 if nacht else 0.35)
        raum = filtern(rosa(rng), None, 180, 2)
        return (norm(bett) + norm(d) * db(-5.0) + norm(tr) * db(-17.0)
                + scheibe * db(-20.0 if nacht else -24.0) + raum * db(-27.0 if nacht else -23.0))
    return zwei_kanaele(kanal, saat)


def sturm(quellen, nacht, saat):
    guss, wind = quellen

    def kanal(rng):
        g = filtern(auf_60s(guss, rng.uniform(0, 36)), 120, 1600, 2)
        w = filtern(auf_60s(wind, rng.uniform(0, 60)), None, 700, 2)
        if nacht:
            g = norm(g) + 0.55 * norm(filtern(auf_60s(guss, rng.uniform(0, 36)), 1000, 4000, 2))
        boen = boenkurve(rng)
        pfeifen = 0
        for mitte, breite, pegel in ((380, 14, -9.0), (560, 20, -8.0), (840, 26, -11.0)):  # Pfeifen in den Ritzen, je eigene Böenkurve
            pfeifen = pfeifen + band_schmal(rng, mitte, breite) * boenkurve(rng, 4, 0.9) ** 2 * db(pegel + (2.0 if nacht else 0.0))
        pfeifen = pfeifen * boen
        luft = filtern(breit(rng), 300, 1500, 2) * boen ** 2 * db(-14.0)  # etwas Luft im Pfeifen, nicht rein tonal
        knarz = knarzen(rng, 7)
        raum = filtern(rosa(rng), None, 160, 2)
        return (norm(g) + norm(w) * boen * db(-3.0) + pfeifen * db(-4.0) + luft
                + knarz * db(-14.0) * (0.5 + 0.5 * boen) + raum * db(-21.0))
    return zwei_kanaele(kanal, saat)


def messen(y):
    """Bandanteile in dB relativ zur Gesamtleistung (Kontrolle der Hörvorgabe: Energie 200 Hz-1 kHz, Nacht heller)."""
    m = y.mean(axis=1)
    s = np.abs(np.fft.rfft(m)) ** 2
    f = np.fft.rfftfreq(len(m), 1.0 / FS)
    return {f"{a}-{b}": 10 * np.log10(s[(f >= a) & (f < b)].sum() / s.sum()) for a, b in ((20, 200), (200, 1000), (1000, 4000), (4000, 12000))}


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--ffmpeg", default="ffmpeg")
    ap.add_argument("--aus", default=str(QUELLEN))
    ap.add_argument("--arbeit", default="/tmp")
    ap.add_argument("--quellen", default=str(QUELLEN))
    a = ap.parse_args()

    def mono(name):
        return ffmpeg_lesen(a.ffmpeg, Path(a.quellen) / f"{name}.ogg").mean(axis=1)
    leicht, stark, dach, traufe, wind = (mono(n) for n in ("regen_leicht", "regen_stark", "regen_dach", "regen_traufe", "wind_mittel"))
    liste = [
        ("innen_regen_tag", lambda: regen((leicht, dach, traufe), False, 101)),
        ("innen_regen_nacht", lambda: regen((leicht, dach, traufe), True, 202)),
        ("innen_sturm_tag", lambda: sturm((stark, wind), False, 303)),
        ("innen_sturm_nacht", lambda: sturm((stark, wind), True, 404)),
    ]
    for name, bauen in liste:
        y = angleichen(a.ffmpeg, bauen())
        i, p = lautheit(a.ffmpeg, y)
        ogg_schreiben(y, Path(a.aus) / f"{name}.ogg", a.ffmpeg, a.arbeit)
        b = messen(y)
        print(f"{name}.ogg: {len(y) / FS:.1f} s, {i:.1f} LUFS, Spitze {p:.1f} dBFS, Bänder dB " + ", ".join(f"{k} Hz {v:.1f}" for k, v in b.items()))


if __name__ == "__main__":
    sys.exit(main())
