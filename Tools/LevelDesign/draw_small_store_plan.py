from pathlib import Path
import json
from PIL import Image, ImageDraw, ImageFont

root = Path(__file__).resolve().parents[2]
data = json.loads((root/'ArtSource/SmallStore30/layout.json').read_text())
fontpath = root/'Assets/_Project/Content/UI/ProjectSvin/Fonts/SofiaSans-Regular.ttf'
font = lambda size: ImageFont.truetype(str(fontpath), size)
im = Image.new('RGB', (1080, 1250), '#211c25')
d = ImageDraw.Draw(im)
d.text((60, 35), 'PROJECT S.V.I.N.', font=font(42), fill='#f6eade')
d.text((60, 95), '30 МАЛЕНЬКИХ КОМНАТ', font=font(48), fill='#edb8ce')
d.text((60, 160), 'Стороны 10–12 м · карта 56 × 68 м', font=font(29), fill='#d0bdc4')
k, cx, oz = 12, 540, 995
row_edges = [-8, 3, 15, 25, 37, 48, 60]
col_edges = [-28, -17, -5, 5, 17, 28]
for r in data['rooms']:
    c, s = r['center'], r['size']
    x0, x1 = cx+(c['x']-s['x']/2)*k, cx+(c['x']+s['x']/2)*k
    z0, z1 = oz-(c['z']+s['z']/2)*k, oz-(c['z']-s['z']/2)*k
    colour = ['#b08096', '#d4a7b8', '#9b8998', '#bfa2ae'][int(r['name'][:2])%4]
    d.rectangle((x0,z0,x1,z1), fill=colour, outline='#211c25', width=5)
    row = min(range(6), key=lambda i: abs(row_edges[i]-(c['z']-s['z']/2)))
    col = min(range(5), key=lambda i: abs(col_edges[i]-(c['x']-s['x']/2)))
    if col < 4:
        zz = oz-(c['z']+(.8 if (row+col)%2==0 else -.8))*k
        d.line((x1,zz-1.7*k,x1,zz+1.7*k), fill=colour, width=6)
    if row < 5:
        xx = cx+c['x']*k
        d.line((xx-1.7*k,z0,xx+1.7*k,z0), fill=colour, width=6)
    d.text(((x0+x1)/2,(z0+z1)/2), r['name'][:2], font=font(23), fill='#211c25', anchor='mm')
d.text((60,1160), '96 вещей · мебель и укрытия · широкие проходы', font=font(29), fill='#f6eade')
im.save(root/'ArtSource/SmallStore30/plan.png')
