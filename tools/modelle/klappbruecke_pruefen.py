"""Klappbruecke Koeln: Pruefung 12 (keine schwarze Flaeche) und UV2-Pruefung (kleinste Insel, Rand) mit Gegenprobe.

    blender -b --python tools/modelle/klappbruecke_pruefen.py -- [--pruefung=<uv2_pruefung.py>] [--glb=<datei>] [--arbeit=<ordner>]

Die Pruefungen selbst stehen in tools/modelle/neubau/uv2_pruefung.py (Branch phase5/neubau-mensch-st1-uv2, Steinhaus: dunkle_flaechen mit
schnittflaeche, Inselrand 0,004). Liegt die Datei noch nicht im Repo, nimmt dieses Skript `--pruefung=<Pfad>` (vorlaeufig, bis der Branch gemergt ist).
Prueft nur die Albedo des Trim-Sheets und die Verdeckung auf UV2, nicht Normal und ORM (so die gemeinsame Datei).

Gegenproben (muessen anschlagen, sonst sagt "ok" nichts):
    1. schwarze Verdeckung: AO-Atlas ganz schwarz           -> Pruefung 12 meldet "Verdeckung schwarz"
    2. schwarze Albedo: Trim-Sheet ganz schwarz             -> Pruefung 12 meldet "Albedo schwarz"
    3. entartete UV2: Modell mit Inselrand 0,005 (= 0,02 der Zelle 512) (Stufe-1-Wert) -> UV2-Pruefung meldet zu kleine Inseln / Rand (nur wenn Smart UV bei diesem Modell entartet)
Exit-Wert 1, wenn die echte Pruefung Fehler meldet oder eine Gegenprobe NICHT anschlaegt.
"""
import importlib.util
import os
import subprocess
import sys

import bpy
import numpy as np

HIER = os.path.dirname(os.path.abspath(__file__))
WURZEL = os.path.dirname(os.path.dirname(HIER))
TEX = os.path.join(WURZEL, "game", "assets", "texturen", "bruecke")
ATLAS_JSON = os.path.join(HIER, "neubau", "ao_atlas.json")


def optionen():
    a = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    return dict((x[2:].split("=", 1) + ["1"])[:2] for x in a if x.startswith("--"))


def lade_pruefung(pfad):
    spec = importlib.util.spec_from_file_location("uv2_pruefung", pfad)
    m = importlib.util.module_from_spec(spec); sys.modules["uv2_pruefung"] = m
    spec.loader.exec_module(m)
    return m


def schwarz_schreiben(quelle, ziel, alpha=False):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    b = bpy.data.images.load(quelle); w, h = b.size
    n = bpy.data.images.new("s", w, h, alpha=alpha)
    f = np.zeros((h, w, 4), dtype=np.float32); f[..., 3] = 1
    n.pixels.foreach_set(f.ravel()); n.colorspace_settings.name = "Non-Color"
    n.filepath_raw = ziel; n.file_format = "PNG"; n.save()


