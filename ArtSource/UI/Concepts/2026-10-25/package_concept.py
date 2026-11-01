from pathlib import Path
from io import BytesIO
from base64 import b64encode
import sys
sys.path.insert(0,str(Path('Tools/UI/.deps/concept-fonts').resolve()))
from fontTools.ttLib import TTFont
from fontTools import subset
from PIL import Image
from snout_font import replace_counters
root=Path.cwd();out=root/'ArtSource/UI/Concepts/2026-10-25'
source=(out/'menu-concepts.source.html').read_text()
chars=set(chr(n) for n in list(range(32,127))+list(range(0x400,0x500)))|set('—·✓…←→')
css=[]
for name,file in [('SvHogles','Hogles.ttf'),('SvClimate','ClimateCrisis-Regular.ttf'),('SvYomogi','Yomogi-Regular.ttf'),('SvClimateSnout','ClimateCrisis-Regular.ttf')]:
 f=TTFont(root/'ArtSource/UI/ProjectSvin/Fonts'/file)
 if name=='SvClimateSnout':replace_counters(f)
 options=subset.Options();options.flavor='woff2';options.desubroutinize=True
 sub=subset.Subsetter(options=options);sub.populate(text=''.join(chars));sub.subset(f);f.flavor='woff2';stream=BytesIO();f.save(stream)
 css.append("@font-face{font-family:'%s';src:url(data:font/woff2;base64,%s) format('woff2');font-display:swap}"%(name,b64encode(stream.getvalue()).decode()))
bg=BytesIO();Image.open(root/'Assets/_Project/Content/UI/ProjectSvin/menu-background.png').convert('RGB').save(bg,format='JPEG',quality=82,optimize=True)
pig=(root/'ArtSource/UI/ProjectSvin/pig-preview.png').read_bytes()
source=source.replace('__FONTS__','\n'.join(css)).replace('__BG__','data:image/jpeg;base64,'+b64encode(bg.getvalue()).decode()).replace('__PIG__','data:image/png;base64,'+b64encode(pig).decode())
path=Path('/Users/chamber/.codex/visualizations/2026/10/21/01a15dcc-1dc2-7d13-9c0e-8b395de2758f/svin-menu-october.html');path.write_text(source)
print('Saved',path,'bytes',path.stat().st_size)
assert path.stat().st_size<1_000_000
