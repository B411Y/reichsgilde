"""Prüft eine GLB-Datei gegen die Konventionen der Gebäudemodelle. Braucht kein Blender.

    python tools/modelle/pruefen.py game/assets/modelle/gebaeude/fachwerkhaus.glb 8 10

Gibt Knoten, Materialien, Dreiecke je Knoten und die Ausdehnung (Y oben, Vorderseite +Z) aus
und meldet Verstöße: Material „Hausfarbe“ fehlt, Modell ragt über die belegte Fläche oder
unter den Boden, ein Zustandsknoten fehlt. Hat das Modell Stufen (Werkstatt), müssen es genau
Stufe1–Stufe3 sein; dann steht auch da, wie viele Dreiecke je Stufe sichtbar sind (Haus + Stufe).
Hat das Modell Varianten (Einwohnerhäuser), müssen sie Variante1, Variante2 … heißen; Zustandsknoten
sind dann nicht nötig, weil Einwohnerhäuser weder leer stehen noch ausgebaut werden.
Mit `--sonderbau` (Dom, Rathaus, Stadtmauer) sind weder Zustände noch „Hausfarbe“ nötig: Sonderbauten gehören der Stadt.
Mit `--umland` (Grube, Holzschlag … Wachturm) braucht das Modell „Haus“, „Arbeit“, „Fahne“ und „Leerstand“, aber kein
Gerüst: Umlandbetriebe werden nicht ausgebaut.
Mit `--natur` (Bäume, Büsche, Boden, Steine, Totholz) sind weder Hausfarbe noch Zustände noch Anker nötig; B und T
sind dann die größte erlaubte Ausdehnung eines einzelnen Knotens (jeder steht mit dem Fuß im Nullpunkt).
Ankerpunkte („Anker_…“, leere Knoten) werden mit Lage und Blickrichtung gelistet; „Anker_Tuer“ ist Pflicht
und jeder Anker muss auf der Fläche liegen.
Mit `--innen` (Innenräume, z. B. schmied_innen.glb) gelten eigene Regeln: Räume „Raum_<Name>“, Stufe1–Stufe3, je Raum
ein Blickpunkt „Blick_<Raum>_0“ (weitere „_L1“, „_R1“, „_O1“, „_U1“ …), Bedienung für Personal, Produktion, Lager,
Einrichten und Karren, Türen paarweise („Tuer_A_B“ und „Tuer_B_A“), Einrichtung standardmäßig aus (und „Holztuer“, wenn es
„Einrichtung_Eisentuer“ gibt), Arbeitsplätze „Arbeit_<n>_<Bewegung>“ (n lückenlos ab 1, die Bewegung muss es in
figur_mensch.glb geben; extras „requisit“ – das Werkzeug statt dem der Bewegung – muss „keins“ oder ein Knoten von
figur_mensch.glb sein), Stellplätze „Lager_<n>“ und Werkstücke „Werkstueck_<n>“ lückenlos ab 1; alle Anker und
Blickpunkte liegen in einem Raum. Die Lage rechnet die Knotenhierarchie mit ein. Ein Werkstück darf statt eines leeren
Knotens ein Netz sein (beim Alchimisten die Flüssigkeit, die das Spiel färbt); Stellplätze der Auslage „Auslage_<n>“ (im
Laden) und Dampfquellen „Dampf_<n>“ sind freiwillig, gibt es sie, laufen sie lückenlos ab 1. Ebenso freiwillig (Wirt): Plätze
der Gäste „Gast_<n>“, „Gast_<Gruppe>_<n>“ (je Gruppe lückenlos ab 1), zu jedem ein „Krug_…“ mit derselben Endung, und Plätze
des Barden „Barde_<n>“; ebenso (Prediger) Plätze der Gläubigen „Glaeubig_<n>“ (mit extras „sitzhoehe“), des Predigers auf der
Kanzel „Predigt_<n>“ und der Beichte „Beichte_<n>“, je lückenlos ab 1; ebenso (Dieb) Plätze der Diebe „Dieb_<n>“ und des
Meisterdiebs „Meister_<n>“ und (Gardist) Plätze der Wachen „Wache_<n>“, der Gefangenen „Gefangen_<n>“ und des Hauptmanns
„Hauptmann_<n>“, je lückenlos ab 1, mit extras „taetigkeit“ und „bewegung“ (die Bewegung muss es in figur_mensch.glb geben;
sitzende Bewegungen brauchen „sitzhoehe“). Hat ein Innenraum Plätze der Diebe oder der Wachen, stellt er keine Ware her:
Arbeitsplätze und Werkstücke sind dann nicht nötig, und statt der Bedienung für die Produktion genügt eine für die Aufträge
(„Bedienung_Auftraege_…“, Dieb) bzw. die Wache („Bedienung_Wache_…“, Gardist).
Ein Raum mit extras „stufe“ (z. B. die Sägehalle des Tischlers ab Stufe 3, Laden und Turm des Alchimisten) gibt es erst ab
dieser Stufe: Anker darin und Türen dorthin brauchen mindestens dieselbe „stufe“. Späne „Spaene1“–„Spaene3“ (falls vorhanden) sind lückenlos und
standardmäßig aus. Hat die Datei Animationen, muss eine „Arbeit“ heißen; sie darf weder Räume noch Stufen, Anker oder
Blickpunkte bewegen.
"""

