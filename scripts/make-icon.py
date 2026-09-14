"""Original QuietDesk Gaussian wave-packet mark; Pillow raster export, no external art."""
from pathlib import Path
from PIL import Image, ImageDraw
import math
root=Path(__file__).resolve().parents[1]
assets=root/'src/QuietDesk/Assets'
S=1024
image=Image.new('RGBA',(S,S));mask=Image.new('L',(S,S))
ImageDraw.Draw(mask).rounded_rectangle((32,32,992,992),radius=228,fill=255)
pixels=image.load()
for y in range(S):
 for x in range(S):
  t=y/S;light=max(0,1-math.hypot(x/S-.3,y/S-.2)*1.3)
  pixels[x,y]=(int(25-8*t+7*light),int(24-7*t+4*light),int(34-10*t+10*light),mask.getpixel((x,y)))
draw=ImageDraw.Draw(image)
draw.rounded_rectangle((32,32,992,992),228,outline=(94,77,103,200),width=3)
points=[];envelope=[]
for i in range(501):
 x=29+i*198/500;u=(x-128)/41;amp=math.exp(-u*u/2)
 points.append((x,140-65*amp*math.cos(u*3.5)));envelope.append((x,140-65*amp))
def line(points,color,width):draw.line([(round(x*4),round(y*4)) for x,y in points],fill=color,width=round(width*4),joint='curve')
line(envelope,(104,85,134,160),1.5);line([(29,140),(227,140)],(74,63,85,190),1)
line(points,(77,49,76,255),13)
wave_mask=Image.new('L',(S,S))
ImageDraw.Draw(wave_mask).line([(round(x*4),round(y*4)) for x,y in points],fill=255,width=26,joint='curve')
gradient=Image.new('RGBA',(S,S));gd=ImageDraw.Draw(gradient)
for x in range(S):
 t=max(0,min(1,(x/4-29)/198));color=tuple(round(c+(d-c)*t) for c,d in zip((185,174,249),(245,130,184)))+(255,)
 gd.line((x,0,x,S),fill=color)
image.paste(gradient,(0,0),wave_mask);draw=ImageDraw.Draw(image)
draw.ellipse((492,276,532,316),fill=(253,225,236,255))
image=image.resize((256,256),Image.Resampling.LANCZOS)
image.save(assets/'quietdesk.png');image.save(assets/'quietdesk.ico',sizes=[(n,n) for n in (16,20,24,32,40,48,64,128,256)])
path=lambda p:'M'+' L'.join(f'{x:.2f},{y:.2f}' for x,y in p)
svg=f'''<svg xmlns="http://www.w3.org/2000/svg" width="256" height="256" viewBox="0 0 256 256">
<title>静隅 · 高斯波包</title><desc>以量子波函数实部及高斯包络为灵感的原创标识。</desc>
<defs><linearGradient id="bg" x2="0" y2="1"><stop stop-color="#242231"/><stop offset="1" stop-color="#11121b"/></linearGradient><linearGradient id="wave"><stop stop-color="#b9aef9"/><stop offset="1" stop-color="#f582b8"/></linearGradient></defs>
<rect x="8" y="8" width="240" height="240" rx="57" fill="url(#bg)" stroke="#5e4d67"/>
<path d="{path(envelope)}" fill="none" stroke="#685586" stroke-width="1.5"/>
<path d="M29 140H227" stroke="#4a3f55"/>
<path d="{path(points)}" fill="none" stroke="#4d314c" stroke-width="13" stroke-linecap="round"/>
<path d="{path(points)}" fill="none" stroke="url(#wave)" stroke-width="6.5" stroke-linecap="round"/>
<circle cx="128" cy="74" r="5" fill="#fde1ec"/></svg>'''
(assets/'quietdesk.svg').write_text(svg,encoding='utf-8')
print('Created SVG, PNG preview, and nine-size Windows ICO')
