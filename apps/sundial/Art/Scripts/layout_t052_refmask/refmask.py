# T-SUN-052 reference measurement step (run from the repo root, in this order: refmask, refmask2, refinner, refsoil). Measures proportions only; no reference pixel is used as an asset.
import cv2, numpy as np
im=cv2.imread('shared/assets/art-reference/A2-05-field-notebook-1.png')
hsv=cv2.cvtColor(im,cv2.COLOR_BGR2HSV)
h,s,v=[hsv[...,i].astype(float) for i in range(3)]
cream=(s<75)&(v>150)   # opencv s 0-255
cream=cream.astype(np.uint8)
# restrict to a window around the dial
win=np.zeros_like(cream); win[290:1000,250:1400]=1
cream&=win
cream=cv2.morphologyEx(cream,cv2.MORPH_OPEN,np.ones((5,5),np.uint8))
n,lab,st,_=cv2.connectedComponentsWithStats(cream)
i=1+np.argmax(st[1:,4]); print(st[i])
comp=(lab==i).astype(np.uint8)*255
cv2.imwrite('scratch/ref_cream.png',comp)
