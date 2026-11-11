import sys,os,glob
from PIL import Image,ImageDraw
V=os.environ.get("VIDEO", os.path.join("..", "video")); G=sys.argv[1]; aus=sys.argv[2]; BG=(230,220,190)
os.makedirs(aus,exist_ok=True)
def video(n,tv):
    k=round(tv*10); f=sorted(glob.glob(os.path.join(V, f"bilder_{n}", f"b{k:03d}_*.jpg")))[0]
    im=Image.open(f).convert("RGB").crop((60,0,760,460)).transpose(Image.FLIP_TOP_BOTTOM); return im.resize((480,315))
def glove(a,tv):
    im=Image.open(os.path.join(G, f"t{tv:.1f}_{a}.png")).convert("RGBA").rotate(90 if a=="unten" else -90,expand=True)
    b=Image.new("RGBA",im.size,BG+(255,)); b.alpha_composite(im); return b.convert("RGB").resize((420,420)).crop((0,52,420,368)).resize((420,316))
phasen=[0.8,1.5,1.7,1.9,2.6]
for n,a,name in ((2,"unten","Innenflaeche"),(1,"oben","Ruecken")):
    blatt=Image.new("RGB",(900,len(phasen)*320),BG); d=ImageDraw.Draw(blatt)
    for i,tv in enumerate(phasen):
        blatt.paste(video(n,tv),(0,i*320)); blatt.paste(glove(a,tv),(480,i*320)); d.text((4,i*320+4),f"Video {n} ({name}, vertikal gespiegelt) t={tv:.1f} s",fill=(255,0,0)); d.text((484,i*320+4),f"Handschuh {a}",fill=(255,0,0))
    blatt.save(os.path.join(aus, f"paar_{name}.png"))
    fr=[]
    for i in range(20):
        tv=0.8+0.1*i; b=Image.new("RGB",(900,316),BG); b.paste(video(n,tv),(0,0)); b.paste(glove(a,tv),(480,0)); fr.append(b)
    fr[0].save(os.path.join(aus, f"paar_{name}.gif"),save_all=True,append_images=fr[1:],duration=100,loop=0)
print("ok")
