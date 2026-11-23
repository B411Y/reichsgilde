"""Abzeichen-Maske des Zugpferds (Eigenbau, numpy und Pillow): eine Textur im UV des Fells, aus der der Shader je Pferd weisse Abzeichen waehlt.

    python -I tools/modelle/pferd2_abzeichen.py <paket.npz> <Stehen_J.npz> <ausgabeordner> [--vorschau=<Horse_braun_2048.png>]

Zwei Texturen (1024 x 1024, UV wie das Fell):
  pferd_abzeichen_kopf.png (RGBA, weich: Wert = Deckung 0..1): R Stern, G Blesse, B Schnippe, A Maulpartie (heller oder dunkler als das Fell)
  pferd_abzeichen_beine.png (Graustufe, ohne Filter lesen): (Beinnummer + Hoehe) / 4, Beinnummer 0 LF, 1 RF, 2 LH, 3 RH, Hoehe = z / 0,5 m; ueber 0,5 m oder kein Bein: 0
Der Shader setzt Weiss, wo (Kopfwahl * Kanal) bzw. (Hoehe < Grenze des Beins) gilt; die Raender sind mit Rauschen unregelmaessig (fester Zufall).
Lage aus der Stehpose (wie pferd2_fell.py): Kopf-Profil von der Stirn (y -1,18, z 1,88) zur Nasenspitze (y -1,45, z 1,45), Mittellinie bei x = -0,048.
"""
import os
import sys

import numpy as np
from PIL import Image

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import pferd2_b as B  # noqa: E402
import pferd2_fell as FE  # noqa: E402

ARGS = sys.argv[1:]
POS = [a for a in ARGS if not a.startswith("--")]
OPT = dict(a[2:].split("=", 1) if "=" in a else (a[2:], "1") for a in ARGS if a.startswith("--"))
HUFE = {"LF": (0.057, -0.492), "RF": (-0.129, -0.507), "LH": (0.082, 0.675), "RH": (-0.182, 0.670)}   # Hufmitten in der Stehpose (x, y)


def glatt(t):
    t = np.clip(t, 0, 1)
    return t * t * (3 - 2 * t)


