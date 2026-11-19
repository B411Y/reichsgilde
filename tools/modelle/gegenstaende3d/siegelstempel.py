"""Siegelstempel mit Wachs (Koeln um 1400): Petschaft (Messingplatte mit Hals, gedrechselter Buchsbaumgriff mit Knauf) neben zwei Urkunden,
auf der oberen ein Klumpen rotes Bienenwachs. Knochen: Blatt (fest), Wachs_Mitte und Wachs_Rand (Wachs; beim Siegeln werden sie flacher und breiter,
der Rand bleibt als Wulst stehen), Abdruck (Wappenflaeche auf dem gepressten Wachs), Stempel (verschiebt sich nur, ohne Drehung).
Clips: bereit (Rast-Pose: Wachsklumpen, Stempel steht rechts), siegeln (hoch, hinueber, absenken, pressen, abheben, zurueck), gesiegelt."""
import gemeinsam as G
import texturen as tx

NAME = "siegelstempel"
GERUEST = "Siegelstempel_Geruest"
HAUPT = "Siegelstempel"

Z_BLATT = 0.0027              # Oberseite der oberen Urkunde
W = (-0.025, -0.012)          # Mitte des Wachses
SP = (0.13, 0.03)             # Standort des Stempels (auf dem Tisch, z = 0)
H_DOME = 0.0105               # Hoehe des frischen Wachsklumpens
H_PRESS = 0.0044              # Hoehe des gepressten Wachses in der Mitte
Z_ABDRUCK = Z_BLATT + 0.0047 + G.HOVER   # ueber dem Wulst (0,0047 m), damit die Ecken nicht ins Wachs tauchen
ABDRUCK = 0.028               # Kantenlaenge der Wappenflaeche (der Kreis des Wappens fuellt die Stempelflaeche)
S_MITTE = (1.35, 0.42)        # Skala (waagrecht, senkrecht) gepresst
S_RAND = (1.55, 0.55)
UMR = 0.0015
UMR_WACHS = 0.001
HUB = 0.05                    # so hoch hebt sich der Stempel

KNOCHEN = [("Blatt", (0.0, 0.0, 0.0), None), ("Wachs_Mitte", (W[0], W[1], Z_BLATT), None), ("Wachs_Rand", (W[0], W[1], Z_BLATT), None),
           ("Abdruck", (W[0], W[1], Z_ABDRUCK), None), ("Stempel", (SP[0], SP[1], 0.0), None)]


def materialien(G_, aus):
    G.SKALA.update({"Siegel_Messing": 60.0, "Siegel_Holz": 40.0, "Siegel_Pergament": 4.0})
    m = {}
    m["Siegel_Pergament"] = G.mat_bild("Siegel_Pergament", G.bild_laden(aus, "siegelstempel_urkunde", tx.urkunde(7701)), rau=0.9)
    m["Siegel_Messing"] = G.mat_bild("Siegel_Messing", G.bild_laden(aus, "siegelstempel_messing", tx.messing(7702)), rau=0.4)
    m["Siegel_Holz"] = G.mat_bild("Siegel_Holz", G.bild_laden(aus, "siegelstempel_holz", tx.holz(7703, (0.78, 0.62, 0.38), n=64, ringe=8, kontrast=0.20, aeste=0)), rau=0.6)
    m["Siegel_Wachs"] = G.mat_flaeche("Siegel_Wachs", (0.60, 0.09, 0.07), rau=0.5)
    # Abdruck: Platzhalter (Ring und Schild, halbdurchsichtig dunkel), die Oberflaeche setzt das Wappen des Hauses als eigenes Material
    import bpy
    import os
    pfad = os.path.join(aus, "siegelstempel_abdruck.png")
    tx.png_rgba(pfad, tx.wappen_vorschau(64))
    img = bpy.data.images.load(pfad)
    img.name = "abdruck"
    img.pack()
    ma, nt, bsdf = G._bsdf("Siegel_Abdruck", 0.5)
    tex = nt.nodes.new("ShaderNodeTexImage")
    tex.image = img
    tex.interpolation = "Closest"
    nt.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
    nt.links.new(tex.outputs["Alpha"], bsdf.inputs["Alpha"])
    try:
        ma.surface_render_method = "BLENDED"
    except Exception as e:
        print("RENDERMETHODE", e)
    m["Siegel_Abdruck"] = ma
    return m


