"""Extract the supplied video into a Windows-only transparent, registered animation.

Requires opencv-python-headless==4.12.0.88, numpy, Pillow and imageio-ffmpeg==0.6.0.
Keep a single spatial/color transform for the entire clip, including the fall.
"""
from pathlib import Path
import argparse
import hashlib
import json
import subprocess
import wave
import cv2
import numpy as np
from PIL import Image, ImageDraw
import imageio_ffmpeg

cv2.setRNGSeed(17)
cv2.setNumThreads(1)
ROOT = Path(__file__).resolve().parents[1]
parser = argparse.ArgumentParser()
parser.add_argument('--video', type=Path, default=ROOT/'assets/source/laugh.mp4')
args = parser.parse_args()
out = ROOT/'assets/windows'
(out/'laugh/frames').mkdir(parents=True, exist_ok=True)
(out/'laugh/exits').mkdir(parents=True, exist_ok=True)
(out/'audio').mkdir(exist_ok=True)
cap = cv2.VideoCapture(str(args.video))
assert cap.isOpened(), 'Cannot open supplied video'
source_fps = cap.get(cv2.CAP_PROP_FPS)
count = int(cap.get(cv2.CAP_PROP_FRAME_COUNT))
start = 51  # Anticipation immediately before the first laugh, not the long idle lead-in.
end = count
fps = 30
frames = []

def matte(frame):
    frame = cv2.resize(frame, (540,737), interpolation=cv2.INTER_AREA)
    rgb = cv2.cvtColor(frame, cv2.COLOR_BGR2RGB)
    hsv = cv2.cvtColor(rgb, cv2.COLOR_RGB2HSV)
    # Background is white; discard its pale floor shadow, retain dark feet and mouth.
    seed = ((hsv[:,:,1] > 52) & (hsv[:,:,2] < 253) &
            (rgb[:,:,0].astype(int) > rgb[:,:,2].astype(int)+18)).astype(np.uint8)
    n, labels, stats, _ = cv2.connectedComponentsWithStats(seed)
    assert n > 1
    mask = (labels == 1+np.argmax(stats[1:,cv2.CC_STAT_AREA])).astype(np.uint8)
    mask = cv2.morphologyEx(mask, cv2.MORPH_CLOSE, np.ones((5,5),np.uint8))
    contours,_ = cv2.findContours(mask, cv2.RETR_EXTERNAL, cv2.CHAIN_APPROX_SIMPLE)
    cv2.drawContours(mask, contours, -1, 1, cv2.FILLED)
    # Refine the border with foreground/background color models. The saturation
    # seed alone cuts into pale belly highlights and retains a brown floor shadow.
    gc=np.zeros(mask.shape,np.uint8)
    band=cv2.dilate(mask,np.ones((61,61),np.uint8))
    gc[band>0]=cv2.GC_PR_BGD
    gc[mask>0]=cv2.GC_PR_FGD
    inner=cv2.erode(mask,np.ones((7,7),np.uint8))>0
    yy=np.arange(mask.shape[0])[:,None]
    last=np.where(mask>0)[0].max()
    sure=inner & ((hsv[:,:,1]>80)|((hsv[:,:,1]>25)&(hsv[:,:,2]>175)&(yy<last-30)))
    gc[sure]=cv2.GC_FGD
    gc[(hsv[:,:,1]<10)&(hsv[:,:,2]>235)]=cv2.GC_BGD
    cv2.grabCut(rgb,gc,None,np.zeros((1,65),np.float64),np.zeros((1,65),np.float64),2,cv2.GC_INIT_WITH_MASK)
    mask=((gc==cv2.GC_FGD)|(gc==cv2.GC_PR_FGD)).astype(np.uint8)
    n,labels,stats,_=cv2.connectedComponentsWithStats(mask)
    mask=(labels==1+np.argmax(stats[1:,cv2.CC_STAT_AREA])).astype(np.uint8)
    contours,_=cv2.findContours(mask,cv2.RETR_EXTERNAL,cv2.CHAIN_APPROX_SIMPLE)
    cv2.drawContours(mask,contours,-1,1,cv2.FILLED)
    # A cast shadow can touch the body and share its brown hue. Follow the last
    # strongly colored body pixel in each column instead of retaining a floor plate.
    strong=(mask>0)&(hsv[:,:,1]>82)
    for xx in range(mask.shape[1]):
        rows=np.where(strong[:,xx])[0]
        if len(rows):mask[rows[-1]+3:,xx]=0
        edge=np.where(mask[:,xx]>0)[0]
        if len(edge) and edge[-1]>600:
            last=int(edge[-1]);first=max(int(edge[0]),last-24)
            values=cv2.GaussianBlur(hsv[first:last+1,xx,2].astype(np.float32).reshape(-1,1),(1,3),.7).ravel()
            valley=first+int(np.argmin(values))
            if float(values[-1]-values.min())>18:mask[valley+2:,xx]=0
    mask=cv2.morphologyEx(mask,cv2.MORPH_OPEN,np.ones((3,3),np.uint8))
    n,labels,stats,_=cv2.connectedComponentsWithStats(mask)
    mask=(labels==1+np.argmax(stats[1:,cv2.CC_STAT_AREA])).astype(np.uint8)
    # The cream belly, green eyes, teeth and tongue are now inside the filled outline.
    alpha = cv2.GaussianBlur(mask.astype(np.float32), (3,3), .55)
    alpha = np.clip((alpha-.06)/.88,0,1)
    # Remove white contamination along the anti-aliased outside boundary.
    clean = np.clip((rgb.astype(float)-255*(1-alpha[:,:,None]))/np.maximum(alpha[:,:,None],.02),0,255)
    return clean.astype(np.uint8), alpha

