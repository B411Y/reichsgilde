#!/usr/bin/env python3
"""Baut die Umgebungsklänge (Ton Schritt 4) aus der Schnittliste tools/ton/umgebung_schnitte.json.

Die Quellen sind HQ-Vorschauen (OGG) von CC0-Klängen auf freesound.org. Sie liegen NICHT im Repository: Man legt sie als
<nummer>.ogg in einen Quellordner (z. B. nach einem Download der Vorschau-Adressen aus der Schnittliste); das Skript prüft
ihre SHA-256 gegen die Liste und bricht bei Abweichung ab. Zwei Schichten sind eigene Synthese (Wind), sie brauchen keine Quelle.

Aufruf (Python mit numpy, scipy, soxr, soundfile, pyloudnorm; ffmpeg mit libvorbis im Pfad oder über --ffmpeg):
  python3 -I tools/ton/umgebung_bauen.py --quellen <ordner> [--aus game/assets/audio/umgebung] [--nur stadt_tag,hahn_1] [--wav <ordner>]

Ablauf je Datei (Reihenfolge wichtig, damit die Schleifennaht nicht knackt):
  1. ganze Quelle lesen, auf 48 kHz umrechnen (soxr, sehr hohe Qualität) – die ganze Datei, nicht nur den Ausschnitt;
  2. Filter (Hochpass 2. Ordnung, Tiefpass 4. Ordnung, Butterworth, vorwärts) über die ganze Datei, also mit Vorlauf vor dem Ausschnitt;
  2.0 optional („entrauschen“: dB) stationäres Grundrauschen der ganzen Quelle absenken, vor allen Filtern;
  2.1 optional („tiefton_db“) den Anteil unter 120 Hz um so viele dB anheben (kräftiger Donner);
  2a. Kerbfilter („kerben“: [[Hz, Q], …], z. B. gegen ein stehendes Netzbrummen) und Absenkungen („absenken“: [[von, bis, dB], …]
      in Sekunden der Quelle, Rampen 0,1 s, z. B. für eine herausstehende Stimme);
  3. Ausschnitt von–bis – oder „folge“: [[von, bis], …], mehrere Stücke der Quelle hintereinander, jeweils mit gleicher Leistung
     über „uebergang“ Sekunden ineinander geblendet (eine längere Schleife aus einer kurzen Quelle, ohne festen Takt);
  4. Schichten: Naht mit gleicher Leistung – das Ende (Länge „blende“) wird mit Kosinus aus- und auf den Anfang mit Sinus eingeblendet,
     die Datei ist danach um die Blende kürzer und läuft ohne Sprung von hinten nach vorn. Rufe: mono, Ränder ein- und ausgeblendet;
  3a. Glocken („muster“): aus einem Schlag einer Aufnahme („schlag“: [von, bis]) wird ein Läuten gesetzt – „schlaege“: [[Sekunde, dB, Tonhöhe], …]
     legen den Schlag mit seinem Ausklang an die Stelle (Tonhöhe 1,0 = Original, 1,26 = große Terz höher, eine kleinere Glocke), „dauer“ Gesamtlänge;
  5. eine lineare Verstärkung auf die Ziellautheit (pyloudnorm, ITU-R BS.1770), kein dynamisches loudnorm; danach Spitze (4-fach
     überabgetastet) prüfen und wenn nötig leiser machen („ziel_lufs“ und „spitze“ je Eintrag ändern die Vorgaben der Liste; „begrenzer“: dBFS –
     bei einzelnen lauten Spitzen über dieser Schwelle weich zusammenpressen, damit die Lautheit trotz der Spitzenvorgabe erreicht wird);
  6. OGG Vorbis Qualität 4 mit ffmpeg (bitexact: ein zweiter Lauf erzeugt byte-gleiche Dateien).
"""
import argparse
import hashlib
import json
import os
import subprocess
import sys
import tempfile
from pathlib import Path

import numpy as np
import pyloudnorm as pyln
import soundfile as sf
import soxr
from scipy.signal import butter, iirnotch, resample_poly, sosfilt, tf2sos

WURZEL = Path(__file__).resolve().parents[2]
LISTE = WURZEL / "tools" / "ton" / "umgebung_schnitte.json"
FS = 48000

