"""Gebackene Körnung für das Fels-Material (Felsen R7): löst den prozeduralen Platzhalter tools/oberflaeche/felstexturen.py ab,
gleiche Datei, gleiche Kanäle, gleiche Kachel (2,5 m × 2,5 m, 512 × 512 Pixel).

    blender -b --python tools/oberflaeche/felstexturen_blender.py -- [--ziel=<png>] [--vorschau=<png>] [--schnell]

Ablauf:
  1. Ein Höhenfeld der Kachel (Meter, nahtlos kachelbar, 1024 × 1024) aus numpy:
       - Bruchflächen: Voronoi-Felder von etwa 0,5 m, jedes eben, mit eigener Neigung und Höhe (kantig gebrochen),
         darin kleinere Splitter (0,15 m),
       - Klüfte: Risse an den Grenzen grober Felder (etwa 0,8 m), nur stückweise offen, nach innen spitz; dazu Haarrisse,
       - Schichtung: feine, wellige Lagen entlang V (an Wänden waagerecht), nur in einem Teil der Kachel,
       - Höhlung: flache Mulden und kleine Löcher der Verwitterung (Kuhlen von 1–4 cm Tiefe),
       - Körnung: feines Rauschen (einige Millimeter).
  2. In Blender ein dichtes Netz daraus (die Kachel und ein Rand aus ihrer Fortsetzung, damit Verdeckung und Normale an den
     Kanten nahtlos sind) und eine flache Zielfläche über genau einer Kachel; Cycles backt von dem Netz auf die Fläche die
     Normale (Tangentenraum) und die Umgebungsverdeckung (Abstand 8 cm).
  3. Auf 512 × 512 verkleinert (Mittel aus 2 × 2) und gepackt:
       R, G  Neigung der Fläche in U und V (128 = flach; G zeigt in Richtung wachsender Bildzeile wie bisher), auf die
             Stärke des Platzhalters gebracht (RMS je Achse 0,24), damit fels.gdshaderinc ohne Änderung passt
       B     Höhlung 0 tief … 1 hoch: 60 % gebackene Verdeckung, 40 % örtliche Höhe (Fugen, Mulden, Lagen), auf Mittel und
             Streuung des Platzhalters gebracht (0,54 ± 0,22)
       A     Flecken: grobes und feines Rauschen (Moospolster, Flechten), etwas mehr in den Höhlungen
Schreibt game/assets/texturen/fels/fels_struktur.png (verlustfrei, 8 Bit RGBA). Alle Zufallszahlen aus festen Saaten.
"""

import os
import sys
import time

import bmesh
import bpy
import numpy as np

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "modelle"))
import bausteine as bs  # noqa: E402

KACHEL = 2.5            # Meter je Kachel (wie „kachel“ im Shader)
HOCH = 1024             # Rechengitter und Backbild
AUS = 512               # ausgegebene Textur
RAND = 96               # Rand des Netzes in Pixeln (Fortsetzung der Kachel)
AO_ABSTAND = 0.08
NEIGUNG_RMS = 0.24      # Stärke des Platzhalters (RMS der Neigung je Achse), gemessen an felstexturen.py


def argumente():
    a = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    wert = lambda n, d: next((x.split("=", 1)[1] for x in a if x.startswith(f"--{n}=")), d)  # noqa: E731
    return dict(ziel=wert("ziel", os.path.join(bs.WURZEL, "game", "assets", "texturen", "fels", "fels_struktur.png")),
                vorschau=wert("vorschau", None), schnell="--schnell" in a)


# ── 1. Höhenfeld (kachelbar) ────────────────────────────────────────────────────────────────────────────────────────
def koordinaten(n):
    """Lage jedes Pixels in Metern (u nach rechts, v nach unten), Zeile 0 oben."""
    s = (np.arange(n) + 0.5) / n * KACHEL
    return np.meshgrid(s, s)


def rauschen(n, groesse_m, saat, streckung=1.0):
    """Kachelbares Rauschen (tiefpassgefiltertes Weißrauschen), etwa −1 … 1; groesse_m = Größe der Flecken in Metern,
    streckung > 1 zieht sie entlang U (Lagen)."""
    r = np.random.default_rng(saat)
    f = np.fft.fftfreq(n, d=KACHEL / n)
    fu, fv = np.meshgrid(f, f)
    filt = np.exp(-((fu * groesse_m / streckung) ** 2 + (fv * groesse_m) ** 2) * 4.0)
    z = np.fft.ifft2(np.fft.fft2(r.standard_normal((n, n))) * filt).real
    return z / (z.std() * 2.2 + 1e-12)