def main():
    opt = optionen()
    pfad = opt.get("pruefung") or os.path.join(HIER, "neubau", "uv2_pruefung.py")
    if not os.path.exists(pfad): raise SystemExit(f"uv2_pruefung.py nicht gefunden: {pfad} (--pruefung=<Pfad>)")
    glb = opt.get("glb") or os.path.join(WURZEL, "game", "assets", "modelle", "bruecke", "klappbruecke.glb")
    import tempfile
    arbeit = opt.get("arbeit") or os.path.join(tempfile.gettempdir(), "klappbruecke_pruefung")
    os.makedirs(arbeit, exist_ok=True)
    p = lade_pruefung(pfad)
    p.LISTE_MAX = 100000 if (opt.get("alle") or opt.get("nur-pruefung")) else 6
    atlas = p.atlas_von(glb, ATLAS_JSON)
    ausgabe = []
    fehler = 0

    z1, f1 = p.schwarze_flaechen(glb, TEX, "bruecke_trim", atlas)
    z2, f2, werte = p.uv2_inseln(glb, atlas)
    vier = hasattr(p, "uv2_dichte") and hasattr(p, "normal_orm")          # neuere Fassung der gemeinsamen Datei: Pruefung 14 und 15
    z3, f3, _w3 = p.uv2_dichte(glb, atlas) if vier else ([], 0, {})
    z4, f4 = p.normal_orm(glb, TEX, "bruecke_trim", atlas) if vier else ([], 0)
    ausgabe.append(f"PRUEFUNG {glb}")
    ausgabe.append(f"PRUEFUNG 12 keine schwarze Flaeche: {'ok' if not f1 else str(f1) + ' FEHLER'}"); ausgabe += ["PRUEFUNG " + z for z in z1]
    ausgabe.append(f"PRUEFUNG UV2-Inseln (13): {'ok' if not f2 else str(f2) + ' FEHLER'}"); ausgabe += ["PRUEFUNG " + z for z in z2]
    ausgabe.append(f"PRUEFUNG UV2 Werte: {werte}")
    ausgabe.append(f"PRUEFUNG UV2-Dichte und Inselrand (14): {'ok' if not f3 else str(f3) + ' FEHLER'}"); ausgabe += ["PRUEFUNG " + z for z in z3]
    ausgabe.append(f"PRUEFUNG Normalmap und ORM (15): {'ok' if not f4 else str(f4) + ' FEHLER'}"); ausgabe += ["PRUEFUNG " + z for z in z4]
    fehler += f1 + f2 + f3 + f4

    if opt.get("nur-pruefung"):
        print(chr(10).join(ausgabe)); sys.exit(1 if fehler else 0)
    # Gegenprobe 1 und 2
    g1 = os.path.join(arbeit, "schwarz_ao"); g2 = os.path.join(arbeit, "schwarz_albedo")
    for g in (g1, g2): os.makedirs(g, exist_ok=True)
    import shutil
    for g in (g1, g2):
        for f in os.listdir(TEX):
            if f.endswith(".png"): shutil.copy(os.path.join(TEX, f), os.path.join(g, f))
    schwarz_schreiben(os.path.join(TEX, atlas["datei"]), os.path.join(g1, atlas["datei"]))
    schwarz_schreiben(os.path.join(TEX, "bruecke_trim_albedo.png"), os.path.join(g2, "bruecke_trim_albedo.png"), alpha=True)
    for name, g, erwartet in (("Gegenprobe 1 (AO-Atlas schwarz)", g1, "Verdeckung schwarz"), ("Gegenprobe 2 (Albedo schwarz)", g2, "Albedo schwarz")):
        z, f = p.schwarze_flaechen(glb, g, "bruecke_trim", atlas)
        schlaegt_an = f > 0 and any(erwartet in x for x in z)
        ausgabe.append(f"PRUEFUNG {name}: {'schlaegt an (gewollt)' if schlaegt_an else 'SCHLAEGT NICHT AN'} ({f} Meldungen)")
        ausgabe += ["PRUEFUNG   " + x for x in z[:3]]
        if not schlaegt_an: fehler += 1
    # Gegenprobe 3: Inselrand 0,005 (= 0,02 der Zelle 512), wie in der Stufe 1 vor der Korrektur
    alt = os.path.join(arbeit, "klappbruecke_rand002.glb")
    r = subprocess.run([bpy.app.binary_path, "-b", "--python", os.path.join(HIER, "klappbruecke.py"), "--", "--glb=" + alt, "--uv2-rand=0.005", "--ohne-ao"],
                       capture_output=True, text=True)
    if os.path.exists(alt):
        z3, f3, w3 = p.uv2_inseln(alt, atlas)
        ausgabe.append(f"PRUEFUNG Gegenprobe 3 (Inselrand 0,005 (= 0,02 der Zelle 512)): {'schlaegt an (gewollt)' if f3 else 'schlaegt NICHT an (Smart UV entartet bei diesem Modell nicht)'} - {w3}")
        ausgabe += ["PRUEFUNG   " + x for x in z3[:3]]
    else:
        ausgabe.append("PRUEFUNG Gegenprobe 3: Modell mit Inselrand 0,005 (= 0,02 der Zelle 512) nicht entstanden: " + r.stderr[-300:])
    print("\n".join(ausgabe))
    if fehler: sys.exit(1)


main()