import math
import os
import re

import json
import struct
import sys

ZUSTAENDE = ("Leerstand", "Geruest")


def lesen(pfad):
    with open(pfad, "rb") as f:
        daten = f.read()
    laenge = struct.unpack_from("<I", daten, 12)[0]
    return json.loads(daten[20:20 + laenge])


def _matrix(knoten):
    """4×4-Matrix (Zeilen) eines glTF-Knotens aus matrix oder translation/rotation/scale."""
    if "matrix" in knoten:
        m = knoten["matrix"]
        return [[m[c * 4 + r] for c in range(4)] for r in range(4)]
    tx, ty, tz = knoten.get("translation", [0, 0, 0])
    x, y, z, w = knoten.get("rotation", [0, 0, 0, 1])
    sx, sy, sz = knoten.get("scale", [1, 1, 1])
    r = [[1 - 2 * (y * y + z * z), 2 * (x * y - z * w), 2 * (x * z + y * w)],
         [2 * (x * y + z * w), 1 - 2 * (x * x + z * z), 2 * (y * z - x * w)],
         [2 * (x * z - y * w), 2 * (y * z + x * w), 1 - 2 * (x * x + y * y)]]
    return [[r[0][0] * sx, r[0][1] * sy, r[0][2] * sz, tx], [r[1][0] * sx, r[1][1] * sy, r[1][2] * sz, ty],
            [r[2][0] * sx, r[2][1] * sy, r[2][2] * sz, tz], [0, 0, 0, 1]]


def _mal(a, b):
    return [[sum(a[i][k] * b[k][j] for k in range(4)) for j in range(4)] for i in range(4)]


def _welt(g):
    """Weltmatrix je Knotenindex (Eltern eingerechnet)."""
    eltern = {k: i for i, n in enumerate(g["nodes"]) for k in n.get("children", [])}
    welt = {}

    def rechne(i):
        if i not in welt:
            m = _matrix(g["nodes"][i])
            welt[i] = _mal(rechne(eltern[i]), m) if i in eltern else m
        return welt[i]
    for i in range(len(g["nodes"])):
        rechne(i)
    return welt


