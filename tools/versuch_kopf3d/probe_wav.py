# Probe-Audiodatei: synthetische "Sprechsilben" (Glottis-Impulse durch Vokalfilter), 22,05 kHz mono 16 Bit.
# Kein echtes Sprechen, nur eine Lautstärke-Hüllkurve mit Vokalfärbung zum Testen der Lippensynchronisation. Selbst erzeugt, CC0.
import wave, struct, math, random, sys
rate = 22050
random.seed(4)
vokale = {"a": (800, 1200), "e": (450, 1900), "i": (300, 2300), "o": (500, 900), "u": (320, 800)}
silben = "ha lo wer ter her r ge su ne dig ha fen ge hen mit uns"
samples = []
t0 = 0
f0 = 118.0
def silbe(v, dauer, pitch):
    f1, f2 = vokale[v]
    n = int(rate * dauer)
    y1 = y2 = z1 = z2 = 0.0
    out = []
    ph = 0.0
    for i in range(n):
        x = i / n
        env = math.sin(math.pi * x) ** 0.7
        ph += pitch * (1 + 0.05 * math.sin(i / rate * 30)) / rate
        imp = 1.0 if ph % 1.0 < 0.04 else 0.0
        imp -= 0.04
        # zwei Resonanzfilter (Formanten)
        for (fc, bw, k) in ((f1, 90, 0), (f2, 140, 1)):
            r = math.exp(-math.pi * bw / rate); c = 2 * r * math.cos(2 * math.pi * fc / rate)
            if k == 0:
                yn = imp + c * y1 - r * r * y2; y2, y1 = y1, yn; res1 = yn
            else:
                zn = imp + c * z1 - r * r * z2; z2, z1 = z1, zn; res2 = zn
        out.append((res1 + 0.6 * res2) * env * 0.05)
    return out
folge = "a o e e e a o e u e i a e e i u"
pitch = f0
for i, v in enumerate(folge.split()):
    d = random.uniform(0.11, 0.2)
    pitch = f0 * (1 + 0.12 * math.sin(i * 0.9))
    samples += silbe(v, d, pitch)
    samples += [0.0] * int(rate * (random.uniform(0.02, 0.07) if i % 5 != 4 else 0.22))
m = max(abs(s) for s in samples) or 1
with wave.open(sys.argv[1], "w") as w:
    w.setnchannels(1); w.setsampwidth(2); w.setframerate(rate)
    w.writeframes(b"".join(struct.pack("<h", int(s / m * 0.8 * 32767)) for s in samples))
print("WAV", len(samples) / rate, "s")
