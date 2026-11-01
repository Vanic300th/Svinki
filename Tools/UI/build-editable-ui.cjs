/* Rebuilds independent Unity PNGs, a vector source, and a layered PSD with live text and shape layers.
   Runtime: Node.js with @napi-rs/canvas and ag-psd. See README for the pinned PSD writer. */
const fs = require('fs');
const path = require('path');
const { createCanvas, loadImage, GlobalFonts } = require('@napi-rs/canvas');
const { initializeCanvas, writePsdBuffer, readPsd } = require('./.deps/ag-psd');
initializeCanvas(createCanvas);
const root = path.resolve(__dirname, '../..');
const assets = path.join(root, 'Assets/_Project/Content/UI/ProjectSvin');
const out = path.join(root, 'ArtSource/UI/ProjectSvin');
fs.mkdirSync(assets, { recursive: true }); fs.mkdirSync(out, { recursive: true });
const palette = { base:'#241921', panel:'#3D2633', button:'#754253', primary:'#EDB3C5', text:'#F8E9E4', dark:'#301D26', muted:'#C99DAE', border:'#B77B90', track:'#573341' };
const fontPath = path.join(root, 'Assets/TextMesh Pro/Fonts/LiberationSans.ttf');
GlobalFonts.registerFromPath(fontPath, 'Liberation Sans');
const typography = JSON.parse(fs.readFileSync(path.join(out, 'Fonts/font-coverage.json'), 'utf8'));
for (const info of typography) {
  GlobalFonts.registerFromPath(path.join(assets, 'Fonts', info.file), info.family);
  info.supported = new Set(info.codepoints);
  if (info.role === 'body') for (let c=48;c<=57;c++) info.supported.delete(c);
}
const iconPaths = {
  friends:'<circle cx="21" cy="20" r="8"/><circle cx="45" cy="22" r="7"/><path d="M7 52v-8c0-14 28-14 28 0v8Zm30-19c11-3 20 2 20 11v8H40"/>',
  ticket:'<path d="M10 15h44v12c-11 0-11 12 0 12v11H10V39c11 0 11-12 0-12Z"/><path d="M32 21v5m0 7v5m0 6v2" stroke="#241921" stroke-width="3"/>',
  play:'<path d="M18 9 56 32 18 55Z"/>',
  book:'<path d="M5 13c9-5 18-5 27 0 9-5 18-5 27 0v39c-9-5-18-5-27 0-9-5-18-5-27 0Z" fill="none" stroke="currentColor" stroke-width="4"/><path d="M32 13v39" fill="none" stroke="currentColor" stroke-width="4"/>',
  settings:'<path d="m27 4 10 0 2 9 7 4 9-3 5 9-7 6v7l7 6-5 9-9-3-7 4-2 9H27l-2-9-7-4-9 3-5-9 7-6v-7l-7-6 5-9 9 3 7-4Z"/><circle cx="32" cy="32" r="10" fill="#241921"/>',
  hoof:'<path d="M27 6c-13 0-19 16-19 31 0 12 6 21 18 23l4-23Zm10 0c13 0 19 16 19 31 0 12-6 21-18 23l-4-23Z"/>',
  hanger:'<path d="M25 17c0-13 18-13 18-3 0 7-11 7-11 16L5 47c-4 3-2 7 3 7h48c5 0 7-4 3-7L32 30" fill="none" stroke="currentColor" stroke-width="4" stroke-linecap="round"/>',
  gamepad:'<path d="M17 18h30c9 0 15 29 11 34-4 7-14-7-17-9H23c-3 2-13 16-17 9-4-5 2-34 11-34Z" fill="none" stroke="currentColor" stroke-width="4"/><path d="M20 26v13m-6-7h13" fill="none" stroke="currentColor" stroke-width="4"/><circle cx="44" cy="29" r="3"/><circle cx="50" cy="36" r="3"/>'
};
const xml = text => String(text).replace(/[&<>"']/g, c => ({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&apos;'}[c]));
const rgb = hex => ({r:parseInt(hex.slice(1,3),16),g:parseInt(hex.slice(3,5),16),b:parseInt(hex.slice(5,7),16)});
function canvas(w,h){return createCanvas(Math.ceil(w),Math.ceil(h));}
function save(c,filename){fs.writeFileSync(filename,c.toBuffer('image/png'));}
async function iconCanvas(name,size=128,color='#ffffff'){
  const svg=`<svg xmlns="http://www.w3.org/2000/svg" width="${size}" height="${size}" viewBox="0 0 64 64" color="${color}" fill="${color}">${iconPaths[name]}</svg>`;
  const c=canvas(size,size);c.getContext('2d').drawImage(await loadImage(Buffer.from(svg)),0,0);return c;
}
async function sprites(){
  for(const [name,w,h,r] of [['panel-mask',512,512,30],['button-mask',512,128,20]]){
    const c=canvas(w,h),ctx=c.getContext('2d');ctx.fillStyle='#fff';ctx.beginPath();ctx.roundRect(0,0,w,h,r);ctx.fill();save(c,path.join(assets,name+'.png'));
  }
  const c=canvas(128,96),ctx=c.getContext('2d');ctx.fillStyle='#fff';ctx.beginPath();ctx.ellipse(64,48,59,41,0,0,Math.PI*2);ctx.fill();ctx.fillStyle='#603747';
  for(const x of [44,84]){ctx.beginPath();ctx.ellipse(x,48,7,14,0,0,Math.PI*2);ctx.fill();}save(c,path.join(assets,'snout.png'));
  for(const name of Object.keys(iconPaths)){save(await iconCanvas(name),path.join(assets,name+'.png'));fs.writeFileSync(path.join(out,name+'.svg'),`<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 64 64" fill="${palette.primary}" color="${palette.primary}">${iconPaths[name]}</svg>`);}
  fs.writeFileSync(path.join(out,'palette.json'),JSON.stringify(palette,null,2)+'\n');
}
const buttonScale=.92,buttonTextScale=.90;
let buttonFrame=null;
function beginButton(x,y){buttonFrame={x,y};}
function endButton(){buttonFrame=null;}
function buttonRect(x,y,w,h){return buttonFrame?[buttonFrame.x+(x-buttonFrame.x)*buttonScale,buttonFrame.y+(y-buttonFrame.y)*buttonScale,w*buttonScale,h*buttonScale]:[x,y,w,h];}
const W=1920,H=2160;
const composite=canvas(W,H),compositeCtx=composite.getContext('2d');
const svg=[];const groups=[];let active;
function group(name,hidden=false){if(active)svg.push('</g>');active={name,opened:false,hidden,children:[]};groups.push(active);svg.push(`<g id="${xml(name)}"${hidden?' style="display:none"':''}>`);}
function add(layer,c,x,y,markup){active.children.push(layer);if(!active.hidden)compositeCtx.drawImage(c,x,y);if(markup)svg.push(markup);}
function knots(x,y,w,h,r){
  const k=.55228475;const p=[
    [x+r-k*r,y,x+r,y,x+r,y], [x+w-r,y,x+w-r,y,x+w-r+k*r,y],
    [x+w,y+r-k*r,x+w,y+r,x+w,y+r], [x+w,y+h-r,x+w,y+h-r,x+w,y+h-r+k*r],
    [x+w-r+k*r,y+h,x+w-r,y+h,x+w-r,y+h], [x+r,y+h,x+r,y+h,x+r-k*r,y+h],
    [x,y+h-r+k*r,x,y+h-r,x,y+h-r], [x,y+r,x,y+r,x,y+r-k*r]
  ];return p.map(points=>({linked:false,points}));
}
function shape(name,x,y,w,h,color,r=16,stroke){
  if(buttonFrame){[x,y,w,h]=buttonRect(x,y,w,h);r*=buttonScale;}
  // A Photoshop shape layer with editable fill/vector mask plus a matching raster cache.
  const pad=stroke?3:0,c=canvas(w+pad*2,h+pad*2),ctx=c.getContext('2d');ctx.beginPath();ctx.roundRect(pad,pad,w,h,r);ctx.fillStyle=color;ctx.fill();
  if(stroke){ctx.strokeStyle=stroke;ctx.lineWidth=2;ctx.stroke();}
  const layer={name,left:x-pad,top:y-pad,canvas:c,vectorFill:{type:'color',color:rgb(color)},vectorMask:{paths:[{open:false,operation:'combine',fillRule:'non-zero',knots:knots(x,y,w,h,r)}]}};
  if(stroke)layer.vectorStroke={strokeEnabled:true,fillEnabled:true,lineWidth:{units:'Pixels',value:2},lineAlignment:'inside',content:{type:'color',color:rgb(stroke)}};
  add(layer,c,x-pad,y-pad,`<rect id="${xml(name)}" x="${x}" y="${y}" width="${w}" height="${h}" rx="${r}" fill="${color}"${stroke?` stroke="${stroke}" stroke-width="2"`:''}/>`);
}
function text(name,value,x,y,w,h,size,color=palette.text,bold=false,role='auto'){
  if(buttonFrame){[x,y,w,h]=buttonRect(x,y,w,h);size*=buttonScale*buttonTextScale;}
  if(role==='auto')role=bold||/кнопк|вклад|бирка|заголов|настройки|готово/i.test(name)||active.name.startsWith('03.')?'heading':'body';
  const font=typography.find(f=>f.role===role||f.additionalRoles?.includes(role));
  const runs=[];
  for(const character of value){const psName=font.supported.has(character.codePointAt(0))?font.postscript:'LiberationSans';const last=runs[runs.length-1];if(last&&last.style.font.name===psName)last.length+=character.length;else runs.push({length:character.length,style:{font:{name:psName}}});}
  const c=canvas(w,h),ctx=c.getContext('2d');ctx.fillStyle=color;ctx.textBaseline='alphabetic';
  let offset=0,ascent=0,descent=0;
  const pieces=runs.map(run=>{const content=value.slice(offset,offset+run.length);offset+=run.length;const family=run.style.font.name==='LiberationSans'?'Liberation Sans':font.family;ctx.font=`${size}px "${family}"`;const metrics=ctx.measureText(content);ascent=Math.max(ascent,metrics.actualBoundingBoxAscent);descent=Math.max(descent,metrics.actualBoundingBoxDescent);return {content,family,width:metrics.width};});
  const baseline=y+(h-ascent-descent)/2+ascent;let cursor=0;
  for(const piece of pieces){ctx.font=`${size}px "${piece.family}"`;ctx.fillText(piece.content,cursor,baseline-y);cursor+=piece.width;}
  // Live Photoshop type layer, with explicit fallback runs for Cyrillic and demo-only numerals.
  const layer={name,left:x,top:y,canvas:c,text:{text:value,transform:[1,0,0,1,x,baseline],shapeType:'point',antiAlias:'smooth',style:{font:{name:font.postscript},fontSize:size,fauxBold:false,fillColor:rgb(color)},styleRuns:runs,paragraphStyle:{justification:'left'}}};
  add(layer,c,x,y,`<text id="${xml(name)}" x="${x}" y="${baseline}" font-family="${font.family},Liberation Sans,Arial,sans-serif" font-size="${size}" fill="${color}">${pieces.map(p=>`<tspan font-family="${p.family}">${xml(p.content)}</tspan>`).join('')}</text>`);
}
async function icon(name,type,x,y,size,color=palette.primary){if(buttonFrame){[x,y,size]=buttonRect(x,y,size,size);}const c=await iconCanvas(type,size,color);add({name,left:x,top:y,canvas:c},c,x,y,`<g id="${xml(name)}" transform="translate(${x} ${y}) scale(${size/64})" color="${color}" fill="${color}">${iconPaths[type]}</g>`);}
async function bitmap(name,file,x,y,w,h){if(!fs.existsSync(file))return;const img=await loadImage(file),c=canvas(w,h);c.getContext('2d').drawImage(img,0,0,w,h);add({name,left:x,top:y,canvas:c},c,x,y,`<image id="${xml(name)}" x="${x}" y="${y}" width="${w}" height="${h}" href="data:image/png;base64,${c.toBuffer('image/png').toString('base64')}"/>`);}
async function master(){
  group('00 / Фон — отдельная картинка');
  shape('Основа документа',0,0,W,H,palette.base,0);
  await bitmap('Фон главного меню — заменить отдельно',path.join(assets,'menu-background.png'),0,0,1920,1080);
  group('01 / Логотип — редактируемые текстовые слои');
  text('Project — ТЕКСТ','Project',100,60,780,88,69,palette.text,false,'brand');
  text('S.V.I.N. — ТЕКСТ','S.V.I.N.',100,145,780,200,162,palette.primary,false,'logo');
  group('02 / Выбор режима — включить вместо кнопок 03',true);
  beginButton(100,355);shape('Назад / фон',100,355,255,55,palette.button,16,palette.border);text('Назад / ТЕКСТ','← Назад',118,355,219,55,22,palette.text,false,'heading');endButton();
  for(const [name,x,value,size]of [['Никнейм',100,'Игрок',32],['Код друга',485,'Код друга',28.5]]){
    shape(name+' / фон',x,425,355,80,palette.panel,24,palette.border);text(name+' / ТЕКСТ',value,x+16,429,323,72,size,palette.text,false,'input');
  }
  text('Статус / ТЕКСТ','Создайте лобби или войдите по коду друга.',100,512,740,55,21,palette.muted);
  for(const [i,label]of ['Создать лобби','Войти по коду','Играть одному','Как играть'].entries()){
    const y=i<3?575+i*116:921,h=i<3?100:52;
    beginButton(100,y);shape(label+' / фон',100,y,740,h,i===0?palette.primary:palette.button,20,palette.border);
    await icon(label+' / иконка',['friends','ticket','play','book'][i],126,y+(h-45)/2,45,i===0?palette.dark:palette.primary);
    text(label+' / ТЕКСТ',label,194,y,624,h,i<3?30:21,i===0?palette.dark:palette.text,false,'heading');endButton();
  }
  beginButton(100,985);shape('Продолжить / фон',100,985,740,48,palette.button,18,palette.border);text('Продолжить / ТЕКСТ','Продолжить сохранённую игру',118,985,704,48,19,palette.muted,false,'heading');endButton();
  for(const [i,label]of ['Играть','Настройки','Выход'].entries()){
    group(`03.${i+1} / Кнопка ${label}`);const y=[450,590,720][i],h=i===0?114:104;
    beginButton(100,y);shape('Фон кнопки — SHAPE',100,y,740,h,i===0?palette.primary:palette.button,20,palette.border);
    await icon('Иконка — отдельный слой',['play','settings','hoof'][i],126,y+(h-45)/2,45,i===0?palette.dark:palette.primary);
    text('Надпись — ТЕКСТ',label,194,y,624,h,i===0?36:34,i===0?palette.dark:palette.text,false,'heading');endButton();
  }
  group('04 / Окно кастомизации');
  shape('Панель / фон — SHAPE',1030,70,820,940,palette.panel,30,palette.border);
  shape('Бирка / фон — SHAPE',1060,45,760,105,palette.primary,13,palette.border);
  text('Бирка / ТЕКСТ','ТВОЯ СВИНКА',1084,45,712,105,52.5,palette.dark,false,'heading');
  await icon('Вешалка — отдельная декорация','hanger',1580,200,110,palette.track);
  await bitmap('Свинка — отдельный PNG из Unity',path.join(out,'pig-preview.png'),1260,160,360,300);
  for(const [x,value]of [[1060,'←'],[1677.5,'→']]){beginButton(x,380);shape('Поворот '+value+' / фон',x,380,142.5,67.5,palette.button,18,palette.border);text('Поворот '+value,value,x+12,380,118.5,67.5,38,palette.primary,false,'heading');endButton();}
  function choiceRow(name,value,x,y,sub=false){
    const width=699.2,left=sub?18:0;
    shape(name+' / фон строки',x+left,y,width-left,63,sub?palette.track:palette.button,18,palette.border);
    text(name+' / значение — ТЕКСТ',value,x+(sub?84:66),y,width-(sub?150:132),63,sub?21:23,palette.text,false,'heading');
    for(const [dir,dx]of [['←',sub?26:8],['→',width-56]]){shape(name+' / '+dir+' / фон',x+dx,y+9,44.16,42.32,palette.track,18);text(name+' / '+dir+' / ТЕКСТ',dir,x+dx,y+9,44.16,42.32,24.84,palette.text,false,'heading');}
  }
  text('Раздел волос / ТЕКСТ','ВОЛОСЫ И УСЫ',1070,485,700,40,24,palette.muted,false,'heading');
  choiceRow('Причёска','Причёска: шторы',1060,538.5);
  choiceRow('Цвет волос','Цвет волос: чёрный',1060,621,true);
  choiceRow('Усы','Усы: нет',1060,703.5);
  choiceRow('Козлиная бородка','Козлиная бородка: нет',1060,786);
  choiceRow('Цвет усов','Цвет усов: чёрный',1060,868.5);
  text('Подсказка / ТЕКСТ','Прокрутите ниже. Изменения сохраняются.',1060,949,760,45,19,palette.muted);
  group('05 / Компоненты — фоны, состояния, ползунки, иконки');
  text('Заголовок набора / ТЕКСТ','PROJECT S.V.I.N. / UI COMPONENTS',75,1130,1750,70,40,palette.text,false,'brand');
  const names=['Обычная кнопка','При наведении','Нажатая кнопка','Недоступная кнопка'];
  const colors=[palette.button,'#9F5C77','#573341','#4B3540'];
  names.forEach((value,i)=>{beginButton(75,1250+i*105);shape(value+' / SHAPE',75,1250+i*105,500,78,colors[i],16,palette.border);text(value+' / ТЕКСТ',value,100,1250+i*105,450,78,28,palette.text,false,'heading');endButton();});
  beginButton(75,1700);shape('Основная кнопка / SHAPE',75,1700,500,78,palette.primary,16);text('Основная кнопка / ТЕКСТ','Главное действие',100,1700,450,78,28,palette.dark,true);endButton();
  shape('Окно настроек / SHAPE',650,1250,560,575,palette.panel,25,palette.border);
  shape('Бирка / SHAPE',675,1225,350,74,palette.primary,12);text('Настройки / ТЕКСТ','НАСТРОЙКИ',695,1225,320,74,30,palette.dark,true);
  for(let i=0;i<3;i++){
    text('Ползунок '+i+' / ТЕКСТ',['Общая громкость','Музыка','Голос друзей'][i],685,1330+i*120,450,40,26);
    shape('Ползунок '+i+' / дорожка',685,1390+i*120,460,8,palette.track,4);
    shape('Ползунок '+i+' / заполнение',685,1390+i*120,200+i*45,8,palette.primary,4);
    await bitmap('Ползунок '+i+' / пятачок PNG',path.join(assets,'snout.png'),860+i*45,1376+i*120,50,36);
  }
  beginButton(685,1740);shape('Готово / SHAPE',685,1740,210,52,palette.primary,12);text('Готово / ТЕКСТ','Готово',715,1740,170,52,26,palette.dark,true);endButton();
  text('Иконки / ТЕКСТ','ИКОНКИ',1290,1245,450,60,28,palette.text,true);
  let i=0;for(const type of Object.keys(iconPaths)){const x=1300+(i%3)*155,y=1340+Math.floor(i/3)*150;await icon(type+' / отдельный слой',type,x,y,76);text(type+' / ТЕКСТ',type,x-10,y+84,150,28,18,palette.muted);i++;}
  text('Палитра / ТЕКСТ','ПАЛИТРА / ОТДЕЛЬНЫЕ ФОРМЫ',75,1870,1200,50,28,palette.text,true);
  Object.entries(palette).forEach(([key,value],i)=>{shape('Цвет '+key,75+i*180,1950,150,75,value,12,palette.border);text('Цвет '+key+' / HEX',value,75+i*180,2030,160,35,21,palette.text);});
  // Additional screens are separate hidden groups: switch off the main-menu groups to inspect them.
  const entries=JSON.parse(fs.readFileSync(path.join(root,'Tools/Localization/en-ru.json'),'utf8')).entries;
  const localized=key=>entries.find(e=>e.source===key)?.translation||key;
  function screen(title){shape('Подложка экрана',0,0,1920,1080,palette.base,0);shape('Окно / SHAPE',250,100,1420,880,palette.panel,28,palette.border);shape('Бирка / SHAPE',285,75,700,84,palette.primary,12);text('Заголовок / ТЕКСТ',title,315,75,640,84,36,palette.dark,true);}
  function smallButton(name,x,y,w=270){beginButton(x,y);shape(name+' / фон',x,y,w,60,palette.button,14,palette.border);text(name+' / ТЕКСТ',localized(name),x+18,y,w-32,60,24,palette.text,false,'heading');endButton();}
  group('06 / ИНВЕНТАРЬ — горизонтальные карточки (включить группу)',true);
  shape('Подложка экрана',0,0,1920,1080,palette.base,0);
  shape('Окно инвентаря / SHAPE',340,270,1240,540,palette.panel,30,palette.border);
  text('Заголовок / ТЕКСТ',localized('YOUR OUTFIT'),375,298,580,50,34,palette.text,false,'heading');
  text('Подсказка / ТЕКСТ',localized('Dropped clothes can be picked up again'),375,356,790,42,23);
  text('Счётчик / ТЕКСТ',localized('Equipped: {0} / 4').replace('{0}','4'),1190,360,355,34,22);
  for(const [i,slot] of ['Head','Torso','Legs','Feet'].entries()){
    const x=375+i*297;
    shape(slot+' / карточка — SHAPE',x,413,279,275,palette.track,30,palette.border);
    await bitmap(slot+' / картинка одежды — заменить PNG отдельно',path.join(out,'Inventory',slot+'.png'),x+18,427,243,202);
    beginButton(x+16,639);shape(slot+' / кнопка — SHAPE',x+16,639,247,43,palette.button,16,palette.border);
    text(slot+' / действие — ТЕКСТ',localized('Drop'),x+35,639,210,43,23,palette.text,false,'heading');endButton();
  }
  beginButton(1400,300);shape('Закрыть / SHAPE',1400,300,145,46,palette.button,16,palette.border);text('Закрыть / ТЕКСТ',localized('Close'),1417,300,125,46,23,palette.text,false,'heading');endButton();
  beginButton(375,720);shape('Выбросить всё / SHAPE',375,720,300,54,palette.button,16,palette.border);text('Выбросить всё / ТЕКСТ',localized('Drop All'),400,720,260,54,23,palette.text,false,'heading');endButton();
  text('Клавиши / ТЕКСТ',localized('Tab / I — inventory · Esc — close'),965,730,580,38,20);
  group('07 / ТЕЛЕЖКА — отдельный экран (включить группу)',true);screen('ТЕЛЕЖКА');
  for(let i=0;i<8;i++){const y=215+i*73;shape('Слот '+(i+1)+' / фон',300,y,660,60,palette.track,12);text('Слот '+(i+1)+' / ТЕКСТ',`${i+1}. ${localized('Empty')}`,320,y,600,60,24);smallButton('Wear',990,y,240);smallButton('Unload',1255,y,280);}
  smallButton('Load nearest floor item',300,865,720);smallButton('Close',1270,865,270);
  group('08 / РЕЗУЛЬТАТЫ — отдельный экран (включить группу)',true);screen('ИТОГИ ПОКАЗА');
  await bitmap('Свинка / отдельный PNG',path.join(out,'pig-preview.png'),320,220,600,500);
  ['СОЧЕТАНИЕ','ПАЛИТРА','УЗОРЫ','КОМПЛЕКТНОСТЬ'].forEach((value,i)=>{text(value+' / ТЕКСТ',value,1010,240+i*120,510,45,24);shape(value+' / трек',1010,305+i*120,510,8,palette.track,4);shape(value+' / заполнение',1010,305+i*120,360,8,palette.primary,4);});
  smallButton('SAVE OUTFIT',310,850,330);smallButton('Photo albums →',670,850,330);smallButton('Next Round',1030,850,270);smallButton('Main Menu',1330,850,270);
  group('09 / ФОТОАЛЬБОМ — отдельный экран (включить группу)',true);screen('ФОТОАЛЬБОМ');shape('Фото / заменяемый фон',450,210,1020,580,palette.track,10,palette.border);smallButton('← Previous',310,850,350);smallButton('Next →',750,850,350);smallButton('Close',1200,850,350);
  group('10 / HUD — отдельные компоненты (включить группу)',true);shape('Подложка HUD',0,0,1920,1080,palette.base,0);shape('Здоровье / фон',35,35,370,90,palette.panel,16,palette.border);text('Здоровье / ТЕКСТ','2 / 2',70,35,250,90,34,palette.primary,true);shape('Миникарта / рамка',1530,35,350,350,palette.panel,20,palette.border);shape('Миникарта / поле',1555,60,300,300,palette.track,6);text('Подсказка / ТЕКСТ','E — Взаимодействовать',650,920,700,65,30);shape('Подсказка / фон',620,900,740,100,palette.panel,18,palette.border);text('Подсказка / надпись','E — Взаимодействовать',650,920,700,65,30);
  if(buttonFrame)throw new Error('Button frame was not closed');
  svg.push('</g>');
  fs.writeFileSync(path.join(out,'Project-SVIN-editable.svg'),`<svg xmlns="http://www.w3.org/2000/svg" xmlns:xlink="http://www.w3.org/1999/xlink" width="1920" height="2160" viewBox="0 0 1920 2160">${svg.join('\n')}</svg>`);
  const psd={width:W,height:H,canvas:composite,children:groups};
  const inventoryPreview=canvas(1920,1080),inventoryContext=inventoryPreview.getContext('2d');
  for(const layer of groups.find(g=>g.name.startsWith('06 /')).children)if(layer.canvas)inventoryContext.drawImage(layer.canvas,layer.left||0,layer.top||0);
  save(inventoryPreview,path.join(out,'Project-SVIN-inventory-preview.png'));
  const buffer=writePsdBuffer(psd,{generateThumbnail:true,invalidateTextLayers:false});
  fs.writeFileSync(path.join(out,'Project-SVIN-editable.psd'),buffer);
  save(composite,path.join(out,'Project-SVIN-editable-preview.png'));
  const parsed=readPsd(buffer,{skipLayerImageData:true,skipCompositeImageData:true,skipThumbnail:true});
  let texts=0,shapes=0,total=0;function count(layers){for(const layer of layers||[]){total++;if(layer.text)texts++;if(layer.vectorFill)shapes++;count(layer.children);}}count(parsed.children);
  if(texts<35||shapes<35)throw new Error('Editable text/shape layers were lost in PSD serialization');
  const report={width:parsed.width,height:parsed.height,groups:parsed.children.length,layers:total,editableTextLayers:texts,editableShapeLayers:shapes,bytes:buffer.length};
  fs.writeFileSync(path.join(out,'psd-validation.json'),JSON.stringify(report,null,2)+'\n');console.log(report);
}
(async()=>{await sprites();if(!process.argv.includes('--sprites-only'))await master();})().catch(e=>{console.error(e);process.exitCode=1;});
