#!/usr/bin/env python3
"""Bereitet die Suno-Stuecke fuer das Spiel auf: Stille kuerzen, weich ein-/ausblenden, nur Gain auf die Ziel-Lautheit,
True-Peak-Pruefung am entschluesselten OGG, Ablage unter game/assets/audio/musik/<lage>/.
Aufruf:  python -I tools/ton/musik_bauen.py <Quellordner> <Repo-Wurzel>   (Venv mit soundfile, numpy, scipy, pyloudnorm)
Schreibt ausserdem game/daten/musik.json und docs/assets/prompts/musik-suno.md-unabhaengig die Messwerte nach <Repo>/docs/assets/musik-messwerte.csv.
Kein Limiter, keine Kompression: nur Verstaerkung. Reicht der Spitzenabstand nicht, wird das Ziel dieses Stuecks gesenkt (Spalte ziel_gesenkt)."""
import sys, os, re, json, csv, hashlib
import numpy as np, soundfile as sf, pyloudnorm as pyln
from scipy.signal import resample_poly

ZIEL_LUFS = -17.0
UNTERGRENZE_LUFS = -18.0   # darunter nicht senken ohne Vermerk
TP_GRENZE = -1.0
TP_ZIEL = -1.3             # Vorhalt fuer den Vorbis-Rundlauf
EINBLENDEN_S, AUSBLENDEN_S = 0.8, 4.0
STILLE_DB = -55.0
OGG_QUALITAET = 0.4

UMLAUTE = {'ä': 'ae', 'ö': 'oe', 'ü': 'ue', 'ß': 'ss', 'Ä': 'ae', 'Ö': 'oe', 'Ü': 'ue'}
LAGEN = {'tag', 'nacht', 'tod', 'event'}
JAHRESZEITEN = {'fruehling', 'sommer', 'herbst', 'winter'}
EREIGNISSE = {'aufstand', 'hochzeit', 'kampf', 'sklaven'}
VOELKER = {'elben': 'Elben', 'halblinge': 'Halblinge', 'menschen': 'Menschen', 'zwerge': 'Zwerge'}

def schlicht(text):
    for k, v in UMLAUTE.items(): text = text.replace(k, v)
    text = re.sub(r'[^A-Za-z0-9]+', '_', text).strip('_').lower()
    return text

def zerlegen(stamm):
    teile = stamm.split('_')
    lage = schlicht(teile[0]); rest = teile[1:]
    zusatz = None; volk = None
    if lage == 'tag' and len(rest) == 1 and schlicht(rest[0]) in VOELKER:
        volk = VOELKER[schlicht(rest[0])]; titel = f'Volk der {volk}' if False else rest[0]
        return lage, None, volk, rest[0]
    if rest and schlicht(rest[0]) in (JAHRESZEITEN if lage == 'tag' else EREIGNISSE if lage == 'event' else set()):
        zusatz = schlicht(rest[0]); rest = rest[1:]
    return lage, zusatz, None, '_'.join(rest)

def lesen(pfad):
    with sf.SoundFile(pfad) as f:
        sr = f.samplerate
        teile = [b for b in f.blocks(blocksize=65536, dtype='float32', always_2d=True)]
    return np.concatenate(teile), sr

def true_peak_db(x):
    # 4fach ueberabgetastet (ITU-R BS.1770 Naeherung)
    sp = 0.0
    for k in range(x.shape[1]):
        y = resample_poly(x[:, k], 4, 1)
        sp = max(sp, float(np.max(np.abs(y))))
    return 20 * np.log10(max(sp, 1e-9))

def kuerzen(x, sr):
    b = int(sr * 0.02)
    n = len(x) // b
    rms = np.sqrt(np.mean(x[:n * b].reshape(n, b, -1) ** 2, axis=(1, 2)))
    laut = np.where(20 * np.log10(np.maximum(rms, 1e-9)) > STILLE_DB)[0]
    if len(laut) == 0: return x
    a = max(0, (laut[0] - 2) * b); e = min(len(x), (laut[-1] + 3) * b)
    return x[a:e]

