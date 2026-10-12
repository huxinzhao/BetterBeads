"""Synthetic image fixtures only. Never reads or writes game saves or Mods."""
from pathlib import Path
from PIL import Image
import struct
root=Path(__file__).resolve().parents[1]/'.tools/rc6-import'
root.mkdir(parents=True,exist_ok=True)
im=Image.new('RGBA',(2,2));im.putdata([(17,34,51,255),(171,205,239,128),(0,0,0,0),(99,88,77,255)])
im.save(root/'alpha.png')
Image.new('RGBA',(1024,768),(160,90,70,255)).save(root/'large.png')
Image.new('RGB',(8,6),(150,90,70)).save(root/'sample.jpg')
(root/'broken.png').write_bytes(b'not an image')
(root/'oversize.png').write_bytes(b'\x89PNG\r\n\x1a\n'+struct.pack('>I',13)+b'IHDR'+struct.pack('>II',4097,1))
print('Prepared isolated RC6 import fixtures.')