def innen(pfad, g):
    """Prüfung für Innenräume (siehe oben). Gibt die Liste der Fehler zurück."""
    fehler = []
    welt = _welt(g)
    knoten = {n.get("name", ""): i for i, n in enumerate(g["nodes"])}
    print("Materialien:", ", ".join(m["name"] for m in g.get("materials", [])))
    raeume = {}
    summe = 0
    for name, i in sorted(knoten.items()):
        n = g["nodes"][i]
        if "mesh" not in n:
            continue
        dreiecke = sum(g["accessors"][p["indices"]]["count"] // 3 for p in g["meshes"][n["mesh"]]["primitives"] if "indices" in p)
        summe += dreiecke
        print(f"  {name:36s} {dreiecke:6d}  {n.get('extras', '')}")
        if name.startswith("Raum_"):
            ecken = []
            for p in g["meshes"][n["mesh"]]["primitives"]:
                acc = g["accessors"][p["attributes"]["POSITION"]]
                for x in (acc["min"][0], acc["max"][0]):
                    for y in (acc["min"][1], acc["max"][1]):
                        for z in (acc["min"][2], acc["max"][2]):
                            m = welt[i]
                            ecken.append([sum(m[r][c] * v for c, v in enumerate((x, y, z, 1))) for r in range(3)])
            raeume[name[5:]] = ([min(e[k] for e in ecken) for k in range(3)], [max(e[k] for e in ecken) for k in range(3)])
    print(f"  {'gesamt':36s} {summe:6d}")
    if not raeume:
        fehler.append("kein Raum „Raum_<Name>“")
    for k in (1, 2, 3):
        if f"Stufe{k}" not in knoten:
            fehler.append(f"Stufe{k} fehlt")

    def raum_bei(p):
        return next((r for r, (lo, hi) in raeume.items() if all(lo[k] - 1e-3 <= p[k] <= hi[k] + 1e-3 for k in range(3))), None)

    bewegungen, teile = None, None
    figur = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(pfad))), "figuren", "figur_mensch.glb")   # neben innen/
    if os.path.exists(figur):
        figur_glb = lesen(figur)
        bewegungen = {a["name"] for a in figur_glb.get("animations", [])}
        teile = {n.get("name") for n in figur_glb["nodes"]}
    print("Anker und Blickpunkte (Spielkoordinaten, Blick = −Z):")
    reihen = {"Arbeit": [], "Lager": [], "Werkstueck": [], "Auslage": [], "Dampf": []}
    for name, i in sorted(knoten.items()):
        if not ist_anker(g, name, i):
            continue
        m = welt[i]
        p = [m[0][3], m[1][3], m[2][3]]
        vor = [-m[0][2], -m[1][2], -m[2][2]]
        laenge = math.sqrt(sum(v * v for v in vor)) or 1.0
        vor = [v / laenge for v in vor]
        gier = math.degrees(math.atan2(-vor[0], -vor[2]))
        neigung = math.degrees(math.asin(max(-1.0, min(1.0, vor[1]))))
        netz = "  Netz " + ", ".join(g["materials"][q["material"]]["name"] for q in g["meshes"][g["nodes"][i]["mesh"]]["primitives"]
                                     if "material" in q) if "mesh" in g["nodes"][i] else ""
        print(f"  {name:22s} bei X {p[0]:6.2f}  Y {p[1]:5.2f}  Z {p[2]:6.2f}  Blick {gier:6.1f}° waagrecht, {neigung:5.1f}° hoch"
              f"  {g['nodes'][i].get('extras', '')}{netz}")
        raum = raum_bei(p)
        if raum is None:
            fehler.append(f"{name} liegt in keinem Raum")
        if name.startswith("Blick_"):
            teile = re.fullmatch(r"Blick_(.+)_(0|[LROU]\d+)", name)
            if not teile or teile.group(1) not in raeume:
                fehler.append(f"{name}: Name muss Blick_<Raum>_<0|L1|R1|O1|U1 …> sein")
            elif raum != teile.group(1):
                fehler.append(f"{name} liegt nicht im Raum {teile.group(1)}")
        for art in reihen:
            treffer = re.fullmatch(art + r"_(\d+)(?:_(.+))?", name)
            if treffer:
                reihen[art].append(int(treffer.group(1)))
                if art == "Arbeit" and bewegungen is not None and treffer.group(2) not in bewegungen:
                    fehler.append(f"{name}: Bewegung „{treffer.group(2)}“ gibt es in figur_mensch.glb nicht")
                requisit = g["nodes"][i].get("extras", {}).get("requisit")
                if art == "Arbeit" and requisit is not None and teile is not None and requisit != "keins" and requisit not in teile:
                    fehler.append(f"{name}: Requisit „{requisit}“ gibt es in figur_mensch.glb nicht (oder „keins“)")
    for raum in raeume:
        if f"Blick_{raum}_0" not in knoten:
            fehler.append(f"Blickpunkt „Blick_{raum}_0“ fehlt")
    fehler += innen_stufen(g, knoten, welt, raeume, raum_bei)
    ohne_ware = any(re.fullmatch(r"(Dieb|Wache)_\d+", n) for n in knoten)
    for art, nummern in reihen.items():
        frei = ("Auslage", "Dampf") + (("Arbeit", "Werkstueck") if ohne_ware else ())
        if sorted(nummern) != list(range(1, len(nummern) + 1)) or (not nummern and art not in frei):
            fehler.append(f"{art}_<n>: Nummern müssen lückenlos ab 1 laufen, gefunden {sorted(nummern)}")
    print("Arbeitsplätze {}, Stellplätze {}, Werkstücke {}, Auslage {}, Dampf {}".format(
        *(len(reihen[a]) for a in ("Arbeit", "Lager", "Werkstueck", "Auslage", "Dampf"))))
    fehler += innen_gaeste(knoten)
    fehler += innen_glaeubige(g, knoten)
    fehler += innen_diebe(g, knoten, bewegungen)
    for art in ("Personal", "Produktion", "Lager", "Einrichten", "Karren"):
        ersatz = ("Auftraege", "Wache") if art == "Produktion" and ohne_ware else (art,)
        if not any(n.startswith((f"Bedienung_{art}_",) + tuple(f"Bedienung_{e}_" for e in ersatz)) for n in knoten):
            fehler.append(f"keine Bedienung „Bedienung_{art}_…“")
    for name in knoten:
        tuer = re.fullmatch(r"Tuer_(.+?)_(.+)", name)
        if tuer:
            a, b = tuer.groups()
            if a not in raeume or b not in raeume:
                fehler.append(f"{name}: Räume {a} und {b} muss es als Raum_{a}, Raum_{b} geben")
            if f"Tuer_{b}_{a}" not in knoten:
                fehler.append(f"{name}: Gegentür Tuer_{b}_{a} fehlt")
        if name.startswith("Einrichtung_") and g["nodes"][knoten[name]].get("extras", {}).get("sichtbar", True):
            fehler.append(f"{name} ist nicht als unsichtbar markiert")
    if "Einrichtung_Eisentuer" in knoten and "Holztuer" not in knoten:
        fehler.append("Einrichtung_Eisentuer ohne Holztuer")
    fehler += innen_spaene_und_animation(g, knoten)
    return fehler