# ── Wind-Synthese (eigenes Verfahren, aus der Recherche Ton Schritt 4) ─────────────────────────────────────────────
# Gefiltertes rosa Rauschen in einer zyklischen STFT, gesteuert von einer periodischen Böenkurve. Alles ist mit der Dateilänge
# periodisch erzeugt: Ende und Anfang passen exakt aneinander, eine Blende ist nicht nötig.
WIND_STUFEN = {
    # f_glatt: Böen oberhalb dieser Frequenz gedämpft; ti: Turbulenzgrad; fc: Eckfrequenz der Böen (Hz); boeen: einzelne Böen je Minute;
    # tp0: Tiefpass bei v=1 (Hz); heul_*: Heulresonanz; zisch_db: Zischen über 3 kHz; koerper_db: Körper 150–300 Hz
    # leicht_boeig: unregelmäßige Böen alle 10–20 s (statt einer 45-s-Welle), heller als die erste Fassung (tp0 1200 Hz)
    "leicht_boeig": dict(f_glatt=0.35, v_min=0.65, ti=0.16, fc=0.07, boeen=4.5, boe_hoehe=0.28, tp0=1200.0, tp_exp=1.2, heul0=430.0,
                         heul_db=-14.0, heul_schwelle=1.25, zisch_db=-19.0, koerper_db=-4.0, pegel_exp=1.3),
    # nacht: sehr ferner, dunkler Wind ohne Heulen und fast ohne Zischen – ein stiller Grund, den Charakter geben die Einzelrufe
    "nacht": dict(f_glatt=0.25, v_min=0.75, ti=0.08, fc=0.05, boeen=2.0, boe_hoehe=0.12, tp0=600.0, tp_exp=1.0, heul0=400.0,
                  heul_db=-40.0, heul_schwelle=9.0, zisch_db=-34.0, koerper_db=-3.0, pegel_exp=1.0),
}


