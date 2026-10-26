# Kind aus zwei Eltern-DNAs mischen: je Regler zufällig Vater, Mutter oder Mittelwert.
# python3 dna_mischen.py vater.json mutter.json kind.json --seed=7 --geschlecht=1.0 --alter=0.5
import json, random, sys

args = [a for a in sys.argv[1:] if not a.startswith("--")]
opt = dict(a[2:].split("=", 1) for a in sys.argv[1:] if a.startswith("--"))
vater, mutter = (json.load(open(p)) for p in args[:2])
rng = random.Random(int(opt.get("seed", 1)))
herkunft = {}


def erben(name, a, b):
    wahl = rng.choice(("vater", "mutter", "mittel"))
    herkunft[name] = wahl
    return a if wahl == "vater" else b if wahl == "mutter" else round((a + b) / 2, 3)


kind = {"name": "Kind", "makro": {}, "regler": {}}
for k in sorted(set(vater["makro"]) | set(mutter["makro"])):
    kind["makro"][k] = erben("makro:" + k, vater["makro"].get(k, 0.5), mutter["makro"].get(k, 0.5))
kind["makro"]["gender"] = float(opt.get("geschlecht", 1.0))
kind["makro"]["age"] = float(opt.get("alter", 0.5))
for k in sorted(set(vater["regler"]) | set(mutter["regler"])):
    kind["regler"][k] = erben(k, vater["regler"].get(k, 0.0), mutter["regler"].get(k, 0.0))
# Haar- und Augenfarbe je ganz von einem Elternteil (wie bei den 2D-Porträts)
kind["holzfarben"] = {}
for paket, schluessel, holz in (("haarfarbe", ("haarfarbe",), ("haar", "brauen")),
                                ("augenfarbe", ("augenfarbe", "augen_textur"), ())):
    quelle = rng.choice((vater, mutter))
    herkunft[paket] = "vater" if quelle is vater else "mutter"
    for k in schluessel:
        if k in quelle:
            kind[k] = quelle[k]
    for k in holz:
        if k in quelle.get("holzfarben", {}):
            kind["holzfarben"][k] = quelle["holzfarben"][k]
# Aussehen, das nicht vererbt wird (Kleidung, Frisur, Bart), vom gleichgeschlechtlichen Elternteil übernehmen
vorbild = vater if kind["makro"]["gender"] >= 0.5 else mutter
for k in ("haut", "haar", "brauen", "bart", "haareditor", "haareditor_werte", "kleidung", "kleiderfarben",
          "kopfhaut", "wimpern", "augen_netz", "kamera"):
    if k in vorbild:
        kind[k] = vorbild[k]
if "kleid" in vorbild.get("holzfarben", {}):
    kind["holzfarben"]["kleid"] = vorbild["holzfarben"]["kleid"]
kind["herkunft"] = herkunft
json.dump(kind, open(args[2], "w"), indent=1, ensure_ascii=False)
print(json.dumps(herkunft, ensure_ascii=False))
