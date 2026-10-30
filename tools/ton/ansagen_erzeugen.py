#!/usr/bin/env python3
"""Erzeugt die Aufnahmen der Haushofmeister-Ansagen mit dem Sprachdienst (Qwen3-TTS, Stimmklon) und wählt je Satz die beste aus.

Ablauf je Satz (Liste aus game/daten/ansagen.json, Sprechtext aus tools/ton/ansagen_sprechtext.json):
  1. Takes erzeugen: POST <dienst>/tts mit text, language=German, ref_audio (Klonvorlage), ref_text. Erst --takes Stück (Standard 3).
  2. Jeden Take fertig machen: Nachbearbeitung (mono, 48 kHz, Stille auf 0,2 s, -18 LUFS), Tempo mit ffmpeg atempo (WSOLA,
     Standard 1,15), noch einmal Nachbearbeitung. So entstand die Probe, die der Nutzer gewählt hat.
  3. Bewerten: Whisper (turbo) muss den Text fehlerfrei erkennen (WER 0), keine Übersteuerung im Rohtake, keine Pause über
     --max-pause s mitten im Satz, Sprechtempo plausibel. Unter den gültigen gewinnt die höchste Sprecher-Ähnlichkeit (ECAPA)
     zur Klonvorlage. Ist keiner gültig: bis zu --nachversuche weitere Takes.
  4. Gewinner als WAV nach --wav-ordner und als OGG Vorbis nach game/assets/audio/stimmen/haushofmeister/<ereignis>_<nr>[_herrin].ogg.

Rohtakes, fertige Takes und die Bewertung (bewertung.json, auswahl.json) liegen in --arbeit; ein zweiter Lauf erzeugt nur, was fehlt.

Aufruf (Python mit numpy, scipy, soundfile, pyloudnorm, requests; zum Bewerten faster-whisper, speechbrain, librosa, jiwer, torch):
  python tools/ton/ansagen_erzeugen.py --dienst http://<rechner>:<port> --ffmpeg <pfad>        # alle fehlenden
  python tools/ton/ansagen_erzeugen.py --dienst ... --nur feuer_1,feuer_1_herrin --neu        # einzelne neu
  python tools/ton/ansagen_erzeugen.py --durchhoeren alle.ogg                                  # alle OGGs hintereinander (0,8 s Pause)
Die Adresse des Dienstes steht nie im Code, nur im Aufruf.
"""
import argparse
import json
import os
import queue
import re
import shutil
import subprocess
import sys
import tempfile
import threading
import time
from math import gcd
from pathlib import Path

import numpy as np
import soundfile as sf

sys.path.insert(0, str(Path(__file__).resolve().parent))
import ansagen_liste  # noqa: E402

WURZEL = ansagen_liste.WURZEL
SPRECHTEXT = WURZEL / "tools" / "ton" / "ansagen_sprechtext.json"
REFERENZ = WURZEL / "tools" / "ton" / "quellen" / "haushofmeister_probe2.wav"
ZIEL_SR = 48000

# Wörter, die Whisper anders schreibt, als sie gesprochen gleich klingen („darnieder“ ist die alte Form von
# „danieder“; Whisper schreibt immer die heutige, auch getrennt)
GLEICHWERTIG = {"medikus": "medicus", "danieder": "darnieder"}
ZUSAMMEN = {"da nieder": "danieder"}


# ---------------------------------------------------------------- Nachbearbeitung (wie in der gewählten Probe)

