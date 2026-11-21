"""Echtes Fachwerk (Nutzer 21.11.2026): Streben, Baender und Figuren auf dem Grundgeruest einer Fachwerkwand.

Grundlage: C:\\Reichsgilde_Austausch\\fachwerk\\recherche.md (rheinisch-fraenkisches Fachwerk des spaeten Mittelalters, Stockwerkbau).
Das Grundgeruest (Schwelle, Staender, Raehm, Brust- und Sturzriegel) baut das jeweilige Skript; hier kommen dazu:
  * Stufe 1: geschosshohe Fussstreben in den Eckgefachen (vom Eckstaender unten nach innen oben).
  * Stufe 2 (Nutzer 21.11.2026): Mann-Figuren am Eckstaender (halber Mann, nach innen) und an einem gewuerfelten Bundstaender (ganzer
    Fraenkischer Mann: dreiviertelgeschosshohe Fussstrebe und Kopfwinkelholz je Seite), Kopfstrebe in einem Eckgefach mit Fenster,
    Kopfbaender an den uebrigen Staendern, Andreaskreuz oder Raute in einzelnen fensterlosen Bruestungsfeldern.
  * Stufe 3: Wilder Mann (dazu ein langes Kopfband, das die Fussstrebe kreuzt), Bruestungsfiguren (Andreaskreuz, Raute, Fuenferkreuz) in
    allen freien Bruestungsfeldern (die Gefache aus Stein baut das Skript des Satzes).
Die Figuren wuerfelt ein fester Startwert je Haus bzw. Modul. Alle Hoelzer einer Wand liegen in einer Holzlage (holzlage.py: eine
Ebene, ueberlappungsfrei vereinigt, zugeschnitten, geschlossen).
"""
from mathutils import Vector

STREBE = 0.14         # Breite der Streben und Kopfwinkelhoelzer
BAND = 0.12           # Breite der Kopfbaender und der Bruestungsfiguren


def felder(staender, bw):
    """Gefache zwischen benachbarten Staendern: (links, rechts, Staender links, Staender rechts) – lichte Kanten."""
    return [(a + bw / 2, b - bw / 2, a, b) for a, b in zip(staender[:-1], staender[1:]) if b - a >= bw + 0.30 - 1e-6]


def frei(feld, oeffnungen, za, zb):
    """Ist das Feld zwischen za und zb frei von Oeffnungen (Fenster, Tueren, Laeden)?"""
    g0, g1 = feld[0], feld[1]
    return not any(o[0] < g1 - 0.01 and o[1] > g0 + 0.01 and o[2] < zb and o[3] > za for o in oeffnungen)


