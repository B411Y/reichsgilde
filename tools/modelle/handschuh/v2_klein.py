import sys
from PIL import Image
o=sys.argv[1]; aus=sys.argv[2]; BG=(230,220,190)
posen=[("Ruhe_00","Ruhe"),("Zeigen_00","Zeigen"),("Greifen_00","Greifen"),("Tippen_08","Tippen")]
rows=[]
for s in (48,64):
    row=Image.new("RGB",(4*(s*4+12),s*4+8),BG); x=6
    for f,n in posen:
        im=Image.open(f"{o}/{f}_schraeg.png").convert("RGBA"); im=im.crop(im.getbbox()); sc=s/max(im.size)
        im=im.resize((max(1,round(im.width*sc)),max(1,round(im.height*sc))),Image.LANCZOS)
        k=Image.new("RGBA",(s,s),BG+(255,)); k.alpha_composite(im,((s-im.width)//2,(s-im.height)//2))
        row.paste(k.convert("RGB").resize((s*4,s*4),Image.NEAREST),(x,4)); x+=s*4+12
    rows.append(row)
blatt=Image.new("RGB",(max(r.width for r in rows),sum(r.height for r in rows)),BG); y=0
for r in rows: blatt.paste(r,(0,y)); y+=r.height
blatt.save(aus)
