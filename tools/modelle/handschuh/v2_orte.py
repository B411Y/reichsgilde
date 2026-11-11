import bpy,sys,os,numpy as np
sys.path.insert(0,os.path.dirname(os.path.abspath(__file__)))
import v2_lib
gl=bpy.data.objects["Handschuh"]
O=np.array(v2_lib.selbst_orte(gl)); print("ORTE n",len(O))
for y0 in np.arange(-0.07,0.2,0.02):
    m=(O[:,1]>=y0)&(O[:,1]<y0+0.02)
    if m.sum(): print("ORT y %.2f n %d  x[%.3f,%.3f] z[%.3f,%.3f]"%(y0,m.sum(),O[m,0].min(),O[m,0].max(),O[m,2].min(),O[m,2].max()))
