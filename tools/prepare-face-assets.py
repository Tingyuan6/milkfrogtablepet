"""Prepare deterministic facial rig data and a waveform-driven speech envelope."""
from pathlib import Path
import array
import hashlib
import json
import math
import shutil
import sys
import wave

root = Path(__file__).resolve().parents[1]
source = root / 'assets/source/andi.wav'
with wave.open(str(source)) as audio:
    assert audio.getsampwidth() == 2 and audio.getcomptype() == 'NONE'
    rate, channels = audio.getframerate(), audio.getnchannels()
    samples = array.array('h', audio.readframes(audio.getnframes()))
    if sys.byteorder != 'little': samples.byteswap()
    mono = [sum(samples[i:i+channels]) / channels / 32768 for i in range(0,len(samples),channels)]
duration = len(mono) * 1000 / rate
levels = []
for ms in range(0, math.ceil(duration)+1, 10):
    segment = mono[max(0,int((ms-10)*rate/1000)):min(len(mono),int((ms+20)*rate/1000))]
    rms = math.sqrt(sum(x*x for x in segment)/max(1,len(segment)))
    change = sum(abs(segment[i]-segment[i-1]) for i in range(1,len(segment)))/max(1,len(segment))
    levels.append((rms, change))
peak = max(.001, sorted(x[0] for x in levels)[int(len(levels)*.95)])
envelope = []
for rms,change in levels:
    opening = max(0,min(1,(rms/peak-.055)/.945)) ** .65
    rounding = max(0,min(1,.85-change/max(.001,rms)*.5))
    envelope.append([round(opening,4),round(rounding,4)])
envelope[0][0] = envelope[-1][0] = 0
rig = {'version':1,'blinkPeriodMs':3000,'blinkDurationMs':240,
       'faces':json.loads((root/'assets/source/faces.json').read_text(encoding='utf-8')),
       'speech':{'file':'audio/andi.wav','durationMs':round(duration),'stepMs':10,'samples':envelope,
                 'sourceSha256':hashlib.sha256(source.read_bytes()).hexdigest()}}
out = root/'assets/derived'
(out/'audio').mkdir(parents=True,exist_ok=True)
shutil.copy2(source,out/'audio/andi.wav')
(out/'face.json').write_text(json.dumps(rig,indent=2)+'\n',encoding='utf-8')
print(json.dumps({'durationMs':duration,'envelopeSamples':len(envelope),'faces':len(rig['faces'])}))
