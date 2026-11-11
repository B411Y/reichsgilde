#!/usr/bin/env python3
"""Bereitet Suno-Stuecke (WAV) fuer das Spiel auf – Fassung ohne pyloudnorm/soundfile, nur numpy und ffmpeg (Runde 2, 11.11.2026).
Gleiche Regeln wie tools/ton/musik_bauen.py (Runde 1): Stille an Anfang und Ende kuerzen (unter -55 dBFS, 20-ms-Bloecke), 0,8 s ein- und 4 s
ausblenden (Kosinus), Lautheit nur durch Verstaerkung auf -17 LUFS (ebur128, BS.1770), KEIN Begrenzer, keine Kompression; liegt der True-Peak am
entschluesselten OGG ueber -1 dBTP, wird die Verstaerkung dieses Stuecks gesenkt (Spalte ziel_gesenkt). OGG Vorbis q4 (= Qualitaet 0,4 in Runde 1).
Dateinamen: ASCII, Kleinbuchstaben, Umlaute umschrieben, Leerzeichen als Unterstrich; Tippfehler der Quelle stehen in KORREKTUREN.

Aufruf:  python -I tools/ton/musik_runde2_bauen.py <Quellordner> <Repo-Wurzel> [--ffmpeg <Pfad>]
Schreibt: game/assets/audio/musik/<lage>/<name>.ogg, docs/assets/musik-messwerte-runde2.csv und fuehrt die Stuecke in game/daten/musik.json ein
(Eintraege mit gleicher Datei werden ersetzt, "menue" auf das Menuestueck gesetzt). Die Quellordner liegen nur auf dem Rechner des Nutzers.
Ausgenommen (AUSSEN_VOR): event_Daemon (Nutzerentscheidung 11.11.2026)."""
import sys, os, re, json, csv, hashlib, subprocess
import numpy as np

ZIEL_LUFS = -17.0
TP_GRENZE = -1.0
TP_ZIEL = -1.3             # Vorhalt fuer den Vorbis-Rundlauf
EINBLENDEN_S, AUSBLENDEN_S = 0.8, 4.0
STILLE_DB = -55.0
OGG_Q = '4'

UMLAUTE = {'ä': 'ae', 'ö': 'oe', 'ü': 'ue', 'ß': 'ss', 'Ä': 'ae', 'Ö': 'oe', 'Ü': 'ue'}
KORREKTUREN = {'gkueckwunsch': 'glueckwunsch'}          # Tippfehler der Quelle (kleingeschrieben, schlicht)
AUSSEN_VOR = {'event_daemon'}                            # schlichter Dateiname ohne Endung
JAHRESZEITEN = {'fruehling', 'sommer', 'herbst', 'winter'}
EREIGNISSE = {'aufstand', 'hochzeit', 'kampf', 'sklaven', 'dieb', 'geburt', 'gefahr', 'levelup'}
VOELKER = {'mensch': 'Menschen', 'elb': 'Elben', 'zwerg': 'Zwerge', 'halbling': 'Halblinge'}
BERUFE = {'handwerk': 'Handwerk', 'dienstleistung': 'Dienstleistung'}
GLAUBEN = {'katholisch': 'Katholisch', 'evangelisch': 'Evangelisch', 'ahtnisch': 'Ahtnisch'}

def schlicht(text):
    for k, v in UMLAUTE.items(): text = text.replace(k, v)
    return re.sub(r'[^A-Za-z0-9]+', '_', text).strip('_').lower()

