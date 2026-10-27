import json,struct,sys,collections
b=open(sys.argv[1],'rb').read()
l=struct.unpack('<I',b[12:16])[0]; j=json.loads(b[20:20+l])
bv=j['bufferViews']; acc=j['accessors']
img=sum(bv[i['bufferView']]['byteLength'] for i in j['images'])
m=j['meshes'][0]; names=m.get('extras',{}).get('targetNames',[])
def size(a):
    A=acc[a]
    if 'sparse' in A: return A['sparse']['count']*16
    return A['count']*12
pertarget=collections.Counter(); perprim=collections.Counter(); perattr=collections.Counter()
for pi,p in enumerate(m['primitives']):
    mat=j['materials'][p['material']]['name']
    for ti,t in enumerate(p.get('targets',[])):
        for k,a in t.items():
            s=size(a); pertarget[names[ti] if ti<len(names) else ti]+=s; perprim[mat]+=s; perattr[k]+=s
print('images MB',round(img/1e6,2),'total',round(len(b)/1e6,2))
print('per prim',{k:round(v/1e6,2) for k,v in perprim.items()})
print('per attr',{k:round(v/1e6,2) for k,v in perattr.items()})
print('top targets',[(k,round(v/1e6,2)) for k,v in pertarget.most_common(15)])
