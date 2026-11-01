from PIL import Image, ImageDraw, ImageFont
from pathlib import Path
import json
root=Path(__file__).resolve().parents[2]
data=[json.loads((root/'ArtSource/CompactStore'/f'{name}.json').read_text()) for name in ['before','after']]
fontpath=root/'Assets/_Project/Content/UI/ProjectSvin/Fonts/SofiaSans-Regular.ttf'
def font(n): return ImageFont.truetype(str(fontpath),n)
im=Image.new('RGB',(1600,1180),'#211c25');d=ImageDraw.Draw(im)
d.text((70,36),'PROJECT S.V.I.N. / КОМПАКТНЫЙ УРОВЕНЬ',font=font(38),fill='#f7eee2')
d.text((70,90),'Меньше пустых залов · больше отдельных комнат',font=font(25),fill='#cbb8bd')
for index,(layout,title,colour) in enumerate(zip(data,['БЫЛО · 30 КОМНАТ','СТАЛО · 42 КОМНАТЫ'],['#796d79','#ca99ab'])):
    left=45+index*785
    d.rounded_rectangle((left,150,left+740,1085),radius=22,fill='#302833')
    d.text((left+28,170),title,font=font(30),fill='#f7eee2')
    k=4.8; ox=left+355; oz=1015
    for room in layout['rooms']:
        c=room['center'];s=room['size']; x0=ox+(c['x']-s['x']/2)*k;x1=ox+(c['x']+s['x']/2)*k
        z0=oz-(c['z']+s['z']/2)*k;z1=oz-(c['z']-s['z']/2)*k
        d.rectangle((x0,z0,x1,z1),fill=colour,outline='#302833',width=3)
        label=room['name'].split()[0]
        if room['name'].endswith(' / A'):label+='a'
        if room['name'].endswith(' / B'):label+='b'
        d.text(((x0+x1)/2,(z0+z1)/2),label,font=font(15),fill='#211c25',anchor='mm')
    d.text((left+28,1038),f"{layout['width']:.1f} × {layout['depth']:.1f} м  ·  {layout['area']:,.0f} м²".replace(',',' '),font=font(23),fill='#e7d8d6')
d.text((70,1110),'Карта −15% по площади     ·     Комнаты в среднем −40%     ·     96 вещей сохранены',font=font(29),fill='#f1c5d4')
im.save(root/'ArtSource/CompactStore/plan-comparison.png')
