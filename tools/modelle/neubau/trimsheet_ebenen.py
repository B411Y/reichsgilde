"""Steinebenen je Volk (Nutzer, 15.11.2026): packt die Trim-Sheets der Gebaeude zu Texture2DArrays, eine Ebene je Blatt, und setzt
in die Steinbaender jeder Ebene den Stein des Volkes (CC0-Texturen K1 bis K5, Herkunft in game/assets/texturen/neubau/HERKUNFT.md).

    python -I tools/modelle/neubau/trimsheet_ebenen.py --array=haeuser --steine=<ordner> [--blaetter=<ordner1>;<ordner2>...] [--ziel=<ordner>]
    python -I tools/modelle/neubau/trimsheet_ebenen.py --array=innen --steine=<ordner>
    python -I tools/modelle/neubau/trimsheet_ebenen.py --array=rathaus --steine=<ordner>       (im Branch des Rathauses)
    python -I tools/modelle/neubau/trimsheet_ebenen.py --array=bruecke --steine=<ordner>       (im Branch der Bruecke)

Braucht Python 3 mit numpy und Pillow (kein Blender). <ordner> der Steine enthaelt je Textur einen Unterordner wie beim Abruf
(Poly Haven: <name>_diff_1k.jpg, <name>_nor_gl_1k.jpg, <name>_arm_1k.jpg; ambientCG: <name>_1K-JPG_Color.jpg, _NormalGL.jpg,
_Roughness.jpg, _AmbientOcclusion.jpg); nie im Repository. --blaetter: Ordner, in denen die Blaetter gesucht werden (der erste
Treffer gilt; Standard der Ordner des Arrays), getrennt mit ";". --ziel: Ausgabeordner (Standard der Ordner des Arrays).
--layout=<datei.py>: Datei mit dem Dict der Bandgrenzen, falls sie nicht neben diesem Skript liegt (Probe aus einem anderen Branch).

Ergebnis je Array drei PNG, die Ebenen senkrecht untereinander (Ebene i = Bildzeilen i*2048 bis (i+1)*2048; Import in Godot als
"Texture2DArray" mit slices/horizontal = 1 und slices/vertical = Ebenenzahl – senkrecht, damit die Reihenfolge eindeutig ist):

    <array>_ebenen_albedo.png   sRGB mit Alpha (Strohfransen)       <array>_ebenen_normal.png   Normale (OpenGL)
    <array>_ebenen_orm.png      R Verdeckung, G Rauheit, B Metall

Fuer "haeuser" und "innen" dazu <array>_ao_ebenen.png: die AO-Atlanten der Gruppen ("ao") als weiteres Array, damit Haeuser
verschiedener Voelker ein Material teilen (Reihenfolge wie Steinebenen.AoEbene). Fehlt die .import-Datei eines Arrays, legt das Skript eine an (VRAM-Kompression BPTC,
Mipmaps); Godot ergaenzt sie beim Import.

Bandlayout: Die Baender kommen unveraendert aus dem jeweiligen Blatt (Bandgrenzen in trimsheet.py BAENDER_AUSSEN,
rathaus_trimsheet.py BAENDER_RATHAUS, klappbruecke_trim.py BRUECKE_BAENDER; hier per ast gelesen, nicht abgeschrieben), die UVs
der Modelle bleiben gleich. Ersetzt werden nur die in EBENEN genannten Baender; Holz, Putz, Stroh und Schiefer bleiben, wie sie im
Blatt sind. Ein Stein wird in seiner eigenen Farbe eingesetzt (nicht umgefaerbt), waagerecht nahtlos (2^k Kacheln je Band) und
senkrecht so ausgeschnitten, dass Ober- und Unterkante des Bandes moeglichst auf dunklen Lagerfugen liegen.
"""
import ast
import os
import sys

import numpy as np
from PIL import Image