def zellen(n, anzahl, saat, zittern=0.9, warp=0.0):
    """Kachelbare Voronoi-Zellen (anzahl × anzahl Punkte je Kachel). Gibt Nummer der Zelle, Abstand zur Zellgrenze (m),
    Abstand zum Punkt (m) und den Weg vom Punkt (du, dv in m) zurück. warp (m): die Lage wird vorher mit Rauschen
    verschoben, damit die Grenzen nicht als gerade Vielecke erscheinen."""
    r = np.random.default_rng(saat)
    pu = (np.arange(anzahl)[None, :] + 0.5 + (r.random((anzahl, anzahl)) - 0.5) * zittern) / anzahl
    pv = (np.arange(anzahl)[:, None] + 0.5 + (r.random((anzahl, anzahl)) - 0.5) * zittern) / anzahl
    U, V = koordinaten(n)
    if warp > 0:
        groesse = KACHEL / anzahl * 0.8
        U = U + warp * rauschen(n, groesse, saat + 50)
        V = V + warp * rauschen(n, groesse, saat + 51)
    U, V = (U / KACHEL) % 1.0, (V / KACHEL) % 1.0
    ci, cj = np.floor(U * anzahl).astype(int), np.floor(V * anzahl).astype(int)
    kand = []
    for dj in (-2, -1, 0, 1, 2):
        for di in (-2, -1, 0, 1, 2):
            i, j = (ci + di) % anzahl, (cj + dj) % anzahl
            du = (pu[j, i] - U + 0.5) % 1.0 - 0.5
            dv = (pv[j, i] - V + 0.5) % 1.0 - 0.5
            kand.append((du, dv, j * anzahl + i))
    d = np.stack([np.hypot(du, dv) for du, dv, _ in kand])
    reihen = np.argsort(d, axis=0)
    nah = reihen[0]
    du1 = np.choose(nah, [k[0] for k in kand])
    dv1 = np.choose(nah, [k[1] for k in kand])
    nummer = np.choose(nah, [k[2] for k in kand])
    # Abstand zur Zellgrenze: kleinster Abstand zur Mittelsenkrechten mit einem Nachbarpunkt
    grenze = np.full(U.shape, 1e9)
    for du, dv, _ in kand:
        nu, nv = du - du1, dv - dv1
        ln = np.hypot(nu, nv)
        g = ((du + du1) * 0.5 * nu + (dv + dv1) * 0.5 * nv) / np.maximum(ln, 1e-9)
        grenze = np.where(ln > 1e-9, np.minimum(grenze, g), grenze)
    return nummer, grenze * KACHEL, np.hypot(du1, dv1) * KACHEL, -du1 * KACHEL, -dv1 * KACHEL


def je_zelle(nummer, saat):
    return (np.random.default_rng(saat).random(nummer.max() + 1) * 2 - 1)[nummer]


def weich(a, radius_m):
    """Kachelbarer Weichzeichner (Gauß über die FFT), radius_m in Metern."""
    n = a.shape[0]
    f = np.fft.fftfreq(n, d=KACHEL / n)
    fu, fv = np.meshgrid(f, f)
    return np.fft.ifft2(np.fft.fft2(a) * np.exp(-2 * (np.pi * radius_m) ** 2 * (fu ** 2 + fv ** 2))).real


def glatt(a, b, x):
    t = np.clip((x - a) / (b - a), 0.0, 1.0)
    return t * t * (3 - 2 * t)


