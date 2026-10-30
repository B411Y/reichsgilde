"""Flammen-Flipbooks aus einer Blender-Feuersimulation (Mantaflow) – Phase 1b „Feuer und Flammen“ (docs/feuer/phase1.md).

    ~/werkzeuge/blender/blender.sh -b -t 6 --python tools/feuer/flammen_sim.py -- --variante ruhig|wild [Optionen]

Optionen:
    --variante ruhig|wild   ruhig: Fackel, Feuerkorb (eine Flamme, die leckt und flackert);
                            wild: Gebäudebrand, Fensterbrand (breites Feuer, große Zungen, gelegentliche Stichflammen)
    --aufloesung N          längste Seite des Gitters (Standard 96: 48 × 48 × 96 Zellen; das Bild wird auf 128 × 256 Pixel vergrößert)
    --cache ORDNER          wohin Blender die Simulation backt (Standard: /tmp/feuer_sim/<variante>_<aufloesung>; nicht ins Repository)
    --ziel ORDNER           wohin Atlas und Kennwerte gehen (Standard: game/assets/texturen/feuer)
    --nur-projektion        nicht neu simulieren, die Bilder aus dem vorhandenen Cache neu zeichnen und packen
    --nur-atlas             nicht neu simulieren, nur die gespeicherten Bilder (Projektionen) neu zum Atlas packen

Ablauf:
1. Szene bauen: ein Rauchgebiet (Domain, Gas) und ein Brenner (Flow, Feuer) mit Rauschtextur, die über die Fläche wandert (das Feuer brennt
   fleckig und flackert); der Brenner wackelt leicht und gibt mal mehr, mal weniger Brennstoff ab. Bei „wild“ dazu kurze Stöße aus kleinen
   Brennern mit hoher Anfangsgeschwindigkeit nach oben: die Stichflammen. Alle Zufallswerte kommen aus einem festen Startwert (SAAT), darum
   ergibt jeder Lauf dieselbe Szene; die Simulation selbst rechnet mit mehreren Kernen und kann deshalb in den letzten Stellen abweichen.
2. Backen (Grundgitter und Rauschverfeinerung, Cache in --cache).
3. Je Bild das Flammen- und das Rauchgitter aus dem Cache lesen und von vorn (entlang der Tiefe) aufsummieren: ein kleines eigenes Volumen-Zeichnen von vorn nach hinten.
   Jedes Würfelchen leuchtet nach seiner Flammenstärke (schwach = dunkelrot, stark = gelb bis weißgelb) und verdeckt nach Flamme und Ruß, was
   hinter ihm liegt. Ergebnis je Bild: Leuchten (RGB) und Deckung (Alpha).
4. Schleife: Von den Bildern nach dem Anlaufen nimmt der Atlas BILDER Stück; die ersten UEBERBLENDEN davon blenden aus der Fortsetzung hinter dem
   letzten Bild herein, darum geht das letzte Bild ohne Sprung ins erste über.
5. Farbe: Das Leuchten wird je Farbkanal weich gesättigt (1 − e^(−k·x)): Rot sättigt zuerst, dann Grün, so wird der dichte Kern gelb bis weiß wie
   auf einem Foto. Unten im Fuß wird die Glut etwas angehoben (hell und kontrastreich).
6. Atlas: 16 × 8 Bilder zu 128 × 256 Pixeln (ruhig, 2048 × 2048) bzw. 128 × 192 (wild, 2048 × 1536), Zeile für Zeile von links oben. Gespeichert als PNG mit „vormultiplizierter“ Farbe
   (RGB = Leuchten, schon mit der Deckung verrechnet; A = Deckung): im Spiel mit blend_premul_alpha, ohne „Alpha-Rand reparieren“ und ohne
   Vormultiplizieren beim Import (beides würde die Farbe verfälschen). Dazu flamme_<variante>.json mit Kennwerten (Höhe und Breite der Flamme im
   Bild, Bildrate, Helligkeit je Bild für das Licht).

Braucht nur Blender (numpy ist dabei). Die Bilder der Projektion bleiben im Cache (projektion.npz), damit sich Farbe und Schleife ohne neue
Simulation nachstellen lassen (--nur-atlas).
"""
import gzip
import json
import math
import os
import sys
import time

import numpy as np

