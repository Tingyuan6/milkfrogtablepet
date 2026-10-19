"""Generate silhouette/flow inbetweens, including a return path every 100ms."""
from pathlib import Path
import json
import wave
import cv2
import numpy as np
from PIL import Image

root=Path(__file__).resolve().parents[1]
out=root/'assets/windows'
meta=json.loads((out/'laugh.json').read_text(encoding='utf-8'))
idle=np.array(Image.open(root/'assets/derived/poses/idle.png').convert('RGBA')).astype(np.float32)
canonical_guide=np.array([30,53,70,118,193,239,290],np.float32)
Y,X=np.mgrid[0:320,0:320].astype(np.float32)

def remap(a,x,y):
    return cv2.remap(a,x.astype(np.float32),y.astype(np.float32),cv2.INTER_LINEAR,borderMode=cv2.BORDER_CONSTANT)

def sprite(a,is_idle=False):
    a=a.copy();a[:,:,:3]*=a[:,:,3:]/255
    opaque=a[:,:,3]>32
    ys,xs=np.where(opaque)
    top,bottom=ys.min(),ys.max()+1
    guide=canonical_guide.copy() if is_idle else top+(bottom-top)*np.array([0,.09,.16,.34,.63,.82,1],np.float32)
    left=np.array([np.where(row)[0].min()-1 if row.any() else np.nan for row in opaque])
    right=np.array([np.where(row)[0].max()+1 if row.any() else np.nan for row in opaque])
    for r in (left,right):
        valid=np.where(~np.isnan(r))[0];r[:]=np.interp(np.arange(320),valid,r[valid])
    sy=np.interp(Y,canonical_guide,guide)
    l=np.interp(sy,np.arange(320),left);r=np.interp(sy,np.arange(320),right)
    ca=remap(a,l+(X-86)/148*(r-l),sy)
    ca[(Y<30)|(Y>=290)|(X<86)|(X>234)]=0
    gray=(ca[:,:,:3]@np.array([.299,.587,.114])+96*(1-ca[:,:,3]/255)).astype(np.uint8)
    return {'rgba':a,'guide':guide,'left':left,'right':right,'canonical':ca,'gray':gray}

base=sprite(idle,True)
def transition(a,b,folder,first,last,count):
    folder_path=out/folder;folder_path.mkdir(parents=True,exist_ok=True)
    f=cv2.calcOpticalFlowFarneback(a['gray'],b['gray'],None,.5,4,31,5,7,1.5,256)
    r=cv2.calcOpticalFlowFarneback(b['gray'],a['gray'],None,.5,4,31,5,7,1.5,256)
    f=np.clip(f,-48,48);r=np.clip(r,-48,48)
    files=[first]
    for i in range(1,count-1):
        t=i/(count-1);t=t*t*(3-2*t)
        guide=a['guide']*(1-t)+b['guide']*t
        ya=np.interp(Y,guide,a['guide']);yb=np.interp(Y,guide,b['guide'])
        left=np.interp(ya,np.arange(320),a['left'])*(1-t)+np.interp(yb,np.arange(320),b['left'])*t
        right=np.interp(ya,np.arange(320),a['right'])*(1-t)+np.interp(yb,np.arange(320),b['right'])*t
        cx=86+(X-left)/np.maximum(1,right-left)*148
        cy=np.interp(Y,guide,canonical_guide)
        def warp(s,flow,fraction):
            sx=cx.copy();sy=cy.copy()
            for _ in range(3):
                v=remap(flow,sx,sy);sx=cx-fraction*v[:,:,0];sy=cy-fraction*v[:,:,1]
            return remap(s['canonical'],sx,sy)
        frame=warp(a,f,t)*(1-t)+warp(b,r,1-t)*t
        frame[(Y<guide[0])|(Y>=guide[-1])|(X<left)|(X>right)]=0
        frame[:,:,:3]*=255/np.maximum(frame[:,:,3:],1)
        frame=np.clip(frame,0,255).astype(np.uint8);frame[frame[:,:,3]==0,:3]=0
        file=f'{folder}/{i:02d}.png';Image.fromarray(frame).save(out/file);files.append(file)
    files.append(last)
    return files

original=meta.get('sourceFrames',meta['frames'])
old_exits=meta['exits'].copy() if meta.get('ready') else {}
first=sprite(np.array(Image.open(out/original[0]).convert('RGBA')).astype(np.float32))
# The supplied video cuts from the floor to standing near its end. Replace that
# edit with the original fall played backwards, followed by a short optical bridge.
standing=sprite(np.array(Image.open(out/original[333]).convert('RGBA')).astype(np.float32))
loop=transition(standing,first,'laugh/loop',original[333],original[0],11)
meta['sourceFrames']=original
meta['sourceVideoFrameCount']=len(original)
meta['frames']=original[:404]+[original[i] for i in range(401,332,-2)]+loop[1:]
meta['durationMs']=round(len(meta['frames'])*1000/meta['fps'])
meta['recoveryForwardFirstFrame']=404
meta['recoveryForwardExitFrame']=438
meta['exits']={}
audio_path=out/meta['audio']
with wave.open(str(audio_path)) as wav:
    params=wav.getparams();pcm=wav.readframes(wav.getnframes())
wanted=round(len(meta['frames'])/meta['fps']*params.framerate)*params.nchannels*params.sampwidth
pcm=(pcm+b'\0'*max(0,wanted-len(pcm)))[:wanted]
with wave.open(str(audio_path),'wb') as wav:
    wav.setparams(params);wav.writeframes(pcm)
meta['entry']=transition(base,first,'laugh/entry','poses/idle.png',meta['frames'][0],11)
for index in sorted(set(range(0,len(meta['frames']),meta['exitStride']))|{len(meta['frames'])-1}):
    file=meta['frames'][index]
    cached=old_exits.get(str(index))
    if cached and cached[0]==file and all((root/'assets/derived' if f.startswith('poses/') else out).joinpath(f).is_file() for f in cached):
        meta['exits'][str(index)]=cached
        continue
    a=sprite(np.array(Image.open(out/file).convert('RGBA')).astype(np.float32))
    meta['exits'][str(index)]=transition(a,base,f'laugh/exits/{index:04d}',file,'poses/idle.png',9)
    if index%60==0:print(f'exit bridges {index}/{len(meta["frames"])}',flush=True)
meta['ready']=True
(out/'laugh.json').write_text(json.dumps(meta,indent=2)+'\n',encoding='utf-8')
print('Entry and interruptible exit bridges ready.',flush=True)
