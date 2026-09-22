import sys, time, numpy as np
from PIL import Image
import bodies
def hillshade(H, step, az=315, alt=35):
    gy, gx = np.gradient(H, step)
    slope = np.arctan(np.hypot(gx, gy)); aspect = np.arctan2(-gx, gy)
    a = np.radians(az); b = np.radians(alt)
    return np.clip(np.sin(b)*np.cos(slope) + np.cos(b)*np.sin(slope)*np.cos(a - aspect), 0, 1)
out=[]
for name in sys.argv[1].split(","):
    t=time.time(); X,Z,step,H,m = bodies.GENS[name](7)
    print(name, "%.1fs"%(time.time()-t), "range %.2f..%.2f"%(H.min(),H.max()))
    hs = hillshade(H, step)
    hn = (H-H.min())/(H.max()-H.min())
    rgb = np.stack([hs*0.75+hn*0.25]*3, -1)
    out.append((np.flipud(rgb)*255).astype(np.uint8))
Image.fromarray(np.concatenate(out,1)).save(sys.argv[2])
