"""Metre-accurate specification and drawn plan of the asymmetrical store level."""
import json
from pathlib import Path
from PIL import Image,ImageDraw,ImageFont
ROOT=Path(__file__).resolve().parents[2];OUT=ROOT/'Docs/LevelDesign';OUT.mkdir(parents=True,exist_ok=True)
rooms=[
('01','ВХОД / ПЕРСИК',-5,-8,5,0,'#b49472',4.8),
('02','АТРИУМ / ПОДИУМ',-10,0,10,18,'#587f7d',7.2),
('03','БУТИК',-24,-4,-10,12,'#937465',4.8),
('04','ОБУВЬ / АТЕЛЬЕ',10,0,24,12,'#737f97',4.8),
('05','ЛАБИРИНТ ПРИМЕРОЧНЫХ',-24,12,-10,28,'#907b8c',4.8),
('06','ГАЛЕРЕЯ',-10,18,4,28,'#8b8466',4.8),
('07','СЛУЖЕБНЫЙ СРЕЗ',4,18,10,28,'#697577',4.8),
('08','СКЛАД / СТЕЛЛАЖИ',10,12,28,28,'#6f8c7a',4.8),
('09','АРХИВ КОЛЛЕКЦИЙ',-24,28,4,36,'#77766f',4.8),
('10','ГНЕЗДО ВОРИШЕК',4,28,28,36,'#9c715e',4.8)]
walls=[]
def wall(axis,fixed,start,end,doors=(),height=4.8):walls.append(dict(axis=axis,fixed=fixed,start=start,end=end,height=height,doors=[dict(start=a,end=b) for a,b in doors]))
wall('h',36,-24,28);wall('v',-24,-4,36);wall('h',-4,-24,-10);wall('v',-10,-4,0)
wall('h',0,-10,-5,height=7.2);wall('h',0,5,10,height=7.2);wall('h',0,10,24)
wall('h',-8,-5,5);wall('v',-5,-8,0);wall('v',5,-8,0)
wall('v',24,0,12);wall('h',12,24,28);wall('v',28,12,36)
wall('h',0,-5,5,[(-4,4)],height=4.8)
wall('v',-10,0,28,[(3,6),(21,24)])
wall('v',10,0,28,[(3,6),(15,18),(22,25)])
wall('h',12,-24,-10,[(-22,-19)]);wall('h',12,10,24,[(18,21)])
wall('h',18,-10,4,[(-7,-3)]);wall('h',18,4,10,[(5.5,8.5)])
wall('v',4,18,36,[(21,24),(30,33)])
wall('h',28,-24,4,[(-21,-18),(-6,-3)])
wall('h',28,4,28,[(5,8),(19,22)])
positions=[(-3,-5),(3,-5),(-22,-1),(-18,-1),(-12,-1),(-22,6),(-16,9),(-12,9),
(12,2),(21,2),(21,8),(17,9),(12,9),(-4,2),(4,2),(-4,6),(4,6),(-4,12),(4,12),
(-22,14),(-22,24),(-18,16),(-18,23),(-12,15),(-12,26),(-8,20),(-2,20),(-2,26),
(6,20),(8,26),(12,13),(26,15),(16,22),(26,25),(12,26),(-22,31),(-12,34),(-2,34),(-8,31),
(6,34),(24,34),(26,30)]
types=['hat','shirt','pants','shoes','hoodie']
pickups=[dict(kind=types[i%5],x=x,z=z,y=0) for i,(x,z) in enumerate(positions)]
for i,x in enumerate([-7,-4,-1,2,5,7]):pickups.append(dict(kind=types[(i+2)%5],x=x,z=16,y=2.6))
ms=[('stalker',0,12,0,180),('stalker',-17,20,0,90),('stalker',24,18,0,270),
('watcher',-17,5,0,90),('watcher',19,6,0,270),('watcher',-5,25,0,180),('watcher',0,15.2,2.6,180),
('thief',18,32,0,180),('thief',26,20,0,270),('thief',7,23,0,180),('thief',-5,32,0,180),('thief',-20,2,0,90),
('display',-1.6,9,.5,160),('display',1.6,9,.5,200),('display',-22,9,0,90),('display',21,5,0,270)]
props=[dict(x=-15,z=3,w=1.4,l=5,kind='rack'),dict(x=15,z=5,w=1.4,l=4,kind='rack'),
dict(x=14,z=17,w=1.6,l=6,kind='shelf'),dict(x=22,z=23,w=1.6,l=6,kind='shelf'),
dict(x=19,z=14,w=5,l=1.3,kind='shelf'),dict(x=18,z=26,w=5,l=1.3,kind='shelf'),
dict(x=-16,z=32,w=1.4,l=5,kind='shelf'),dict(x=-8,z=33,w=1.4,l=5,kind='shelf')]
data=dict(rooms=[dict(id=i,name=n,x0=x0,z0=z0,x1=x1,z1=z1,color=c,height=h) for i,n,x0,z0,x1,z1,c,h in rooms],walls=walls,pickups=pickups,
mannequins=[dict(kind=k,x=x,z=z,y=y,yaw=yaw) for k,x,z,y,yaw in ms],props=props)
(OUT/'PeachStoreLayout.json').write_text(json.dumps(data,ensure_ascii=False,indent=2)+'\n')
im=Image.new('RGB',(1700,1450),'#12191e');d=ImageDraw.Draw(im)
def font(s):return ImageFont.truetype('/System/Library/Fonts/Supplemental/Arial.ttf',s)
def text(x,y,t,s=22,c='#e5ebed',anchor=None):d.text((x,y),t,font=font(s),fill=c,anchor=anchor)
text(65,35,'SVINKI / УНИВЕРМАГ ПОСЛЕ ЗАКРЫТИЯ',37)
text(68,91,'Асимметричный план • атриум 7,2 м • мостик +2,6 м • 48 вещей • 16 манекенов',23,'#9daeb8')
S=23;OX=630;OY=1070

