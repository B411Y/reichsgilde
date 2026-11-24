"""Massblaetter fuer den Entwurf der neuen Karren (Phase 1): Seiten- und Draufsicht mit Massen, nur Zeichnung (Pillow, kein Blender).

    python tools/modelle/karren_neu_masse.py <Ausgabeordner>

Zahlen wie in karren_neu_entwurf.py (Blender: Fahrtrichtung -Y, Ursprung unter der hinteren Achse). Seitenansicht: vorn links; Draufsicht: vorn oben.
"""
import math
import os
import sys

from PIL import Image, ImageDraw, ImageFont

PX = 100                                  # Pixel je Meter
SCHRIFT = ImageFont.truetype("C:/Windows/Fonts/arial.ttf", 17)
SCHRIFT_K = ImageFont.truetype("C:/Windows/Fonts/arial.ttf", 14)
SCHRIFT_T = ImageFont.truetype("C:/Windows/Fonts/arialbd.ttf", 22)
HOLZ, DUNKEL, EISEN, LEDER, PFERD, MASS = (150, 108, 62), (92, 66, 40), (72, 74, 82), (150, 104, 62), (190, 120, 90), (200, 30, 30)


def zahl(x):
    return f"{x:.2f}".replace(".", ",") + " m"


class Blatt:
    def __init__(self, breite_m, hoehe_m, titel, ox_m, oy_m, seite=True):
        self.b, self.h = int(breite_m * PX) + 80, int(hoehe_m * PX) + 80
        self.im = Image.new("RGB", (self.b, self.h), (244, 238, 222))
        self.d = ImageDraw.Draw(self.im)
        self.ox, self.oy = ox_m, oy_m           # Weltpunkt (Fahrtrichtung -Y bzw. Hoehe) -> Bildmitte-Bezug
        self.seite = seite
        self.d.text((16, 10), titel, fill=(30, 30, 30), font=SCHRIFT_T)

    # Welt -> Bild. Seite: (y, z) -> x = -(y - y_min) , Bildy = Boden - z. Oben: (x, y) -> Bildx = x, Bildy = y.
    def p(self, a, b):
        if self.seite:
            return (40 + (self.ox - a) * PX, self.oy * PX + 40 - b * PX)      # a = y (vorn = negativ = links): Bildx = (ox - a)*PX  -> kleiner y = weiter links? nein: siehe ox
        return (40 + (self.ox + a) * PX, 40 + (b - self.oy) * PX)

    def rect(self, a0, a1, b0, b1, fuell, rand=(30, 30, 30), breite=1):
        x0, y0 = self.p(a0, b0)
        x1, y1 = self.p(a1, b1)
        self.d.rectangle([min(x0, x1), min(y0, y1), max(x0, x1), max(y0, y1)], fill=fuell, outline=rand, width=breite)

    def kreis(self, a, b, r, fuell=None, rand=(30, 30, 30), breite=2):
        x, y = self.p(a, b)
        self.d.ellipse([x - r * PX, y - r * PX, x + r * PX, y + r * PX], fill=fuell, outline=rand, width=breite)

    def linie(self, a0, b0, a1, b1, farbe=(30, 30, 30), breite=2):
        self.d.line([self.p(a0, b0), self.p(a1, b1)], fill=farbe, width=breite)

    def text(self, a, b, t, farbe=(30, 30, 30), font=SCHRIFT, anker="la"):
        self.d.text(self.p(a, b), t, fill=farbe, font=font, anchor=anker)

    def pfeil(self, p0, p1):
        self.d.line([p0, p1], fill=MASS, width=2)
        for q, r in ((p0, p1), (p1, p0)):
            ang = math.atan2(r[1] - q[1], r[0] - q[0])
            for s in (-0.4, 0.4):
                self.d.line([q, (q[0] + 11 * math.cos(ang + s), q[1] + 11 * math.sin(ang + s))], fill=MASS, width=2)

    def mass_waage(self, a0, a1, b, text, versatz=0.0):
        """Massband zwischen a0 und a1 (Wertachse a) bei Querwert b (+Versatz in Bild-Pixeln)."""
        p0, p1 = self.p(a0, b), self.p(a1, b)
        p0, p1 = (p0[0], p0[1] + versatz), (p1[0], p1[1] + versatz)
        for q, r in ((self.p(a0, b), p0), (self.p(a1, b), p1)):
            self.d.line([q, r], fill=MASS, width=1)
        self.pfeil(p0, p1)
        self.d.text(((p0[0] + p1[0]) / 2, p0[1] - 4), text, fill=MASS, font=SCHRIFT, anchor="ms")

    def mass_senkrecht(self, a, b0, b1, text, versatz=0.0):
        p0, p1 = self.p(a, b0), self.p(a, b1)
        p0, p1 = (p0[0] + versatz, p0[1]), (p1[0] + versatz, p1[1])
        self.pfeil(p0, p1)
        self.d.text(((p0[0] + p1[0]) / 2 + 6, (p0[1] + p1[1]) / 2), text, fill=MASS, font=SCHRIFT, anchor="lm")

    def speichern(self, pfad):
        self.im.save(pfad)


