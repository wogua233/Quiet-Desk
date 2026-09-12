"""Reproducible conversion of the attributed Blanket recordings to loopable PCM."""
from pathlib import Path
import sys, json, hashlib, math
ROOT=Path(__file__).resolve().parents[1]
sys.path.insert(0,str(ROOT/'.tools/pylibs'))
import numpy as np
import soundfile as sf
from scipy.signal import resample_poly

report=[]
for name in ('rain','stream','waves','wind','birds','fireplace'):
    source=ROOT/'.tools/sound-source'/f'{name}.ogg'
    audio,rate=sf.read(source,always_2d=True,dtype='float32')
    audio=audio[:min(len(audio),rate*60),:2]
    if audio.shape[1]==1: audio=np.repeat(audio,2,axis=1)
    if rate!=44100:
        g=math.gcd(rate,44100);audio=resample_poly(audio,44100//g,rate//g,axis=0)
    n=min(44100,len(audio)//4)
    t=np.linspace(0,1,n,dtype=np.float32)[:,None]
    # End with the head's last crossfade sample, then wrap to its next sample.
    loop=np.concatenate((audio[n:-n],audio[-n:]*(1-t)+audio[:n]*t))
    peak=float(np.max(np.abs(loop))); rms=float(np.sqrt(np.mean(loop**2)))
    loop*=min(.85/max(peak,1e-6),.18/max(rms,1e-6))
    target=ROOT/'src/QuietDesk/Assets/Sounds'/f'{name}.wav'
    sf.write(target,loop,44100,subtype='PCM_16')
    quantized,_=sf.read(target,always_2d=True)
    seam=float(np.max(np.abs(quantized[-1]-quantized[0])))
    report.append(dict(sound=name,seconds=len(loop)/44100,peak=float(np.max(np.abs(quantized))),seam=seam,source_sha256=hashlib.sha256(source.read_bytes()).hexdigest(),output_sha256=hashlib.sha256(target.read_bytes()).hexdigest()))
(ROOT/'docs/sound-validation.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
print(json.dumps(report,indent=2))