def _periodisch(n, form, rng):
    k = np.arange(n // 2 + 1)
    phase = rng.uniform(0, 2 * np.pi, k.size)
    betrag = form(k) * rng.rayleigh(1.0, k.size)
    betrag[0] = 0.0
    x = np.fft.irfft(betrag * np.exp(1j * phase), n=n)
    return x / np.std(x)


def _steuerung(rahmen, rahmen_dauer, p, rng):
    dauer = rahmen * rahmen_dauer
    df = 1.0 / dauer

    def form(k):
        f = k * df
        s = 1.0 / (1.0 + (f / p["fc"]) ** 2) ** (5.0 / 6.0) * np.exp(-(f / p["f_glatt"]) ** 2)
        s[f > 1.0] = 0.0
        return np.sqrt(s)

    v = 1.0 + p["ti"] * _periodisch(rahmen, form, rng)
    t = np.arange(rahmen) * rahmen_dauer
    for _ in range(int(round(p["boeen"] * dauer / 60.0))):
        mitte, breite = rng.uniform(0, dauer), rng.uniform(3.0, 8.0)
        hoehe = p["boe_hoehe"] * rng.uniform(0.5, 1.0)
        d = (t - mitte + dauer / 2) % dauer - dauer / 2
        v += hoehe * np.where(np.abs(d) < breite / 2, 0.5 * (1 + np.cos(2 * np.pi * d / breite)), 0.0)
    vmin = p["v_min"]
    v = vmin + np.log1p(np.exp((v - vmin) * 6.0)) / 6.0
    return v / np.mean(v)


def _huelle(f, v, p, heul_faktor, zisch_mod):
    hp = (f / 90.0) ** 2 / np.sqrt(1 + (f / 90.0) ** 4) * (f / 60.0) ** 2 / np.sqrt(1 + (f / 60.0) ** 4)
    tp = 1.0 / np.sqrt(1 + (f / (p["tp0"] * v ** p["tp_exp"])) ** 4)
    koerper_f = 170.0 + 90.0 * v
    h = hp * tp * (1.0 + 10 ** (p["koerper_db"] / 20) * np.exp(-0.5 * (np.log2(np.maximum(f, 1) / koerper_f) / 0.7) ** 2))
    staerke = np.clip((v - p["heul_schwelle"]) / 0.5, 0.0, 1.0) ** 1.5
    if staerke > 0:
        fh = p["heul0"] * v * heul_faktor
        for verh, rel in ((1.0, 1.0), (1.62, 0.55)):
            fr = fh * verh
            h = h + hp * staerke * 10 ** (p["heul_db"] / 20) * rel * 3.0 * np.exp(-0.5 * ((f - fr) / (fr / 14.0)) ** 2)
    zisch = 10 ** (p["zisch_db"] / 20) * zisch_mod * v ** 1.5 / np.sqrt(1 + (3000.0 / np.maximum(f, 1)) ** 4)
    h = h + zisch / np.sqrt(1 + (f / 12000.0) ** 4) * hp
    return h * v ** p["pegel_exp"]


def _windkanal(n, v, p, heul_faktor, rng):
    hop, nfft = 960, 3840
    rahmen = n // hop
    rosa = _periodisch(n, lambda k: 1.0 / np.sqrt(np.maximum(k, 1)), rng)
    fenster = np.sqrt(0.5 - 0.5 * np.cos(2 * np.pi * np.arange(nfft) / nfft))
    idx = (np.arange(rahmen)[:, None] * hop + np.arange(nfft)[None, :]) % n
    f = np.fft.rfftfreq(nfft, 1.0 / FS)
    dauer = n / FS
    flatter = _periodisch(rahmen, lambda k: ((k / dauer >= 0.5) & (k / dauer <= 4.0)).astype(float), rng)
    zisch_mod = np.clip(1.0 + 0.45 * flatter, 0.2, 2.0)
    y = np.zeros((rahmen, hop))
    for a in range(0, rahmen, 600):
        b = min(rahmen, a + 600)
        spek = np.fft.rfft(rosa[idx[a:b]] * fenster, axis=1)
        H = np.stack([_huelle(f, v[k], p, heul_faktor, zisch_mod[k]) for k in range(a, b)])
        aus = np.fft.irfft(spek * H, n=nfft, axis=1) * fenster
        for j in range(4):
            np.add.at(y, (np.arange(a, b) + j) % rahmen, aus[:, j * hop:(j + 1) * hop])
    return y.reshape(-1) / 2.0


def wind(stufe, saat, dauer):
    p = WIND_STUFEN[stufe]
    rng = np.random.default_rng(saat)
    n = int(round(dauer * FS))
    n -= n % 960
    v = _steuerung(n // 960, 960 / FS, p, rng)
    links = _windkanal(n, v, p, 1.0, rng)
    rechts = _windkanal(n, np.roll(v, int(round(0.6 * FS / 960))), p, 1.03, rng)
    return np.stack([links + 0.25 * rechts, rechts + 0.25 * links], axis=1)


# ── Schnitt aus einer Quelle ──────────────────────────────────────────────────────────────────────────────────────

def quelle_lesen(pfad, sha_soll):
    daten = Path(pfad).read_bytes()
    sha = hashlib.sha256(daten).hexdigest()
    if sha != sha_soll:
        sys.exit(f"FEHLER: {pfad} hat SHA-256 {sha}, erwartet {sha_soll} (andere Datei als in der Schnittliste)")
    x, fs = sf.read(pfad, always_2d=True, dtype="float64")
    if x.shape[1] == 1:
        x = np.repeat(x, 2, axis=1)
    if fs != FS:
        x = soxr.resample(x, fs, FS, quality="VHQ")
    return x


def entrauschen(x, db, n=1024, hop=256):
    """Stationäres Grundrauschen der ganzen Quelle um höchstens db dB absenken (spektrale Subtraktion): Das Rauschen je Frequenzbereich ist das 20. Perzentil
    des Betrags über die Zeit, abgezogen wird das Doppelte davon; Boden der Verstärkung 10^(-db/20), in Zeit und Frequenz geglättet (wenig „Zwitschern“)."""
    from scipy.signal import stft, istft
    from scipy.ndimage import uniform_filter
    boden = 10 ** (-db / 20)
    y = np.zeros_like(x)
    for k in range(x.shape[1]):
        f, t, Z = stft(x[:, k], fs=FS, nperseg=n, noverlap=n - hop)
        betrag = np.abs(Z)
        rausch = np.percentile(betrag, 20, axis=1, keepdims=True)
        g = np.clip(1.0 - 2.0 * rausch / (betrag + 1e-12), boden, 1.0)
        g = uniform_filter(g, size=(3, 5))
        _, rein = istft(Z * g, fs=FS, nperseg=n, noverlap=n - hop)
        y[:, k] = rein[:len(x)] if len(rein) >= len(x) else np.pad(rein, (0, len(x) - len(rein)))
    return y


def filtern(x, eintrag):
    if eintrag.get("entrauschen"):
        x = entrauschen(x, eintrag["entrauschen"])
    if eintrag.get("hp"):
        x = sosfilt(butter(2, eintrag["hp"], "highpass", fs=FS, output="sos"), x, axis=0)
    if eintrag.get("tp"):
        x = sosfilt(butter(4, eintrag["tp"], "lowpass", fs=FS, output="sos"), x, axis=0)
    if eintrag.get("tiefton_db"):
        # kräftiger Tiefbass: den Anteil unter 120 Hz um so viele dB anheben (Donner)
        tief = sosfilt(butter(2, 120, "lowpass", fs=FS, output="sos"), x, axis=0)
        x = x + (10 ** (eintrag["tiefton_db"] / 20) - 1.0) * tief
    for hz, q in eintrag.get("kerben", []):
        x = sosfilt(tf2sos(*iirnotch(hz, q, fs=FS)), x, axis=0)
    if eintrag.get("absenken"):
        g = np.ones(len(x))
        t = np.arange(len(x)) / FS
        for von, bis, db in eintrag["absenken"]:
            # Trapez mit 0,1-s-Rampen: 0 außerhalb, 1 innerhalb; Verstärkung in dB darüber
            k = np.clip(np.minimum(t - (von - 0.1), (bis + 0.1) - t) / 0.1, 0, 1)
            g *= 10 ** (db * k / 20)
        x = x * g[:, None]
    return x


def folge(x, stuecke, uebergang):
    """Stücke der Quelle hintereinander, je über „uebergang“ Sekunden mit gleicher Leistung ineinander geblendet."""
    f = int(round(uebergang * FS))
    t = (np.arange(f) + 0.5) / f
    ein, aus = np.sin(t * np.pi / 2)[:, None], np.cos(t * np.pi / 2)[:, None]
    y = x[int(round(stuecke[0][0] * FS)):int(round(stuecke[0][1] * FS))]
    for von, bis in stuecke[1:]:
        z = x[int(round(von * FS)):int(round(bis * FS))]
        y = np.concatenate([y[:-f], y[-f:] * aus + z[:f] * ein, z[f:]])
    return y


def naht(y, blende):
    """Schleifennaht mit gleicher Leistung: das Ende über den Anfang blenden; Ergebnis um die Blende kürzer."""
    f = int(round(blende * FS))
    m = len(y) - f
    t = (np.arange(f) + 0.5) / f
    ein, aus = np.sin(t * np.pi / 2)[:, None], np.cos(t * np.pi / 2)[:, None]
    z = y[:m].copy()
    z[:f] = y[:f] * ein + y[m:] * aus
    return z


def raender(y, ein, aus):
    a, b = int(ein * FS), int(aus * FS)
    y = y.copy()
    y[:a] *= (np.sin(np.linspace(0, np.pi / 2, a)) ** 2)[:, None]
    y[len(y) - b:] *= (np.cos(np.linspace(0, np.pi / 2, b)) ** 2)[:, None]
    return y


def echte_spitze_db(y):
    ueber = resample_poly(y, 4, 1, axis=0)
    return 20 * np.log10(np.max(np.abs(ueber)) + 1e-12)


def weich_begrenzen(y, schwelle_db):
    """Spitzen über der Schwelle (dBFS) weich zusammenpressen (tanh bis 0 dBFS); darunter bleibt alles unverändert."""
    t = 10 ** (schwelle_db / 20)
    a = np.abs(y)
    ueber = a > t
    z = y.copy()
    z[ueber] = np.sign(y[ueber]) * (t + (1 - t) * np.tanh((a[ueber] - t) / (1 - t)))
    return z


def angleichen(y, ziel_lufs, spitze_max, begrenzer=None):
    meter = pyln.Meter(FS)
    lufs = meter.integrated_loudness(y)
    y = y * 10 ** ((ziel_lufs - lufs) / 20)
    if begrenzer is not None:
        # Aufnahmen mit einzelnen lauten Spitzen (Tropfen auf Laub): erst weich begrenzen, dann die Lautheit nachregeln, bis sie stimmt
        g = 1.0
        for _ in range(8):
            z = weich_begrenzen(y * g, begrenzer)
            z = z * 10 ** (min(0.0, spitze_max - echte_spitze_db(z)) / 20)
            g *= 10 ** ((ziel_lufs - meter.integrated_loudness(z)) / 20)
        y = weich_begrenzen(y * g, begrenzer)
    spitze = echte_spitze_db(y)
    if spitze > spitze_max:
        y = y * 10 ** ((spitze_max - spitze) / 20)
    return y, meter.integrated_loudness(y), echte_spitze_db(y)


def ogg_schreiben(y, ziel, ffmpeg):
    with tempfile.TemporaryDirectory() as tmp:
        wav = Path(tmp) / "x.wav"
        sf.write(wav, y.astype(np.float32), FS, subtype="FLOAT")
        befehl = [ffmpeg, "-v", "error", "-y", "-i", str(wav), "-map_metadata", "-1", "-fflags", "+bitexact", "-flags:a", "+bitexact",
                  "-c:a", "libvorbis", "-q:a", "4", str(ziel)]
        subprocess.run(befehl, check=True)


def laeuten(x, mu):
    """Ein Läuten aus Schlägen einer Glocke: Schlag samt Ausklang ausschneiden, an den Zeiten mit Pegel und Tonhöhe übereinanderlegen (mono)."""
    m = x.mean(axis=1)
    von, bis = mu["schlag"]
    schlag = m[int(round(von * FS)):int(round(bis * FS))].copy()
    a = int(0.004 * FS)
    schlag[:a] *= np.linspace(0, 1, a)                               # der Anschlag der Aufnahme beginnt vor dem Schnitt: sanft einblenden
    b = int(0.8 * FS)
    schlag[-b:] *= np.cos(np.linspace(0, np.pi / 2, b)) ** 2         # und den Ausklang am Ende ausblenden
    n = int(round(mu["dauer"] * FS))
    y = np.zeros(n + len(schlag) + FS)
    for zeit, db, hoehe in mu["schlaege"]:
        z = schlag if abs(hoehe - 1.0) < 1e-9 else soxr.resample(schlag, FS * hoehe, FS, quality="VHQ")  # schneller abgespielt = höher
        i = int(round(zeit * FS))
        y[i:i + len(z)] += z * 10 ** (db / 20)
    y = y[:n]
    f = int(min(mu.get("ausblenden", 2.0), mu["dauer"] / 2) * FS)
    y[-f:] *= np.cos(np.linspace(0, np.pi / 2, f)) ** 2
    return y[:, None]


def bauen(eintrag, liste, quellordner):
    if "synth" in eintrag:
        y = wind(eintrag["synth"], eintrag["saat"], eintrag["dauer"])
    elif "muster" in eintrag:
        q = liste["quellen"][eintrag["quelle"]]
        x = filtern(quelle_lesen(Path(quellordner) / f"{eintrag['quelle']}.ogg", q["sha256"]), eintrag)
        y = laeuten(x, eintrag["muster"])
    else:
        q = liste["quellen"][eintrag["quelle"]]
        x = filtern(quelle_lesen(Path(quellordner) / f"{eintrag['quelle']}.ogg", q["sha256"]), eintrag)
        if "folge" in eintrag:
            y = folge(x, eintrag["folge"], eintrag["uebergang"])
        else:
            y = x[int(round(eintrag["von"] * FS)):int(round(eintrag["bis"] * FS))]
        if eintrag["art"] == "schicht":
            y = naht(y, eintrag["blende"])
        else:
            y = raender(y.mean(axis=1, keepdims=True), eintrag.get("ein", 0.02), eintrag.get("aus", 0.15))
    if eintrag["art"] == "schicht":
        return angleichen(y, eintrag.get("ziel_lufs", liste["ziel_lufs_schicht"]), eintrag.get("spitze", liste["spitze_schicht_dbtp"]), eintrag.get("begrenzer"))
    return angleichen(y, eintrag.get("ziel_lufs", liste["ziel_lufs_ruf"]), eintrag.get("spitze", liste["spitze_ruf_dbtp"]), eintrag.get("begrenzer"))


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--quellen", required=True, help="Ordner mit den Vorschauen als <nummer>.ogg")
    ap.add_argument("--aus", default=str(WURZEL / "game" / "assets" / "audio" / "umgebung"))
    ap.add_argument("--nur", default="", help="nur diese Dateien (Namen mit Komma)")
    ap.add_argument("--wav", default="", help="zusätzlich unkomprimierte WAVs in diesen Ordner (zum Prüfen)")
    ap.add_argument("--ffmpeg", default=os.environ.get("FFMPEG", "ffmpeg"))
    a = ap.parse_args()
    liste = json.loads(LISTE.read_text(encoding="utf-8"))
    nur = {n for n in a.nur.split(",") if n}
    for eintrag in liste["dateien"]:
        if nur and eintrag["name"] not in nur:
            continue
        unter = eintrag.get("ordner", "schichten" if eintrag["art"] == "schicht" else "rufe")   # „ordner“: z. B. glocken oder rufe/gespraeche/gasse
        ziel = Path(a.aus) / unter / f"{eintrag['name']}.ogg"
        ziel.parent.mkdir(parents=True, exist_ok=True)
        y, lufs, spitze = bauen(eintrag, liste, a.quellen)
        ogg_schreiben(y, ziel, a.ffmpeg)
        if a.wav:
            Path(a.wav).mkdir(parents=True, exist_ok=True)
            sf.write(Path(a.wav) / f"{eintrag['name']}.wav", y.astype(np.float32), FS, subtype="FLOAT")
        print(f"{unter}/{eintrag['name']}.ogg: {len(y) / FS:6.2f} s, {y.shape[1]} Kanal/Kanäle, {lufs:6.1f} LUFS, Spitze {spitze:5.1f} dBTP")


if __name__ == "__main__":
    main()
