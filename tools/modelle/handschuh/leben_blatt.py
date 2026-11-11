"""GIFs, Bildfolgen und Lesbarkeitsblatt der lebendigen Clips. python -I leben_blatt.py <ausordner> <belegordner>  (Bilder aus leben_bilder.py)
GIFs: jedes Bild 20 ms (GIF kennt nur 10-ms-Schritte, 60 fps nicht darstellbar: 1,2-fach langsamer als echt); die Bilder selbst sind die Zeitpunkte i/60 s."""
import sys, os, json
from PIL import Image, ImageDraw
aus, beleg = sys.argv[1], sys.argv[2]; BG = (230, 220, 190)
def lade(p, s=384):
    im = Image.open(p).convert("RGBA").resize((s, s), Image.LANCZOS); b = Image.new("RGBA", (s, s), BG + (255,)); b.alpha_composite(im); return b.convert("RGB")
def liste(c): return json.load(open(os.path.join(aus, "bilder", f"{c}_liste.json")))
for c in ("Bereit_atmen", "Bereit_zucken", "Winken"):
    ims = [lade(p) for p in liste(c)]; ims[0].save(os.path.join(beleg, f"{c}_60fps.gif"), save_all=True, append_images=ims[1:], duration=20, loop=0)
    pick = list(range(0, len(ims), max(1, len(ims) // 18))); S = 150; cols = 9; rows = (len(pick) + cols - 1) // cols
    B = Image.new("RGB", (S * cols, S * rows), BG); d = ImageDraw.Draw(B)
    for k, i in enumerate(pick):
        B.paste(ims[i].resize((S, S), Image.LANCZOS), ((k % cols) * S, (k // cols) * S)); d.text(((k % cols) * S + 3, (k // cols) * S + 2), f"{i / 60:.2f}s", fill=(40, 30, 20))
    B.save(os.path.join(beleg, f"bilder_{c}.png"))
# Uebergaenge: ein Blatt, jedes Bild 3
ue = [c for c in ("Uebergang_Ruhe_Winken", "Uebergang_Winken_Ruhe", "Uebergang_Zeigen_Winken", "Uebergang_Winken_Zeigen", "Uebergang_Text_Winken", "Uebergang_Winken_Text",
                  "Uebergang_Greifen_Winken", "Uebergang_Winken_Greifen", "Uebergang_Bereit_Winken", "Uebergang_Winken_Bereit")]
S = 130; B = Image.new("RGB", (S * 7 + 150, S * len(ue)), BG); d = ImageDraw.Draw(B)
for j, c in enumerate(ue):
    L = liste(c); idx = [round(i * (len(L) - 1) / 5) for i in range(6)]; d.text((3, j * S + S // 2), c.replace("Uebergang_", ""), fill=(40, 30, 20))
    for k, i in enumerate(idx): B.paste(lade(L[i], S), (150 + k * S, j * S))
B.save(os.path.join(beleg, "bilder_uebergaenge_winken.png"))
# Winken bei Zeigergroesse 48 / 64 px (Bild = 1,5 Zeigergroessen = 72 / 96 px), 4x NEAREST: sechs Zeitpunkte
L = liste("Winken"); n = len(L); st = [0, int(n * 0.2), int(n * 0.3), int(n * 0.45), int(n * 0.6), n - 1]; rows = []
for s in (48, 64):
    px = int(s * 1.5); z = 4; row = Image.new("RGB", (len(st) * (px * z + 8) + 8, px * z + 8), BG); x = 8
    for i in st:
        im = Image.open(L[i]).convert("RGBA").resize((px, px), Image.LANCZOS); bb = Image.new("RGBA", (px, px), BG + (255,)); bb.alpha_composite(im)
        row.paste(bb.convert("RGB").resize((px * z, px * z), Image.NEAREST), (x, 4)); x += px * z + 8
    rows.append(row)
W = max(r.width for r in rows); out = Image.new("RGB", (W, sum(r.height for r in rows)), BG); y = 0
for r in rows: out.paste(r, (0, y)); y += r.height
out.save(os.path.join(beleg, "klein_lesbarkeit_winken_48_64.png")); print("fertig")
