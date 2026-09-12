from pathlib import Path
import sys,json,hashlib,math
ROOT=Path(__file__).resolve().parents[1];sys.path.insert(0,str(ROOT/'.tools/pylibs'))
import numpy as np
import soundfile as sf
from scipy.signal import resample_poly,butter,sosfilt
out=ROOT/'src/QuietDesk/Assets/Sounds';sr=44100
sources={'storm':'storm.ogg','night':'summer-night.ogg','cafe':'coffee-shop.ogg','train':'train.ogg','keyboard':'keyboard.mp3','leaves':'leaves.mp3','fan':'fan.mp3','library':'library.mp3','windowrain':'rain.ogg'}
report=json.loads((ROOT/'docs/sound-validation.json').read_text())[:6]
for name in list(sources)+['white','pink','brown']:
 source=None
 if name in sources:
  source=ROOT/'.tools/sound-source'/sources[name];a,rate=sf.read(source,dtype='float32',always_2d=True);a=a[:rate*180,:2]
  if a.shape[1]==1:a=np.repeat(a,2,axis=1)
  if rate!=sr:
   g=math.gcd(rate,sr);a=resample_poly(a,sr//g,rate//g,axis=0)
  if name=='windowrain':a=sosfilt(butter(2,1700,fs=sr,output='sos'),a,axis=0)
  if name=='storm':a=sosfilt(butter(3,700,fs=sr,output='sos'),a,axis=0);a=np.tanh(a*2)
  if name in ('keyboard','cafe'):a=sosfilt(butter(2,2500,fs=sr,output='sos'),a,axis=0)
  # A short fan source is made into a seamless cycle before repeating, not hard-spliced.
  if len(a)<sr*12:
   n=min(sr//5,len(a)//4);t=np.linspace(0,1,n)[:,None];a=np.concatenate((a[n:-n],a[-n:]*(1-t)+a[:n]*t));a=np.tile(a,(math.ceil(sr*60/len(a)),1))
 else:
  rng=np.random.default_rng({'white':201,'pink':202,'brown':203}[name]);n=sr*90;a=rng.normal(size=(n,2));f=np.fft.rfftfreq(n,1/sr);power={'white':0,'pink':.5,'brown':1}[name];spectrum=np.fft.rfft(a,axis=0);spectrum*=np.maximum(f,25)[:,None]**(-power);spectrum[f<20]=0;a=np.fft.irfft(spectrum,n=n,axis=0)
 a-=a.mean(axis=0);n=min(sr*2,len(a)//4);t=np.linspace(0,1,n)[:,None];a=np.concatenate((a[n:-n],a[-n:]*(1-t)+a[:n]*t))
 peak=np.max(np.abs(a));rms=np.sqrt(np.mean(a*a));target=.10 if name in ('storm','keyboard','library') else .16;a*=min(.7/max(peak,1e-8),target/max(rms,1e-8))
 path=out/(name+'.wav');sf.write(path,a,sr,subtype='PCM_16');q,_=sf.read(path,always_2d=True)
 report.append(dict(sound=name,seconds=len(a)/sr,peak=float(np.max(np.abs(q))),rms=float(np.sqrt(np.mean(q*q))),seam=float(np.max(np.abs(q[-1]-q[0]))),source_file=source.name if source else 'generated seed '+str({'white':201,'pink':202,'brown':203}[name]),source_sha256=hashlib.sha256(source.read_bytes()).hexdigest() if source else None,output_sha256=hashlib.sha256(path.read_bytes()).hexdigest()))
 print(name,round(len(a)/sr,1),flush=True)
(ROOT/'docs/sound-validation-v02.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