def blenden(x, sr):
    n_ein = min(int(EINBLENDEN_S * sr), len(x) // 4); n_aus = min(int(AUSBLENDEN_S * sr), len(x) // 4)
    x = x.copy()
    x[:n_ein] *= (0.5 - 0.5 * np.cos(np.linspace(0, np.pi, n_ein)))[:, None]
    x[-n_aus:] *= (0.5 + 0.5 * np.cos(np.linspace(0, np.pi, n_aus)))[:, None]
    return x

def kurzzeit_std(x, sr):
    m = pyln.Meter(sr)
    f = int(3 * sr); werte = []
    for a in range(0, len(x) - f, int(sr)):
        l = m.integrated_loudness(x[a:a + f])
        if l > -70: werte.append(l)
    return float(np.std(werte)) if werte else 0.0

def schwerpunkt(x, sr):
    m = x.mean(axis=1)[: sr * 120]
    sp = np.abs(np.fft.rfft(m * np.hanning(len(m))))
    fr = np.fft.rfftfreq(len(m), 1 / sr)
    return float((sp * fr).sum() / sp.sum()), float(sp[fr > 2000].sum() / sp.sum())

def main():
    quelle, repo = sys.argv[1], sys.argv[2]
    zielwurzel = os.path.join(repo, 'game', 'assets', 'audio', 'musik')
    eintraege, messungen = [], []
    for dn in sorted(os.listdir(quelle)):
        if not dn.lower().endswith('.wav'): continue
        stamm = os.path.splitext(dn)[0]
        lage, zusatz, volk, titel = zerlegen(stamm)
        if lage not in LAGEN: raise SystemExit(f'Unbekannte Lage: {dn}')
        name = '_'.join(t for t in [lage, zusatz or (schlicht(volk) if volk else None), schlicht(titel) if not volk else None] if t)
        rel = f'{lage}/{name}.ogg'
        ziel = os.path.join(zielwurzel, rel); os.makedirs(os.path.dirname(ziel), exist_ok=True)
        pfad = os.path.join(quelle, dn)
        sha = hashlib.sha256(open(pfad, 'rb').read()).hexdigest()
        x, sr = lesen(pfad)
        if x.shape[1] == 1: x = np.repeat(x, 2, axis=1)
        roh_dauer = len(x) / sr
        x = blenden(kuerzen(x, sr), sr)
        meter = pyln.Meter(sr)
        lufs0 = meter.integrated_loudness(x)
        tp0 = true_peak_db(x)
        ziel_l = ZIEL_LUFS
        gain = ziel_l - lufs0
        if tp0 + gain > TP_ZIEL:
            ziel_l = lufs0 + (TP_ZIEL - tp0); gain = TP_ZIEL - tp0
        y = None
        for _ in range(6):
            y = np.clip(x * 10 ** (gain / 20), -1.0, 1.0)
            with sf.SoundFile(ziel, 'w', samplerate=sr, channels=2, format='OGG', subtype='VORBIS', compression_level=OGG_QUALITAET) as f:
                for a in range(0, len(y), 32768): f.write(y[a:a + 32768])  # blockweise: ein Schreibaufruf mit ganzem Stueck laeuft in Windows in einen Stapelueberlauf
            d, sr2 = lesen(ziel)
            tp1 = true_peak_db(d)
            if tp1 <= TP_GRENZE: break
            gain -= (tp1 - TP_GRENZE) + 0.1; ziel_l = lufs0 + gain
        d, sr2 = lesen(ziel)
        lufs1 = pyln.Meter(sr2).integrated_loudness(d); tp1 = true_peak_db(d)
        sw, ueber2k = schwerpunkt(d, sr2)
        std = kurzzeit_std(d, sr2)
        gesenkt = lufs1 < ZIEL_LUFS - 0.3
        m = dict(datei=rel, quelle=dn, sha256_quelle=sha, dauer_s=round(len(d) / sr2, 1), roh_dauer_s=round(roh_dauer, 1), lufs=round(lufs1, 1),
                 true_peak_dbtp=round(tp1, 2), groesse_mb=round(os.path.getsize(ziel) / 1e6, 2), gain_db=round(gain, 1),
                 ziel_gesenkt='ja' if gesenkt else 'nein', schwerpunkt_hz=int(sw), anteil_ueber_2khz=round(ueber2k, 3), kurzzeit_std=round(std, 2),
                 abtastrate=sr2)
        messungen.append(m)
        eintraege.append(dict(datei=rel, titel=titel, lage=lage, zusatz=zusatz, volk=volk, dauer=round(len(d) / sr2, 1), lufs=round(lufs1, 1)))
        print(rel, m['dauer_s'], m['lufs'], m['true_peak_dbtp'], m['groesse_mb'], 'gesenkt' if gesenkt else '', flush=True)
    os.makedirs(os.path.join(repo, 'docs', 'assets'), exist_ok=True)
    with open(os.path.join(repo, 'docs', 'assets', 'musik-messwerte.csv'), 'w', newline='', encoding='utf-8') as f:
        w = csv.DictWriter(f, fieldnames=list(messungen[0].keys())); w.writeheader(); w.writerows(messungen)
    json.dump(eintraege, open(os.path.join(repo, 'ent_musik_roh.json'), 'w', encoding='utf-8'), ensure_ascii=False, indent=1)

main()