def hoehenfeld(n):
    U, V = koordinaten(n)
    # Bruchflächen: ebene Felder mit eigener Neigung (bis etwa 8°) und Höhe
    nr, grenze, _, du, dv = zellen(n, 5, 701, warp=0.06)
    h = 0.006 * je_zelle(nr, 702) + 0.07 * (je_zelle(nr, 703) * du + je_zelle(nr, 704) * dv)
    # Splitter in den Feldern, flacher
    nr2, grenze2, _, du2, dv2 = zellen(n, 16, 705, warp=0.025)
    h += 0.003 * je_zelle(nr2, 706) + 0.05 * (je_zelle(nr2, 707) * du2 + je_zelle(nr2, 708) * dv2)
    # Bruchkanten leicht gerundet: an den Grenzen sinkt die Fläche etwas ab
    h -= 0.004 * (1 - glatt(0.0, 0.02, grenze)) + 0.0015 * (1 - glatt(0.0, 0.008, grenze2))
    # Klüfte: grobe Felder, nur stückweise offen, keilförmig (Tiefe nach der Mitte), dazu Haarrisse
    _, kluft_g, _, _, _ = zellen(n, 3, 709, 0.95, warp=0.1)
    offen = glatt(-0.05, 0.25, rauschen(n, 0.9, 710))
    breite = 0.004 + 0.004 * glatt(0.0, 1.0, rauschen(n, 0.5, 711) + 0.5)
    h -= 0.022 * np.clip(1 - kluft_g / breite, 0, 1) ** 1.5 * offen
    haar = glatt(0.3, 0.6, rauschen(n, 0.35, 712))
    h -= 0.006 * np.clip(1 - grenze / 0.0025, 0, 1) * haar
    # Schichtung: feine Lagen entlang V, gewellt, nur in einem Teil der Kachel
    lage = V / 0.07 + 1.4 * rauschen(n, 0.6, 713, 3.0) + 0.6 * rauschen(n, 0.2, 714)
    fr = lage - np.floor(lage)
    lamelle = (1 - np.clip(np.minimum(fr, 1 - fr) / 0.18, 0, 1)) ** 2
    h -= 0.0035 * lamelle * glatt(0.0, 0.4, rauschen(n, 0.8, 715))
    # Höhlung: flache Mulden und kleine Verwitterungslöcher
    _, _, abst, _, _ = zellen(n, 9, 716)
    mulde_r = 0.04 + 0.05 * (rauschen(n, 0.5, 717) + 0.5)
    mulden = glatt(0.15, 0.45, rauschen(n, 0.7, 718))
    h -= 0.012 * np.clip(1 - abst / mulde_r, 0, 1) ** 2 * mulden
    _, _, loch, _, _ = zellen(n, 23, 719)
    loecher = glatt(0.3, 0.55, rauschen(n, 0.4, 720))
    h -= 0.008 * np.clip(1 - loch / 0.012, 0, 1) ** 0.7 * loecher
    # großes Wellen und Körnung
    h += 0.012 * rauschen(n, 0.9, 721) + 0.003 * rauschen(n, 0.15, 724) + 0.0012 * rauschen(n, 0.02, 722) + 0.0007 * rauschen(n, 0.008, 723)
    return h - h.mean()


# ── 2. Backen in Blender ────────────────────────────────────────────────────────────────────────────────────────────
def netz_aus_hoehen(h):
    """Dichtes Netz: die Kachel und RAND Pixel ihrer Fortsetzung ringsum (Zeile 0 = oben = Blender +Y)."""
    n = h.shape[0]
    g = np.pad(h, RAND, mode="wrap")
    m = g.shape[0]
    s = (np.arange(m) - RAND + 0.5) / n * KACHEL
    X, Yz = np.meshgrid(s, s)
    Y = KACHEL - Yz                                     # Zeile 0 oben
    punkte = np.stack([X.ravel(), Y.ravel(), g.ravel()], 1)
    i = np.arange(m - 1)
    I, J = np.meshgrid(i, i)
    a = (J * m + I).ravel()
    vier = np.stack([a, a + m, a + m + 1, a + 1], 1)   # gegen den Uhrzeigersinn von oben gesehen
    me = bpy.data.meshes.new("_KoernungHoch")
    me.vertices.add(len(punkte))
    me.vertices.foreach_set("co", punkte.astype(np.float32).ravel())
    me.loops.add(vier.size)
    me.loops.foreach_set("vertex_index", vier.astype(np.int32).ravel())
    me.polygons.add(len(vier))
    me.polygons.foreach_set("loop_start", np.arange(0, vier.size, 4, dtype=np.int32))
    me.update(calc_edges=True)
    me.validate()
    o = bpy.data.objects.new("_KoernungHoch", me)
    bpy.context.scene.collection.objects.link(o)
    me.shade_smooth()
    return o


