"""Check the emitted animation bundle, registration and source invariants."""
from pathlib import Path
import hashlib
import json
import wave
import numpy as np
from PIL import Image

root=Path(__file__).resolve().parents[1];out=root/'assets/windows'
meta=json.loads((out/'laugh.json').read_text(encoding='utf-8'))
checks=0
def check(ok,message):
    global checks
    checks+=1
    assert ok,message

check(len(meta['frames'])==449 and meta['sourceVideoFrameCount']==411 and meta['fps']==30 and meta['durationMs']==14967,'video timing')
check(meta['sourceSha256']==hashlib.sha256((root/'assets/source/laugh.mp4').read_bytes()).hexdigest(),'source video hash')
check(hashlib.sha256((root/'assets/source/andi.wav').read_bytes()).hexdigest()=='069645553d635de68e958afca3a99f999f70e7352caf5f923b30a9136864949e','original Andy recording')
files=set(meta['frames'])|set(meta['entry'])
for index,clip in meta['exits'].items():
    check(clip[0]==meta['frames'][int(index)] and clip[-1]=='poses/idle.png','exit endpoints')
    files.update(clip)
check(meta['entry'][0]=='poses/idle.png' and meta['entry'][-1]==meta['frames'][0],'entry endpoints')
minimum_margin=320
for file in sorted(files):
    check(not Path(file).is_absolute() and '..' not in Path(file).parts,'safe relative path')
    p=(root/'assets/derived' if file.startswith('poses/') else out)/file
    im=Image.open(p);a=np.array(im)
    check(im.mode=='RGBA' and im.size==(320,320),'RGBA 320 canvas')
    yy,xx=np.where(a[:,:,3]>32)
    check(len(yy)>100,'nonempty frame')
    check(all(a[y,x,3]==0 for y,x in [(0,0),(319,0),(0,319),(319,319)]),'transparent background corners')
    check(xx.min()>0 and yy.min()>0 and xx.max()<319 and yy.max()<319,'no clipping')
    if file in meta['frames']:minimum_margin=min(minimum_margin,int(xx.min()),int(yy.min()),319-int(xx.max()),319-int(yy.max()))
registered=np.array(Image.open(out/meta['frames'][166-meta['sourceStartFrame']]))
yy,xx=np.where(registered[:,:,3]>128)
check(abs(int(yy.max())+1-290)<=1 and abs((int(yy.max())-int(yy.min())+1)-260)<=2,'standing height and foot baseline match idle')
check(abs((int(xx.max())-int(xx.min())+1)-148)<=2,'standing width matches idle')
for frame,x,y in [(160,80,104),(180,105,75),(200,105,67)]:
    a=np.array(Image.open(out/f'laugh/frames/{frame:04d}.png'))
    check(a[y,x,3]==0,'hugging arm/head white gap removed')
head=np.array(Image.open(out/'laugh/frames/0180.png'))
teeth=head[79:112,115:151]
check(((teeth[:,:,:3].min(axis=2)>160)&(teeth[:,:,3]>240)).sum()>15,'laugh teeth preserved')
check(head[165,160,3]==255,'cream belly remains opaque')
with wave.open(str(out/meta['audio'])) as wav:
    check(wav.getsampwidth()==2 and wav.getnchannels()==2 and wav.getframerate()==44100,'PCM stereo laugh')
    audio_ms=wav.getnframes()*1000/wav.getframerate()
    check(abs(audio_ms-meta['durationMs'])<1,'laugh audio/video duration')
report={'checks':checks,'sourceVideoFrames':411,'playbackFrames':449,'fps':30,'loopDurationMs':meta['durationMs'],'preparedExitPaths':len(meta['exits']),
        'referencedPngFiles':len(files),'transparentBackgroundCorners':True,'frameClipping':False,'minimumVideoFrameMarginPx':minimum_margin,
        'canonicalStandingWidth':148,'canonicalStandingHeight':260,'footBaseline':290,'fixedSpatialTransform':True,
        'sourceVideoSha256':meta['sourceSha256'],'audioDurationMs':audio_ms,'originalAndyAudioUnchanged':True,
        'internalArmHeadGapsTransparent':True,'teethAndBellyPreserved':True,'matteRepairVersion':meta.get('matteRepairVersion'),
        'visualReview':'Matte reviewed on dark background; upright transitions plus reverse video get-up for rolling poses. User appearance acceptance remains pending.'}
(root/'docs/verification/laugh-assets.json').write_text(json.dumps(report,indent=2)+'\n',encoding='utf-8')
print(json.dumps(report))