def bauen(G_):
    t = G.Teil(HAUPT)
    P = "Siegel_Pergament"
    # zwei Urkunden, leicht gegeneinander verdreht
    Mu = G.T(-0.005, 0.004, 0.0) @ G.RZ(5.0)
    t.box(-0.105, 0.105, -0.075, 0.075, 0.0, 0.0012, "Blatt", P, UMR, ohne=("-z",), M=Mu)
    Mo = G.T(-0.012, -0.004, 0.0) @ G.RZ(-4.0)
    t.box(-0.098, 0.098, -0.070, 0.070, 0.0012, Z_BLATT, "Blatt", P, UMR, ohne=("-z", "+z"), M=Mo)
    t.flaeche([(-0.098, -0.070, Z_BLATT), (0.098, -0.070, Z_BLATT), (0.098, 0.070, Z_BLATT), (-0.098, 0.070, Z_BLATT)], (0, 0, 1), P, "Blatt", UMR,
              uvs=[(0, 0), (1, 0), (1, 1), (0, 1)], M=Mo)
    # Wachsklumpen: Rand und Mitte an je einem Knochen
    prof = [(0.0145, 0.0), (0.0140, 0.0022), (0.0105, 0.0085), (0.0075, 0.0098), (0.0035, 0.0104), (0.0, H_DOME)]
    kn = ["Wachs_Rand", "Wachs_Rand", "Wachs_Rand", "Wachs_Mitte", "Wachs_Mitte", "Wachs_Mitte"]
    t.dreh(prof, 16, kn, "Siegel_Wachs", UMR_WACHS, mitte=(W[0], W[1], Z_BLATT), k_uv=60.0)
    # Petschaft: Messingplatte, Hals und Kragen, Griff mit Knauf
    sprof = [(0.0, 0.0), (0.0155, 0.0), (0.0155, 0.0045, "h"), (0.0120, 0.0065), (0.0085, 0.0110, "h"), (0.0092, 0.0125, "h"),
             (0.0068, 0.028), (0.0078, 0.050), (0.0118, 0.064), (0.0122, 0.070), (0.0095, 0.078), (0.0, 0.0815)]
    sm = ["Siegel_Messing"] * 5 + ["Siegel_Holz"] * 6
    t.dreh(sprof, 12, "Stempel", sm, UMR, mitte=(SP[0], SP[1], 0.0), k_uv=60.0, tausch=True)
    a = G.Teil("Abdruck_Wappen")
    h = ABDRUCK / 2
    a.flaeche([(W[0] - h, W[1] - h, Z_ABDRUCK), (W[0] + h, W[1] - h, Z_ABDRUCK), (W[0] + h, W[1] + h, Z_ABDRUCK), (W[0] - h, W[1] + h, Z_ABDRUCK)], (0, 0, 1),
              "Siegel_Abdruck", "Abdruck", 0.0, uvs=[(0, 0), (1, 0), (1, 1), (0, 1)])
    return [G.Netz(t), G.Netz(a, umriss=False)]


DZ_KONTAKT = Z_BLATT + H_DOME
DZ_PRESS = Z_BLATT + H_PRESS
B_ENDE = 38
_dz = G.verlauf([(0, 0.0), (5, HUB), (11, HUB), (15, DZ_KONTAKT), (20, DZ_PRESS), (23, DZ_PRESS), (27, HUB), (33, HUB), (38, 0.0)])
_mv = G.verlauf([(0, 0.0), (5, 0.0), (11, 1.0), (27, 1.0), (33, 0.0)])


def _w_inst(f):
    if _mv(f) < 0.999:
        return 0.0
    return min(1.0, max(0.0, (DZ_KONTAKT - _dz(f)) / (DZ_KONTAKT - DZ_PRESS)))


def _w(f):
    """Pressung 0..1: groesster Wert bisher (das Wachs bleibt gepresst, wenn der Stempel abhebt)."""
    w = 0.0
    x = 0.0
    while x <= f + 1e-9:
        w = max(w, _w_inst(x))
        x += 0.25
    return max(w, _w_inst(f))


