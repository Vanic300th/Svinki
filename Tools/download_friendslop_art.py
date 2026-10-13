"""Download original CC0 models, with source provenance and checked hashes."""
import concurrent.futures
import hashlib
import json
from pathlib import Path
import urllib.request

ROOT = Path(__file__).resolve().parents[1]
DEST = ROOT / 'Assets/Art/IndustrialCC0'
UA = {'User-Agent': 'FriendslopAssetImport/1.0'}
MODELS = ['sofa_02', 'Television_01', 'modular_industrial_pipes_01',
          'metal_tool_chest', 'ratchet_wrench', 'industrial_coffee_table',
          'can_rusted', 'multi_cleaner_bottle', 'cardboard_box_01',
          'throw_pillows_01', 'rubber_duck_toy', 'metal_toolbox',
          'plastic_bottle_gallon', 'industrial_wall_sconce', 'boombox',
          'modular_airduct_rectangular_01', 'cement_bag', 'dartboard']
TEXTURES = ['concrete_floor_worn_001', 'worn_corrugated_iron',
            'rusty_painted_metal', 'dirty_carpet']

def fetch(url):
    return urllib.request.urlopen(urllib.request.Request(url, headers=UA), timeout=60).read()

def model(name):
    data = json.loads(fetch('https://api.polyhaven.com/files/' + name))
    info = json.loads(fetch('https://api.polyhaven.com/info/' + name))
    variants = data.get('fbx', data.get('blend', {}))
    resolution = '1k' if '1k' in variants else sorted(variants)[0]
    format_name = 'fbx' if 'fbx' in data else 'blend'
    item = variants[resolution][format_name]
    folder = DEST / name
    folder.mkdir(parents=True, exist_ok=True)
    files = [(name + '.' + format_name, item)] + list(item.get('include', {}).items())
    for path, file in files:
        target = folder / path
        target.parent.mkdir(parents=True, exist_ok=True)
        if not target.exists():
            binary = fetch(file['url'])
            if hashlib.md5(binary).hexdigest() != file['md5']:
                raise ValueError('Hash mismatch: ' + path)
            target.write_bytes(binary)
    entry = {'id': name, 'source': 'https://polyhaven.com/a/' + name,
             'license': 'CC0-1.0', 'resolution': resolution, 'info': info,
             'files': [{'path': path, **{k:v for k,v in file.items() if k != 'include'}} for path,file in files]}
    (folder / 'source.json').write_text(json.dumps(entry, indent=2))
    return name + ': ' + str(sum(f['size'] for _,f in files)) + ' bytes'

def texture(name):
    data = json.loads(fetch('https://api.polyhaven.com/files/' + name))
    folder = DEST / name
    folder.mkdir(parents=True, exist_ok=True)
    files = []
    for kind in ['Diffuse', 'nor_gl']:
        item = data[kind]['1k']['jpg']
        target = folder / Path(item['url']).name
        if not target.exists():
            target.write_bytes(fetch(item['url']))
        if hashlib.md5(target.read_bytes()).hexdigest() != item['md5']:
            raise ValueError('Hash mismatch: ' + target.name)
        files.append({'path':target.name, **item})
    (folder / 'source.json').write_text(json.dumps({'id':name,
        'source':'https://polyhaven.com/a/'+name, 'license':'CC0-1.0', 'files':files}, indent=2))
    return name + ': checked textures'

if __name__ == '__main__':
    with concurrent.futures.ThreadPoolExecutor(max_workers=4) as pool:
        futures = {pool.submit(model, name):name for name in MODELS}
        for future in concurrent.futures.as_completed(futures):
            try: print(future.result(), flush=True)
            except Exception as error: print(futures[future] + ': ERROR ' + str(error), flush=True)
        for result in pool.map(texture, TEXTURES):
            print(result, flush=True)
    for name, slug in [('wall_basketball_hoop', 'wall-basketball-hoop-sports-equipment-7cfb56b9'),
                       ('locker_bank', 'lock-3-office-locker-bank-c5ce4b13')]:
        folder = DEST / name
        folder.mkdir(parents=True, exist_ok=True)
        binary = fetch('https://www.innerscene.com/api/library/' + slug + '/download')
        if binary[:4] != b'glTF': raise ValueError('Invalid GLB')
        (folder / (name + '.glb')).write_bytes(binary)
        (folder / 'source.json').write_text(json.dumps({'id':name,'source':'https://www.innerscene.com/tools/library/3d-parts/'+slug,
                                                     'license':'CC0-1.0','sha256':hashlib.sha256(binary).hexdigest()},indent=2))
        print(name + ': downloaded',flush=True)