HIER = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HIER)
from texturen import png  # noqa: E402  (derselbe PNG-Schreiber wie die übrigen Feuertexturen)

SAAT = 20261029
SPALTEN, ZEILEN = 16, 8
BILDER = SPALTEN * ZEILEN       # 128 Bilder im Atlas
UEBERBLENDEN = 24               # so viele Bilder blenden am Anfang der Schleife über
ANLAUF = 50                     # so viele Bilder braucht das Feuer, bis es gleichmäßig brennt (zählen nicht)

# Je Variante: Größe des Gebiets (Meter), Brenner, Feuer- und Rauschwerte. Die Zahlen sind von Hand abgestimmt (Bilder angesehen).
VARIANTEN = {
    "ruhig": dict(
        gebiet=(0.4, 0.4, 0.8),          # Breite, Tiefe, Höhe in Metern (Fackel und Feuerkorb: die Flamme ist etwa 0,4 m hoch)
        bild=(128, 256),                 # Bildgröße im Atlas (Pixel); das Gitter wird darauf vergrößert
        brenner_radius=0.075, brenner_hoehe=0.05, brenner_z=0.03,
        brennstoff=1.0, waerme=1.2, brennstoff_schwanken=0.6, schwanken_abstand=4,
        textur_groesse=0.3, textur_tempo=0.06,
        wackeln=0.01,
        brennrate=2.8, flammen_rauch=0.5, flammen_wirbel=2.0, wirbel=0.3, zuendung=1.25, hoechsttemp=3.0,
        auftrieb_dichte=0.2, auftrieb_waerme=1.0, aufloesen=12,
        rauschen=False, rauschen_staerke=0.5, rauschen_massstab=3.0, rauschen_zeit=0.1,
        zeitmassstab=0.7,
        stiche=0,
        bildrate=30,
        russ=1.2, flammen_deckung=2.2,
        flammen_exponent=1.3, belichtung=1.0, weich=False, rand_seite=0.0,
    ),
    "wild": dict(
        gebiet=(3.0, 3.0, 4.5),          # Gebäudebrand: die Flammen sind 2,5 bis 3,5 m hoch; Bild 2 : 3 (128 × 192 Pixel)
        bild=(128, 192),
        brenner_radius=0.9, brenner_hoehe=0.2, brenner_z=0.12,
        brennstoff=3.5, waerme=2.5, brennstoff_schwanken=0.5, schwanken_abstand=8,
        textur_groesse=0.2, textur_tempo=0.04,
        wackeln=0.05,
        brennrate=0.35, flammen_rauch=1.0, flammen_wirbel=2.5, wirbel=0.4, zuendung=1.25, hoechsttemp=3.0,
        auftrieb_dichte=0.3, auftrieb_waerme=2.4, aufloesen=20,
        rauschen=False, rauschen_staerke=0.5, rauschen_massstab=3.0, rauschen_zeit=0.1,
        zeitmassstab=0.8,
        stiche=7, stich_radius=0.2, stich_tempo=14.0, stich_dauer=(4, 7), stich_brennstoff=3.5,
        bildrate=24,
        russ=1.2, flammen_deckung=2.0,
        # Höherer Exponent: nur die dichten Flammen leuchten hell, die dünnen Ränder oben bleiben dunkelrot – sonst wird der große Brand ein
        # gleichmäßig oranger Klumpen ohne Zungen. „weich“: einmal leicht weichzeichnen (bei viel Wirbel wird die Flamme sonst fleckig)
        flammen_exponent=2.0, belichtung=0.85, weich=True, rand_seite=0.25,
    ),
}

# Farbe eines Würfelchens nach seiner Flammenstärke f (0 … 1): schwach dunkelrot, stark gelb bis weißgelb (linear, vor der Sättigung)
F_STELLEN = [0.00, 0.20, 0.45, 0.70, 0.90, 1.00]
F_FARBEN = np.array([
    [1.00, 0.10, 0.012],
    [1.00, 0.18, 0.025],
    [1.00, 0.34, 0.060],
    [1.00, 0.52, 0.140],
    [1.00, 0.68, 0.280],
    [1.00, 0.78, 0.420],
])


