# T-SUN-052 reference measurement step (run from the repo root, in this order: refmask, refmask2, refinner, refsoil). Measures proportions only; no reference pixel is used as an asset.
import cv2, numpy as np, json
comp=(cv2.imread('scratch/ref_cream.png',0)>0).astype(np.uint8)
comp[290:741,250:300]=0; comp[:300,:]=0
comp=cv2.morphologyEx(comp,cv2.MORPH_CLOSE,cv2.getStructuringElement(cv2.MORPH_ELLIPSE,(41,41)))
fit=json.load(open('apps/sundial/Art/Scripts/ref_dial_fit.json'))['ellipse']
(cx,cy),(w,h),ang=fit
cx,cy=778.5,602.6
pts=[]
for a in np.arange(0,360,1.0):
    r=np.deg2rad(a); dx,dy=np.cos(r),np.sin(r)
    # march inward from the outer edge until cream stops
    inner=None
    for t in np.arange(1.0,0.5,-0.004):
        x=int(cx+dx*t*500); y=int(cy+dy*t*307)
        if not(0<=x<1824 and 0<=y<1024): continue
        if comp[y,x]: inner=(x,y)
        elif inner is not None: break
    if inner: pts.append((a,inner))
sel=[]
for a,(x,y) in pts:
    if x>1000 and y<560: continue
    if x<612 and y<420: continue
    sel.append((x,y))
sel=np.array(sel,np.float32)
el=cv2.fitEllipse(sel)
def dist(el,p):
    (cx,cy),(w,h),ang=el; t=np.deg2rad(ang); c,s=np.cos(t),np.sin(t)
    x=p[:,0]-cx; y=p[:,1]-cy; u=(x*c+y*s)/(w/2); v=(-x*s+y*c)/(h/2)
    return (np.hypot(u,v)-1)*(w+h)/4
for _ in range(3):
    d=dist(el,sel); keep=np.abs(d)<6
    el=cv2.fitEllipse(sel[keep])
print(el,keep.sum(),len(sel))
json.dump({'inner_ellipse':[list(map(float,el[0])),list(map(float,el[1])),float(el[2])]},open('scratch/ref_inner.json','w'))
im=cv2.imread('shared/assets/art-reference/A2-05-field-notebook-1.png')
cv2.ellipse(im,((el[0][0],el[0][1]),el[1],el[2]),(0,0,255),2)
for p in sel[~keep]: cv2.circle(im,(int(p[0]),int(p[1])),4,(255,0,0),-1)
cv2.imwrite('scratch/ref_inner_fit.png',im[250:1000,200:1400])
