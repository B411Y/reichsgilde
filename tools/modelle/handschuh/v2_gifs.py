import sys,glob,os,re
from PIL import Image,ImageDraw
ordner=sys.argv[1]; aus=sys.argv[2]; BG=(230,220,190)
os.makedirs(aus,exist_ok=True)
clips={}
for f in glob.glob(ordner+"/*_oben.png"):
    m=re.match(r"(.*)_(\d+)_oben\.png",os.path.basename(f))
    if not m: continue
    clips.setdefault(m.group(1),[]).append(int(m.group(2)))
st={"Ruhe":4,"Zeigen":4,"Greifen":3,"Tippen":2}
def lade(n,f,a,s):
    im=Image.open(f"{ordner}/{n}_{f:02d}_{a}.png").convert("RGBA").resize((s,s),Image.LANCZOS)
    b=Image.new("RGBA",(s,s),BG+(255,)); b.alpha_composite(im); return b.convert("RGB")
for n,fr in clips.items():
    fr.sort(); s=200; cols=len(fr)
    blatt=Image.new("RGB",(cols*s,3*s+24),BG); d=ImageDraw.Draw(blatt)
    for j,f in enumerate(fr):
        d.text((j*s+4,3*s+6),f"{n} Bild {f:02d}",fill=(0,0,0))
        for i,a in enumerate(("oben","seite","schraeg")): blatt.paste(lade(n,f,a,s),(j*s,i*s))
    blatt.save(f"{aus}/blatt_{n}.png")
    dur=int(1000*(st.get(n,1))/24)
    for a in ("schraeg","seite","oben"):
        if a!="schraeg" and n not in ("Tippen","Zeigen","Greifen","Ruhe"): continue
        fl=[lade(n,f,a,288) for f in fr]
        fl[0].save(f"{aus}/{n}{'' if a=='schraeg' else '_'+a}.gif",save_all=True,append_images=fl[1:],duration=dur,loop=0,disposal=2)
print("fertig",list(clips))

