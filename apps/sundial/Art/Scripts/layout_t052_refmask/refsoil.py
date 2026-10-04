# T-SUN-052 reference measurement step (run from the repo root, in this order: refmask, refmask2, refinner, refsoil). Measures proportions only; no reference pixel is used as an asset.
import cv2, numpy as np, json, sys
sys.path.insert(0,'apps/sundial/Art/Scripts')
import layout_t052_metrics as M
im=cv2.imread('shared/assets/art-reference/A2-05-field-notebook-1.png')
hsv=cv2.cvtColor(im,cv2.COLOR_BGR2HSV)
top=M.top_mask()
v=hsv[...,2]; s=hsv[...,1]; h=hsv[...,0]
dark=(v<125)&top
# drop green plants (hue 35..85 in opencv 0-180) and the gnomon (grey metal)
green=((h>30)&(h<90)&(s>60))
dark&=~green
dark=dark.astype(np.uint8)
dark=cv2.morphologyEx(dark,cv2.MORPH_CLOSE,cv2.getStructuringElement(cv2.MORPH_ELLIPSE,(61,61)))
dark=cv2.morphologyEx(dark,cv2.MORPH_OPEN,cv2.getStructuringElement(cv2.MORPH_ELLIPSE,(25,25)))
n,lab,st,_=cv2.connectedComponentsWithStats(dark)
i=1+np.argmax(st[1:,4]); m=(lab==i).astype(np.uint8)
ff=m.copy(); cv2.floodFill(ff,np.zeros((M.H+2,M.W+2),np.uint8),(0,0),2); m=((m>0)|(ff==0)).astype(np.uint8)
cv2.imwrite('scratch/ref_soil.png',m*255)
vis=im.copy(); vis[m>0]=(vis[m>0]*0.5+np.array([0,0,255])*0.5).astype(np.uint8)
cv2.imwrite('scratch/ref_soil_vis.png',vis[250:1000,200:1400])
print(m.sum(), top.sum(), m.sum()/top.sum())

# refine: inner-ring clip and a far-edge polyline read off the reference (absolute px)
pts=np.array([(205,395),(300,340),(400,318),(500,305),(560,300),(640,304),(700,322),(780,335),(860,350),(930,372),(965,410)],np.float32)
pts[:,0]+=200; pts[:,1]+=250
xs=np.arange(M.W); edge=np.interp(xs,pts[:,0],pts[:,1],left=pts[0,1],right=pts[-1,1])
yy=np.arange(M.H)[:,None]
clip=(yy>edge[None,:]-3)
f=M.ref_fit(); (cx,cy),(w,h_),ang=f['ellipse']
inner=np.zeros((M.H,M.W),np.uint8); (icx,icy),(iw,ih),iang=f['inner_ellipse']; cv2.ellipse(inner,((icx,icy),(iw*0.995,ih*0.995),iang),255,-1)
m2=(m>0)&clip&(inner>0)
m2=cv2.morphologyEx(m2.astype(np.uint8),cv2.MORPH_OPEN,cv2.getStructuringElement(cv2.MORPH_ELLIPSE,(15,15)))
cv2.imwrite('scratch/ref_soil.png',m2*255)
vis=im.copy(); vis[m2>0]=(vis[m2>0]*0.5+np.array([0,0,255])*0.5).astype(np.uint8)
cv2.imwrite('scratch/ref_soil_vis.png',vis[250:1000,200:1400])
print('refined',m2.sum(), m2.sum()/top.sum())