HIER = os.path.dirname(os.path.abspath(__file__))
WURZEL = os.path.dirname(os.path.dirname(os.path.dirname(HIER)))
NEUBAU = os.path.join(WURZEL, "game", "assets", "texturen", "neubau")
N = 2048

# Steine (K1 bis K5 aus der Auswahl vom 15.11.2026). kachel = reale Groesse (Breite, Hoehe) in Metern laut Anbieter;
# ambientCG nennt keine Groesse (API 0): 2 x 2 m bzw. 2 x 1 m wie in der Probe der Auswahl (Annahme).
STEINE = {
    "k1": dict(name="castle_wall_varriation", art="ph", kachel=(2.0, 2.0)),     # Mensch: Mauern
    "k2": dict(name="large_sandstone_blocks_01", art="ph", kachel=(3.0, 3.0)),  # Elb/Halbling: Flaechen; Bruecke: elbisches Zierwerk
    # K3 stone_wall_04 und K4 PavingStones142 (Auswahl vom 15.11.2026) sind nach der Rueckmeldung des Nutzers nicht mehr eingesetzt
    "k5": dict(name="Bricks100", art="acg", kachel=(2.0, 1.0)),                 # Elb/Halbling: nur Akzent
}

# Je Array: Layout (Datei, Name des Dicts), Ordner der Blaetter und die Ebenen (Name, Blatt, {Band: Stein oder (Stein, Bezugsband)}).
# Haeuser (Entscheidung Nutzer/Teamleiter 15.11.2026): 8 Ebenen = Volk x Stufe; die Reihenfolge gilt auch in Steinebenen.cs und
# building_trim.gdshader (STEIN_EBENE). Nach der Rueckmeldung des Nutzers zum Vergleichsbild (15.11.2026): Mensch S1 behaelt den
# bisherigen Sockelstein (K4 nicht am Sockel; das Aussenblatt hat kein Pflaster- oder Bodenband, K4 kommt also in keiner Ebene vor),
# Mensch S2/S3 Mauer (Band "bruchstein") K1; die Zwerge behalten den bisherigen dunklen Stein (K3 zurueckgenommen); Halbling
# als Platzhalter auf dem Blatt des Steinhauses (Mensch S3), Flaechen K2, Akzent (Werksteinband "putz") K5; Elb Stufe 1 siehe unten. Zwerge Stufe 3 (Auftrag
# 16.11.2026): das Werksteinband "bruchstein" des Blatts trim_s3_zwerg war ein einfarbig brauner 0,4-m-Ausschnitt eines grossen
# Sandsteinquaders; dort jetzt K2 (heller Haustein mit Fugen und Verwitterung, in eigener Farbe); der
# dunkle Quader im Band "putz" bleibt.
# Ein Stein kann als (Stein, Bezugsband) auf Mittel und Farbton eines Bands desselben Blatts getoent werden (derzeit nicht genutzt).
ARRAYS = {
    "haeuser": dict(
        layout=(os.path.join(HIER, "trimsheet.py"), "BAENDER_AUSSEN"), ordner=NEUBAU,
        ebenen=[
            ("mensch_s1", "trim", {}),
            ("mensch_s2", "trim_s2", {"bruchstein": "k1"}),
            ("mensch_s3", "trim_s3", {"bruchstein": "k1"}),
            ("zwerg_s1", "trim_zwerg", {}),
            ("zwerg_s2", "trim_s2_zwerg", {}),
            ("zwerg_s3", "trim_s3_zwerg", {"bruchstein": "k2"}),   # heller Haustein mit Struktur in eigener Farbe (getoent auf den alten Sandstein waere er dunkelbraun)
            # Elben Stufe 1 (Auftrag 17.11.2026): eigenes Blatt trim_elb (Kunstwerk: Fachwerk mit Kalkputz, das Putzband bleibt Putz),
            # Haustein K2 im Bruchsteinband (Sockel, Schlot); K5 kommt in Stufe 1 nicht vor (das Kunstwerk zeigt keinen Backstein)
            ("elb_s1", "trim_elb", {"bruchstein": "k2"}),
            ("halbling", "trim_s3", {"bruchstein": "k2", "putz": "k5"}),
        ],
        # Reihenfolge wie Steinebenen.AoGruppen; Buergerhaus und Steinhaus haben je Volk eigene Atlanten (groessere Zellen), die Elben (Stufe 1) auch
        ao=["wohnhaus_mensch", "wohnhaus_zwerg", "wohnhaus_mensch_s2", "wohnhaus_mensch_s3", "wohnhaus_zwerg_s2", "wohnhaus_zwerg_s3", "wohnhaus_elb"]),
    # Innenraeume (vorerst ohne Steinwahl): dasselbe Array-Format, damit ein Shader fuer alle Gebaeude reicht.
    "innen": dict(
        layout=(os.path.join(HIER, "trimsheet.py"), "BAENDER_INNEN"), ordner=NEUBAU,
        # Reihenfolge wie Steinebenen.InnenBlaetter / InnenAoGruppen; die kleineren AO-Atlanten (512, 1024) werden auf N gebracht
        ebenen=[("innen", "innen_trim", {}), ("innen_s23", "innen_trim_s23", {}), ("raeume", "raeume_innen_trim", {}),
                ("innen_zwerg", "innen_trim_zwerg", {}), ("innen_s23_zwerg", "innen_trim_s23_zwerg", {}), ("raeume_zwerg", "raeume_innen_trim_zwerg", {}),
                ("innen_elb", "innen_trim_elb", {})],
        ao=["wohnhaus_mensch_innen", "wohnhaus_mensch_dachraeume_innen", "wohnhaus_mensch_raeume_innen",
            "wohnhaus_zwerg_innen", "wohnhaus_zwerg_dachraeume_innen", "wohnhaus_zwerg_raeume_innen", "wohnhaus_elb_innen"]),
    # Rathaus (Mensch): Quader auf K1. Eine Ebene, eigenes Layout.
    "rathaus": dict(
        layout=(os.path.join(HIER, "rathaus_trimsheet.py"), "BAENDER_RATHAUS"), ordner=NEUBAU,
        ebenen=[("rathaus", "rathaus_trim", {"quader": "k1"})]),
    # Bruecke (zwergisch-elbisch): dunkler Zwergenstein (die Quader des Blatts) bleibt fuer Pfeiler und Torhaeuser, Werkstein = K2.
    "bruecke": dict(
        layout=(os.path.join(os.path.dirname(HIER), "klappbruecke_trim.py"), "BRUECKE_BAENDER"),
        ordner=os.path.join(WURZEL, "game", "assets", "texturen", "bruecke"),
        ebenen=[("bruecke", "bruecke_trim", {"werkstein": "k2"})]),
}