def optionen():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    o = {"variante": "ruhig", "aufloesung": 96, "cache": None, "ziel": os.path.normpath(os.path.join(HIER, "..", "..", "game", "assets", "texturen", "feuer")),
         "nur_atlas": False, "nur_projektion": False}
    i = 0
    while i < len(argv):
        a = argv[i]
        if a == "--nur-atlas":
            o["nur_atlas"] = True
        elif a == "--nur-projektion":
            o["nur_projektion"] = True
        elif a in ("--variante", "--aufloesung", "--cache", "--ziel"):
            o[a[2:]] = argv[i + 1]
            i += 1
        i += 1
    o["aufloesung"] = int(o["aufloesung"])
    if o["cache"] is None:
        o["cache"] = f"/tmp/feuer_sim/{o['variante']}_{o['aufloesung']}"
    return o


# ═════════════════════════════════════════════════════════════════════════════
#  Szene
# ═════════════════════════════════════════════════════════════════════════════

def szene_bauen(bpy, v, aufloesung, cache):
    rng = np.random.default_rng(SAAT + (0 if v is VARIANTEN["ruhig"] else 1000))
    for o in list(bpy.data.objects):
        bpy.data.objects.remove(o)
    sc = bpy.context.scene
    ende = ANLAUF + BILDER + UEBERBLENDEN
    sc.frame_start, sc.frame_end = 1, ende
    sc.render.fps = 24
    sc.use_gravity = True

    b, t, h = v["gebiet"]
    bpy.ops.mesh.primitive_cube_add(size=2, location=(0, 0, h / 2))
    gebiet = bpy.context.active_object
    gebiet.name = "Gebiet"
    gebiet.scale = (b / 2, t / 2, h / 2)
    bpy.ops.object.transform_apply(scale=True)
    m = gebiet.modifiers.new("Fluid", "FLUID")
    m.fluid_type = "DOMAIN"
    d = m.domain_settings
    d.domain_type = "GAS"
    d.resolution_max = aufloesung
    d.use_adaptive_timesteps = True
    d.cfl_condition = 3.0
    d.timesteps_max = 4
    d.timesteps_min = 1
    d.time_scale = v["zeitmassstab"]
    d.vorticity = v["wirbel"]
    d.alpha = v["auftrieb_dichte"]
    d.beta = v["auftrieb_waerme"]
    d.use_dissolve_smoke = True
    d.dissolve_speed = v["aufloesen"]
    d.burning_rate = v["brennrate"]
    d.flame_smoke = v["flammen_rauch"]
    d.flame_vorticity = v["flammen_wirbel"]
    d.flame_ignition = v["zuendung"]
    d.flame_max_temp = v["hoechsttemp"]
    # Nur der Boden ist geschlossen: Rauch und Hitze ziehen oben und seitlich ab
    d.use_collision_border_bottom = True
    for seite in ("top", "front", "back", "left", "right"):
        setattr(d, f"use_collision_border_{seite}", False)
    # Rauschverfeinerung (Wavelet): bei kleinem Grundgitter körnig wie Salz und Pfeffer; darum aus und lieber ein feineres Grundgitter
    d.use_noise = v["rauschen"]
    d.noise_scale = 2
    d.noise_strength = v["rauschen_staerke"]
    d.noise_pos_scale = v["rauschen_massstab"]
    d.noise_time_anim = v["rauschen_zeit"]
    d.cache_directory = cache
    d.cache_type = "ALL"
    d.cache_data_format = "UNI"
    d.cache_noise_format = "UNI"
    d.cache_frame_start = 1
    d.cache_frame_end = ende

    # Rauschtextur für den Brenner: das Feuer brennt fleckig, die Flecken wandern
    tex = bpy.data.textures.new("Flackern", type="CLOUDS")
    tex.noise_scale = v["textur_groesse"]
    tex.noise_depth = 2
    tex.noise_type = "SOFT_NOISE"

    def brenner(name, radius, hoehe, lage):
        bpy.ops.mesh.primitive_cylinder_add(radius=radius, depth=hoehe, location=lage, vertices=24)
        o = bpy.context.active_object
        o.name = name
        f = o.modifiers.new("Fluid", "FLUID")
        f.fluid_type = "FLOW"
        fs = f.flow_settings
        fs.flow_type = "FIRE"
        fs.flow_behavior = "INFLOW"
        fs.flow_source = "MESH"
        fs.surface_distance = 1.0
        fs.volume_density = 1.0
        fs.subframes = 1
        return o, fs

    haupt, fs = brenner("Brenner", v["brenner_radius"], v["brenner_hoehe"], (0, 0, v["brenner_z"]))
    fs.temperature = v["waerme"]
    fs.use_texture = True
    fs.noise_texture = tex
    fs.texture_map_type = "AUTO"
    fs.texture_size = 1.0
    start = float(rng.uniform(0, 10))
    # Brennstoff schwankt (das Feuer „pufft“), der Brenner wackelt ein wenig, die Textur wandert: alles als Schlüsselbilder aus dem Startwert
    sa = v["schwanken_abstand"]
    schwanken = np.interp(np.arange(ende + 1), np.arange(0, ende + sa + 1, sa), rng.uniform(-1, 1, len(range(0, ende + sa + 1, sa))))
    wx = np.interp(np.arange(ende + 1), np.arange(0, ende + 13, 12), rng.uniform(-1, 1, len(range(0, ende + 13, 12))))
    wy = np.interp(np.arange(ende + 1), np.arange(0, ende + 13, 12), rng.uniform(-1, 1, len(range(0, ende + 13, 12))))
    for bild in range(1, ende + 1):
        fs.fuel_amount = v["brennstoff"] * (1 + v["brennstoff_schwanken"] * schwanken[bild])
        fs.keyframe_insert("fuel_amount", frame=bild)
        fs.texture_offset = start + v["textur_tempo"] * bild
        fs.keyframe_insert("texture_offset", frame=bild)
        haupt.location = (v["wackeln"] * wx[bild], v["wackeln"] * wy[bild], v["brenner_z"])
        haupt.keyframe_insert("location", frame=bild)

    # Stichflammen (nur „wild“): kurze Stöße mit viel Brennstoff und hoher Geschwindigkeit nach oben, zu Zeiten, die nicht in die Überblendung fallen
    stiche = []
    if v["stiche"]:
        frei_von, frei_bis = ANLAUF + UEBERBLENDEN + 2, ANLAUF + BILDER - 22
        zeiten = np.sort(rng.uniform(frei_von, frei_bis, v["stiche"])).round().astype(int)
        for k, z in enumerate(zeiten):
            winkel = rng.uniform(0, 2 * math.pi)
            r = v["brenner_radius"] * math.sqrt(rng.uniform(0.0, 0.8))
            # nur in der Bildebene verstreut (vorn/hinten wenig), damit man die Stichflamme sieht und sie nicht hinter dem Feuer verschwindet
            lage = (r * math.cos(winkel), 0.35 * r * math.sin(winkel), v["brenner_z"] + v["brenner_hoehe"])
            o, sfs = brenner(f"Stich_{k}", v["stich_radius"] * rng.uniform(0.8, 1.2), v["brenner_hoehe"], lage)
            sfs.fuel_amount = v["stich_brennstoff"]
            sfs.temperature = v["waerme"] * 1.3
            sfs.use_initial_velocity = True
            sfs.velocity_coord = (rng.uniform(-0.15, 0.15) * v["stich_tempo"], 0.0, v["stich_tempo"] * rng.uniform(0.85, 1.15))
            dauer = int(rng.integers(v["stich_dauer"][0], v["stich_dauer"][1] + 1))
            for bild, an in ((1, False), (int(z), True), (int(z) + dauer, False)):
                sfs.use_inflow = an
                sfs.keyframe_insert("use_inflow", frame=bild)
            stiche.append((int(z), dauer))
    return gebiet, stiche