PLAETZE = ("Dieb", "Meister", "Wache", "Gefangen", "Hauptmann")   # Plätze mit extras „taetigkeit“ und „bewegung“
ANKER = ("Blick_", "Arbeit_", "Lager_", "Werkstueck_", "Licht_", "Auslage_", "Dampf_", "Gast_", "Krug_", "Barde_", "Glaeubig_", "Predigt_",
         "Beichte_") + tuple(p + "_" for p in PLAETZE)
SITZEND = ("Sitzen", "Kutschieren", "Feinarbeit", "Trinken", "Schreiben", "Beten", "Zaehlen", "Putzen")   # Bewegungen im Sitzen


def ist_anker(g, name, i):
    """Anker und Blickpunkte sind leere Knoten; nur ein Werkstück darf ein Netz sein."""
    return name.startswith(ANKER) and ("mesh" not in g["nodes"][i] or name.startswith("Werkstueck_"))


def innen_gaeste(knoten):
    """Plätze der Gäste (Gast_<n>, Gast_<Gruppe>_<n>) je Gruppe lückenlos ab 1, zu jedem ein Krug mit derselben Endung;
    Plätze des Barden (Barde_<n>) lückenlos ab 1."""
    fehler = []
    gruppen = {}
    for name in knoten:
        treffer = re.fullmatch(r"(Gast|Barde)_(?:(\D\w*?)_)?(\d+)", name)
        if treffer:
            gruppen.setdefault((treffer.group(1), treffer.group(2) or ""), []).append(int(treffer.group(3)))
            if treffer.group(1) == "Gast" and "Krug_" + name[5:] not in knoten:
                fehler.append(f"{name}: Krug_{name[5:]} fehlt")
        elif name.startswith(("Gast_", "Barde_")):
            fehler.append(f"{name}: Name muss Gast_<n>, Gast_<Gruppe>_<n> oder Barde_<n> sein")
        if name.startswith("Krug_") and "Gast_" + name[5:] not in knoten:
            fehler.append(f"{name}: Gast_{name[5:]} fehlt")
    for (art, gruppe), nummern in sorted(gruppen.items()):
        if sorted(nummern) != list(range(1, len(nummern) + 1)):
            fehler.append(f"{art}_{gruppe + '_' if gruppe else ''}<n>: Nummern müssen lückenlos ab 1 laufen, gefunden {sorted(nummern)}")
    if gruppen:
        print("Gäste und Barde: " + ", ".join(f"{art}_{gruppe + '_' if gruppe else ''}1–{len(n)}" for (art, gruppe), n in sorted(gruppen.items())))
    return fehler