def _D(w, dz, mv):
    I = G.Matrix.Identity(4)
    pw = (W[0], W[1], Z_BLATT)
    sm = 1 + (S_MITTE[0] - 1) * w, 1 + (S_MITTE[1] - 1) * w
    sr = 1 + (S_RAND[0] - 1) * w, 1 + (S_RAND[1] - 1) * w
    a = min(1.0, max(0.0, (w - 0.85) / 0.15))
    s = 0.3 + 0.7 * a
    pa = (W[0], W[1], Z_ABDRUCK)
    return {"Blatt": I,
            "Wachs_Mitte": G.um_punkt(pw, G.S(sm[0], sm[0], sm[1])),
            "Wachs_Rand": G.um_punkt(pw, G.S(sr[0], sr[0], sr[1])),
            "Abdruck": G.T(0, 0, -0.0025 * (1 - a)) @ G.um_punkt(pa, G.S(s, s, s)),
            "Stempel": G.T((W[0] - SP[0]) * mv, (W[1] - SP[1]) * mv, dz)}


def bind():
    return _D(0.0, 0.0, 0.0)


def clips():
    return [
        ("bereit", 1, lambda f: _D(0.0, 0.0, 0.0)),
        ("gesiegelt", 1, lambda f: _D(1.0, 0.0, 0.0)),
        ("siegeln", B_ENDE, lambda f: _D(_w(f), _dz(f), _mv(f))),
    ]


def messen(ctx, mess):
    sichtbar = next(f for f in range(21, B_ENDE + 1) if _dz(f) >= DZ_PRESS + 0.003)
    beruehrt = next(f for f in range(B_ENDE + 1) if _w_inst(f) > 0.0)
    voll = next(f for f in range(B_ENDE + 1) if _w(f) >= 0.999)
    mess["zeiten_s"] = {"siegeln_dauer": round(B_ENDE / G.FPS, 3), "siegeln_stempel_beruehrt_wachs": round(beruehrt / G.FPS, 3),
                        "siegeln_wachs_ganz_gepresst": round(voll / G.FPS, 3), "siegeln_abdruck_sichtbar_ab": round(sichtbar / G.FPS, 3),
                        "abdruck_schwelle": "Stempelflaeche mindestens 3 mm ueber dem gepressten Wachs"}
    # Stempelflaeche nie tiefer als das gepresste Wachs, nie unter dem Tisch
    tief = min(_dz(f / 4) for f in range(B_ENDE * 4 + 1))
    mess["stempel_tiefster_hub_m"] = round(tief, 5)
    mess["stempel_drueckt_nicht_durch"] = all(_dz(f / 4) >= DZ_PRESS - 1e-9 for f in range(11 * 4, 27 * 4 + 1))
    o = {ob.name: (ob, gew, flach) for ob, gew, flach, haut, um in ctx["objekte"] if not um}
    _, gh, fh = o[HAUPT]
    import numpy as np
    gw = np.array([str(g) for g in gh])
    wachs = np.array([g.startswith("Wachs") for g in gw])
    for name, w in (("frisch", 0.0), ("gepresst", 1.0)):
        p = ctx["anwenden"](_D(w, 0.0, 0.0), fh[wachs], [g for g, k in zip(gh, wachs) if k])
        r = np.sqrt((p[:, 0] - W[0]) ** 2 + (p[:, 1] - W[1]) ** 2)
        mess["wachs_" + name] = {"hoehe_m": round(float(p[:, 2].max()) - Z_BLATT, 5), "halbmesser_m": round(float(r.max()), 5)}
    _, ga, fa = o["Abdruck_Wappen"]
    pa = ctx["anwenden"](_D(1.0, 0.0, 0.0), fa, ga)
    mess["abdruck_gesiegelt"] = {"z_m": round(float(pa[:, 2].mean()), 5), "kante_m": round(float(pa[:, 0].max() - pa[:, 0].min()), 5)}
    pb = ctx["anwenden"](_D(0.0, 0.0, 0.0), fa, ga)
    mess["abdruck_bereit_im_wachs"] = bool(pb[:, 2].max() < Z_BLATT + 0.0095 and np.sqrt(((pb[:, :2] - np.array(W)) ** 2).sum(axis=1)).max() < 0.007)
    mess["masse"] = {"wachs_mitte": [W[0], W[1], Z_BLATT], "stempel_stand": [SP[0], SP[1], 0.0], "stempel_hoehe_m": 0.0815, "stempelflaeche_r_m": 0.0155,
                     "abdruck_kante_m": ABDRUCK, "umriss": UMR, "umriss_wachs": UMR_WACHS}