def backen(bpy, gebiet):
    t = time.time()
    with bpy.context.temp_override(object=gebiet, active_object=gebiet, selected_objects=[gebiet], selected_editable_objects=[gebiet]):
        r = bpy.ops.fluid.bake_all()
    print(f"[feuer] gebacken {r} in {time.time() - t:.0f} s", flush=True)


def uni_lesen(cache, name, bild):
    """Ein Gitter aus dem Cache von Mantaflow (Format UNI: gzip, Kennung „MNT3“, Kopf 288 Byte, dann float32, x läuft am schnellsten)."""
    with gzip.open(os.path.join(cache, "data", f"{name}_{bild:04d}.uni")) as f:
        roh = f.read()
    if roh[:4] != b"MNT3":
        raise ValueError(f"unbekanntes Cache-Format in {name}_{bild:04d}.uni")
    nx, ny, nz = np.frombuffer(roh[4:16], "<i4")
    return np.frombuffer(roh[4 + 288:], "<f4").reshape(nz, ny, nx)


def projektion(cache, v):
    """Je Bild Leuchten (3 × H × B) und Durchlässigkeit (H × B), von vorn (−Y) nach hinten (+Y) gezeichnet; liest den Cache direkt."""
    erstes, letztes = ANLAUF + 1, ANLAUF + BILDER + UEBERBLENDEN
    leuchten, durch = [], []
    t0 = time.time()
    for bild in range(erstes, letztes + 1):
        flamme = uni_lesen(cache, "flame", bild)
        dichte = uni_lesen(cache, "density", bild)
        nz, ny, nx = flamme.shape
        schritt = 1.0 / ny                                     # Tiefe in Gebietsbreiten
        # Im Brenner selbst steht die Flamme als voller Block (dort wird Brennstoff eingesetzt): ausblenden, sonst leuchtet ein heller Klotz am Fuß
        # (alles unter der Oberkante des Brenners, auch daneben: dort steht nur Rauch, der als dunkle Streifen unter dem Fuß der Flamme läge)
        hh = v["gebiet"][2]
        zz = (np.arange(nz) + 0.5) / nz * hh
        unter = zz < v["brenner_z"] + v["brenner_hoehe"] / 2 + hh / nz
        flamme = np.where(unter[:, None, None], 0.0, flamme)
        dichte = np.where(unter[:, None, None], 0.0, dichte)
        tr = np.ones((nz, nx), np.float32)
        c = np.zeros((3, nz, nx), np.float32)
        for j in range(ny):
            f = np.clip(flamme[:, j, :], 0, 1)
            dd = np.clip(dichte[:, j, :], 0, 4)
            staerke = f ** v["flammen_exponent"]
            farbe = np.stack([np.interp(f, F_STELLEN, F_FARBEN[:, k]) for k in range(3)])
            c += tr * farbe * staerke * schritt
            tr *= np.exp(-(v["russ"] * dd + v["flammen_deckung"] * f) * schritt)
        if v["weich"]:
            c, tr = weich(c), weich(tr)
        leuchten.append(c[:, ::-1, :].copy())                  # Zeile 0 = oben
        durch.append(tr[::-1, :].copy())
        if bild == erstes or bild % 20 == 0:
            print(f"[feuer] Bild {bild}/{letztes}  {nx}×{ny}×{nz}  Flamme max {flamme.max():.2f}  Dichte max {dichte.max():.2f}  ({time.time() - t0:.0f} s)", flush=True)
    return np.array(leuchten), np.array(durch)


