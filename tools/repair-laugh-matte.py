"""Remove the video's white interior background without removing teeth or belly."""
from pathlib import Path
import json
import cv2
import numpy as np
from PIL import Image, ImageDraw

root=Path(__file__).resolve().parents[1];out=root/'assets/windows'
meta=json.loads((out/'laugh.json').read_text(encoding='utf-8'))
changes=[]
for index,file in enumerate(meta.get('sourceFrames',meta['frames'])):
    if not 150<=index<=214:continue
    path=out/file;a=np.array(Image.open(path).convert('RGBA'));rgb=a[:,:,:3].astype(float)
    bright=(rgb.min(axis=2)>237)&((rgb.max(axis=2)-rgb.min(axis=2))<18)&(a[:,:,3]>0)
    # Teeth bordering the dark reddish mouth cavity must remain opaque.
    cavity=(rgb.max(axis=2)<110)&(a[:,:,3]>150)
    protect=np.zeros((320,320),'uint8')
    contours,_=cv2.findContours(cavity.astype('uint8'),cv2.RETR_EXTERNAL,cv2.CHAIN_APPROX_SIMPLE)
    mouth_candidates=[]
    for contour in contours:
        x,y,w,h=cv2.boundingRect(contour)
        if 90<=x<=175 and y<180 and w>=10 and h>=8 and cv2.contourArea(contour)>max(25,w*h*.3):mouth_candidates.append(contour)
    def mouth_score(contour):
        _,_,w,h=cv2.boundingRect(contour);area=cv2.contourArea(contour)
        return area*area/(w*h)
    if mouth_candidates:cv2.drawContours(protect,[cv2.convexHull(max(mouth_candidates,key=mouth_score))],-1,1,cv2.FILLED)
    protect=cv2.dilate(protect,np.ones((3,3),'uint8'))>0
    holes=bright&~protect&(np.arange(320)[:,None]<125)
    remove=holes
    if remove.any():
        # Small feather softens the new inner boundary, while leaving warm skin.
        strength=cv2.GaussianBlur(remove.astype('float32'),(3,3),.5)
        strength=np.maximum(strength,remove.astype('float32'))
        a[:,:,3]=np.uint8(a[:,:,3]*(1-strength));a[a[:,:,3]==0,:3]=0
        Image.fromarray(a).save(path);changes.append({'file':file,'removedPixels':int(remove.sum())})
if meta.get('ready'):
    changed=set(meta.get('sourceFrames',meta['frames'])[150:215])
    meta['reusableExits']={k:v for k,v in meta['exits'].items() if v[0] not in changed}
meta['ready']=False;meta['matteRepairVersion']=2
(out/'laugh.json').write_text(json.dumps(meta,indent=2),encoding='utf-8')
report={'method':'Remove neutral-white internal video background, preserve warm belly and dark-cavity teeth','changedSourceFrames':len(changes),'removedPixels':sum(x['removedPixels'] for x in changes),'frames':changes}
if changes:(root/'docs/verification/laugh-matte-repair.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
sheet=Image.new('RGBA',(960,320),'#34445b')
for j,i in enumerate([160,180,200]):sheet.alpha_composite(Image.open(out/f'laugh/frames/{i:04d}.png'),(j*320,0))
sheet.convert('RGB').save(root.parent/'outputs/laugh-matte-fixed.png')
print(json.dumps({k:v for k,v in report.items() if k!='frames'}),flush=True)
