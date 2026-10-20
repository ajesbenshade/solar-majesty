# Rough flat-shaded mirror of the colony HAB (BuildHabitat + DetailHabitat), before (db63256) vs after (art/charging-pods).
# NOT a Unity capture. Proportions approximate; cylinders shown at Unity primitive scale.
import math,numpy as np
ns={};exec(open('/workspace/art/colony-domes/render.py').read().split('CER=(')[0],ns)
box,cyl,render,rotx=ns['box'],ns['cyl'],ns['render'],ns['rotx']
from PIL import Image,ImageDraw
W=4.0;bx=bz=W*0.88;hx=hz=bx/2;tall=2.28;y=0.18+tall/2;top=0.18+tall
TAN=(0.78,0.62,0.46);SEAMT=(0.54,0.40,0.28);WH=(0.88,0.82,0.74);CER=(0.92,0.88,0.82)
GR=(0.16,0.17,0.19);CAR=(0.26,0.24,0.22);OR=(0.96,0.42,0.08);CY=(0.22,0.84,0.98);CYE=(0.1,0.5,0.7)
AM=(0.95,0.74,0.26);AME=(0.35,0.24,0.05)
PV=(0.10,0.16,0.30);WARM=(0.6,0.4,0.15);ST=(0.42,0.44,0.48)
def P(b,c,e=None):return (b,c,e,1)
def common(shell,seam,port,porte,hatch,frame):
    o=[P(box((0,0.08,0),(bx*1.06,0.16,bz*1.06)),GR),P(box((0,y,0),(bx,tall,bz)),shell),
       P(box((0,y+0.12,0),(bx+0.04,0.1,bz+0.04)),seam),P(box((0,y+tall*0.18,0),(bx+0.03,0.045,bz+0.03)),seam)]
    for i in range(3):
        px=(i-1)*bx*0.28
        for s in(-1,1):o.append(P(cyl((px,y+0.22,s*bz*0.51),(0.38,0.05,0.38),rot=rotx(90)),port,porte))
    o+=[P(box((-bx*0.51,y-0.15,0),(0.08,1.15,0.72)),CAR),P(box((-bx*0.5-0.02,y-0.15,0),(0.06,0.95,0.55)),hatch)]
    o.append(P(box((0,top+0.04,-bz*0.06),(bx*0.9,0.06,bz*0.62)),GR))
    for r in range(2):
        for c in range(4):
            at=(-bx*0.21*1.5+c*bx*0.21,top+0.18,-bz*0.08-bz*0.12+r*bz*0.24)
            o+=[P(box(at,(bx*0.18*1.08,0.04,bz*0.22*1.08),rotx(-28)),frame),P(box((at[0],at[1]+0.03,at[2]),(bx*0.18,0.03,bz*0.22),rotx(-28)),PV)]
    for sx in(-1,1):
        for sz in(-1,1):o+=[P(box((sx*bx*0.42,0.28,sz*bz*0.42),(0.38,0.42,0.32)),CAR),P(box((sx*bx*0.42,0.08,sz*bz*0.42),(0.55,0.12,0.46)),GR)]
    return o
def before():
    o=common(TAN,SEAMT,OR,None,OR,OR)
    for x in(-hx*0.55,0,hx*0.55):o+=[P(box((x,top+0.13,-hz+0.36),(0.55,0.26,0.42)),WH),P(cyl((x,top+0.28,-hz+0.36),(0.3,0.01,0.3)),GR)]
    o+=[P(cyl((-hx*0.45,top+0.3,hz-0.58),(0.5,0.6,0.5),rot=np.array([[0,-1,0],[1,0,0],[0,0,1]])),WH),
        P(box((hx*0.2,top+0.11,hz-0.58),(0.5,0.03,0.5)),OR),P(box((-hx-0.12,1.2,0),(0.05,1.2,1.2)),OR),
        P(box((-hx-0.13,1.3,-0.62),(0.1,0.1,0.1)),WARM,(0.5,0.3,0.1)),P(box((-hx-0.13,1.3,0.62),(0.1,0.1,0.1)),WARM,(0.5,0.3,0.1)),
        P(box((hx+0.1,0.35,0),(0.09,0.09,bz-0.8)),OR),P(box((hx*0.62,top+0.4,hz-0.48),(0.5,0.05,0.5),rotx(-30)),ST)]
    return o
def after():
    o=common(WH,GR,AM,AME,GR,GR)
    o+=[P(box((hx*0.2,top+0.11,hz-0.58),(0.5,0.03,0.5)),AM,AME),P(box((-hx-0.12,1.2,0),(0.05,1.2,1.2)),GR),
        P(box((-hx-0.13,1.3,-0.62),(0.1,0.1,0.1)),CY,CYE),P(box((-hx-0.13,1.3,0.62),(0.1,0.1,0.1)),CY,CYE)]
    for cx in(-hx*0.5,hx*0.5):
        o+=[P(box((cx,0.2,-hz-0.4),(0.9,0.06,0.7)),GR),P(box((cx,0.75,-hz-0.03),(0.7,1.1,0.06)),CAR),
            P(box((cx-0.3,0.75,-hz-0.25),(0.08,0.5,0.45)),CAR),P(box((cx+0.3,0.75,-hz-0.25),(0.08,0.5,0.45)),CAR),
            P(box((cx,1.05,-hz-0.07),(0.5,0.06,0.02)),AM,AME)]
    sy=0.18+tall*0.78
    o+=[P(box((0,sy,-hz-0.03),(bx,0.06,0.03)),AM,AME),P(box((0,sy,hz+0.03),(bx,0.06,0.03)),AM,AME),
        P(box((-hx-0.03,sy,0),(0.03,0.06,bz)),AM,AME),P(box((hx+0.03,sy,0),(0.03,0.06,bz)),AM,AME)]
    return o
for tag,fn in(('before',before),('after',after)):
    o=fn()
    render(o,f'pod_{tag}_overseer.png',elev=45,azim=-125,zoom=3.6,center=(0,1.3,0))
    render(o,f'pod_{tag}_closeup.png',elev=12,azim=-110,zoom=2.4,center=(0,1.2,-1))
    render(o,f'pod_{tag}_night.png',night=True,elev=45,azim=-125,zoom=3.6,center=(0,1.3,0))
ims=[[Image.open(f'pod_{t}_{v}.png') for v in('overseer','closeup','night')] for t in('before','after')]
w,h=ims[0][0].size;sheet=Image.new('RGB',(w*3,h*2+60),(20,20,24));d=ImageDraw.Draw(sheet)
for r,row in enumerate(ims):
    for c,im in enumerate(row):
        sheet.paste(im,(c*w,40+r*(h+10)));d.text((c*w+8,46+r*(h+10)),f"{('BEFORE db63256 (tan HAB)','AFTER art/charging-pods')[r]} - {('overseer','close-up','night')[c]}",fill=(255,255,255))
d.text((8,10),"Charging pods (colony HOUSES -> CHARGERS). ROUGH matplotlib mock-ups, NOT Unity captures. Amber charge lights.",fill=(255,220,120))
sheet.save('contact_sheet.png')