def layout_lesen(datei, name):
    """Liest ein Dict aus Literalen (Bandgrenzen) aus einer Python-Datei, ohne sie auszufuehren (sie braucht Blender)."""
    with open(datei, encoding="utf-8") as f:
        baum = ast.parse(f.read())
    for knoten in baum.body:
        if isinstance(knoten, ast.Assign) and any(getattr(z, "id", None) == name for z in knoten.targets):
            return ast.literal_eval(knoten.value)
    raise SystemExit(f"{name} nicht in {datei}")


def bild(pfad, kanaele):
    """Bild als float32 [Zeile von oben, Spalte, Kanal] in 0..1."""
    with Image.open(pfad) as b:
        b = b.convert("RGBA" if kanaele == 4 else ("L" if kanaele == 1 else "RGB"))
        a = np.asarray(b, dtype=np.float32) / 255.0
    return a[..., None] if kanaele == 1 else a


def stein_laden(ordner, schluessel):
    s = STEINE[schluessel]
    d = os.path.join(ordner, s["name"])
    n = s["name"]
    if s["art"] == "ph":
        farbe = bild(os.path.join(d, f"{n}_diff_1k.jpg"), 3)
        normale = bild(os.path.join(d, f"{n}_nor_gl_1k.jpg"), 3)
        orm = bild(os.path.join(d, f"{n}_arm_1k.jpg"), 3)          # Poly-Haven-ARM = R Verdeckung, G Rauheit, B Metall
    else:
        farbe = bild(os.path.join(d, f"{n}_1K-JPG_Color.jpg"), 3)
        normale = bild(os.path.join(d, f"{n}_1K-JPG_NormalGL.jpg"), 3)
        ao = bild(os.path.join(d, f"{n}_1K-JPG_AmbientOcclusion.jpg"), 1)
        rau = bild(os.path.join(d, f"{n}_1K-JPG_Roughness.jpg"), 1)
        orm = np.concatenate([ao, rau, np.zeros_like(ao)], axis=2)
    return dict(farbe=farbe, normale=normale, orm=orm, kachel=s["kachel"], name=n)


