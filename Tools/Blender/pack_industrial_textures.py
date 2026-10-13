"""Pack roughness/metalness for URP and convert source EXRs to small PNGs."""
import bpy,json
from pathlib import Path
root=Path(__file__).resolve().parents[2]
base=root/'Assets/_Project/Content/Environment/IndustrialCC0'
raw_base=root/'ArtSource/IndustrialCC0/RawTextures'
for folder in sorted(base.iterdir()):
    if not folder.is_dir():
        continue
    # A fresh import may still have source maps beside the FBX; archived maps take priority.
    textures={p.name:p for p in (folder/'textures').glob('*') if p.is_file()}
    textures.update({p.name:p for p in (raw_base/folder.name/'textures').glob('*') if p.is_file()})
    textures=list(textures.values())
    rough=[p for p in textures if 'rough' in p.stem and p.suffix in ['.jpg','.png','.exr']]
    for path in rough:
        prefix=path.stem.split('_rough')[0]
        metal=next((p for p in textures if p.stem.startswith(prefix+'_metal') and not p.stem.lower().endswith('_metalsmooth') and p.suffix in ['.exr','.jpg','.png']),None)
        r=bpy.data.images.load(str(path)); r.colorspace_settings.name='Non-Color';rp=list(r.pixels[:]);w,h=r.size
        if metal:
            m=bpy.data.images.load(str(metal));m.colorspace_settings.name='Non-Color';mp=list(m.pixels[:])
        else: mp=[0]*len(rp)
        pixels=[]
        for i in range(0,len(rp),4):pixels.extend([mp[i],0,0,1-rp[i]])
        image=bpy.data.images.new(prefix+'_URP',width=w,height=h,alpha=True);image.colorspace_settings.name='Non-Color';image.pixels=pixels
        output=folder/'textures'/(prefix+'_metalSmooth.png')
        output.parent.mkdir(parents=True,exist_ok=True)
        image.filepath_raw=str(output);image.file_format='PNG';image.save()
        print(prefix,flush=True)
