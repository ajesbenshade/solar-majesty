# Rough flat-shaded mirror of HeroCitadel (Commons) before (#93 / db63256) vs after (art/ai-core-v2). NOT a Unity capture.
import sys,math,numpy as np
src=open('/workspace/art/colony-domes/render.py').read().split('CER=(')[0]
ns={};exec(src,ns);box,cyl,sph,render,rotx=ns['box'],ns['cyl'],ns['sph'],ns['render'],ns['rotx']
old=open('/workspace/art/ai-core/render.py').read().split('for tag,o in')[0].split("ns={};exec(src,ns)")[1]
g=dict(ns);g.update(dict(box=box,cyl=cyl,sph=sph,render=render,rotx=rotx,np=np,math=math));exec(old.split('\n',1)[1],g)
before=g['after']  # #93 state == current main
WH,CAR,GR,OR,CY,CYE,GL=g['WH'],g['CAR'],g['GR'],g['OR'],g['CY'],g['CYE'],g['GL']
P=0.4;WALL=2.1;TOW=5.6;KEEP=6.6;H=6.0;T=H-1.7;kw=H*0.78;kd=H*0.66;kz=H*0.08;kt=P+KEEP;crown=P+KEEP+4.55
y1=P+KEEP*0.45;y2=P+KEEP*0.8
def beam(a,b,t,col,e=None):
    a=np.array(a,float);b=np.array(b,float);v=b-a;L=np.linalg.norm(v);mid=(a+b)/2
    ang=math.degrees(math.atan2(math.hypot(v[0],v[2]),v[1]));R=g['roty'](math.degrees(math.atan2(v[0],v[2])))@rotx(ang)
    return (box(mid,(t,L,t),R),col,e,1)