def innen_glaeubige(g, knoten):
    """Plätze der Gläubigen (Glaeubig_<n>, mit extras „sitzhoehe“), des Predigers (Predigt_<n>) und der Beichte (Beichte_<n>),
    je lückenlos ab 1."""
    fehler = []
    reihen = {"Glaeubig": [], "Predigt": [], "Beichte": []}
    for name, i in knoten.items():
        treffer = re.fullmatch(r"(Glaeubig|Predigt|Beichte)_(\d+)", name)
        if treffer:
            reihen[treffer.group(1)].append(int(treffer.group(2)))
            if treffer.group(1) == "Glaeubig" and "sitzhoehe" not in g["nodes"][i].get("extras", {}):
                fehler.append(f"{name}: extras „sitzhoehe“ fehlt")
        elif name.startswith(("Glaeubig_", "Predigt_", "Beichte_")):
            fehler.append(f"{name}: Name muss Glaeubig_<n>, Predigt_<n> oder Beichte_<n> sein")
    for art, nummern in reihen.items():
        if sorted(nummern) != list(range(1, len(nummern) + 1)):
            fehler.append(f"{art}_<n>: Nummern müssen lückenlos ab 1 laufen, gefunden {sorted(nummern)}")
    if any(reihen.values()):
        print("Gläubige, Kanzel, Beichte: " + ", ".join(f"{art}_1–{len(n)}" for art, n in reihen.items() if n))
    return fehler


def innen_diebe(g, knoten, bewegungen):
    """Plätze der Diebe (Dieb_<n>), des Meisterdiebs (Meister_<n>), der Wachen (Wache_<n>), der Gefangenen (Gefangen_<n>) und
    des Hauptmanns (Hauptmann_<n>), je lückenlos ab 1, mit extras „taetigkeit“ und „bewegung“ (die es in figur_mensch.glb gibt);
    sitzende brauchen „sitzhoehe“. Druckt je Tätigkeit die Plätze in Füllreihenfolge."""
    fehler = []
    reihen = {p: [] for p in PLAETZE}
    taetigkeiten = {}
    for name, i in knoten.items():
        treffer = re.fullmatch(r"(" + "|".join(PLAETZE) + r")_(\d+)", name)
        if not treffer:
            if name.startswith(tuple(p + "_" for p in PLAETZE)):
                fehler.append(f"{name}: Name muss " + ", ".join(f"{p}_<n>" for p in PLAETZE) + " sein")
            continue
        reihen[treffer.group(1)].append(int(treffer.group(2)))
        extras = g["nodes"][i].get("extras", {})
        for feld in ("taetigkeit", "bewegung"):
            if feld not in extras:
                fehler.append(f"{name}: extras „{feld}“ fehlt")
        if bewegungen is not None and extras.get("bewegung") not in bewegungen:
            fehler.append(f"{name}: Bewegung „{extras.get('bewegung')}“ gibt es in figur_mensch.glb nicht")
        if extras.get("bewegung") in SITZEND and "sitzhoehe" not in extras:
            fehler.append(f"{name}: sitzt („{extras.get('bewegung')}“), extras „sitzhoehe“ fehlt")
        taetigkeiten.setdefault(extras.get("taetigkeit"), []).append((extras.get("platz", 0), name, extras.get("bewegung")))
    for art, nummern in reihen.items():
        if sorted(nummern) != list(range(1, len(nummern) + 1)):
            fehler.append(f"{art}_<n>: Nummern müssen lückenlos ab 1 laufen, gefunden {sorted(nummern)}")
    for taetigkeit, liste in sorted(taetigkeiten.items(), key=lambda e: str(e[0])):
        print(f"Tätigkeit {taetigkeit}: " + ", ".join(f"{n} ({b})" for _, n, b in sorted(liste)))
    return fehler


