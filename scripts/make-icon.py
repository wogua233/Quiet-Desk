"""Original code-drawn leaf mark. Standard library only; emits multi-size PNG ICO."""
from pathlib import Path
import math, struct, zlib
def png(size):
    rows=[]
    for y in range(size):
        row=bytearray([0])
        for x in range(size):
            u=(x+.5)/size;v=(y+.5)/size
            inside=(max(abs(u-.5)-.34,0)**2+max(abs(v-.5)-.34,0)**2)<.14**2
            color=(25,39,33,255 if inside else 0)
            # Two slender leaves and a stem, independent of installed fonts.
            for cx,cy,a in ((.39,.39,-.6),(.61,.57,.6)):
                dx=u-cx;dy=v-cy;q=dx*math.cos(a)-dy*math.sin(a);r=dx*math.sin(a)+dy*math.cos(a)
                if (q/.1)**2+(r/.22)**2<1:color=(178,207,186,255)
            if abs(u-.5)<.018 and .42<v<.82:color=(178,207,186,255)
            row.extend(color)
        rows.append(bytes(row))
    def chunk(tag,data):return struct.pack('>I',len(data))+tag+data+struct.pack('>I',zlib.crc32(tag+data)&0xffffffff)
    return b'\x89PNG\r\n\x1a\n'+chunk(b'IHDR',struct.pack('>IIBBBBB',size,size,8,6,0,0,0))+chunk(b'IDAT',zlib.compress(b''.join(rows)))+chunk(b'IEND',b'')
sizes=(16,32,48,256);images=[png(s) for s in sizes];offset=6+16*len(sizes);directory=bytearray(struct.pack('<HHH',0,1,len(sizes)))
for size,data in zip(sizes,images):
    directory.extend(struct.pack('<BBBBHHII',size%256,size%256,0,0,1,32,len(data),offset));offset+=len(data)
target=Path(__file__).resolve().parents[1]/'src/QuietDesk/Assets/quietdesk.ico';target.write_bytes(directory+b''.join(images))