def masken(lage, nrm, maske):
    rng = np.random.default_rng(int(OPT.get("saat", 1480)))
    N = lage.shape[0]
    from PIL import ImageFilter
    rausch = np.asarray(Image.fromarray((rng.random((N, N)) * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(6))).astype(np.float32)
    rausch = (rausch - rausch.mean()) / max(rausch.std(), 1e-6)
    x, y, z = lage[..., 0], lage[..., 1], lage[..., 2]
    xm = float(OPT.get("mitte_x", -0.048))
    p0, p1 = np.array([-1.18, 1.88]), np.array([-1.45, 1.45])
    a = p1 - p0
    t = ((y - p0[0]) * a[0] + (z - p0[1]) * a[1]) / (a @ a)                # Lage entlang des Profils (0 Stirn, 1 Nase)
    vorn = np.array([0.0, a[1], -a[0]]) / np.linalg.norm(a)             # senkrecht zum Profil nach vorn-oben (y nach vorn negativ)
    vorn = np.array([0.0, -abs(vorn[1]), abs(vorn[2])])
    blick = (nrm * vorn).sum(-1) / np.maximum(np.linalg.norm(nrm, axis=-1), 1e-9)
    kopf = maske & (y < -1.0) & (blick > 0.25)
    dx = np.abs(x - xm) + 0.006 * rausch
    stern = kopf * glatt((0.040 - np.hypot(dx, (t - 0.22) * 0.30)) / 0.008)
    vorn_k = maske & (y < -1.0) & (blick > 0.05)                         # Blesse: auch auf den schraegen Flaechen des Nasenrueckens (sonst unterbrochen)
    blesse = vorn_k * glatt((0.030 + 0.012 * t - dx) / 0.007) * glatt((t - 0.12) / 0.04) * glatt((0.88 - t) / 0.04)
    schnippe = kopf * glatt((0.030 - np.hypot(dx, (t - 0.93) * 0.25)) / 0.008)
    maul = maske & (y < -1.2) & (z < 1.75)                                # Maulpartie: rund um die Schnauze (Lippen, Nuestern, Kinn), weicher Uebergang
    maul = maul * glatt((t - 0.84) / 0.07) * np.clip(0.85 + 0.15 * rausch / 3, 0, 1)
    bein = np.zeros_like(z)
    for k, (nm, (hx, hy)) in enumerate(HUFE.items()):
        nah = maske & (np.hypot(x - hx, y - hy) < 0.16) & (z < 0.5)
        h = np.clip(z / 0.5 + 0.02 * rausch, 0, 0.99)
        bein = np.where(nah, (k + h) / 4.0, bein)
    return stern, blesse, schnippe, maul, bein


def main():
    paket, stehen, aus = POS
    os.makedirs(aus, exist_ok=True)
    d = np.load(paket, allow_pickle=True)
    i = next(k for k in range(int(d["n"])) if str(d[f"mat{k}"]) == "Horse")
    p = dict(pos=d[f"pos{i}"].astype(float), jo=d[f"jo{i}"].astype(int), w=d[f"w{i}"].astype(float))
    F = d[f"idx{i}"].astype(int)
    UV = d[f"uv{i}"].astype(float)
    H = B.Haut()
    J = np.load(stehen)["J"][0].astype(float)
    w, _ = H.haut(p, J)
    Q = B.anwenden(B.lade_norm(FE.NORM), B.blender_koord(w))
    lage, nrm, du, dv, maske = FE.rastern(Q, B.weiche_normalen(Q, F), UV, F)
    stern, blesse, schnippe, maul, bein = masken(lage, nrm, maske)
    rgba = np.stack([stern, blesse, schnippe, maul], -1)
    for k in range(4):                                                   # ueber die Inselraender ausdehnen (Mip-Stufen)
        rgba[..., k] = FE.fuellen(rgba[..., k].astype(np.float32), maske | (rgba[..., k] > 0), 6)
    kan = [Image.fromarray(np.clip(rgba[..., k] * 255 + 0.5, 0, 255).astype(np.uint8), "L").resize((1024, 1024), Image.BOX) for k in range(4)]   # je Kanal (RGBA-Verkleinerung wuerde mit Alpha vormultiplizieren)
    Image.merge("RGBA", kan).save(os.path.join(aus, "pferd_abzeichen_kopf.png"))
    # Beine: (Beinnummer + Hoehe) / 4 als Graustufe; im Spiel ohne Filter gelesen (zwischen zwei Beinen darf nichts gemischt werden)
    Image.fromarray(np.clip(bein * 255 + 0.5, 0, 255).astype(np.uint8), "L").resize((1024, 1024), Image.NEAREST).save(os.path.join(aus, "pferd_abzeichen_beine.png"))
    print("geschrieben", aus)
    if "vorschau" in OPT:                                                # Albedo mit eingebrannten Abzeichen (nur fuer Bilder; im Spiel macht es der Shader, gleiche Regeln)
        alb = np.asarray(Image.open(OPT["vorschau"]).convert("RGB")).astype(np.float32)
        for name, (kopf, beine, maul_art) in VORSCHAU.items():
            if maul_art == 1 and "rappe" in os.path.basename(OPT["vorschau"]).lower():
                maul_art = 0                                             # wie im Spiel: der Rappe bekommt kein dunkleres Maul
            neu = einbrennen(alb, stern, blesse, schnippe, maul, bein, kopf, beine, maul_art)
            Image.fromarray(np.clip(neu + 0.5, 0, 255).astype(np.uint8)).save(os.path.join(aus, f"vorschau_{name}.png"))
        print("Vorschau", ", ".join(VORSCHAU))


# Regeln wie im Shader (tier_vat.gdshader, tier_skelett.gdshader): Kopf 0 keins, 1 Stern, 2 Blesse, 3 Schnippe, 4 Stern+Schnippe, 5 Blesse+Schnippe;
# je Bein 0 nichts, 1 Krone/Fessel (bis 18 cm), 2 Socke (bis 35 cm); Maul 0 wie das Fell, 1 dunkler, 2 heller (Mehlmaul)
BEIN_GRENZE = (0.0, 0.36, 0.70)
WEISS = np.array([236, 232, 224], np.float32)
MEHL = np.array([200, 172, 128], np.float32)
VORSCHAU = {"v0_keins": (0, (0, 0, 0, 0), 0), "v1_stern": (1, (1, 0, 0, 0), 1), "v2_blesse": (2, (2, 2, 0, 0), 2),
            "v3_schnippe": (3, (0, 0, 1, 1), 0), "v4_stern_schnippe": (4, (0, 0, 2, 2), 1), "v5_blesse_schnippe": (5, (1, 1, 1, 1), 2)}


def einbrennen(alb, stern, blesse, schnippe, maul_k, bein, kopf, beine, maul):
    ks, kb, kn = kopf in (1, 4), kopf in (2, 5), kopf in (3, 4, 5)
    mk = np.maximum.reduce([stern * ks, blesse * kb, schnippe * kn])
    leg = np.clip(np.floor(bein * 4 + 1e-6).astype(int), 0, 3)
    h = bein * 4 - leg
    g = np.array([BEIN_GRENZE[b] for b in beine])[leg]
    mb = ((bein > 0) & (h < g)).astype(np.float32)
    neu = alb.copy()
    if maul == 1:
        neu = neu * (1 - 0.65 * maul_k[..., None])
    elif maul == 2:
        neu = neu * (1 - 0.45 * maul_k[..., None]) + MEHL * 0.45 * maul_k[..., None]
    m_ = np.maximum(mk, mb)
    return neu * (1 - m_[..., None]) + WEISS * m_[..., None]


if __name__ == "__main__":
    main()