def groesse(feld, breite, hoehe, normale=False):
    """Skaliert ein Feld (Flaechenmittel beim Verkleinern, bikubisch beim Vergroessern); Normalen werden neu normiert."""
    h, b = feld.shape[:2]
    filt = Image.BOX if breite < b else Image.BICUBIC
    kanaele = [np.asarray(Image.fromarray(feld[..., k]).resize((breite, hoehe), filt), dtype=np.float32) for k in range(feld.shape[2])]
    neu = np.clip(np.stack(kanaele, axis=2), 0.0, 1.0)
    if normale:
        v = neu * 2 - 1
        v /= np.maximum(np.linalg.norm(v, axis=2, keepdims=True), 1e-6)
        neu = v * 0.5 + 0.5
    return neu


def stein_band(stein, hoehe, meter):
    """Ein Band aus dem Stein: volle Blattbreite, waagerecht nahtlos (2^k Kacheln), Fugen an Ober- und Unterkante.
    Rueckgabe Farbe, Normale, ORM und eine Beschreibung (Kacheln, Massstab, Ausschnitt)."""
    kb, kh = stein["kachel"]
    k = max(1, int(2 ** round(np.log2(meter / kb))))          # Kacheln je Bandbreite (Zweierpotenz: teilt 2048 ohne Rest)
    tw = N // k
    massstab = (meter / k) / kb                                # 1 = reale Groesse
    th = max(2, int(round(kh * massstab * N / meter)))         # Kachelhoehe in Bildpunkten des Blatts
    farbe = groesse(stein["farbe"], tw, th)
    normale = groesse(stein["normale"], tw, th, normale=True)
    orm = groesse(stein["orm"], tw, th)
    # Lagerfugen: dunkelste Zeilen; Ober- und Unterkante des Bandes auf moeglichst dunkle Zeilen legen (senkrecht kachelt der Stein)
    hell = (farbe @ np.array([0.2126, 0.7152, 0.0722], dtype=np.float32)).mean(axis=1)
    hell = np.convolve(np.concatenate([hell[-2:], hell, hell[:2]]), np.ones(5) / 5, mode="valid")
    kosten = hell + np.roll(hell, -(hoehe % th))
    ab = int(np.argmin(kosten))
    zeilen = (ab + np.arange(hoehe)) % th
    spalten = np.arange(N) % tw
    hole = lambda f: f[zeilen][:, spalten]
    text = f"{stein['name']}: {k} Kachel(n) je {meter:g} m, Massstab {massstab:.2f}, ab Zeile {ab} von {th}"
    return hole(farbe), hole(normale), hole(orm), text


def linear(c):
    return np.where(c <= 0.04045, c / 12.92, ((c + 0.055) / 1.055) ** 2.4)


def srgb(c):
    c = np.clip(c, 0.0, 1.0)
    return np.where(c <= 0.0031308, c * 12.92, 1.055 * c ** (1 / 2.4) - 0.055)