cap.set(cv2.CAP_PROP_POS_FRAMES, 166)
ok, reference = cap.read(); assert ok
ref_rgb, ref_alpha = matte(reference)
target = np.array(Image.open(ROOT/'assets/derived/poses/idle.png').convert('RGBA'))

def skin_mask(rgb, a):
    hsv=cv2.cvtColor(rgb,cv2.COLOR_RGB2HSV)
    return (a>.95) & (hsv[:,:,1]>85) & (hsv[:,:,0]>12) & (hsv[:,:,0]<40)

ref_lab=cv2.cvtColor(ref_rgb,cv2.COLOR_RGB2LAB).astype(float)
dst_lab=cv2.cvtColor(target[:,:,:3],cv2.COLOR_RGB2LAB).astype(float)
ref_pixels=ref_lab[skin_mask(ref_rgb,ref_alpha)]
dst_pixels=dst_lab[skin_mask(target[:,:,:3],target[:,:,3]/255)]
source_mid=np.median(ref_pixels,axis=0); target_mid=np.median(dst_pixels,axis=0)
# Match the overall light/cream body too, while protecting eyes, hands and mouth.
source_belly=(ref_alpha>.95)&(ref_rgb[:,:,0]>150)&(ref_rgb[:,:,1]>135)&(ref_rgb[:,:,2]>100)
target_belly=(target[:,:,3]>240)&(target[:,:,0]>190)&(target[:,:,1]>175)&(target[:,:,2]>130)
belly_shift=np.median(dst_lab[target_belly],axis=0)-np.median(ref_lab[source_belly],axis=0)

