# Approximate flat-shaded mirror of HeroCitadel big masses + key detail (before/after). Not a Unity capture.
import sys,math,numpy as np; sys.path.insert(0,'/workspace/art/colony-domes')
import importlib.util
spec=importlib.util.spec_from_file_location('r','/workspace/art/colony-domes/render.py')
src=open('/workspace/art/colony-domes/render.py').read().split('CER=(')[0]
ns={};exec(src,ns);box,cyl,sph,render,rotx=ns['box'],ns['cyl'],ns['sph'],ns['render'],ns['rotx']
def roty(d):a=math.radians(d);return np.array([[math.cos(a),0,math.sin(a)],[0,1,0],[-math.sin(a),0,math.cos(a)]])
def rotz(d):a=math.radians(d);return np.array([[math.cos(a),-math.sin(a),0],[math.sin(a),math.cos(a),0],[0,0,1]])
WH=(0.88,0.82,0.74);CAR=(0.26,0.24,0.22);GR=(0.16,0.17,0.19);OR=(0.96,0.42,0.08);CY=(0.22,0.84,0.98);CYE=(0.15,0.6,0.85)
WARM=(1.0,0.8,0.5);WARME=(0.5,0.35,0.15);GL=(0.30,0.40,0.45)
P=0.4;WALL=3.2;TOW=5.6;KEEP=6.6;H=6.0;T=H-1.7;kw=H*0.78;kd=H*0.66;kz=H*0.08;kt=P+KEEP;crown=P+KEEP+4.55
def base(o):
    o+=[(box((0,P/2,0),(2*H-0.6,P,2*H-0.6)),CAR,None,1)]
    for sx in(-1,1):
        for sz in(-1,1):
            o+=[(cyl((sx*T,P+TOW/2,sz*T),(3,TOW/2,3)),WH,None,1),(cyl((sx*T,P+TOW+0.12,sz*T),(3.5,0.12,3.5)),GR,None,1)]
    span=T*2-3;wy=P+WALL/2
    o+=[(box((0,wy,T),(span,WALL,1)),WH,None,1),(box((-T,wy,0),(1,WALL,span)),WH,None,1),(box((T,wy,0),(1,WALL,span)),WH,None,1)]
    g=1.7;fs=(span-2*g)/2;fx=g+fs/2
    o+=[(box((-fx,wy,-T),(fs,WALL,1)),WH,None,1),(box((fx,wy,-T),(fs,WALL,1)),WH,None,1),(box((0,P+3.75,-T-0.2),(2*g+0.2,1.7,1.5)),WH,None,1)]
    for s in(-1,1):o+=[(box((s*(g+0.55),P+2.3,-T-0.2),(1.3,4.6,1.7)),WH,None,1)]
    o+=[(box((0,P+KEEP/2,kz),(kw,KEEP,kd)),WH,None,1),(cyl((0,kt+1.1,kz),(kd*0.78,0.95,kd*0.78)),WH,None,1),
        (cyl((0,kt+3.25,kz),(1.4,1.1,1.4)),WH,None,1),(cyl((0,crown,kz),(1.2,0.04,1.2)),GR,None,1)]
    return o
def before():
    o=base([])
    for f in range(4):
        for x in np.arange(-T+1.75,T-1.75,0.9):
            p=[(x,-T-0.5),(-T-0.5,x),(T+0.5,x),(x,T+0.5)][f]
            if f==0 and abs(x)<2: continue
            o.append((box((p[0],P+WALL+0.25,p[1]),(0.5,0.5,0.5)),WH,None,1))
    for sx in(-1,1):
        for sz in(-1,1):
            c=np.array([sx*T,P+TOW+0.5,sz*T])
            for m in range(10):
                a=math.radians(m*36);o.append((box(c+[1.55*math.cos(a),0,1.55*math.sin(a)],(0.5,0.5,0.32)),WH,None,1))
            o.append((box(c+[0.6,1.6,0.6],(0.7,0.42,0.03)),OR,None,1))
    for s in(-1,1):o.append((box((s*2.25,P+2.2,-T-1.1),(0.7,2.4,0.1)),OR,None,1))
    for y in(1.5,3.7,5.9):o.append((box((0,P+y-0.6,kz-kd/2-0.03),(kw*0.8,0.9,0.05)),WARM,WARME,1))
    return o
def after():
    o=base([])
    for x in np.arange(-T+1.5,T-1.5,2.2):
        o.append((box((x,P+1.7,-T-0.53),(0.06,2.5,0.06)),GR,None,1));o.append((box((-T-0.53,P+1.7,x),(0.06,2.5,0.06)),GR,None,1))
    o+=[(box((0,P+WALL+0.06,T),(T*2-3,0.12,1.1)),WH,None,1),(box((0,P+WALL+0.06,-T),(T*2-3,0.12,1.1)),WH,None,1),
        (box((T,P+WALL+0.06,0),(1.1,0.12,T*2-3)),WH,None,1),(box((-T,P+WALL+0.06,0),(1.1,0.12,T*2-3)),WH,None,1)]
    for sx in(-1,1):
        for sz in(-1,1):
            c=np.array([sx*T,P+TOW+0.24,sz*T])
            o+=[(cyl(c+[0,0.6,0],(0.5,0.6,0.5)),CAR,None,1),(box(c+[0,1.325,0],(0.7,0.25,0.7)),WH,None,1),(sph(c+[0,1.52,0],(0.14,)*3),CY,CYE,1)]
            top=np.array([sx*(kw/2+0.05),kt-0.4,kz+sz*(kd/2+0.05)]);bot=np.array([top[0]+sx,P+0.05,top[2]+sz])
            mid=(top+bot)/2;v=bot-top;L=np.linalg.norm(v)
            # rod approx as box
            ang=math.degrees(math.atan2(math.hypot(v[0],v[2]),-v[1]))
            R=roty(math.degrees(math.atan2(v[0],v[2])))@rotx(ang)
            o.append((box(mid,(0.24,L,0.24),R),GR,None,1))
    cy=P+KEEP*0.55;fz=kz-kd/2
    o+=[(box((0,cy,fz-0.03),(1.6,3.4,0.06)),GR,None,1),(box((0,cy,fz-0.07),(0.5,3.0,0.06)),CY,CYE,1)]
    for s in(-1,1):
        for v in(-1,0,1):o.append((box((s*(kw/2+0.8),P+KEEP/2,kz+v*1.6),(0.08,3.2,1.4),rotz(-25*s)),GR,None,1))
    o.append((cyl((0,kt+0.95,kz),(kd*0.79,0.3,kd*0.79)),GL,(0.03,0.15,0.22),1))
    return o
for tag,o in(('before',before()),('after',after())):
    render(o,f'commons_{tag}_overseer.png',elev=40,azim=-135,zoom=9,center=(0,4,0))
    render(o,f'commons_{tag}_closeup.png',elev=15,azim=-115,zoom=5.5,center=(0,4,-2))
    render(o,f'commons_{tag}_night.png',night=True,elev=40,azim=-135,zoom=9,center=(0,4,0))
