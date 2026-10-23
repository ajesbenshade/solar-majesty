# Approximate flat-shaded previews of the CampusDressing / PlanetArchitecture.Earth parts (Unity y-up -> z-up).
import numpy as np, math, matplotlib
matplotlib.use('Agg'); import matplotlib.pyplot as plt
from mpl_toolkits.mplot3d.art3d import Poly3DCollection
def cyl(c,s,n=20,rot=None):
    f=[];r=0.5;h=1.0  # unity cylinder: dia 1, height 2 at scale 1
    ang=np.linspace(0,2*np.pi,n+1)
    pts=lambda a,y:np.array([r*math.cos(a),y,r*math.sin(a)])
    for i in range(n):
        a,b=ang[i],ang[i+1]
        f.append([pts(a,-h),pts(b,-h),pts(b,h),pts(a,h)])
        f.append([np.array([0,h,0]),pts(a,h),pts(b,h)]); f.append([np.array([0,-h,0]),pts(b,-h),pts(a,-h)])
    return xf(f,c,s,rot)
def box(c,s,rot=None):
    v=[np.array([x,y,z])*0.5 for x in(-1,1) for y in(-1,1) for z in(-1,1)]
    idx=[(0,1,3,2),(4,6,7,5),(0,4,5,1),(2,3,7,6),(0,2,6,4),(1,5,7,3)]
    return xf([[v[i] for i in q] for q in idx],c,s,rot)
def sph(c,s,n=16):
    f=[]
    for i in range(n):
        for j in range(n*2):
            t0,t1=math.pi*i/n,math.pi*(i+1)/n; p0,p1=math.pi*j/n,math.pi*(j+1)/n
            P=lambda t,p:np.array([0.5*math.sin(t)*math.cos(p),0.5*math.cos(t),0.5*math.sin(t)*math.sin(p)])
            f.append([P(t0,p0),P(t0,p1),P(t1,p1),P(t1,p0)])
    return xf(f,c,s,None)
def rotx(deg):
    a=math.radians(deg);return np.array([[1,0,0],[0,math.cos(a),-math.sin(a)],[0,math.sin(a),math.cos(a)]])
def xf(f,c,s,rot):
    R=rot if rot is not None else np.eye(3)
    return [[R@(p*np.array(s))+np.array(c) for p in q] for q in f]
def render(objs,fn,night=False,elev=45,azim=-55,zoom=6,center=(0,1,0)):
    fig=plt.figure(figsize=(6,5),dpi=110); ax=fig.add_subplot(111,projection='3d')
    bg=(0.05,0.07,0.10) if night else (0.33,0.47,0.24)
    fig.patch.set_facecolor(bg); ax.set_facecolor(bg)
    L=np.array([0.4,0.8,0.45]);L/=np.linalg.norm(L)
    polys=[];cols=[]
    
    for faces,col,emit,alpha in objs:
        for q in faces:
            q=np.array(q);n=np.cross(q[1]-q[0],q[2]-q[0]);nn=np.linalg.norm(n)
            n=n/nn if nn>1e-9 else np.array([0,1,0])
            lam=0.35+0.65*max(0,abs(np.dot(n,L)))
            if night: lam=0.12+0.18*max(0,abs(np.dot(n,[0,1,0])))
            c=np.clip(np.array(col)*lam+(np.array(emit) if emit is not None else 0),0,1)
            polys.append(q);cols.append((*c,alpha))
    P=[[ (p[0],p[2],p[1]) for p in q] for q in polys]
    pc=Poly3DCollection(P,facecolors=cols,edgecolors='none');ax.add_collection3d(pc)
    cx,cy,cz=center
    ax.set_xlim(cx-zoom,cx+zoom);ax.set_ylim(cz-zoom,cz+zoom);ax.set_zlim(cy-zoom,cy+zoom)
    ax.set_box_aspect((1,1,1));ax.view_init(elev,azim);ax.set_axis_off()
    plt.subplots_adjust(0,0,1,1);fig.savefig(fn,facecolor=bg);plt.close(fig)
