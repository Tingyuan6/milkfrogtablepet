from pathlib import Path
import json,hashlib,wave
import numpy as np
from PIL import Image
root=Path(__file__).resolve().parents[1];out=root/'assets/derived'
meta=json.loads((out/'fart.json').read_text(encoding='utf-8'))
checks=0
def check(ok,msg):
    global checks
    checks+=1;assert ok,msg
base=np.array(Image.open(out/'poses/bottom.png').convert('RGBA'))
check(meta['name']=='奶蛙','character name')
check(meta['durationMs']==3000 and meta['fps']==30 and len(meta['frames'])==90,'three second video')
check(meta['sourceSha256']==hashlib.sha256((root/'assets/source/fart.mp4').read_bytes()).hexdigest(),'supplied video')
Y,X=np.mgrid[:320,:320]
protected=(base[:,:,3]==255)&~((X>230)&(Y>174)&(Y<218))&~((X>=38)&(X<=80)&(Y>=239)&(Y<=261))
for i,file in enumerate(meta['frames']):
    a=np.array(Image.open(out/file).convert('RGBA'))
    check(a.shape==(320,320,4),'sprite dimensions')
    check(np.array_equal(a[protected],base[protected]),'body unchanged except expression')
    check(not a[0,:,3].any() and not a[-1,:,3].any() and not a[:,0,3].any() and not a[:,-1,3].any(),'no clipping/background')
    if i in [0,89]:check(np.array_equal(a,base),'smooth original endpoints')
mid=np.array(Image.open(out/meta['frames'][30]))
eye=((X-258.5)/7.8)**2+((Y-190)/9)**2<.9
old=eye&(base[:,:,:3].max(2)<85);new=eye&(mid[:,:,:3].max(2)<85)
shift=float(X[new].mean()-X[old].mean());check(shift<-3,'far-left gaze')
check(int(mid[204,247,0])-int(mid[204,247,1])>int(base[204,247,0])-int(base[204,247,1])+15,'blush')
check(((mid[:,:,1]>mid[:,:,0]+10)&(mid[:,:,1]>mid[:,:,2]+20)&(X<80)&(mid[:,:,3]>64)).sum()>120,'video green plume')
check(meta['emitter']==[78,250] and mid[250,77,1]>base[250,77,1],'green jet comes from rear crease')
with wave.open(str(out/meta['audio'])) as w:
    check(w.getframerate()==44100 and w.getnchannels()==2 and w.getsampwidth()==2,'PCM audio')
    check(w.getnframes()==132300,'three second audio')
    pcm=np.frombuffer(w.readframes(w.getnframes()),dtype='<i2');check(np.max(np.abs(pcm.astype(int)))>500,'non-silent video sound')
report={'checks':checks,'durationMs':3000,'frames':90,'sourceStartMs':meta['sourceStartMs'],'leftPupilShiftPx':shift,'blush':True,'bodyPreserved':True,'transparentBackground':True,'sourceSha256':meta['sourceSha256']}
(root/'docs/verification/fart-assets.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8');print(json.dumps(report))