def weich(a):
    """Leicht weichzeichnen (1-2-1 in beiden Richtungen der letzten zwei Achsen, Rand wiederholt)."""
    for achse in (-1, -2):
        vor = np.concatenate([np.take(a, [0], axis=achse), np.take(a, range(a.shape[achse] - 1), axis=achse)], axis=achse)
        nach = np.concatenate([np.take(a, range(1, a.shape[achse]), axis=achse), np.take(a, [a.shape[achse] - 1], axis=achse)], axis=achse)
        a = 0.25 * vor + 0.5 * a + 0.25 * nach
    return a


# ═════════════════════════════════════════════════════════════════════════════
#  Schleife, Farbe, Atlas
# ═════════════════════════════════════════════════════════════════════════════

def schleife(folge):
    """BILDER + UEBERBLENDEN Bilder → BILDER Bilder, die sich ohne Sprung schließen (Anfang blendet aus der Fortsetzung herein)."""
    aus = folge[:BILDER].copy()
    for i in range(UEBERBLENDEN):
        w = (i + 0.5) / UEBERBLENDEN
        w = w * w * (3 - 2 * w)
        aus[i] = (1 - w) * folge[BILDER + i] + w * folge[i]
    return aus


def vergroessern(a, h, b):
    """Bilineares Vergrößern der letzten beiden Achsen auf h × b (Mitte der Bildpunkte bleibt Mitte)."""
    h0, b0 = a.shape[-2:]
    if (h0, b0) == (h, b):
        return a
    y = np.clip((np.arange(h) + 0.5) * h0 / h - 0.5, 0, h0 - 1)
    x = np.clip((np.arange(b) + 0.5) * b0 / b - 0.5, 0, b0 - 1)
    y0, x0 = np.floor(y).astype(int), np.floor(x).astype(int)
    y1, x1 = np.minimum(y0 + 1, h0 - 1), np.minimum(x0 + 1, b0 - 1)
    fy, fx = (y - y0)[:, None], (x - x0)[None, :]
    oben = a[..., y0, :][..., x0] * (1 - fx) + a[..., y0, :][..., x1] * fx
    unten = a[..., y1, :][..., x0] * (1 - fx) + a[..., y1, :][..., x1] * fx
    return oben * (1 - fy) + unten * fy