CER=(0.88,0.82,0.74);CAR=(0.16,0.17,0.19);GUN=(0.30,0.32,0.35);CY=(0.22,0.84,0.98);CYE=(0.10,0.5,0.75)
KIT=(0.55,0.56,0.58);SOL=(0.10,0.16,0.30)
def kit(tower):
    o=[(box((0,0.6,0),(2.4,1.2,2.4)),KIT,None,1),(box((0,1.4,0),(1.2,0.4,1.6)),(0.26,0.24,0.22),None,1)]
    if tower:
        o=[(box((x,1.0,z),(0.2,2.0,0.2)),CAR,None,1) for x in(-0.8,0.8) for z in(-0.8,0.8)]+[(box((0,2.3,0),(2.0,0.8,2.0)),KIT,None,1)]
    return o
def before_def(tower): return kit(tower)+[(sph((0,1.6,0),(5.4,3.2,5.4)),(0.35,0.72,1.0),(0.05,0.1,0.15),0.85)]
def after_def(tower):
    o=kit(tower);r=2.7*0.7071
    for sx in(-1,1):
        for sz in(-1,1):
            a=np.array([sx*r,0,sz*r])
            o+= [(cyl(a+[0,0.08,0],(0.95,0.10,0.95)),GUN,None,1),(cyl(a+[0,0.55,0],(0.72,0.38,0.72)),CER,None,1),
                 (cyl(a+[0,0.68,0],(0.75,0.04,0.75)),CY,CYE,1),(cyl(a+[0,0.96,0],(0.56,0.06,0.56)),CAR,None,1)]
    if tower:
        m=np.array([r,0,-r])
        o+=[(cyl(m+[0,1.9,0],(0.10,1.2,0.10)),CAR,None,1),(cyl(m+[0,2.9,0.12],(0.62,0.04,0.62),rot=rotx(35)),CER,None,1),
            (sph(m+[0,3.18,0],(0.14,0.14,0.14)),CY,CYE,1)]
    return o
W=D=4;H=1.5
def shelter_base(): return [(box((x,H/2,z),(0.15,H,0.15)),CAR,None,1) for x in(-1.6,1.6) for z in(-1,1)]+[(box((0,0.1,0),(3.6,0.2,2.6)),GUN,None,1)]
R=rotx(-12)
def before_sh(): return shelter_base()+[(box((0,H+0.7,0),(W*0.9,0.06,D*0.6),R),(0.62,0.86,0.88),None,0.85),
    (cyl((W*0.36,0.8,-D*0.36),(1.1,0.8,1.1)),(0.62,0.86,0.88),None,0.7)]
def after_sh(): return shelter_base()+[(box((0,H+0.7,0),(W*0.9,0.10,D*0.6),R),CER,None,1),(box((0,H+0.66,0),(W*0.92,0.07,D*0.62),R),CAR,None,1),
    (box((0,H+0.76,0),(0.05,0.02,D*0.6),R),CAR,None,1),(box((W*0.22,H+0.77,0),(W*0.22,0.02,D*0.5),R),SOL,(0.01,0.04,0.12),1),
    (cyl((W*0.36,0.8,-D*0.36),(1.1,0.8,1.1)),CER,None,1)]
sets={'defense':(before_def(False),after_def(False)),'watchtower':(before_def(True),after_def(True)),'shelter':(before_sh(),after_sh())}
for name,(b,a) in sets.items():
    for tag,o in (('before',b),('after',a)):
        render(o,f'{name}_{tag}_overseer.png',elev=50,zoom=3.6,center=(0,0.4,0))
        render(o,f'{name}_{tag}_closeup.png',elev=18,zoom=2.4,center=(0.6,0.6,0))
    render(a,f'{name}_after_night.png',night=True,elev=50,zoom=3.6,center=(0,0.4,0))
