from PIL import Image, ImageDraw, ImageFont
import os
roh="roh"; aus="../bilder"
os.makedirs(aus, exist_ok=True)
try: f=ImageFont.truetype("C:/Windows/Fonts/arial.ttf",22); fk=ImageFont.truetype("C:/Windows/Fonts/arial.ttf",18)
except: f=fk=ImageFont.load_default()
zust=[("zu","1 Schriftrolle zu"),("halb","2 Schriftrolle halb offen"),("offen","3 Schriftrolle offen")]
look={1:"Pixel-Look aus",2:"Pixel-Look an, Pixel 2",3:"Pixel-Look an, Pixel 3"}
W,H=720,480
tile=[]
for z,(k,t) in enumerate(zust):
    b=Image.new("RGB",(W*3,H+44),(24,22,20)); d=ImageDraw.Draw(b)
    d.text((10,8),t+"  (links Handschuh als Stilvergleich)",fill=(235,225,200),font=f)
    for c,ps in enumerate((1,2,3)):
        im=Image.open(f"{roh}/still_{ps}_{k}.png").convert("RGB"); b.paste(im,(c*W,44))
        d.text((c*W+10,H+16),look[ps],fill=(255,220,120),font=fk)
    b.save(f"{aus}/{z+1:02d}_{k}.png"); tile.append(b)
u=Image.new("RGB",(W*3,(H+44)*3))
for i,b in enumerate(tile): u.paste(b,(0,i*(H+44)))
u.resize((W*3//2,(H+44)*3//2),Image.LANCZOS).save(f"{aus}/00_uebersicht.png")
