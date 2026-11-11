"""Messfunktionen fuer das Handschuh-Netz (Blender, bmesh/BVH). Nur lesen."""
import bpy, bmesh, numpy as np
from mathutils import Vector
from mathutils.bvhtree import BVHTree

def netz_messen(ob, depsgraph=None, ausgewertet=False):
    """Inseln, Randschleifen, nicht-mannigfaltige Kanten, entartete Dreiecke."""
    bm = bmesh.new()
    if ausgewertet:
        dg = depsgraph or bpy.context.evaluated_depsgraph_get()
        bm.from_mesh(ob.evaluated_get(dg).to_mesh())
    else:
        bm.from_mesh(ob.data)
    bm.verts.ensure_lookup_table(); bm.edges.ensure_lookup_table(); bm.faces.ensure_lookup_table()
    # Inseln
    seen=set(); inseln=[]
    for v in bm.verts:
        if v.index in seen: continue
        st=[v]; seen.add(v.index); n=0; nf=set()
        while st:
            c=st.pop(); n+=1
            for f in c.link_faces: nf.add(f.index)
            for e in c.link_edges:
                o=e.other_vert(c)
                if o.index not in seen: seen.add(o.index); st.append(o)
        inseln.append((n,len(nf)))
    rand=[e for e in bm.edges if len(e.link_faces)==1]
    nm=[e for e in bm.edges if len(e.link_faces)>2]
    lose=[e for e in bm.edges if len(e.link_faces)==0]
    # Randschleifen zaehlen
    brand={e.index for e in rand}; gesehen=set(); schleifen=[]
    for e in rand:
        if e.index in gesehen: continue
        st=[e]; gesehen.add(e.index); laenge=0; pts=[]
        while st:
            c=st.pop(); laenge+=1; pts.append((c.verts[0].co+c.verts[1].co)/2)
            for v in c.verts:
                for o in v.link_edges:
                    if o.index in brand and o.index not in gesehen: gesehen.add(o.index); st.append(o)
        m=sum(pts,Vector())/len(pts)
        schleifen.append((laenge,tuple(round(x,3) for x in m)))
    ent=sum(1 for f in bm.faces if f.calc_area()<1e-9)
    schmal=0
    for f in bm.faces:
        l=[e.calc_length() for e in f.edges]
        if f.calc_area()>0 and (max(l)**2/ f.calc_area())>12: schmal+=1
    r=dict(verts=len(bm.verts),tris=len(bm.faces),inseln=sorted(inseln,reverse=True)[:6],n_inseln=len(inseln),
           randkanten=len(rand),randschleifen=sorted(schleifen,reverse=True)[:8],n_schleifen=len(schleifen),
           nichtmannigfaltig=len(nm),lose_kanten=len(lose),entartet=ent,schmal=schmal)
    bm.free(); return r

def selbstschnitt(ob, depsgraph=None):
    """Zahl der Dreieckspaare, die sich schneiden und keine Ecke teilen (ausgewertete Pose)."""
    dg = depsgraph or bpy.context.evaluated_depsgraph_get()
    me = ob.evaluated_get(dg).to_mesh()
    bm = bmesh.new(); bm.from_mesh(me); bmesh.ops.triangulate(bm, faces=bm.faces[:])
    bm.verts.ensure_lookup_table(); bm.faces.ensure_lookup_table()
    t = BVHTree.FromBMesh(bm, epsilon=1e-6)
    paare = t.overlap(t)
    fv=[set(v.index for v in f.verts) for f in bm.faces]
    n=0; ok=set()
    for a,b in paare:
        if a<b and not (fv[a]&fv[b]):
            # Nachbarschaft ueber 2 Kanten Abstand ignorieren: gemeinsame Nachbarecke
            n+=1; ok.add((a,b))
    bm.free()
    return n

def kategorie_je_dreieck(ob, bm_faces_verts_idx, names):
    pass

def selbstschnitt_zerlegt(ob, depsgraph=None):
    """Wie selbstschnitt, aber je Paar nach Gruppe (F2..F5, D=Daumen, H=Hand/Stulpe) aufgeschluesselt."""
    from collections import Counter
    dg = depsgraph or bpy.context.evaluated_depsgraph_get()
    ev = ob.evaluated_get(dg); me = ev.to_mesh()
    gnames = {g.index: g.name for g in ob.vertex_groups}
    def kat(vi):
        best = None; bw = 0
        acc = {}
        for g in ob.data.vertices[vi].groups:
            n = gnames[g.group]
            c = "H"
            if n.startswith("finger"):
                c = "D" if n.startswith("finger1") else "F" + n[6]
            acc[c] = acc.get(c, 0) + g.weight
        return max(acc.items(), key=lambda kv: kv[1])[0] if acc else "H"
    vk = [kat(i) for i in range(len(ob.data.vertices))]
    bm = bmesh.new(); bm.from_mesh(me); bmesh.ops.triangulate(bm, faces=bm.faces[:])
    bm.verts.ensure_lookup_table(); bm.faces.ensure_lookup_table()
    t = BVHTree.FromBMesh(bm, epsilon=1e-6)
    fv = [[v.index for v in f.verts] for f in bm.faces]
    fk = [Counter(vk[i] for i in vs).most_common(1)[0][0] for vs in fv]
    c = Counter()
    for a, b in t.overlap(t):
        if a < b and not (set(fv[a]) & set(fv[b])):
            c[tuple(sorted((fk[a], fk[b])))] += 1
    bm.free(); return dict(c)

def selbst_orte(ob, depsgraph=None):
    dg = depsgraph or bpy.context.evaluated_depsgraph_get()
    me = ob.evaluated_get(dg).to_mesh()
    bm = bmesh.new(); bm.from_mesh(me); bmesh.ops.triangulate(bm, faces=bm.faces[:])
    bm.verts.ensure_lookup_table(); bm.faces.ensure_lookup_table()
    t = BVHTree.FromBMesh(bm, epsilon=1e-6)
    fv = [set(v.index for v in f.verts) for f in bm.faces]
    out = []
    for a, b in t.overlap(t):
        if a < b and not (fv[a] & fv[b]):
            out.append(tuple(bm.faces[a].calc_center_median()))
    bm.free(); return out
