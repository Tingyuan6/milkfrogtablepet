"""Use only the supplied video's green effect/audio with the existing bottom pose."""
from pathlib import Path
import hashlib, json, shutil, subprocess, wave
import cv2
import numpy as np
from PIL import Image, ImageDraw
import imageio_ffmpeg

ROOT=Path(__file__).resolve().parents[1]
source=ROOT/'assets/source/fart.mp4'
out=ROOT/'assets/derived'
(out/'effects/fart').mkdir(parents=True,exist_ok=True)
(out/'audio').mkdir(exist_ok=True)
base=np.array(Image.open(out/'poses/bottom.png').convert('RGBA'))
rig=json.loads((out/'face.json').read_text(encoding='utf-8'))['faces']['bottom']
Y,X=np.mgrid[:320,:320]
eyes=[]
for cx,cy,rx,ry in rig['eyes']:
    region=((X-cx)/rx)**2+((Y-cy)/ry)**2<.9
    pupil=region & (base[:,:,:3].max(axis=2)<110) & (base[:,:,3]>240)
    clean=cv2.inpaint(base[:,:,:3],pupil.astype('uint8')*255,3,cv2.INPAINT_TELEA)
    eyes.append((region,pupil,clean,-min(6.8,rx*.85)))

def expression(amount):
    rgb=base[:,:,:3].copy()
    for region,pupil,clean,shift in eyes:
        if not pupil.any():continue
        # Move the actual pupil, filling only its old footprint with iris color.
        # Preserve the eye outline, body and original alpha.
        rgb[pupil]=clean[pupil]
        moved=cv2.warpAffine(pupil.astype('float32'),np.float32([[1,0,shift*amount],[0,1,0]]),(320,320))
        colors=cv2.warpAffine(base[:,:,:3],np.float32([[1,0,shift*amount],[0,1,0]]),(320,320))
        weight=np.clip(moved,0,1)*region
        rgb=np.uint8(np.clip(rgb*(1-weight[:,:,None])+colors*weight[:,:,None],0,255))
    blush=(np.exp(-(((X-247)/8)**2+((Y-204)/4.8)**2)*1.8)+np.exp(-(((X-276)/5)**2+((Y-207)/3.7)**2)*1.8))
    blush=np.clip(blush*.42*amount,0,.45)*(base[:,:,3]>235)
    blush[blush<.01]=0
    rgb=np.uint8(rgb*(1-blush[:,:,None])+np.array([245,92,111])*blush[:,:,None])
    return np.dstack([rgb,base[:,:,3]])

cap=cv2.VideoCapture(str(source));assert cap.isOpened()
start=0;fps=30;count=90
frames=[];eye_centers=[]
for i in range(count):
    t=i/fps;cap.set(cv2.CAP_PROP_POS_MSEC,(start+t)*1000);ok,bgr=cap.read();assert ok
    rgb=cv2.cvtColor(bgr,cv2.COLOR_BGR2RGB)
    hsv=cv2.cvtColor(rgb,cv2.COLOR_RGB2HSV)
    alpha=np.clip((hsv[:,:,1].astype(float)-45)/65,0,1)*((hsv[:,:,0]>33)&(hsv[:,:,0]<95))
    # The source is a screen recording. Only its green pixels are used; feather
    # the recording's clipped edges so no panel-shaped border can remain.
    h,w=alpha.shape;yy,xx=np.mgrid[:h,:w]
    alpha*=np.clip(np.minimum.reduce([xx,w-1-xx,yy,h-1-yy])/15,0,1)
    alpha*=np.clip((1-((xx-w/2)/(w*.49))**2-((yy-h/2)/(h*.49))**2)/.28,0,1)
    clean=np.clip((rgb-255*(1-alpha[:,:,None]))/np.maximum(alpha[:,:,None],.02),0,255)
    rgba=np.dstack([clean,alpha*255]).astype('uint8')
    # Original plume faces right: mirror it to emit from the left rear of bottom.
    grow=min(1,t/.2,(3-t-1/fps)/.4);grow=max(0,grow);grow=grow*grow*(3-2*grow)
    # Fixed transform: the source nozzle lands on the actual rear crease (78,250).
    pm=rgba.astype('float32');pm[:,:,:3]*=pm[:,:,3:]/255
    plume=cv2.warpAffine(pm,np.float32([[-.2,0,82],[0,.24,208]]),(320,320))
    plume[:,:,:3]*=255/np.maximum(plume[:,:,3:],1);plume[:,:,3]*=grow*.9
    plume=np.uint8(np.clip(plume,0,255))
    plume[plume[:,:,3]==0,:3]=0
    canvas=Image.fromarray(plume)
    face=expression(grow) if grow>0 else base
    canvas.alpha_composite(Image.fromarray(face))
    # A narrow jet sits over the crease itself, connecting it to the video plume.
    # Its green color comes from this same source frame, not from the body image.
    green=rgb[(alpha>.5)]
    if len(green) and grow>0:
        color=np.median(green,axis=0)
        jet=np.clip((78-X)/40,0,1)
        strength=np.clip((1+jet*7-np.abs(Y-250))*.7,0,1)*(X>=40)*(X<=78)*grow*.72
        strength=cv2.GaussianBlur(strength,(5,5),.7)
        layer=np.dstack([np.broadcast_to(color,(320,320,3)),strength*255]).astype('uint8')
        canvas.alpha_composite(Image.fromarray(layer))
    if i in (0,89):canvas=Image.fromarray(base)
    file=f'effects/fart/{i:03d}.png';canvas.save(out/file);frames.append(file)
cap.release()
audio=out/'audio/fart.wav'
subprocess.run([imageio_ffmpeg.get_ffmpeg_exe(),'-hide_banner','-loglevel','error','-y','-ss',str(start),'-i',str(source),'-t','3',
    '-vn','-af','apad,atrim=duration=3,afade=t=in:d=0.025,afade=t=out:st=2.92:d=0.08,asetpts=PTS-STARTPTS',
    '-ar','44100','-ac','2','-acodec','pcm_s16le',str(audio)],check=True)
with wave.open(str(audio)) as wav:params=wav.getparams();pcm=wav.readframes(wav.getnframes())
wanted=44100*3*4;pcm=(pcm+b'\0'*wanted)[:wanted]
with wave.open(str(audio),'wb') as wav:wav.setparams(params);wav.writeframes(pcm)
meta={'version':1,'name':'奶蛙','durationMs':3000,'fps':30,'frames':frames,'audio':'audio/fart.wav',
 'sourceStartMs':0,'originalSourceStartMs':9200,'sourceDurationMs':3000,'sourceSha256':hashlib.sha256(source.read_bytes()).hexdigest(),
 'hitEllipse':[78,248,34,28],'emitter':[78,250],'effectBounds':[3,208,83,277],'gaze':'screen-left','blush':True,
 'preservesBasePose':True,'firstAndLastFrameMatchBase':True}
(out/'fart.json').write_text(json.dumps(meta,ensure_ascii=False,indent=2),encoding='utf-8')
sheet=Image.new('RGBA',(960,640),'#34445b');draw=ImageDraw.Draw(sheet)
for j,i in enumerate([0,8,25,45,70,89]):
    sheet.alpha_composite(Image.open(out/frames[i]),((j%3)*320,(j//3)*320));draw.text(((j%3)*320+8,(j//3)*320+8),f'{i/fps:.2f}s',fill='white')
sheet.convert('RGB').save(ROOT.parent/'outputs/fart-preview.png')
print(json.dumps({'frames':count,'durationMs':3000,'sourceSha256':meta['sourceSha256']}))