# ─────────────────────────────────────────────────────────────────────────────
#  Pferdekarren (1 Pferd)
# ─────────────────────────────────────────────────────────────────────────────

def pferd_seite(b, tip_y, x_welt=0.0):
    """Pferd als Huelle: Rumpf 2,2 m, Widerrist 1,6 m; Kruppe 1,64 m hinter dem Ring, Kopf bis 1,4 m vor dem Ring."""
    kruppe = tip_y + 1.64
    b.rect(tip_y - 0.25, kruppe, 0.55, 1.55, (205, 150, 120))                              # Rumpf (Huelle)
    b.rect(tip_y - 0.2, tip_y + 0.2, 1.1, 1.65, (205, 150, 120))
    b.rect(tip_y - 1.35, tip_y - 0.15, 1.35, 1.95, (205, 150, 120))                        # Hals und Kopf (grob)
    for dy in (-0.15, 0.15, 1.2, 1.55):
        b.rect(tip_y + dy - 0.07, tip_y + dy + 0.07, 0.0, 0.62, (180, 120, 90))             # Beine
    b.kreis(tip_y, 1.279, 0.12, None, LEDER, 3)
    b.text(tip_y + 0.2, 0.2, "Pferd (Huelle)", font=SCHRIFT_K)


def blatt_klein(pfad):
    zf, y0, y1, hw, r = 0.95, -1.2, 1.2, 0.5, 0.72
    tip_y = y0 - 0.45 - 1.64
    # Seite: Bildx = (ox - y)*PX; vorn (kleines y) rechts waere falsch -> wir wollen vorn links: Bildx = (y - ymin)*PX, daher ox negativ gedreht
    b = Blatt(7.4, 3.1, "Pferdekarren (1 Pferd) - Seite, Fahrtrichtung nach links", -tip_y + 1.45 + 0.0, 2.2)
    # p() Seite: Bildx = 40 + (ox - a)*PX mit ox = -ymin... vorn (a negativ) -> grosses Bildx; wir spiegeln daher a -> -a:
    b.p = lambda a, c: (40 + (a - (tip_y - 1.45)) * PX, 40 + (2.2 - c) * PX)
    b.linie(tip_y - 1.6, 0, y1 + 1.0, 0, (60, 60, 60), 3)
    pferd_seite(b, tip_y)
    b.rect(y0, y1, zf, zf + hw, HOLZ)
    b.rect(y0, y1, zf - 0.06, zf, DUNKEL)
    b.kreis(0, r, r, None, DUNKEL, 6)
    b.kreis(0, r, 0.07, EISEN)
    for dy in (-0.56, 0.0):
        pass
    b.linie(y1 - 0.05, 0.845, y0, 0.845, DUNKEL, 7)                                         # Schaftbaum
    b.linie(y0, 0.845, tip_y + 0.45, 1.02, DUNKEL, 7)
    b.linie(tip_y, 1.279, tip_y + 0.45 + 0.25, 1.02, LEDER, 3)                              # Zugstrang
    b.mass_waage(tip_y, 0, 0.0, "Zugpunkt - Achse (Drehpunkt) 3,29 m", versatz=34)
    b.mass_waage(y0 - 0.45, y0, 1.95, "Kruppe - Stirnwand 0,45 m", versatz=0)
    b.mass_waage(y0, y1, zf + hw + 0.35, "Ladeflaeche 2,40 m", versatz=0)
    b.mass_senkrecht(y1 + 0.35, 0, 2 * r, "Rad 1,44 m")
    b.mass_senkrecht(y1 + 0.35 + 0.9, 0, zf, "Boden 0,95 m")
    b.text(y0 + 0.1, zf + hw + 0.05, "Kasten 2,40 x 1,44 x 0,50 m (wie Ochsenkarren)", font=SCHRIFT_K)
    b.speichern(pfad + "_seite.png")
    # Draufsicht: Bildx = 40 + (x + 0.0)*PX zentriert, Bildy = 40 + (y - ymin)*PX
    t = Blatt(4.6, 7.2, "Pferdekarren (1 Pferd) - Draufsicht, Fahrtrichtung nach oben", 2.3, tip_y - 1.45, seite=False)
    t.rect(-0.72, 0.72, y0, y1, HOLZ)
    for s in (-1, 1):
        t.rect(s * 0.9 - 0.05, s * 0.9 + 0.05, -0.72, 0.72, DUNKEL)                       # Raeder
        t.rect(s * 0.56 - 0.045, s * 0.56 + 0.045, tip_y + 0.45, y1 - 0.05, DUNKEL)       # Schaftbaeume
        t.linie(s * 0.347, tip_y, s * 0.56, tip_y + 0.7, LEDER, 3)
    t.rect(-0.56, 0.56, y0 - 0.19, y0 - 0.11, DUNKEL)
    t.rect(-0.34, 0.34, tip_y - 0.2, tip_y + 1.64, (205, 150, 120))
    t.rect(-0.12, 0.12, tip_y - 1.35, tip_y - 0.2, (205, 150, 120))
    t.kreis(0, tip_y, 0.12, None, LEDER, 3)
    t.mass_waage(-0.72, 0.72, y1 + 0.3, "Kasten 1,44 m")
    t.mass_waage(-0.9 - 0.05, 0.9 + 0.05, y1 + 0.7, "Spur 1,80 m (Mitte Rad)")
    t.mass_waage(-0.56, 0.56, y0 + 0.5 - 0.5 + 0.0, "Schaft 1,12 m", versatz=0)
    t.text(0.45, tip_y + 0.9, "Flanke 0,20 m", font=SCHRIFT_K)
    t.speichern(pfad + "_oben.png")