def atlas_bauen(leuchten, durch, v, name, ziel, stiche, bild_b=128, bild_h=256):
    leuchten = vergroessern(schleife(leuchten), bild_h, bild_b)
    deckung = vergroessern(schleife(1.0 - durch), bild_h, bild_b)
    n, _, h, b = leuchten.shape
    # Belichtung: so, dass die hellsten Stellen (99,7 %) im Rot gerade sättigen
    rot = leuchten[:, 0]
    k = 3.2 / max(np.percentile(rot[rot > 1e-4], 99.7), 1e-6) * v["belichtung"]
    zeilen_h = (np.arange(h) + 0.5) / h
    spalten_b = (np.arange(b) + 0.5) / b
    # Rand: seitlich und unten schmal, oben breiter ausblenden (dort zieht Rauch aus dem Bild); verhindert Kanten und Farbe aus dem Nachbarbild
    # Bei „wild“ reicht das Feuer bis an die Seiten des Gebiets: dort weich über einen breiten Streifen ausblenden, sonst entstehen gerade, senkrechte Kanten
    seite = np.minimum(spalten_b, 1 - spalten_b)
    seitlich = np.clip(seite * b / 4.0, 0, 1)
    if v["rand_seite"] > 0:
        x = np.clip(seite / v["rand_seite"], 0, 1)
        seitlich = seitlich * x * x * (3 - 2 * x)
    rand = (seitlich[None, :]
            * np.clip(zeilen_h / 0.22, 0, 1)[:, None] ** 1.5
            * np.clip((1 - zeilen_h) * h / 2.0, 0, 1)[:, None])
    # Glut am Fuß: die untersten Zeilen der Flamme leuchten heller (Brennstoff glüht)
    fuss = np.clip((zeilen_h - 0.80) / 0.17, 0, 1) ** 2
    bilder = np.zeros((n, h, b, 4), np.float32)
    helligkeit = []
    for i in range(n):
        c = leuchten[i] * k * (1 + 0.6 * fuss)[None, :, None]
        rgb = 1 - np.exp(-c)
        a = np.clip(deckung[i], 0, 1)
        # Wo die Flamme leuchtet, deckt sie auch etwas (sonst wirkt sie wie eine Lichtspiegelung)
        a = np.maximum(a, 0.55 * rgb[0] ** 1.5)
        rgb = rgb * rand[None]
        a = a * rand
        bilder[i, ..., :3] = np.moveaxis(rgb, 0, -1)
        bilder[i, ..., 3] = a
        helligkeit.append(float((rgb[0] + rgb[1]).mean()))
    blatt = np.zeros((ZEILEN * h, SPALTEN * b, 4), np.float32)
    for i in range(n):
        z, s = divmod(i, SPALTEN)
        blatt[z * h:(z + 1) * h, s * b:(s + 1) * b] = bilder[i]
    os.makedirs(ziel, exist_ok=True)
    pfad = os.path.join(ziel, f"flamme_{name}.png")
    png(pfad, blatt)

    # Kennwerte: Wo steht die Flamme im Bild (Deckung über 0,15 in mindestens einem der Bilder, als Mittel über alle)?
    lum = bilder[..., 0] + bilder[..., 1]
    oben, breite, unten = [], [], []
    for i in range(n):
        zeilen = np.where((lum[i] > 0.25).any(axis=1))[0]
        sp = np.where((lum[i] > 0.25).any(axis=0))[0]
        oben.append(1 - zeilen.min() / h if len(zeilen) else 0)
        unten.append(1 - (zeilen.max() + 1) / h if len(zeilen) else 0)
        breite.append((sp.max() - sp.min() + 1) / b if len(sp) else 0)
    hell = np.array(helligkeit)
    hell = hell / hell.mean()
    # Flackern: Hauptfrequenz der Flammenhöhe (zur Abstimmung der Bildrate)
    spektrum = np.abs(np.fft.rfft(np.array(oben) - np.mean(oben)))
    frequenz = np.fft.rfftfreq(n, 1.0 / v["bildrate"])
    haupt = float(frequenz[1 + np.argmax(spektrum[1:])])
    # Wie schnell steigen die Formen? Verschiebung (Pixel je Bild) mit der besten Übereinstimmung zweier Nachbarbilder, ohne den festen Anteil
    rest = lum - lum.mean(axis=0, keepdims=True)
    guete = []
    for s in range(0, 13):
        guete.append(sum(float((rest[i + 1][:h - s] * rest[i][s:]).sum()) for i in range(n - 1)))
    steigen = int(np.argmax(guete))
    # Wie ähnlich sind zwei aufeinanderfolgende Bilder (Korrelation der Helligkeit)? Unter etwa 0,85 springt das Flipbook sichtbar
    def korr(a, b):
        a, b = a - a.mean(), b - b.mean()
        return float((a * b).sum() / max(np.sqrt((a * a).sum() * (b * b).sum()), 1e-9))
    aehnlich = float(np.mean([korr(lum[i], lum[(i + 1) % n]) for i in range(n)]))
    kennwerte = {
        "quelle": "tools/feuer/flammen_sim.py",
        "variante": name,
        "spalten": SPALTEN, "zeilen": ZEILEN, "bilder": n, "bild_pixel": [b, h],
        "bildrate": v["bildrate"],
        "fuss": round(float(np.mean(unten)), 3),
        "flamme_hoehe": round(float(np.mean(oben) - np.mean(unten)), 3),
        "flamme_hoehe_max": round(float(np.max(oben)), 3),
        "flamme_breite": round(float(np.mean(breite)), 3),
        "flackern_hz": round(haupt, 2),
        "steigen_pixel_je_bild": steigen,
        "aehnlichkeit_nachbarbild": round(aehnlich, 3),
        "stichflammen": [round((z - ANLAUF - 1) / n, 3) for z, _ in stiche],
        "helligkeit": [round(float(x), 3) for x in hell],
    }
    with open(os.path.join(ziel, f"flamme_{name}.json"), "w", encoding="utf-8") as f:
        json.dump(kennwerte, f, ensure_ascii=False, indent=1)
        f.write("\n")
    print(f"[feuer] geschrieben {pfad} ({os.path.getsize(pfad) // 1024} KB); Flamme Höhe {kennwerte['flamme_hoehe']} (max {kennwerte['flamme_hoehe_max']}), "
          f"Breite {kennwerte['flamme_breite']}, Flackern {haupt:.2f} Hz, Steigen {steigen} px/Bild, Nachbarbild {aehnlich:.3f}, Helligkeit {hell.min():.2f}–{hell.max():.2f}", flush=True)


