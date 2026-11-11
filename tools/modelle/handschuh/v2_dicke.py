import bpy,numpy as np
gl=bpy.data.objects["Handschuh"]; V=np.array([v.co[:] for v in gl.data.vertices])
for y0 in np.arange(-0.06,0.19,0.02):
    m=(V[:,1]>=y0)&(V[:,1]<y0+0.02)
    for (xa,xb,nm) in ((-0.075,-0.03,"Daumenseite"),(-0.03,0.03,"Mitte"),(0.03,0.08,"Kleinfinger")):
        mm=m&(V[:,0]>=xa)&(V[:,0]<xb)
        if mm.sum()>2: print("DICKE y %.2f %-12s zmin %.3f zmax %.3f n %d"%(y0,nm,V[mm,2].min(),V[mm,2].max(),mm.sum()))