# ─────────────────────────────────────────────────────────────────────────────
#  Grosser Pferdekarren (2 Pferde, Drehschemel)
# ─────────────────────────────────────────────────────────────────────────────

G = dict(zf=1.04, halb=0.80, y0=-2.85, y1=0.55, hw=0.62, r_h=0.66, r_v=0.46, spur_h=1.02, spur_v=0.98, y_v=-2.20, ws=1.10, pole_z=0.86, bock_halb=0.65, bock_lang=1.175)
G["pole_y"] = G["y0"] - G["bock_lang"] - 0.35
G["tip_y"] = G["pole_y"] - 1.64 - 0.45


def blatt_gross(pfad):
    g = G
    tip_y, y_v = g["tip_y"], g["y_v"]
    b = Blatt(10.7, 3.2, "Grosser Pferdekarren (2 Pferde) - Seite, Fahrtrichtung nach links", 0, 0)
    b.p = lambda a, c: (40 + (a - (tip_y - 1.45)) * PX, 40 + (2.2 - c) * PX)
    b.linie(tip_y - 1.6, 0, g["y1"] + 1.0, 0, (60, 60, 60), 3)
    pferd_seite(b, tip_y)
    b.rect(g["y0"], g["y1"], g["zf"], g["zf"] + g["hw"], HOLZ)
    b.rect(g["y0"], g["y1"], g["zf"] - 0.06, g["zf"], DUNKEL)
    b.rect(g["y0"] - g["bock_lang"], g["y0"], g["zf"] - 0.04, g["zf"], DUNKEL)             # Fussbrett
    b.rect(g["y0"] - g["bock_lang"], g["y0"] - g["bock_lang"] + 0.04, g["zf"], g["zf"] + 0.34, HOLZ)   # Spritzbrett
    b.kreis(0, g["r_h"], g["r_h"], None, DUNKEL, 6)
    b.kreis(y_v, g["r_v"], g["r_v"], None, DUNKEL, 6)
    b.rect(y_v - 0.25, y_v + 0.25, g["zf"] - 0.18, g["zf"] - 0.06, EISEN)                  # Schemel
    b.linie(y_v, 0.62, g["pole_y"], g["pole_z"], DUNKEL, 8)                                # Deichsel
    b.rect(g["pole_y"] - 0.06, g["pole_y"] + 0.06, g["pole_z"] - 0.07, g["pole_z"] + 0.07, EISEN)
    b.linie(g["pole_y"], g["pole_z"], tip_y, 1.279, LEDER, 3)
    b.mass_waage(tip_y, y_v, 0.0, "Zugpunkt - Drehpunkt (Vorderachse) 4,27 m  [Deichselabstand]", versatz=34)
    b.mass_waage(tip_y, 0, 0.0, "Zugpunkt - Hinterachse 6,47 m", versatz=62)
    b.mass_waage(y_v, 0, 0.0, "Radstand 2,20 m", versatz=-28)
    b.mass_waage(tip_y + 1.64, g["pole_y"], 2.05, "Kruppe - Waagscheit 0,45", versatz=0)
    b.mass_waage(g["pole_y"], g["y0"] - g["bock_lang"], 1.75, "Waagscheit - Spritzbrett 0,35", versatz=0)
    b.mass_waage(g["y0"], g["y1"], g["zf"] + g["hw"] + 0.4, "Ladeflaeche 3,40 m", versatz=0)
    b.mass_senkrecht(g["y1"] + 0.3, 0, 2 * g["r_h"], "Hinterrad 1,32 m")
    b.mass_senkrecht(g["y1"] + 0.3 + 0.9, 0, 2 * g["r_v"], "Vorderrad 0,92 m")
    b.text(g["pole_y"] - 1.3, 0.35, "Deichsel 2,18 m, Waagscheit 2,20 m", font=SCHRIFT_K)
    b.speichern(pfad + "_seite.png")
    for phi, nm in ((0, "oben"), (30, "oben_knick30")):
        t = Blatt(8.6, 9.9, f"Grosser Pferdekarren - Draufsicht, Vorderwagen {phi} Grad geknickt", 4.3, tip_y - 1.9, seite=False)
        c, s_ = math.cos(math.radians(phi)), math.sin(math.radians(phi))

        def dreh(x, y):
            return ((x * c - (y - y_v) * s_), y_v + x * s_ + (y - y_v) * c)

        def poly(pts, fuell, rand=(30, 30, 30)):
            q = [t.p(*dreh(x, y)) for x, y in pts]
            t.d.polygon(q, fill=fuell, outline=rand)

        def vorn_rect(x0, x1, ya, yb, fuell):
            poly([(x0, ya), (x1, ya), (x1, yb), (x0, yb)], fuell)
        t.rect(-g["halb"], g["halb"], g["y0"], g["y1"], HOLZ)
        t.rect(-g["bock_halb"], g["bock_halb"], g["y0"] - g["bock_lang"], g["y0"], (120, 90, 56))
        for sx in (-1, 1):
            t.rect(sx * g["spur_h"] - 0.055, sx * g["spur_h"] + 0.055, -g["r_h"], g["r_h"], DUNKEL)
            vorn_rect(sx * g["spur_v"] - 0.05, sx * g["spur_v"] + 0.05, y_v - g["r_v"], y_v + g["r_v"], DUNKEL)
        vorn_rect(-0.07, 0.07, g["pole_y"], y_v, DUNKEL)
        vorn_rect(-g["ws"], g["ws"], g["pole_y"] - 0.05, g["pole_y"] + 0.05, EISEN)
        for x in (0.62, -0.62):
            vorn_rect(x - 0.34, x + 0.34, tip_y - 0.2, tip_y + 1.64, (205, 150, 120))
            vorn_rect(x - 0.12, x + 0.12, tip_y - 1.35, tip_y - 0.2, (205, 150, 120))
        vorn_rect(-0.25, 0.25, y_v - 0.25, y_v + 0.25, EISEN)
        t.d.ellipse([t.p(0, y_v)[0] - 6, t.p(0, y_v)[1] - 6, t.p(0, y_v)[0] + 6, t.p(0, y_v)[1] + 6], fill=(200, 30, 30))
        t.text(0.1, y_v + 0.1, "Reibnagel", MASS, SCHRIFT_K)
        if phi == 0:
            t.mass_waage(-g["halb"], g["halb"], g["y1"] + 0.3, "Kasten 1,60 m")
            t.mass_waage(-g["spur_h"] - 0.05, g["spur_h"] + 0.05, g["y1"] + 0.75, "Spur 2,04 m (Mitte Rad)")
            t.mass_waage(-g["ws"], g["ws"], g["pole_y"] - 0.4, "Waagscheit 2,20 m")
        t.speichern(f"{pfad}_{nm}.png")


if __name__ == "__main__":
    aus = sys.argv[1]
    os.makedirs(aus, exist_ok=True)
    blatt_klein(os.path.join(aus, "mass_pferdekarren"))
    blatt_gross(os.path.join(aus, "mass_grosser_pferdekarren"))