def p(x,z):return(OX+x*S,OY-z*S)
for i,n,x0,z0,x1,z1,c,h in rooms:
 a=p(x0,z1);b=p(x1,z0);d.rectangle([a,b],fill=c)
 xx=(a[0]+b[0])/2;yy=a[1]+36
 text(xx,yy,i,25,'#f5ece1','mm');text(xx,yy+30,n,15 if len(n)>20 else 17,'#f5ece1','mm')
# Upper traversal: two sloping ramps and a bridge overlooking the podium.
for x in [-7.3,7.3]:
 d.rectangle([p(x-1.2,14),p(x+1.2,3)],fill='#b28d58',outline='#f6dca3',width=2)
 a=p(x,5);b=p(x,12);d.line([a,b],fill='#ffe4a3',width=3);d.polygon([b,(b[0]-7,b[1]+12),(b[0]+7,b[1]+12)],fill='#ffe4a3')
d.rectangle([p(-8.5,16.7),p(8.5,14)],fill='#b28d58',outline='#f6dca3',width=2)
text(*p(0,14.55),'МОСТИК +2,6 м',15,'#fff0c9','mm')
a=p(-3,11.5);b=p(3,6.5);d.ellipse([a,b],fill='#2a383b',outline='#f2c09e',width=3)
for q in props:d.rectangle([p(q['x']-q['w']/2,q['z']+q['l']/2),p(q['x']+q['w']/2,q['z']-q['l']/2)],fill='#2d3a41',outline='#b7bbb6',width=2)
for x,ds in [(-20,[(18,20),(22,24)]),(-15,[(14,16),(22,24)])]:
 cursor=14
 for a,b in ds+[(26,26)]:
  if a>cursor:d.line([p(x,cursor),p(x,a)],fill='#d6c2d4',width=6)
  cursor=b
for w in walls:
 cursor=w['start']
 for a,b in [(q['start'],q['end']) for q in w['doors']]+[(w['end'],w['end'])]:
  if a>cursor:d.line([p(cursor,w['fixed']) if w['axis']=='h' else p(w['fixed'],cursor),p(a,w['fixed']) if w['axis']=='h' else p(w['fixed'],a)],fill='#e6dfd2',width=7)
  cursor=b
 for q in w['doors']:d.line([p(q['start'],w['fixed']) if w['axis']=='h' else p(w['fixed'],q['start']),p(q['end'],w['fixed']) if w['axis']=='h' else p(w['fixed'],q['end'])],fill='#88dac9',width=4)
for q in pickups:
 x,y=p(q['x'],q['z']);d.rounded_rectangle((x-6,y-6,x+6,y+6),radius=2,fill='#ffd568',outline='#3c3430',width=1)
colors={'stalker':'#fa7776','watcher':'#b7a0e7','thief':'#ffa653','display':'#e8ecec'}
for q in data['mannequins']:
 x,y=p(q['x'],q['z']);d.ellipse((x-9,y-9,x+9,y+9),fill=colors[q['kind']],outline='#20282e',width=2)
x,y=p(0,-3);d.polygon([(x,y-13),(x-10,y+10),(x+10,y+10)],fill='#88edce')
x,y=p(0,-7.7);d.line([(x-40,y),(x+40,y)],fill='#ff9d77',width=10)
text(x,y-20,'ПЕРСИК',16,'#fff2df','mm')
text(1320,205,'ЧТО ЗДЕСЬ ИНТЕРЕСНО',23)
lines=['01  Постер-ориентир у входа','02  Подиум под мостиком','03  Стойки закрывают обзор','05  Примерочные с поворотами','08  Извилистые проходы склада','10  Тайник с украденной одеждой','','ВЕРХНИЙ МАРШРУТ','Два пандуса → мостик.','Шесть вещей наверху.','Оттуда видно оба входа в зал.','','ПЕТЛИ И СРЕЗЫ','Левое крыло: бутик →','примерочные → галерея → зал.','','Правое: ателье → склад →','служебный срез → зал.','','Архив соединяет оба крыла.']
for j,l in enumerate(lines):text(1320,258+j*32,l,19,'#aabcc5')
for j,(k,l) in enumerate([('stalker','Сталкеры × 3'),('watcher','Подглядывающие × 4'),('thief','Воришки × 5'),('display','Витринные × 4')]):
 yy=990+j*43;d.ellipse((1288,yy-8,1304,yy+8),fill=colors[k]);text(1320,yy,l,18,anchor='lm')
text(70,1315,'Открытый атриум → тесные примерочные → тёмный склад → обход наверху',25)
text(70,1363,'▲ старт     ■ одежда     ● манекен     золотой = верхний ярус     бирюзовый = проход',22,'#9daeb8')
im.save(OUT/'PeachStorePlan.png');print(OUT/'PeachStorePlan.png')
