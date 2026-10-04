# T-SUN-052 reference measurement step (run from the repo root, in this order: refmask, refmask2, refinner, refsoil). Measures proportions only; no reference pixel is used as an asset.
import cv2, numpy as np
comp=cv2.imread('scratch/ref_cream.png',0)>0
# drop left patch and hand streak
comp[290:741,250:300]=False   # left patch edge column
comp[:300,:]=False
pts=[]
cx,cy=790,600
H,W=comp.shape
for a in np.arange(0,360,1.0):
    r=np.deg2rad(a); dx,dy=np.cos(r),np.sin(r)
    last=None
    for t in range(150,700):
        x=int(cx+dx*t*1.0); y=int(cy+dy*t*0.62)
        if x<0 or y<0 or x>=W or y>=H: break
        if comp[y,x]: last=(x,y)
    if last: pts.append((a,last))
sel=[]
for a,(x,y) in pts:
    if 250<x<612 and y<420: continue   # left patch region
    if x>1000 and y<560: continue      # hand
    if x<300 and y<745 and y>285 and x<=290 and False: continue
    sel.append((x,y))
sel=np.array(sel,np.float32)
el=cv2.fitEllipse(sel)
print(el)
# robust: drop outliers >6px then refit
def dist(el,p):
    (cx,cy),(w,h),ang=el
    t=np.deg2rad(ang); 
    c,s=np.cos(t),np.sin(t)
    x=(p[:,0]-cx); y=(p[:,1]-cy)
    u=(x*c+y*s)/(w/2); v=(-x*s+y*c)/(h/2)
    return (np.hypot(u,v)-1)*(w+h)/4
d=dist(el,sel); keep=np.abs(d)<8
el=cv2.fitEllipse(sel[keep]); print(el, keep.sum(), len(sel))
import json; json.dump({'ellipse':[list(map(float,el[0])),list(map(float,el[1])),float(el[2])]},open('scratch/ref_ellipse.json','w'))
im=cv2.imread('shared/assets/art-reference/A2-05-field-notebook-1.png')
cv2.ellipse(im,((el[0][0],el[0][1]),el[1],el[2]),(0,0,255),2)
for p in sel[~keep]: cv2.circle(im,(int(p[0]),int(p[1])),4,(255,0,0),-1)
cv2.imwrite('scratch/ref_fit.png',im[250:1000,200:1400])