def zerlegen(stamm):
    """-> dict(lage, zusatz, volk, beruf, glaube, titel, name) aus <lage>_[<art>_]<Titel>."""
    teile = stamm.split('_')
    lage = schlicht(teile[0]); rest = teile[1:]
    d = dict(lage=lage, zusatz=None, volk=None, beruf=None, glaube=None)
    if lage == 'glauben':
        lage = d['lage'] = 'glaube'
        d['glaube'] = GLAUBEN[schlicht(rest[0])]
        titel = d['glaube'] + (' (V2)' if len(rest) > 1 and schlicht(rest[1]) == 'v2' else '')
        name = 'glaube_' + schlicht(rest[0]) + ('_v2' if titel.endswith('(V2)') else '')
        return {**d, 'titel': titel, 'name': name}
    if lage == 'menu':
        lage = d['lage'] = 'menue'
        return {**d, 'titel': ' '.join(rest), 'name': 'menue_' + schlicht(' '.join(rest))}
    kopf = None
    if lage == 'tag' and rest and schlicht(rest[0]) in VOELKER:
        kopf = schlicht(rest[0]); d['volk'] = VOELKER[kopf]; rest = rest[1:]
    elif lage == 'tag' and rest and schlicht(rest[0]) == 'beruf':
        kopf = 'beruf'; rest = rest[1:]
        d['beruf'] = BERUFE[schlicht(rest[0])]
    elif lage == 'tag' and rest and schlicht(rest[0]) in JAHRESZEITEN:
        kopf = d['zusatz'] = schlicht(rest[0]); rest = rest[1:]
    elif lage == 'event' and rest and schlicht(rest[0]) in EREIGNISSE:
        kopf = d['zusatz'] = schlicht(rest[0]); rest = rest[1:]
    titel_roh = ' '.join(rest)
    ziel = schlicht(titel_roh)
    for falsch, richtig in KORREKTUREN.items():
        if falsch in ziel:
            ziel = ziel.replace(falsch, richtig)
            titel_roh = re.sub('(?i)gkückwunsch', 'Glückwunsch', titel_roh)
    name = '_'.join(t for t in [lage, kopf, ziel] if t)
    titel = titel_roh.replace(' V2', ' (V2)')
    return {**d, 'titel': titel, 'name': name}

# ── ffmpeg ─────────────────────────────────────────────────────────────────

FFMPEG = r'C:\Program Files\Jellyfin\Server\ffmpeg.exe'

def probe_sr(pfad):
    r = subprocess.run([FFMPEG, '-hide_banner', '-i', pfad], capture_output=True, text=True)
    m = re.search(r'(\d+) Hz', r.stderr)
    return int(m.group(1)) if m else 44100

def dekodieren(pfad, sr):
    r = subprocess.run([FFMPEG, '-v', 'error', '-i', pfad, '-f', 'f32le', '-ac', '2', '-ar', str(sr), '-'], capture_output=True, check=True)
    return np.frombuffer(r.stdout, dtype=np.float32).reshape(-1, 2).copy()

def messen(x, sr):
    """ebur128: (integrierte Lautheit LUFS, True-Peak dBTP) fuer ein float32-Feld (n, 2)."""
    r = subprocess.run([FFMPEG, '-hide_banner', '-nostats', '-f', 'f32le', '-ar', str(sr), '-ac', '2', '-i', '-', '-af', 'ebur128=peak=true', '-f', 'null', '-'],
                       input=x.astype('<f4').tobytes(), capture_output=True, check=True)
    t = r.stderr.decode('utf-8', 'replace')
    t = t[t.rfind('Summary:'):]
    i = float(re.search(r'I:\s+(-?[\d.]+) LUFS', t).group(1))
    p = float(re.search(r'Peak:\s+(-?[\d.]+) dBFS', t).group(1))
    return i, p

def kodieren(x, sr, ziel):
    subprocess.run([FFMPEG, '-v', 'error', '-y', '-f', 'f32le', '-ar', str(sr), '-ac', '2', '-i', '-', '-c:a', 'libvorbis', '-q:a', OGG_Q, ziel],
                   input=x.astype('<f4').tobytes(), check=True)

def kuerzen(x, sr):
    b = int(sr * 0.02); n = len(x) // b
    rms = np.sqrt(np.mean(x[:n * b].reshape(n, b, -1) ** 2, axis=(1, 2)))
    laut = np.where(20 * np.log10(np.maximum(rms, 1e-9)) > STILLE_DB)[0]
    if len(laut) == 0: return x
    return x[max(0, (laut[0] - 2) * b): min(len(x), (laut[-1] + 3) * b)]