def after():
    o=[(box((0,P/2,0),(2*H-0.6,P,2*H-0.6)),CAR,None,1)]
    span=T*2-3;wy=P+WALL/2;gh=1.7;fs=(span-2*gh)/2;fx=gh+fs/2
    o+=[(box((0,wy,T),(span,WALL,1)),WH,None,1),(box((-T,wy,0),(1,WALL,span)),WH,None,1),(box((T,wy,0),(1,WALL,span)),WH,None,1),
        (box((-fx,wy,-T),(fs,WALL,1)),WH,None,1),(box((fx,wy,-T),(fs,WALL,1)),WH,None,1)]
    for s in(-1,1):o.append((box((s*(gh+0.55),P+1.6,-T-0.2),(1.1,3.2,1.5)),WH,None,1))
    o+=[(box((0,P+2.9,-T-0.2),(2*gh+0.2,0.8,1.5)),WH,None,1),(box((0,P+2.9,-T-0.98),(2*gh+2.2,0.14,0.06)),OR,None,1)]
    for z in(-T-0.53,T+0.53):o.append((box((0,P+WALL-0.2,z),(span,0.08,0.04)),CY,CYE,1))
    for x in(-T-0.53,T+0.53):o.append((box((x,P+WALL-0.2,0),(0.04,0.08,span)),CY,CYE,1))
    for sx in(-1,1):
        for sz in(-1,1):
            c=np.array([sx*T,0,sz*T]);yb=P+0.5;yt=P+TOW
            def C(q,y):
                r=0.75+(0.45-0.75)*(y-yb)/(yt-yb);qx=-1 if q in(0,3) else 1;qz=-1 if q<2 else 1
                return c+[qx*r,y,qz*r]
            o.append((cyl(c+[0,P+TOW/2,0],(0.55,TOW/2,0.55)),CAR,None,1));o.append((cyl(c+[0,P+0.25,0],(1.8,0.25,1.8)),CAR,None,1))
            for q in range(4):o.append(beam(C(q,yb),C(q,yt),0.12,CAR))
            for b in range(4):
                y=yb+(yt-yb)*b/3
                for q in range(4):o.append(beam(C(q,y),C((q+1)%4,y),0.07,GR))
            for b in range(3):
                ya=yb+(yt-yb)*b/3;yc=yb+(yt-yb)*(b+1)/3
                for q in range(4):n=(q+1)%4;o+=[beam(C(q,ya),C(n,yc),0.05,CAR),beam(C(n,ya),C(q,yc),0.05,CAR)]
            o+=[(box(c+[0,P+TOW+0.22,0],(1.3,0.45,1.3)),WH,None,1),(sph(c+[0,P+TOW+0.61,0],(0.26,)*3),CY,CYE,1)]
    w2,d2,w3,d3=kw*0.74,kd*0.74,kw*0.5,kd*0.5
    o+=[(box((0,P+(y1-P)/2,kz),(kw,y1-P,kd)),WH,None,1),(box((0,y1+0.08,kz),(kw+0.3,0.16,kd+0.3)),GR,None,1),
        (box((0,(y1+0.16+y2)/2,kz),(w2,y2-y1-0.16,d2)),WH,None,1),(box((0,y2+0.05,kz),(w2+0.16,0.1,d2+0.16)),OR,None,1),
        (box((0,(y2+0.1+kt)/2,kz),(w3,kt-y2-0.1,d3)),WH,None,1),(box((0,kt+0.06,kz),(kw*0.56,0.12,kd*0.56)),GR,None,1),
        (cyl((0,kt+1.26,kz),(1.5,1.14,1.5)),WH,None,1),(cyl((0,kt+2.45,kz),(1.58,0.05,1.58)),OR,None,1),
        (cyl((0,(kt+2.5+crown)/2,kz),(0.8,(crown-kt-2.5)/2,0.8)),WH,None,1),(cyl((0,crown,kz),(1.0,0.04,1.0)),GR,None,1),
        (sph((0,crown+0.2,kz),(0.3,)*3),OR,(0.5,0.2,0.03),1)]
    # core band + core window + tier3 seams + spire ring
    for (x,z,sx,sz) in((0,kz-d2/2-0.03,w2-0.1,0.05),(0,kz+d2/2+0.03,w2-0.1,0.05),(-w2/2-0.03,kz,0.05,d2-0.1),(w2/2+0.03,kz,0.05,d2-0.1)):
        o.append((box((x,y2-0.45,z),(sx,0.22,sz)),CY,CYE,1))
    cy=(y1+y2)/2-0.15;fz=kz-d2/2
    o+=[(box((0,cy,fz-0.03),(1.3,1.5,0.06)),GR,None,1),(box((0,cy,fz-0.07),(0.42,1.3,0.06)),CY,CYE,1)]
    for s in(-1,1):o.append((box((s*0.45,(y2+kt)/2,kz-d3/2-0.02),(0.06,kt-y2-0.5,0.04)),CY,CYE,1))
    for r in range(8):
        a=math.radians(r*45);o.append((box((0.76*math.sin(a),kt+1.4,kz+0.76*math.cos(a)),(0.26,0.5,0.04),g['roty'](r*45)),CY,CYE,1))
    for s in(-1,1):
        for v in range(-2,3):o.append((box((s*(kw/2+0.3),P+(y1-P)/2,kz+v*kd*0.18),(0.6,y1-P-0.6,0.06)),GR,None,1))
        for v in(-1,0,1):o.append((box((s*(w2/2+0.25),(y1+y2)/2+0.08,kz+v*d2*0.28),(0.5,y2-y1-0.6,0.06)),GR,None,1))
    for x in np.arange(-kw/2+0.6,kw/2-0.6,0.9):
        if abs(x)<1.1: continue
        o.append((box((x,P+(y1-P)/2,kz-kd/2-0.03),(0.05,y1-P-0.5,0.05)),GR,None,1))
    o.append((box((0,P+1.0,kz-kd/2-0.03),(1.5,2.0,0.05)),GR,None,1))
    for sx in(-1,1):
        for sz in(-1,1):
            top=np.array([sx*(kw/2+0.05),y1-0.3,kz+sz*(kd/2+0.05)]);o.append(beam(top,top+[sx*0.8,P+0.05-top[1],sz*0.8],0.24,GR))
    return o
import os
for tag,fn in(('before',before),('after',after)):
    o=fn()
    render(o,f'commons_{tag}_overseer.png',elev=40,azim=-135,zoom=9,center=(0,4,0))
    render(o,f'commons_{tag}_closeup.png',elev=15,azim=-115,zoom=5.5,center=(0,4,-2))
    render(o,f'commons_{tag}_night.png',night=True,elev=40,azim=-135,zoom=9,center=(0,4,0))
