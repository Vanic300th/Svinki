"""Pack roughness/metalness for URP and convert source EXRs to small PNGs."""
import bpy,json
from pathlib import Path
root=Path(__file__).resolve().parents[2]
base=root/'Assets/Art/IndustrialCC0'
for folder in base.iterdir():
    textures=list((folder/'textures').glob('*')) if (folder/'textures').exists() else []
    rough=[p for p in textures if 'rough' in p.stem and p.suffix in ['.jpg','.png','.exr']]
    for path in rough:
        prefix=path.stem.split('_rough')[0]
        metal=next((p for p in textures if p.stem.startswith(prefix+'_metal') and p.suffix in ['.exr','.jpg','.png']),None)
        r=bpy.data.images.load(str(path)); r.colorspace_settings.name='Non-Color';rp=list(r.pixels[:]);w,h=r.size
        if metal:
            m=bpy.data.images.load(str(metal));m.colorspace_settings.name='Non-Color';mp=list(m.pixels[:])
        else: mp=[0]*len(rp)
        pixels=[]
        for i in range(0,len(rp),4):pixels.extend([mp[i],0,0,1-rp[i]])
        image=bpy.data.images.new(prefix+'_URP',width=w,height=h,alpha=True);image.colorspace_settings.name='Non-Color';image.pixels=pixels
        image.filepath_raw=str(folder/'textures'/(prefix+'_metalSmooth.png'));image.file_format='PNG';image.save()
        print(prefix,flush=True)