def main():
    o = optionen()
    v = VARIANTEN[o["variante"]]
    os.makedirs(o["cache"], exist_ok=True)
    npz = os.path.join(o["cache"], "projektion.npz")
    stiche = []
    if o["nur_projektion"]:
        # Ohne Blender: eine neu gebaute Szene könnte beim Setzen der Cache-Werte den vorhandenen Cache löschen. Die Zeiten der Stichflammen
        # stehen im alten projektion.npz.
        stiche = [tuple(x) for x in np.load(npz)["stiche"]] if os.path.exists(npz) else []
        leuchten, durch = projektion(o["cache"], v)
        np.savez_compressed(npz, leuchten=leuchten.astype(np.float16), durch=durch.astype(np.float16), stiche=np.array(stiche).reshape(-1, 2))
    elif not o["nur_atlas"]:
        import bpy
        gebiet, stiche = szene_bauen(bpy, v, o["aufloesung"], o["cache"])
        backen(bpy, gebiet)
        leuchten, durch = projektion(o["cache"], v)
        np.savez_compressed(npz, leuchten=leuchten.astype(np.float16), durch=durch.astype(np.float16), stiche=np.array(stiche).reshape(-1, 2))
    daten = np.load(npz)
    stiche = [tuple(x) for x in daten["stiche"]]
    atlas_bauen(daten["leuchten"].astype(np.float32), daten["durch"].astype(np.float32), v, o["variante"], o["ziel"], stiche, *v["bild"])


if __name__ == "__main__":
    main()