def blenden(x, sr):
    n_ein = min(int(EINBLENDEN_S * sr), len(x) // 4); n_aus = min(int(AUSBLENDEN_S * sr), len(x) // 4)
    x = x.copy()
    x[:n_ein] *= (0.5 - 0.5 * np.cos(np.linspace(0, np.pi, n_ein)))[:, None]
    x[-n_aus:] *= (0.5 + 0.5 * np.cos(np.linspace(0, np.pi, n_aus)))[:, None]
    return x

def main():
    global FFMPEG
    args = sys.argv[1:]
    if '--ffmpeg' in args:
        i = args.index('--ffmpeg'); FFMPEG = args[i + 1]; del args[i:i + 2]
    quelle, repo = args[0], args[1]
    zielwurzel = os.path.join(repo, 'game', 'assets', 'audio', 'musik')
    eintraege, messungen = [], []
    for dn in sorted(os.listdir(quelle)):
        if not dn.lower().endswith('.wav'): continue
        stamm = os.path.splitext(dn)[0]
        if schlicht(stamm) in AUSSEN_VOR:
            print('uebersprungen (ausser vor):', dn); continue
        z = zerlegen(stamm)
        rel = f"{z['lage']}/{z['name']}.ogg"
        ziel = os.path.join(zielwurzel, rel); os.makedirs(os.path.dirname(ziel), exist_ok=True)
        pfad = os.path.join(quelle, dn)
        sha = hashlib.sha256(open(pfad, 'rb').read()).hexdigest()
        sr = probe_sr(pfad)
        x = dekodieren(pfad, sr)
        roh_dauer = len(x) / sr
        x = blenden(kuerzen(x, sr), sr)
        lufs0, tp0 = messen(x, sr)
        gain = ZIEL_LUFS - lufs0
        if tp0 + gain > TP_ZIEL: gain = TP_ZIEL - tp0
        for _ in range(6):
            y = np.clip(x * 10 ** (gain / 20), -1.0, 1.0)
            kodieren(y, sr, ziel)
            d = dekodieren(ziel, sr)
            lufs1, tp1 = messen(d, sr)
            if tp1 <= TP_GRENZE: break
            gain -= (tp1 - TP_GRENZE) + 0.1
        dauer = round(len(d) / sr, 1)
        gesenkt = lufs1 < ZIEL_LUFS - 0.3
        messungen.append(dict(datei=rel, quelle=dn, sha256_quelle=sha, dauer_s=dauer, roh_dauer_s=round(roh_dauer, 1), lufs=round(lufs1, 1),
                              true_peak_dbtp=round(tp1, 2), groesse_mb=round(os.path.getsize(ziel) / 1e6, 2), gain_db=round(gain, 1),
                              ziel_gesenkt='ja' if gesenkt else 'nein', abtastrate=sr))
        e = dict(datei=rel, titel=z['titel'], lage=z['lage'])
        for k in ('zusatz', 'volk', 'beruf', 'glaube'):
            if z[k]: e[k] = z[k]
        e['dauer'] = dauer; e['lufs'] = round(lufs1, 1)
        eintraege.append(e)
        print(rel, dauer, round(lufs1, 1), round(tp1, 2), messungen[-1]['groesse_mb'], 'gesenkt' if gesenkt else '', flush=True)
    with open(os.path.join(repo, 'docs', 'assets', 'musik-messwerte-runde2.csv'), 'w', newline='', encoding='utf-8') as f:
        w = csv.DictWriter(f, fieldnames=list(messungen[0].keys())); w.writeheader(); w.writerows(messungen)
    # Liste fortschreiben
    lp = os.path.join(repo, 'game', 'daten', 'musik.json')
    daten = json.load(open(lp, encoding='utf-8'))
    neu = {e['datei'] for e in eintraege}
    daten['stuecke'] = [s for s in daten['stuecke'] if s['datei'] not in neu] + eintraege
    menue = [e['datei'] for e in eintraege if e['lage'] == 'menue']
    if menue: daten['menue'] = menue[0]
    json.dump(daten, open(lp, 'w', encoding='utf-8', newline='\n'), ensure_ascii=False, indent=1)
    open(lp, 'a', encoding='utf-8').write('\n')

if __name__ == '__main__':
    main()