def zielflaeche():
    bm = bmesh.new()
    ecken = [bm.verts.new(p) for p in ((0, 0, 0), (KACHEL, 0, 0), (KACHEL, KACHEL, 0), (0, KACHEL, 0))]
    f = bm.faces.new(ecken)
    uv = bm.loops.layers.uv.new("UVMap")
    for l, w in zip(f.loops, ((0, 0), (1, 0), (1, 1), (0, 1))):
        l[uv].uv = w
    me = bpy.data.meshes.new("_KoernungZiel")
    bm.to_mesh(me)
    bm.free()
    o = bpy.data.objects.new("_KoernungZiel", me)
    bpy.context.scene.collection.objects.link(o)
    # die Zielfläche liegt mitten im Netz: sie selbst darf weder Schatten noch Verdeckung werfen
    for name in ("visible_camera", "visible_diffuse", "visible_glossy", "visible_transmission", "visible_volume_scatter",
                 "visible_shadow"):
        if hasattr(o, name):
            setattr(o, name, False)
    return o


def backen(hoch, ziel, schnell):
    szene = bpy.context.scene
    szene.render.engine = "CYCLES"
    print("Cycles backt auf:", bs.cycles_geraet(szene), flush=True)
    szene.cycles.samples = 32 if schnell else 128
    welt = bpy.data.worlds.new("_Backwelt")
    welt.light_settings.distance = AO_ABSTAND
    szene.world = welt
    bake = szene.render.bake
    bake.use_selected_to_active = True
    bake.use_clear = True
    bake.margin = 0
    bake.cage_extrusion = 0.1
    bake.max_ray_distance = 0.2
    bake.target = "IMAGE_TEXTURES"
    aus = {}
    for art in ("NORMAL", "AO"):
        img = bpy.data.images.new(f"_{art}", HOCH, HOCH, alpha=False, float_buffer=True)
        img.colorspace_settings.name = "Non-Color"
        m = bpy.data.materials.new(f"_Ziel{art}")
        try:
            m.use_nodes = True
        except AttributeError:
            pass
        k = m.node_tree.nodes.new("ShaderNodeTexImage")
        k.image = img
        m.node_tree.nodes.active = k
        ziel.data.materials.clear()
        ziel.data.materials.append(m)
        for o in szene.objects:
            o.select_set(o in (hoch, ziel))
        bpy.context.view_layer.objects.active = ziel
        t0 = time.time()
        if art == "NORMAL":
            bpy.ops.object.bake(type="NORMAL", normal_space="TANGENT")
        else:
            bpy.ops.object.bake(type="AO")
        px = np.empty(HOCH * HOCH * 4, np.float32)
        img.pixels.foreach_get(px)
        aus[art] = px.reshape(HOCH, HOCH, 4)[::-1, :, :3]          # Zeile 0 = oben
        print(f"  gebacken: {art} in {time.time() - t0:.0f} s", flush=True)
    return aus


# ── 3. Packen ───────────────────────────────────────────────────────────────────────────────────────────────────────
def kleiner(a):
    """Mittel aus 2 × 2 Pixeln."""
    n = a.shape[0] // 2
    return a.reshape(n, 2, n, 2, *a.shape[2:]).mean(axis=(1, 3))


def kodieren(w):
    return np.clip(np.round(128 + 127 * w), 1, 255).astype(np.uint8)