def figuren(lage, staender, z0, z1, oeffnungen, stufe, wuerfel, bw=0.17, schwelle=0.20, raehm=0.18, z_brust=None, spiegel=False):
    """Streben, Baender und Figuren einer Fachwerkwand in die Holzlage (holzlage.Holzlage, Wandkoordinaten s, z). staender: Lage der
    Staender (sortiert; der erste und letzte sind Eckstaender), oeffnungen: (s0, s1, t0, t1, art), z_brust: Unterkante des Brustriegels
    (Oberkante der Bruestungsfelder). Jedes Holz wird auf sein Gefach zugeschnitten (endet genau an Staender, Schwelle, Raehm).
    spiegel: spiegelgleich zur Wandmitte (Giebelseiten der Neubau-Haeuser, Pruefung 2)."""
    from holzlage import kasten
    if stufe < 1 or len(staender) < 2: return
    h = z1 - z0
    unten, oben = z0 + schwelle, z1 - raehm
    fs = felder(staender, bw)
    if not fs: return
    gefach = lambda f, za=unten, zb=oben: kasten(f[0], f[1], za, zb)
    if stufe == 1:
        for k, sg in ((0, 1), (len(fs) - 1, -1)):
            f = fs[k]
            if not frei(f, oeffnungen, unten, oben) or f[1] - f[0] < 0.45: continue     # keine steile Strebe im schmalen Feld
            aussen, innen = (f[0], f[1]) if sg > 0 else (f[1], f[0])
            lage.holz((aussen, unten), (innen, oben), STREBE, "balken_a", 1, zuschnitt=gefach(f))
        return
    # Mann-Figuren ab Stufe 2 (Nutzer 21.11.2026): halber Mann am Eckstaender nach innen, ganzer Mann an einem gewuerfelten
    # Bundstaender. Stufe 2: Fraenkischer Mann (dreiviertelhohe Fussstrebe, Kopfwinkelholz), Stufe 3: Wilder Mann (dazu ein langes
    # Kopfband, das die Fussstrebe kreuzt). Ist ein Eckgefach nicht frei, bekommt es eine Kopfstrebe ueber dem Fenster.
    mann = {staender[0]: (1,), staender[-1]: (-1,)}
    kandidaten = [s for i, s in enumerate(staender[1:-1], 1)
                  if all(frei(f, oeffnungen, unten, oben) for f in fs if abs(f[2] - s) < 1e-6 or abs(f[3] - s) < 1e-6)]
    if spiegel:   # nur der Staender in der Wandmitte
        mitte_s = (staender[0] + staender[-1]) / 2
        kandidaten = [s for s in kandidaten if abs(s - mitte_s) < 0.02]
    if kandidaten and wuerfel.random() < 0.75:
        mann[kandidaten[wuerfel.randrange(len(kandidaten))]] = (-1, 1)
    figur_felder = set()
    for s in staender:
        for sg in (-1, 1):
            nachbar = next((f for f in fs if (abs(f[2] - s) < 1e-6 and sg > 0) or (abs(f[3] - s) < 1e-6 and sg < 0)), None)
            if nachbar is None: continue
            breite = nachbar[1] - nachbar[0]
            kante = s + sg * bw / 2
            eck = s in (staender[0], staender[-1])
            box = gefach(nachbar)
            if s in mann and sg in mann[s] and frei(nachbar, oeffnungen, unten, oben) and breite > 0.45 and id(nachbar) not in figur_felder:
                weit = min(breite - 0.06, 0.95)
                lage.holz((kante + sg * weit, unten), (kante, z0 + 0.74 * h), STREBE, "balken_a", 1, zuschnitt=box)        # Fussstrebe
                kw = min(breite - 0.06, 0.55)
                lage.holz((kante, oben - 0.55), (kante + sg * kw, oben), STREBE, "balken_a", 1, zuschnitt=box)            # Kopfwinkelholz
                if stufe >= 3:                                                                                          # Wilder Mann
                    lage.holz((kante, z0 + 0.42 * h), (kante + sg * min(breite - 0.06, 0.9), oben), BAND, "balken_b", 1, zuschnitt=box)
                figur_felder.add(id(nachbar))
            elif eck and s in mann and sg in mann[s] and frei(nachbar, oeffnungen, oben - 0.5, oben) and breite > 0.45:
                lage.holz((kante, oben - 0.48), (kante + sg * min(0.6, breite - 0.05), oben), STREBE, "balken_a", 1,
                          zuschnitt=gefach(nachbar, max(o[3] for o in oeffnungen if o[0] < nachbar[1] and o[1] > nachbar[0]) + 0.08
                                           if any(o[0] < nachbar[1] and o[1] > nachbar[0] for o in oeffnungen) else unten))   # Kopfstrebe
            elif frei(nachbar, oeffnungen, oben - 0.42, oben) and breite > 0.45:
                lage.holz((kante, oben - 0.36), (kante + sg * 0.36, oben), BAND, "balken_a", 1, zuschnitt=box)               # Kopfband
    if stufe >= 2 and z_brust is not None:
        wurf = [wuerfel.random() for _ in fs]
        arten = [wuerfel.choice(("andreaskreuz", "raute", "fuenferkreuz") if stufe >= 3 else ("andreaskreuz", "raute")) for _ in fs]
        if spiegel:
            for k in range(len(fs)):
                j = min(k, len(fs) - 1 - k); wurf[k], arten[k] = wurf[j], arten[j]
        for k, f in enumerate(fs):
            if not frei(f, oeffnungen, unten, z_brust) or z_brust - unten < 0.4 or f[1] - f[0] < 0.4: continue
            if id(f) in figur_felder: continue                                             # dort steht ein Mann
            if stufe == 2 and wurf[k] > 0.4: continue                                      # Stufe 2: nur einzelne Felder
            # Feld zwischen den tatsaechlichen Hoelzern darunter und darueber (Schwelle, Brustriegel): die Strebenenden liegen genau an
            za, zb = lage.feld_z((f[0] + f[1]) / 2, (unten + z_brust) / 2)
            if zb > 1e8 or zb - za < 0.4 or not frei(f, oeffnungen, za, zb): continue
            a, b = f[0], f[1]
            box = kasten(a, b, za, zb)
            m = ((a + b) / 2, (za + zb) / 2)
            art = arten[k]
            if art in ("andreaskreuz", "fuenferkreuz"):
                lage.holz((a, za), (b, zb), BAND, "balken_b", 3, zuschnitt=box)
                lage.holz((a, zb), (b, za), BAND, "balken_b", 3, zuschnitt=box)
            if art in ("raute", "fuenferkreuz"):
                for p, q in (((m[0], za), (b, m[1])), ((b, m[1]), (m[0], zb)), ((m[0], zb), (a, m[1])), ((a, m[1]), (m[0], za))):
                    lage.holz(p, q, BAND * 0.8, "balken_b", 3, zuschnitt=box)


def knaggen(teil, punkte, n_aussen, hoehe=0.45, krag=0.22, holz="Fachwerk"):
    """Knaggen unter vorkragenden Balkenkoepfen: je Punkt (Fuss an der Wand unter dem Balken, Vector) ein schraeges Kantholz
    von der Wand nach aussen oben bis unter den Balkenkopf."""
    n = Vector(n_aussen).normalized()
    for p in punkte:
        p = Vector(p)
        teil.stab(holz, p, p + n * (krag * 0.85) + Vector((0, 0, hoehe)), 0.13, "balken_c", breite=0.13, kappen=True)
