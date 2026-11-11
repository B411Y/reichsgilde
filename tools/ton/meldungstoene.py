#!/usr/bin/env python3
"""Erzeugt die Töne der Benachrichtigungen (Siegel und Pergament) rein rechnerisch – ohne fremde Aufnahmen.

Aufruf:  python3 tools/ton/meldungstoene.py game/assets/audio/geraeusche/oberflaeche

Nur die Python-Standardbibliothek (kein numpy). Jeder Ton ist Mono, 44,1 kHz, 16 Bit; der Zufall ist an einen festen
Startwert je Datei gebunden, ein neuer Lauf liefert also dieselben Bytes.
  siegel_neu_1/2.wav   Siegel erscheint: weicher Wachsklopfer (tiefer Sinus, fallende Tonhöhe) + kurzes Schmatzen + ganz leises Tick
  siegel_feder.wav     Entscheidung kommt: Wachsklopfer wie das Siegel, dazu ein kurzer Federstrich (Rauschen mit steigendem Filter) und ein zweiter, hellerer Ton
  pergament_1/2.wav    Brief kommt / wird gelesen: Rascheln von Pergament (gefiltertes Rauschen mit unregelmäßigen Knacksern)
"""
import math, random, struct, sys, wave

RATE = 44100


def schreiben(pfad, samples):
    spitze = max(1e-9, max(abs(x) for x in samples))
    skala = 0.85 * 32767 / spitze
    with wave.open(pfad, "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(RATE)
        w.writeframes(b"".join(struct.pack("<h", int(max(-32767, min(32767, x * skala)))) for x in samples))


def tiefpass(xs, grenze):
    a = 1 - math.exp(-2 * math.pi * grenze / RATE)
    y, aus = 0.0, []
    for x in xs:
        y += a * (x - y)
        aus.append(y)
    return aus


def hochpass(xs, grenze):
    t = tiefpass(xs, grenze)
    return [x - y for x, y in zip(xs, t)]


def siegel(start, tief):
    r = random.Random(start)
    n = int(RATE * 0.32)
    aus = [0.0] * n
    phase = 0.0
    for i in range(n):
        t = i / RATE
        f = tief * (1 + 1.6 * math.exp(-t * 28))          # Tonhöhe fällt schnell ab: das Wachs sackt zusammen
        phase += 2 * math.pi * f / RATE
        aus[i] += math.sin(phase) * math.exp(-t * 17) * 0.9
    # Schmatzen: kurzes, dunkles Rauschen
    schmatz = tiefpass([r.uniform(-1, 1) for _ in range(n)], 900)
    for i in range(int(RATE * 0.09)):
        t = i / RATE
        aus[i + int(RATE * 0.012)] += schmatz[i] * math.exp(-t * 45) * 1.6
    # ganz leises Tick am Anfang (Berührung)
    tick = hochpass([r.uniform(-1, 1) for _ in range(300)], 2500)
    for i, x in enumerate(tick):
        aus[i] += x * math.exp(-i / 60) * 0.25
    for i in range(80):                                    # Anfang und Ende weich
        aus[i] *= i / 80
    for i in range(int(RATE * 0.05)):
        aus[n - 1 - i] *= i / (RATE * 0.05)
    return aus


def feder(start, tief):
    """Wachsklopfer, dann ein Federstrich über Pergament (kurzes, hohes Kratzen) und ein heller Nachklang: „hier ist etwas zu entscheiden“."""
    r = random.Random(start)
    n = int(RATE * 0.55)
    aus = siegel(start, tief) + [0.0] * (n - int(RATE * 0.32))
    kratz = hochpass(tiefpass([r.uniform(-1, 1) for _ in range(int(RATE * 0.2))], 7000), 2200)
    anfang = int(RATE * 0.11)
    for i, x in enumerate(kratz):
        t = i / RATE
        huell = math.sin(math.pi * min(1.0, t / 0.2)) ** 2 * (0.55 + 0.45 * math.sin(t * 70))
        aus[anfang + i] += x * huell * 0.9
    ton = int(RATE * 0.25)
    for i in range(ton):
        t = i / RATE
        aus[int(RATE * 0.26) + i] += math.sin(2 * math.pi * 392 * t) * math.exp(-t * 14) * 0.22
    return aus


def pergament(start, dauer):
    r = random.Random(start)
    n = int(RATE * dauer)
    rausch = hochpass(tiefpass([r.uniform(-1, 1) for _ in range(n)], 6500), 900)
    huell = [0.0] * n
    # Rascheln = viele kleine Knackser in Gruppen, dazwischen leise Flächen
    t = 0.0
    while t < dauer:
        i = int(t * RATE)
        laenge = int(RATE * r.uniform(0.012, 0.05))
        stark = r.uniform(0.35, 1.0)
        for k in range(laenge):
            if i + k < n:
                huell[i + k] = max(huell[i + k], stark * math.exp(-k / (laenge * 0.4)))
        t += r.uniform(0.012, 0.075) * (1.8 if r.random() < 0.25 else 1.0)
    glatt = tiefpass(huell, 220)
    aus = [x * (g + 0.04) for x, g in zip(rausch, glatt)]
    rand = int(RATE * 0.04)
    for i in range(rand):
        aus[i] *= i / rand
        aus[n - 1 - i] *= i / rand
    return aus


if __name__ == "__main__":
    ziel = sys.argv[1] if len(sys.argv) > 1 else "."
    schreiben(f"{ziel}/siegel_neu_1.wav", siegel(11, 150))
    schreiben(f"{ziel}/siegel_neu_2.wav", siegel(12, 175))
    schreiben(f"{ziel}/siegel_feder.wav", feder(13, 165))
    schreiben(f"{ziel}/pergament_1.wav", pergament(21, 0.55))
    schreiben(f"{ziel}/pergament_2.wav", pergament(22, 0.7))
    print("fertig")