# Reference standing silhouette is registered to the existing 260-pixel body.
ys,xs=np.where(ref_alpha>.5)
scale_y=260/(ys.max()-ys.min()+1)
scale_x=148/(xs.max()-xs.min()+1)
center_x=(xs.max()+xs.min())/2
floor=ys.max()+1
transform=np.array([[scale_x,0,160-center_x*scale_x],[0,scale_y,290-floor*scale_y]],np.float32)
indices=list(range(start,end))
cap.set(cv2.CAP_PROP_POS_FRAMES,start)
for j,i in enumerate(indices):
    ok,bgr=cap.read();assert ok
    rgb,a=matte(bgr)
    hsv=cv2.cvtColor(rgb,cv2.COLOR_RGB2HSV)
    lab=cv2.cvtColor(rgb,cv2.COLOR_RGB2LAB).astype(float)
    yellow_weight=np.clip((hsv[:,:,1].astype(float)-40)/70,0,1)
    warm=(hsv[:,:,0]>10)&(hsv[:,:,0]<45)&(hsv[:,:,2]>105)
    weight=yellow_weight*warm
    shift=(target_mid-source_mid)[None,None,:]*weight[:,:,None]
    cream=(1-yellow_weight)*warm
    shift+=belly_shift[None,None,:]*cream[:,:,None]
    adjusted=cv2.cvtColor(np.clip(lab+shift,0,255).astype(np.uint8),cv2.COLOR_LAB2RGB)
    rgba=np.dstack([adjusted,a*255]).astype(np.uint8)
    # Interpolate premultiplied pixels to prevent black/white fringes.
    pm=rgba.astype(np.float32);pm[:,:,:3]*=pm[:,:,3:]/255
    canvas=cv2.warpAffine(pm,transform,(320,320),flags=cv2.INTER_AREA)
    canvas[:,:,:3]=canvas[:,:,:3]*255/np.maximum(canvas[:,:,3:],1)
    canvas=np.clip(canvas,0,255).astype(np.uint8)
    canvas[canvas[:,:,3]==0,:3]=0
    file=f'laugh/frames/{j:04d}.png'
    Image.fromarray(canvas).save(out/file)
    frames.append(file)
    if j%60==0: print(f'video frames {j}/{len(indices)}',flush=True)
cap.release()

subprocess.run([imageio_ffmpeg.get_ffmpeg_exe(),'-hide_banner','-loglevel','error','-y',
    '-ss',str(start/source_fps),'-i',str(args.video),'-t',str(len(frames)/fps),
    '-vn','-af',f'apad,atrim=duration={len(frames)/fps},asetpts=PTS-STARTPTS','-acodec','pcm_s16le','-ar','44100','-ac','2',str(out/'audio/laugh.wav')],check=True)
# AAC pre-roll can leave a few milliseconds beyond the video slice after seeking.
# Store exactly one video cycle of PCM samples so repeated playback cannot drift.
audio_path=out/'audio/laugh.wav'
with wave.open(str(audio_path)) as wav:
    params=wav.getparams();pcm=wav.readframes(wav.getnframes())
byte_count=round(len(frames)/fps*params.framerate)*params.nchannels*params.sampwidth
pcm=(pcm+b'\0'*max(0,byte_count-len(pcm)))[:byte_count]
with wave.open(str(audio_path),'wb') as wav:
    wav.setparams(params);wav.writeframes(pcm)

metadata={'version':1,'ready':False,'fps':fps,'durationMs':round(len(frames)*1000/fps),
          'frames':frames,'audio':'audio/laugh.wav','exitStride':3,
          'entryDurationMs':320,'exitDurationMs':280,'recoveryFirstFrame':337,'recoveryLastFrame':403,'recoveryExitFrame':333,
          'sourceSha256':hashlib.sha256(args.video.read_bytes()).hexdigest(),
          'sourceStartFrame':start,'sourceFps':source_fps,
          'registration':transform.tolist(),'skinLabShift':(target_mid-source_mid).tolist(),
          'exits':{}}
(out/'laugh.json').write_text(json.dumps(metadata,indent=2)+'\n',encoding='utf-8')
sheet=Image.new('RGBA',(960,640),(70,83,103,255));draw=ImageDraw.Draw(sheet)
chosen=np.linspace(0,len(frames)-1,8,dtype=int)
for j,idx in enumerate(chosen):
    im=Image.open(out/frames[idx]);im.thumbnail((240,280))
    sheet.alpha_composite(im,((j%4)*240,(j//4)*320))
    draw.text(((j%4)*240+6,(j//4)*320+285),f'{idx/fps:.2f}s',fill='white')
preview=ROOT.parent/'outputs/laugh-frames-preview.png';sheet.convert('RGB').save(preview)
print(json.dumps({'frames':len(frames),'durationMs':metadata['durationMs'],'preview':str(preview)},ensure_ascii=False))
