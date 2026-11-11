"""Kontaktbögen und GIFs aus den Bildfolgen von zugtiere_pruefen.py (normales Python mit Pillow, Python -I).

    python -I tools/modelle/zugtiere_bogen.py <ordner mit <glb>/<clip>/seite_NNN.png und spiel_NNN.png> <glb> [--gif]

Schreibt <ordner>/<glb>_<clip>_bogen.png (oben Seite, unten Spielkamera, links nach rechts die Bilder des Clips, Bildnummer darunter)
und mit --gif <ordner>/<glb>_<clip>.gif (Seite und Spielkamera nebeneinander, jedes dritte Bild, 8 Bilder je Sekunde = Echtzeit).
"""
import glob
import os
import sys

from PIL import Image, ImageDraw


def main():
    ordner, glb = sys.argv[1], sys.argv[2]
    gif = "--gif" in sys.argv
    for clip in sorted(os.listdir(os.path.join(ordner, glb))):
        d = os.path.join(ordner, glb, clip)
        seiten = sorted(glob.glob(os.path.join(d, "seite_*.png")))
        if not seiten:
            continue
        spiele = [s.replace("seite_", "spiel_") for s in seiten]
        w, h = Image.open(seiten[0]).size
        alle_s, alle_p = seiten, spiele
        schritt = max(1, len(seiten) // 12)
        seiten, spiele = seiten[::schritt][:16], spiele[::schritt][:16]
        spalten = 4 if len(seiten) > 6 else len(seiten)
        reihen = -(-len(seiten) // spalten)
        oben = [s.replace("seite_", "oben_") for s in seiten]
        drei = all(os.path.exists(o) for o in oben)
        zeilen_je = 3 if drei else 2
        bogen = Image.new("RGB", (spalten * w, reihen * zeilen_je * (h + 14)), (30, 30, 30))
        dr = ImageDraw.Draw(bogen)
        for i, (s, p) in enumerate(zip(seiten, spiele)):
            r, c = divmod(i, spalten)
            y0 = r * zeilen_je * (h + 14)
            bogen.paste(Image.open(s).convert("RGB"), (c * w, y0))
            bogen.paste(Image.open(p).convert("RGB"), (c * w, y0 + h + 14))
            if drei:
                bogen.paste(Image.open(oben[i]).convert("RGB").resize((w, h)), (c * w, y0 + 2 * (h + 14)))
            dr.text((c * w + 4, y0 + h), f"{clip} Bild {os.path.basename(s)[6:9]} Seite / Spielkamera", fill=(230, 230, 230))
        bogen.thumbnail((2600, 3600))
        bogen.save(os.path.join(ordner, f"{glb}_{clip}_bogen.png"))
        if gif:
            fr = []
            for s, p in zip(alle_s, alle_p):
                a, b = Image.open(s).convert("RGB"), Image.open(p).convert("RGB")
                z = Image.new("RGB", (a.width + b.width, a.height))
                z.paste(a, (0, 0))
                z.paste(b, (a.width, 0))
                fr.append(z.quantize(colors=128))
            fr[0].save(os.path.join(ordner, f"{glb}_{clip}.gif"), save_all=True, append_images=fr[1:], duration=int(1000 / 8), loop=0)
        print("Bogen:", clip, len(seiten))


main()