def umtasten(x, s, sr=ZIEL_SR):
    from scipy.signal import resample_poly
    if s != sr:
        g = gcd(s, sr)
        x = resample_poly(x, sr // g, s // g).astype("float32")
    return x


def stille_kuerzen(x, sr, rand=0.2, schwelle_db=-42.0):
    rahmen = int(0.01 * sr)
    n = len(x) // rahmen
    if n == 0:
        return x
    e = np.array([np.sqrt(np.mean(x[i * rahmen:(i + 1) * rahmen] ** 2)) + 1e-9 for i in range(n)])
    db = 20 * np.log10(e / e.max())
    aktiv = np.where(db > schwelle_db)[0]
    if len(aktiv) == 0:
        return x
    a = max(0, aktiv[0] * rahmen - int(rand * sr))
    b = min(len(x), (aktiv[-1] + 1) * rahmen + int(rand * sr))
    y = x[a:b].copy()
    f = int(0.01 * sr)
    y[:f] *= np.linspace(0, 1, f)
    y[-f:] *= np.linspace(1, 0, f)
    return y


def lautheit(x, sr, ziel=-18.0, spitze_db=-1.0):
    import pyloudnorm as pyln
    l = pyln.Meter(sr).integrated_loudness(x)
    y = x * (10 ** ((ziel - l) / 20))
    p = np.abs(y).max()
    grenze = 10 ** (spitze_db / 20)
    if p > grenze:  # weiche Begrenzung nur der Spitzen
        y = np.tanh(y / grenze) * grenze
    return y.astype("float32")


def nachbearbeiten(x, sr):
    x = umtasten(x, sr)
    x = stille_kuerzen(x, ZIEL_SR)
    return lautheit(x, ZIEL_SR)


def mono_laden(pfad):
    x, sr = sf.read(str(pfad), dtype="float32", always_2d=True)
    return x.mean(1), sr


def fertig_machen(roh, ziel, ffmpeg, tempo):
    """Roh-WAV -> Nachbearbeitung -> atempo -> Nachbearbeitung -> WAV 48 kHz mono 16 bit."""
    x, sr = mono_laden(roh)
    y = nachbearbeiten(x, sr)
    with tempfile.TemporaryDirectory() as d:
        a, b = f"{d}/a.wav", f"{d}/b.wav"
        sf.write(a, y, ZIEL_SR, subtype="PCM_16")
        filt = "anull" if abs(tempo - 1.0) < 1e-6 else f"atempo={tempo}"
        subprocess.run([ffmpeg, "-v", "error", "-y", "-i", a, "-af", filt, "-ac", "1", b], check=True)
        x, sr = mono_laden(b)
    y = nachbearbeiten(x, sr)
    sf.write(str(ziel), y, ZIEL_SR, subtype="PCM_16")


def als_ogg(wav, ogg, ffmpeg, qualitaet):
    ogg.parent.mkdir(parents=True, exist_ok=True)
    subprocess.run([ffmpeg, "-v", "error", "-y", "-i", str(wav), "-ac", "1", "-ar", str(ZIEL_SR),
                    "-c:a", "libvorbis", "-q:a", str(qualitaet), "-map_metadata", "-1", str(ogg)], check=True)


# ---------------------------------------------------------------- Texte

def woerter(t):
    t = t.lower().replace("-", " ").replace("–", " ")
    t = " ".join(re.sub(r"[^a-zäöüß ]", " ", t).split())
    for alt, neu in ZUSAMMEN.items():
        t = re.sub(rf"\b{alt}\b", neu, t)
    w = t.split()
    return [GLEICHWERTIG.get(x, x) for x in w]


def silben(t):
    return sum(max(1, len(re.findall(r"[aeiouäöüy]+", w))) for w in woerter(t))


def auftraege_laden():
    daten = ansagen_liste.laden()
    sprech = json.load(open(SPRECHTEXT, encoding="utf-8"))
    liste = []
    for z in ansagen_liste.zeilen(daten):
        name = z["ziel_ogg_name"][:-4]
        schluessel = f"{z['ereignis']}_{z['nr']}"
        s = sprech["saetze"].get(schluessel)
        if s is None:
            sys.exit(f"{SPRECHTEXT.name}: Sprechtext für {schluessel} fehlt")
        if not s.startswith("Mein Herr"):
            sys.exit(f"{SPRECHTEXT.name}: {schluessel} beginnt nicht mit 'Mein Herr'")
        if z["fassung"] == "herrin":
            s = "Meine Herrin" + s[len("Mein Herr"):]
        if woerter(s) != woerter(z["text"]):
            sys.exit(f"{SPRECHTEXT.name}: {name}: Wörter weichen von ansagen.json ab\n  {s}\n  {z['text']}")
        liste.append(dict(name=name, text=z["text"], sprechtext=s, ogg=WURZEL / z["datei"]))
    return liste, sprech["referenztext"]


# ---------------------------------------------------------------- Dienst

def take_holen(dienst, sprechtext, referenz, referenztext, ziel, versuche=3):
    import requests
    for v in range(versuche):
        try:
            with open(referenz, "rb") as f:
                r = requests.post(dienst.rstrip("/") + "/tts", timeout=600,
                                  data={"text": sprechtext, "language": "German", "ref_text": referenztext},
                                  files={"ref_audio": (referenz.name, f, "audio/wav")})
            if r.status_code == 200 and r.content[:4] == b"RIFF":
                tmp = ziel.with_suffix(".tmp")
                tmp.write_bytes(r.content)
                tmp.rename(ziel)
                return True
            print(f"  Dienst: {ziel.name}: HTTP {r.status_code} {r.content[:200]!r}", flush=True)
        except Exception as e:  # Netz weg, Zeitüberschreitung
            print(f"  Dienst: {ziel.name}: {e}", flush=True)
        time.sleep(20 * (v + 1))
    return False


# ---------------------------------------------------------------- Bewertung

class Bewerter:
    def __init__(self, referenz, whisper_modell, threads):
        import torch
        torch.set_num_threads(threads)
        from faster_whisper import WhisperModel
        from speechbrain.inference.speaker import EncoderClassifier
        self.torch = torch
        self.asr = WhisperModel(whisper_modell, device="cpu", compute_type="int8", cpu_threads=threads)
        self.ec = EncoderClassifier.from_hparams("speechbrain/spkrec-ecapa-voxceleb",
                                                 savedir=os.path.expanduser("~/werkzeuge/stimme/modelle/ecapa"),
                                                 run_opts={"device": "cpu"})
        self.ziel = self.einbettung(self.laden16(referenz))

    @staticmethod
    def laden16(pfad):
        import librosa
        return librosa.load(str(pfad), sr=16000)[0]

    def einbettung(self, w16):
        e = self.ec.encode_batch(self.torch.from_numpy(w16)[None]).squeeze().numpy()
        return e / np.linalg.norm(e)

    def vorpruefen(self, roh, fertig, text, max_pause, silben_min, silben_max, max_sek):
        """Schnelle Prüfungen ohne Spracherkennung: Übersteuerung, Pausen, Tempo, Länge, Ähnlichkeit."""
        xr, _ = mono_laden(roh)
        clip = int((np.abs(xr) >= 0.999).sum())
        x, sr = mono_laden(fertig)
        # längste Pause mitten im Satz (10-ms-Rahmen unter -40 dB zur Spitze)
        r = int(0.01 * sr)
        e = np.array([np.sqrt(np.mean(x[i * r:(i + 1) * r] ** 2)) + 1e-9 for i in range(len(x) // r)])
        db = 20 * np.log10(e / e.max())
        aktiv = np.where(db > -40)[0]
        laengste, lauf = 0, 0
        for v in (db[aktiv[0]:aktiv[-1] + 1] if len(aktiv) else []):
            lauf = lauf + 1 if v <= -40 else 0
            laengste = max(laengste, lauf)
        pause = laengste * 0.01
        sprechzeit = (aktiv[-1] - aktiv[0] + 1) * 0.01 if len(aktiv) else 0.0
        sps = float(silben(text) / sprechzeit) if sprechzeit else 0.0
        aehnl = float(self.einbettung(self.laden16(fertig)) @ self.ziel)
        gruende = []
        if clip:
            gruende.append(f"übersteuert ({clip})")
        if pause > max_pause:
            gruende.append(f"Pause {pause:.2f} s")
        if not silben_min <= sps <= silben_max:
            gruende.append(f"Tempo {sps:.2f} Silben/s")
        if len(x) / sr > max_sek:
            gruende.append(f"zu lang {len(x) / sr:.1f} s")
        return dict(wer=None, aehnlichkeit=round(aehnl, 4), clip=clip, pause_s=round(pause, 2),
                    silben_s=round(sps, 2), sek=round(len(x) / sr, 2), erkannt=None, gueltig=False, gruende=gruende)

    def sprache(self, fertig, text, b):
        """Whisper: Der Text muss fehlerfrei erkannt werden (WER 0)."""
        import jiwer
        segs, _ = self.asr.transcribe(str(fertig), language="de", beam_size=5)
        hyp = " ".join(s.text for s in segs).strip()
        b["erkannt"] = hyp
        b["wer"] = round(jiwer.wer(" ".join(woerter(text)), " ".join(woerter(hyp)) or "-"), 3)
        if b["wer"] > 0:
            b["gruende"].append(f"WER {b['wer']:.2f}")
        b["gueltig"] = not b["gruende"]
        return b


# ---------------------------------------------------------------- Durchhören

def durchhoeren(ziel, ffmpeg, pause=0.8):
    liste, _ = auftraege_laden()
    teile = []
    with tempfile.TemporaryDirectory() as d:
        for i, a in enumerate(liste):
            if not a["ogg"].exists():
                print("fehlt:", a["ogg"].name)
                continue
            w = f"{d}/{i}.wav"
            subprocess.run([ffmpeg, "-v", "error", "-y", "-i", str(a["ogg"]), "-ac", "1", "-ar", str(ZIEL_SR), w], check=True)
            x, _ = mono_laden(w)
            teile += [x, np.zeros(int(pause * ZIEL_SR), "float32")]
        if not teile:
            sys.exit("keine Aufnahmen")
        w = f"{d}/alle.wav"
        sf.write(w, np.concatenate(teile[:-1]), ZIEL_SR, subtype="PCM_16")
        subprocess.run([ffmpeg, "-v", "error", "-y", "-i", w, "-c:a", "libvorbis", "-q:a", "5", str(ziel)], check=True)
    print(f"{ziel}: {len(teile) // 2} Aufnahmen, {sum(len(t) for t in teile) / ZIEL_SR:.0f} s")


# ---------------------------------------------------------------- Hauptablauf

def main():
    p = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    p.add_argument("--dienst", help="Adresse des Sprachdienstes, z. B. http://<rechner>:<port>")
    p.add_argument("--referenz", type=Path, default=REFERENZ, help="Klonvorlage (WAV)")
    p.add_argument("--arbeit", type=Path, default=Path("~/werkzeuge/stimme/haushofmeister_arbeit").expanduser())
    p.add_argument("--wav-ordner", type=Path, default=Path("~/werkzeuge/stimme/haushofmeister_wav").expanduser())
    p.add_argument("--ffmpeg", default="ffmpeg")
    p.add_argument("--nur", help="nur diese Aufnahmen (Dateinamen ohne .ogg, mit Komma)")
    p.add_argument("--neu", action="store_true", help="vorhandene Takes und OGGs der Auswahl verwerfen")
    p.add_argument("--takes", type=int, default=3)
    p.add_argument("--nachversuche", type=int, default=3)
    p.add_argument("--tempo", type=float, default=1.15)
    p.add_argument("--qualitaet", type=float, default=5, help="Vorbis-Qualität (-q:a)")
    p.add_argument("--whisper", default="turbo")
    p.add_argument("--threads", type=int, default=4)
    p.add_argument("--max-pause", type=float, default=1.2, help="längste erlaubte Pause mitten im Satz (s, nach dem Tempo)")
    p.add_argument("--silben-min", type=float, default=1.8)
    p.add_argument("--silben-max", type=float, default=6.0)
    p.add_argument("--max-sek", type=float, default=8.0)
    p.add_argument("--durchhoeren", type=Path, help="nur alle OGGs in Listenreihenfolge zu einer Datei zusammensetzen")
    a = p.parse_args()

    if a.durchhoeren:
        durchhoeren(a.durchhoeren, a.ffmpeg)
        return 0
    if not a.dienst:
        p.error("--dienst fehlt")

    liste, referenztext = auftraege_laden()
    if a.nur:
        namen = set(a.nur.split(","))
        liste = [x for x in liste if x["name"] in namen]
        if len(liste) != len(namen):
            sys.exit("unbekannte Namen in --nur")
    roh_d, fertig_d = a.arbeit / "roh", a.arbeit / "fertig"
    for d in (roh_d, fertig_d, a.wav_ordner):
        d.mkdir(parents=True, exist_ok=True)
    bew_datei, ausw_datei = a.arbeit / "bewertung.json", a.arbeit / "auswahl.json"
    bewertung = json.load(open(bew_datei, encoding="utf-8")) if bew_datei.exists() else {}
    auswahl = json.load(open(ausw_datei, encoding="utf-8")) if ausw_datei.exists() else {}
    if a.neu:
        for x in liste:
            for f in list(roh_d.glob(x["name"] + "_t*.wav")) + list(fertig_d.glob(x["name"] + "_t*.wav")):
                f.unlink()
            bewertung = {k: v for k, v in bewertung.items() if not k.startswith(x["name"] + "_t")}
            auswahl.pop(x["name"], None)
            x["ogg"].unlink(missing_ok=True)
    offen = [x for x in liste if not x["ogg"].exists()]
    print(f"{len(liste)} ausgewählt, {len(offen)} zu erzeugen", flush=True)
    if not offen:
        return 0

    bewerter = Bewerter(a.referenz, a.whisper, a.threads)
    # Ein Faden holt Takes vom Dienst, der Hauptfaden bewertet; Nachversuche haben Vorrang.
    auftrag_q, fertig_q = queue.PriorityQueue(), queue.Queue()
    zaehler = [0]

    def holer():
        while True:
            _, _, job = auftrag_q.get()
            if job is None:
                return
            x, k = job
            roh = roh_d / f"{x['name']}_t{k}.wav"
            ok = roh.exists() or take_holen(a.dienst, x["sprechtext"], a.referenz, referenztext, roh)
            fertig_q.put((x, k, ok))

    def einreihen(x, k, vorrang):
        zaehler[0] += 1
        auftrag_q.put((0 if vorrang else 1, zaehler[0], (x, k)))

    faden = threading.Thread(target=holer, daemon=True)
    faden.start()
    stand = {}
    for x in offen:
        stand[x["name"]] = dict(x=x, ausstehend=a.takes, takes=a.takes)
        for k in range(1, a.takes + 1):
            einreihen(x, k, False)
    gescheitert, nachversuch_gelungen = [], []
    fertig_n = 0
    while stand:
        x, k, ok = fertig_q.get()
        s = stand[x["name"]]
        s["ausstehend"] -= 1
        schl = f"{x['name']}_t{k}"
        if ok and schl not in bewertung:
            roh, fertig = roh_d / f"{schl}.wav", fertig_d / f"{schl}.wav"
            try:
                fertig_machen(roh, fertig, a.ffmpeg, a.tempo)
                bewertung[schl] = bewerter.vorpruefen(roh, fertig, x["text"], a.max_pause, a.silben_min, a.silben_max, a.max_sek)
            except Exception as e:
                bewertung[schl] = dict(gueltig=False, gruende=[f"Fehler {e}"], aehnlichkeit=0, wer=None)
        elif not ok:
            bewertung[schl] = dict(gueltig=False, gruende=["Dienst lieferte nichts"], aehnlichkeit=0, wer=None)
        if s["ausstehend"] > 0:
            continue
        # Whisper (teuer) nur in Reihenfolge der Ähnlichkeit, bis ein Take alle Prüfungen besteht: Ergebnis wie
        # „alle prüfen, dann den ähnlichsten gültigen nehmen“, aber meist mit einer statt drei Erkennungen.
        reihe = sorted(range(1, s["takes"] + 1), key=lambda j: -bewertung[f"{x['name']}_t{j}"]["aehnlichkeit"])
        for j in reihe:
            b = bewertung[f"{x['name']}_t{j}"]
            if b["gueltig"]:
                break
            if b["wer"] is None and not b["gruende"]:
                bewerter.sprache(fertig_d / f"{x['name']}_t{j}.wav", x["text"], b)
                print(f"  {x['name']}_t{j}: {'ok' if b['gueltig'] else 'NEIN ' + ', '.join(b['gruende'])} "
                      f"Ähnl. {b['aehnlichkeit']} – {b['erkannt']}", flush=True)
                if b["gueltig"]:
                    break
        for j in reihe:
            b = bewertung[f"{x['name']}_t{j}"]
            if not b["gueltig"] and b["wer"] is None and b["gruende"]:
                print(f"  {x['name']}_t{j}: NEIN {', '.join(b['gruende'])} Ähnl. {b['aehnlichkeit']}", flush=True)
        json.dump(bewertung, open(bew_datei, "w", encoding="utf-8"), ensure_ascii=False, indent=1)
        kandidaten = [(bewertung[f"{x['name']}_t{j}"]["aehnlichkeit"], j) for j in range(1, s["takes"] + 1)
                      if bewertung[f"{x['name']}_t{j}"]["gueltig"]]
        if kandidaten:
            _, j = max(kandidaten)
            quelle = fertig_d / f"{x['name']}_t{j}.wav"
            shutil.copyfile(quelle, a.wav_ordner / f"{x['name']}.wav")
            als_ogg(quelle, x["ogg"], a.ffmpeg, a.qualitaet)
            auswahl[x["name"]] = dict(take=j, takes=s["takes"], **bewertung[f"{x['name']}_t{j}"])
            json.dump(auswahl, open(ausw_datei, "w", encoding="utf-8"), ensure_ascii=False, indent=1)
            if s["takes"] > a.takes:
                nachversuch_gelungen.append(x["name"])
            fertig_n += 1
            print(f"FERTIG {x['name']} (Take {j} von {s['takes']}) [{fertig_n}/{len(offen)}]", flush=True)
            del stand[x["name"]]
        elif s["takes"] < a.takes + a.nachversuche:
            s["takes"] += 1
            s["ausstehend"] = 1
            einreihen(x, s["takes"], True)
        else:
            gescheitert.append(x["name"])
            print(f"GESCHEITERT {x['name']}", flush=True)
            del stand[x["name"]]
    auftrag_q.put((9, 0, None))
    print(f"ENDE: {fertig_n} fertig, davon {len(nachversuch_gelungen)} erst im Nachversuch "
          f"({', '.join(nachversuch_gelungen) or '-'}), gescheitert {len(gescheitert)} ({', '.join(gescheitert) or '-'})", flush=True)
    return 1 if gescheitert else 0


if __name__ == "__main__":
    sys.exit(main())