def innen_stufen(g, knoten, welt, raeume, raum_bei):
    """Räume ab einer Stufe (extras „stufe“): Anker darin und Türen dorthin brauchen mindestens dieselbe Stufe."""
    fehler = []

    def stufe(name):
        return g["nodes"][knoten[name]].get("extras", {}).get("stufe", 1)
    ab = {r: stufe(f"Raum_{r}") for r in raeume}
    for raum, k in ab.items():
        if k > 1:
            print(f"Raum_{raum} erst ab Stufe {k}")
    for name, i in knoten.items():
        if not ist_anker(g, name, i):
            continue
        m = welt[i]
        raum = raum_bei([m[0][3], m[1][3], m[2][3]])
        if raum is not None and stufe(name) < ab[raum]:
            fehler.append(f"{name} liegt in Raum_{raum} (ab Stufe {ab[raum]}), hat aber stufe {stufe(name)}")
    for name in knoten:
        tuer = re.fullmatch(r"Tuer_(.+?)_(.+)", name)
        if tuer and all(r in ab for r in tuer.groups()):
            noetig = max(ab[r] for r in tuer.groups())
            if stufe(name) < noetig:
                fehler.append(f"{name} führt in einen Raum ab Stufe {noetig}, hat aber stufe {stufe(name)}")
    return fehler


def innen_spaene_und_animation(g, knoten):
    """Späne „Spaene1“–„Spaene3“ lückenlos und aus; die Animation „Arbeit“ bewegt nur Teile, keine Räume oder Anker."""
    fehler = []
    spaene = sorted(int(n[6:]) for n in knoten if re.fullmatch(r"Spaene\d+", n))
    if spaene:
        print("Späne:", ", ".join(f"Spaene{k}" + "".join(f" (+ {g['nodes'][c]['name']}, stufe {g['nodes'][c].get('extras', {}).get('stufe', 1)})"
                                                         for c in g["nodes"][knoten[f"Spaene{k}"]].get("children", []))
                                    for k in spaene))
        if spaene != list(range(1, len(spaene) + 1)):
            fehler.append(f"Spaene<n>: Nummern müssen lückenlos ab 1 laufen, gefunden {spaene}")
        for k in spaene:
            if g["nodes"][knoten[f"Spaene{k}"]].get("extras", {}).get("sichtbar", True):
                fehler.append(f"Spaene{k} ist nicht als unsichtbar markiert")
    animationen = g.get("animations", [])
    if animationen and "Arbeit" not in [a.get("name") for a in animationen]:
        fehler.append("Animationen vorhanden, aber keine heißt „Arbeit“")
    for a in animationen:
        ziele = sorted({(g["nodes"][c["target"]["node"]].get("name", ""), c["target"]["path"]) for c in a["channels"]})
        laenge = max(g["accessors"][s["input"]]["max"][0] for s in a["samplers"])
        print(f"Animation „{a.get('name')}“: {laenge:.2f} s, bewegt " + ", ".join(f"{n} ({p})" for n, p in ziele))
        for n, _ in ziele:
            if re.match(r"(Raum|Stufe\d|Blick|Arbeit|Lager|Werkstueck|Licht|Bedienung|Tuer|Einrichtung)_?", n):
                fehler.append(f"Animation „{a.get('name')}“ bewegt {n} – nur eigene Teile dürfen sich bewegen")
    return fehler