def lab(feld):
    """Mittel eines sRGB-Felds als (L*, Farbton in Grad, Buntheit C*) – Mittel in linearem Licht, CIELAB mit D65."""
    rgb = linear(feld.reshape(-1, 3)).mean(axis=0)
    xyz = np.array([[0.4124, 0.3576, 0.1805], [0.2126, 0.7152, 0.0722], [0.0193, 0.1192, 0.9505]]) @ rgb
    xyz = xyz / np.array([0.95047, 1.0, 1.08883])
    f = np.where(xyz > (6 / 29) ** 3, np.cbrt(xyz), xyz / (3 * (6 / 29) ** 2) + 4 / 29)
    l_, a_, b_ = 116 * f[1] - 16, 500 * (f[0] - f[1]), 200 * (f[1] - f[2])
    return float(l_), float(np.degrees(np.arctan2(b_, a_)) % 360), float(np.hypot(a_, b_))


def toenen(farbe, bezug):
    """Toent einen Stein auf den Ton eines Bezugssteins: jeder Kanal in linearem Licht mit dem Verhaeltnis der Mittelwerte
    multipliziert – Mittel und Farbton wie der Bezug, die Zeichnung (Fugen, Steine) bleibt im Verhaeltnis erhalten."""
    lin = linear(farbe)
    faktor = linear(bezug.reshape(-1, 3)).mean(axis=0) / np.maximum(lin.reshape(-1, 3).mean(axis=0), 1e-5)
    return srgb(lin * faktor[None, None, :]).astype(np.float32)


def blatt_finden(ordner_liste, blatt):
    for o in ordner_liste:
        p = [os.path.join(o, f"{blatt}_{art}.png") for art in ("albedo", "normal", "orm")]
        if all(os.path.exists(x) for x in p):
            return p
    raise SystemExit(f"Blatt {blatt} nicht gefunden in: " + "; ".join(ordner_liste))


def speichere(feld, pfad, modus):
    a = (np.clip(feld, 0, 1) * 255 + 0.5).astype(np.uint8)
    Image.fromarray(a, modus).save(pfad, compress_level=6)


IMPORT_VORLAGE = """[remap]

importer="2d_array_texture"
type="CompressedTexture2DArray"

[deps]

source_file="{quelle}"

[params]

compress/mode=2
compress/high_quality=true
compress/lossy_quality=0.7
compress/hdr_compression=1
compress/channel_pack=0
mipmaps/generate=true
mipmaps/limit=-1
slices/horizontal=1
slices/vertical={ebenen}
"""


def import_anlegen(pfad, ebenen, verlustfrei=False):
    """Legt die .import-Datei an, wenn sie fehlt (VRAM-Kompression BPTC mit Mipmaps; AO wie bisher verlustfrei)."""
    imp = pfad + ".import"
    if os.path.exists(imp):
        return
    rel = os.path.relpath(pfad, os.path.join(WURZEL, "game")).replace(os.sep, "/")
    text = IMPORT_VORLAGE.format(quelle="res://" + rel, ebenen=ebenen)
    if verlustfrei:
        text = text.replace("compress/mode=2", "compress/mode=0").replace("compress/high_quality=true", "compress/high_quality=false")
    with open(imp, "w", encoding="utf-8", newline="\n") as f:
        f.write(text)


