# Python mirror of UiIcons primitives + the new DrawCoin (EU cell). y-up like Unity.
import numpy as np, math
from PIL import Image
S=64
def C(r,g,b,a=1.0): return np.array([r,g,b,a],float)
px=np.zeros((S,S,4))
def blend(x,y,c,cov):
    if x<0 or y<0 or x>=S or y>=S: return
    cov=min(max(cov,0),1)
    if cov<=0: return
    d=px[y,x]; a=c[3]*cov; oa=a+d[3]*(1-a)
    if oa<=1e-4: px[y,x]=0; return
    rgb=(c[:3]*a+d[:3]*d[3]*(1-a))/oa; px[y,x]=[*rgb,oa]
def disc(cx,cy,r,c):
    for y in range(S):
        for x in range(S):
            blend(x,y,c,r-math.hypot(x-cx,y-cy)+0.5)
def rbox(x0,y0,x1,y1,r,c):
    cx,cy=(x0+x1)/2,(y0+y1)/2; hx,hy=(x1-x0)/2-r,(y1-y0)/2-r
    for y in range(S):
        for x in range(S):
            qx=abs(x+0.5-cx)-hx; qy=abs(y+0.5-cy)-hy
            d=math.hypot(max(qx,0),max(qy,0))+min(max(qx,qy),0)-r
            blend(x,y,c,0.5-d)
def sgn(p1,p2,p3): return (p1[0]-p3[0])*(p2[1]-p3[1])-(p2[0]-p3[0])*(p1[1]-p3[1])
def tri(a,b,cc,col):
    for y in range(S):
        for x in range(S):
            h=0
            for sy in (0,1):
                for sx in (0,1):
                    p=(x+0.25+sx*0.5,y+0.25+sy*0.5)
                    d1,d2,d3=sgn(p,a,b),sgn(p,b,cc),sgn(p,cc,a)
                    if not((d1<0 or d2<0 or d3<0) and (d1>0 or d2>0 or d3>0)): h+=0.25
            blend(x,y,col,h)
Ink=C(0.05,0.06,0.08); Bone=C(0.94,0.94,0.92); Carbon=C(0.10,0.11,0.13)
Cy=C(0.25,0.88,1.0); Halo=C(0.25,0.88,1.0,0.35)
# outline
rbox(15,3,49,55,8,Ink); rbox(24,51,40,61,3,Ink)
# ceramic shell + terminal
rbox(18,6,46,52,6,Bone); rbox(27,52,37,58,1.5,Bone)
# carbon window
rbox(22,10,42,48,3.5,Carbon)
# glow
disc(32,29,11,Halo)
# bolt: dark outline pass then cyan then white core
def bolt(o,col):
    tri((36+o*0.3,46+o),(24-o,27-o*0.3),(34+o,27-o*0.3),col)
    tri((28-o*0.3,12-o),(40+o,31+o*0.3),(30-o,31+o*0.3),col)
bolt(1.6,Ink); bolt(0,Cy)
tri((34.5,41),(29,30),(32.5,30),C(1,1,1,0.85))
img=Image.fromarray((np.flipud(px)*255).round().astype(np.uint8),'RGBA'); img.save('eu_icon_64_master.png')
for s in (16,24,32,64):
    ic=img.resize((s,s),Image.LANCZOS)
    for name,bg in (('dark',(18,20,24)),('light',(232,230,224))):
        b=Image.new('RGBA',(s*4,s*4),bg+(255,)); big=ic.resize((s*4,s*4),Image.NEAREST)
        Image.new('RGBA',(s+8,s+8),bg+(255,)).save('/dev/null','PNG') if False else None
        canvas=Image.new('RGBA',(s+16,s+16),bg+(255,)); canvas.alpha_composite(ic,(8,8)); canvas.save(f'eu_icon_{s}px_{name}.png')
# sheet: actual-size row + 4x zoom row
W=900;H=260; sheet=Image.new('RGBA',(W,H*2),(0,0,0,255))
for row,(name,bg) in enumerate((('dark',(18,20,24)),('light',(232,230,224)))):
    part=Image.new('RGBA',(W,H),bg+(255,)); x=20
    for s in (16,24,32,64):
        ic=img.resize((s,s),Image.LANCZOS); part.alpha_composite(ic,(x,20))
        z=ic.resize((s*4 if s<64 else 128,)*2,Image.NEAREST); part.alpha_composite(z,(x,100)); x+=max(s*4,128)+30
    sheet.paste(part,(0,row*H))
sheet.save('eu_icon_preview_sheet.png')