def main():
    if "--innen" in sys.argv:
        pfad = [a for a in sys.argv[1:] if not a.startswith("--")][0]
        fehler = innen(pfad, lesen(pfad))
        print("in Ordnung" if not fehler else "FEHLER:\n  " + "\n  ".join(fehler))
        sys.exit(1 if fehler else 0)
    sonderbau = "--sonderbau" in sys.argv
    umland = "--umland" in sys.argv
    natur = "--natur" in sys.argv
    sonderbau = sonderbau or natur
    argumente = [a for a in sys.argv[1:] if not a.startswith("--")]
    pfad = argumente[0]
    breite, tiefe = (float(a) for a in argumente[1:3]) if len(argumente) >= 3 else (None, None)
    g = lesen(pfad)
    fehler = []
    materialien = [m["name"] for m in g.get("materials", [])]
    print("Materialien:", ", ".join(materialien))
    if "Hausfarbe" not in materialien and not sonderbau:
        fehler.append("Material „Hausfarbe“ fehlt")

    mins, maxs = [1e9] * 3, [-1e9] * 3
    print(f"{'Knoten':12s} {'Dreiecke':>8s}  Extras")
    namen = []
    dreiecke_je = {}
    summe = 0
    anker = []
    for knoten in g["nodes"]:
        if "mesh" not in knoten:
            if knoten.get("name", "").startswith("Anker_"):
                anker.append(knoten)
            continue
        namen.append(knoten["name"])
        dreiecke = 0
        for prim in g["meshes"][knoten["mesh"]]["primitives"]:
            if "indices" in prim:
                dreiecke += g["accessors"][prim["indices"]]["count"] // 3
            pos = g["accessors"][prim["attributes"]["POSITION"]]
            versatz = knoten.get("translation", [0, 0, 0])
            for i in range(3):
                mins[i] = min(mins[i], pos["min"][i] + versatz[i])
                maxs[i] = max(maxs[i], pos["max"][i] + versatz[i])
        summe += dreiecke
        dreiecke_je[knoten["name"]] = dreiecke
        print(f"{knoten['name']:12s} {dreiecke:8d}  {knoten.get('extras', '')}")
    print(f"{'gesamt':12s} {summe:8d}")
    print("Ausdehnung X {:.2f} … {:.2f}   Y {:.2f} … {:.2f}   Z {:.2f} … {:.2f}".format(
        mins[0], maxs[0], mins[1], maxs[1], mins[2], maxs[2]))

    stufen = sorted(n for n in namen if n.startswith("Stufe"))
    if stufen:
        if stufen != ["Stufe1", "Stufe2", "Stufe3"]:
            fehler.append("Stufen müssen genau Stufe1, Stufe2, Stufe3 heißen, gefunden: " + ", ".join(stufen))
        fest = sum(n for k, n in dreiecke_je.items() if k in ("Haus", "Wappen", "Fahne"))
        print("sichtbar je Stufe (Haus, Wappen, Fahne + Stufe): " +
              ", ".join(f"{k} {fest + dreiecke_je[k]}" for k in stufen))
    varianten = sorted(n for n in namen if n.startswith("Variante"))
    if varianten:
        if varianten != [f"Variante{i}" for i in range(1, len(varianten) + 1)]:
            fehler.append("Varianten müssen Variante1, Variante2 … heißen, gefunden: " + ", ".join(varianten))
        print("sichtbar je Variante: " + ", ".join(f"{k} {dreiecke_je[k]}" for k in varianten))
    for k in anker:
        x, y, z = k.get("translation", [0, 0, 0])
        qx, qy, qz, qw = k.get("rotation", [0, 0, 0, 1])
        # Blickrichtung = lokal −Z, gedreht mit dem Quaternion (nur um die Hochachse)
        winkel = math.degrees(2 * math.atan2(qy, qw))
        richtung = (-math.sin(math.radians(winkel)), -math.cos(math.radians(winkel)))
        print(f"{k['name']:14s} bei X {x:.2f}  Y {y:.2f}  Z {z:.2f}, Blick ins Haus nach ({round(richtung[0]) + 0}, {round(richtung[1]) + 0})")
        if breite is not None and (abs(x) > breite / 2 or abs(z) > tiefe / 2):
            fehler.append(f"{k['name']} liegt außerhalb der Fläche")
    if "Anker_Tuer" not in [k["name"] for k in anker] and not natur:
        fehler.append("Ankerpunkt „Anker_Tuer“ fehlt")
    for z in (("Haus", "Arbeit", "Fahne", "Leerstand") if umland else ZUSTAENDE):
        if z not in namen and not varianten and not sonderbau:
            fehler.append(f"Zustandsknoten „{z}“ fehlt")
    if mins[1] < -0.06:
        fehler.append(f"ragt {-mins[1]:.2f} m unter den Boden")
    if breite is not None:
        if max(-mins[0], maxs[0]) > breite / 2 + 1e-3:
            fehler.append(f"breiter als {breite} m (X bis ±{max(-mins[0], maxs[0]):.2f})")
        if max(-mins[2], maxs[2]) > tiefe / 2 + 1e-3:
            fehler.append(f"tiefer als {tiefe} m (Z bis ±{max(-mins[2], maxs[2]):.2f})")
    print("in Ordnung" if not fehler else "FEHLER:\n  " + "\n  ".join(fehler))
    sys.exit(1 if fehler else 0)


if __name__ == "__main__":
    main()