def baue(array, steine_ordner, blaetter, ziel, layout=None):
    cfg = ARRAYS[array]
    baender = layout_lesen(layout or cfg["layout"][0], cfg["layout"][1])
    ordner = blaetter or [cfg["ordner"]]
    ziel = ziel or cfg["ordner"]
    alb, nor, orm = [], [], []
    cache = {}
    for name, blatt, ersatz in cfg["ebenen"]:
        pa, pn, po = blatt_finden(ordner, blatt)
        a, n, o = bild(pa, 4), bild(pn, 3), bild(po, 3)
        if a.shape[:2] != (N, N) or n.shape[:2] != (N, N) or o.shape[:2] != (N, N):
            raise SystemExit(f"{blatt}: Blatt nicht {N} x {N}")
        print(f"Ebene {len(alb)} {name}: Blatt {pa}")
        original = a.copy()
        for band, wahl in ersatz.items():
            schluessel, ton = (wahl, None) if isinstance(wahl, str) else wahl
            v0, v1, meter = baender[band]
            z0, z1 = int(round(v0 * N)), int(round(v1 * N))
            if schluessel not in cache:
                cache[schluessel] = stein_laden(steine_ordner, schluessel)
            f, nn, oo, text = stein_band(cache[schluessel], z1 - z0, meter)
            if ton:
                r0, r1 = (int(round(x * N)) for x in baender[ton][:2])
                ref = original[r0:r1, :, :3]
                vorher_lab = lab(f)
                f = toenen(f, ref)
                text += (f"; getoent auf Band {ton}: L* {vorher_lab[0]:.1f} -> {lab(f)[0]:.1f} (Bezug {lab(ref)[0]:.1f}), "
                         f"Farbton {vorher_lab[1]:.0f}° -> {lab(f)[1]:.0f}° (Bezug {lab(ref)[1]:.0f}°), "
                         f"Buntheit {vorher_lab[2]:.1f} -> {lab(f)[2]:.1f} (Bezug {lab(ref)[2]:.1f})")
            a[z0:z1, :, :3] = f
            a[z0:z1, :, 3] = 1.0
            n[z0:z1] = nn
            o[z0:z1] = oo
            print(f"   Band {band} (Zeilen {z0}-{z1}): {schluessel} {text}")
        alb.append(a); nor.append(n); orm.append(o)
    os.makedirs(ziel, exist_ok=True)
    zahl = len(alb)
    for art, felder, modus in (("albedo", alb, "RGBA"), ("normal", nor, "RGB"), ("orm", orm, "RGB")):
        pfad = os.path.join(ziel, f"{array}_ebenen_{art}.png")
        speichere(np.concatenate(felder, axis=0), pfad, modus)
        import_anlegen(pfad, zahl)
        print(f"geschrieben: {pfad} ({N} x {N * zahl}, {zahl} Ebenen)")
    if cfg.get("ao"):
        atlanten = []
        for g in cfg["ao"]:
            p = next((os.path.join(o_, f"{g}_ao_atlas.png") for o_ in ordner if os.path.exists(os.path.join(o_, f"{g}_ao_atlas.png"))), None)
            if p is None:
                raise SystemExit(f"AO-Atlas {g} nicht gefunden")
            a_ = bild(p, 3)
            if a_.shape[:2] != (N, N):   # kleinere Atlanten (Raeume) hochskalieren: dieselben UVs, AO ist glatt
                a_ = np.asarray(Image.fromarray((a_ * 255 + 0.5).astype(np.uint8), "RGB").resize((N, N), Image.BILINEAR), dtype=np.float32) / 255.0
            atlanten.append(a_)
            print(f"AO-Ebene {len(atlanten) - 1} {g}: {p}")
        if len({x.shape for x in atlanten}) != 1:
            raise SystemExit("AO-Atlanten verschieden gross")
        pfad = os.path.join(ziel, f"{array}_ao_ebenen.png")
        speichere(np.concatenate(atlanten, axis=0), pfad, "RGB")
        import_anlegen(pfad, len(atlanten), verlustfrei=True)
        print(f"geschrieben: {pfad} ({len(atlanten)} Ebenen)")


if __name__ == "__main__":
    opt = dict((a[2:].split("=", 1) + [""])[:2] for a in sys.argv[1:] if a.startswith("--"))
    if opt.get("array") not in ARRAYS or not opt.get("steine"):
        raise SystemExit(__doc__)
    blaetter = [x for x in opt.get("blaetter", "").split(";") if x]
    baue(opt["array"], opt["steine"], blaetter, opt.get("ziel"), opt.get("layout"))