def packen(h, gebacken):
    nrm = kleiner(gebacken["NORMAL"]) * 2 - 1                  # x = U nach rechts, y = V nach oben (Blender)
    ao = kleiner(gebacken["AO"][:, :, 0])
    hk = kleiner(h - weich(h, 0.05))                          # nur das Örtliche: Fugen, Mulden, Lagen, nicht die Neigung der Felder
    nu, nv = nrm[:, :, 0], -nrm[:, :, 1]                       # nv: in Richtung wachsender Bildzeile (wie felstexturen.py)
    staerke = NEIGUNG_RMS / max(1e-6, np.sqrt(0.5 * ((nu ** 2).mean() + (nv ** 2).mean())))
    nu, nv = nu * staerke, nv * staerke
    print(f"  Neigung: Faktor {staerke:.2f} auf RMS {NEIGUNG_RMS}, Verdeckung Mittel {ao.mean():.3f}", flush=True)  # noqa: E501
    h01 = (hk - np.percentile(hk, 1)) / (np.percentile(hk, 99) - np.percentile(hk, 1))
    ao01 = np.clip((ao - np.percentile(ao, 1)) / (np.percentile(ao, 99) - np.percentile(ao, 1)), 0, 1)
    hoehlung = np.clip(0.4 * h01 + 0.6 * ao01, 0, 1)
    # auf Mittel und Streuung des Platzhalters (0,54 ± 0,22), damit Farbe und Moos im Shader im Mittel gleich bleiben
    hoehlung = np.clip(0.54 + 0.22 * (hoehlung - hoehlung.mean()) / max(1e-6, hoehlung.std()), 0, 1)
    flecken = 0.65 * rauschen(AUS, 0.35, 730) + 0.35 * rauschen(AUS, 0.08, 731)
    flecken = (flecken - flecken.min()) / (flecken.max() - flecken.min())
    flecken = np.clip(0.82 * flecken + 0.25 * (1 - ao) + 0.04, 0, 1)
    bild = np.dstack([kodieren(np.clip(nu, -1, 1)), kodieren(np.clip(nv, -1, 1)),
                      np.clip(np.round(hoehlung * 255), 0, 255).astype(np.uint8),
                      np.clip(np.round(flecken * 255), 0, 255).astype(np.uint8)])
    return bild


def png_schreiben(pfad, bild):
    h, w, k = bild.shape
    img = bpy.data.images.new("_png", w, h, alpha=k == 4)
    img.colorspace_settings.name = "Non-Color"
    px = bild[::-1].astype(np.float32) / 255
    img.pixels.foreach_set(px.ravel())
    img.filepath_raw = pfad
    img.file_format = "PNG"
    img.alpha_mode = "STRAIGHT"
    szene = bpy.context.scene
    szene.render.image_settings.color_depth = "8"
    img.save()
    bpy.data.images.remove(img)
    # Prüfung: zurücklesen
    z = bpy.data.images.load(pfad)
    z.colorspace_settings.name = "Non-Color"
    q = np.empty(w * h * 4, np.float32)
    z.pixels.foreach_get(q)
    q = np.round(q.reshape(h, w, 4)[::-1] * 255)
    print(f"geschrieben: {pfad} (Abweichung nach Zurücklesen höchstens {np.abs(q - bild).max():.0f})", flush=True)


def vorschau(pfad, bild):
    """2 × 2 Kacheln nebeneinander: Licht von links oben aus der Neigung, daneben B und A."""
    nu = (bild[:, :, 0].astype(float) - 128) / 127
    nv = (bild[:, :, 1].astype(float) - 128) / 127
    nz = np.sqrt(np.clip(1 - nu ** 2 - nv ** 2, 0, 1))
    licht = np.clip(0.6 * (-0.5 * nu - 0.5 * nv + 0.7 * nz) / 0.95 + 0.15, 0, 1)
    teile = [np.tile(licht, (2, 2)), np.tile(bild[:, :, 2] / 255, (2, 2)), np.tile(bild[:, :, 3] / 255, (2, 2))]
    g = np.concatenate(teile, axis=1)
    rgb = np.dstack([g, g, g, np.ones_like(g)]).astype(np.float32)
    img = bpy.data.images.new("_vorschau", g.shape[1], g.shape[0], alpha=False)
    img.pixels.foreach_set(rgb[::-1].ravel())
    img.filepath_raw = pfad
    img.file_format = "PNG"
    img.save()
    print("Bild:", pfad, flush=True)


def main():
    arg = argumente()
    bs.neue_szene()
    t0 = time.time()
    h = hoehenfeld(HOCH)
    print(f"Höhenfeld {HOCH}² in {time.time() - t0:.0f} s, Spanne {h.min() * 100:.1f} … {h.max() * 100:.1f} cm", flush=True)
    hoch = netz_aus_hoehen(h)
    ziel = zielflaeche()
    gebacken = backen(hoch, ziel, arg["schnell"])
    bild = packen(h, gebacken)
    png_schreiben(arg["ziel"], bild)
    if arg["vorschau"]:
        vorschau(arg["vorschau"], bild)


if __name__ == "__main__":
    main()
