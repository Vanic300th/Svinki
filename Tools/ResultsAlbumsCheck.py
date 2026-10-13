"""Native multiplayer check: score-screen photo sharing, ghosts, late spectators and round cleanup."""
import json
import shutil
import subprocess
import sys
import tempfile
import time
from pathlib import Path

root=Path.cwd();exe=Path(sys.argv[1]);out=root/'ArtSource/ResultsAlbums';out.mkdir(parents=True,exist_ok=True)
temporary=Path(tempfile.mkdtemp(prefix='svinki-albums-input-'))
host,guest,spectator=(temporary/name for name in ('host','guest','spectator'));processes=[]
checkpoint=Path.home()/'Library/Application Support/DefaultCompany/Svinki/round-checkpoint.json'
saved_checkpoint=checkpoint.read_bytes() if checkpoint.exists() else None
preferences=temporary/'preferences.plist'
subprocess.run(['defaults','export','unity.DefaultCompany.Svinki',str(preferences)],check=True,stdout=subprocess.DEVNULL)

def state(prefix):
    try:return json.loads(Path(str(prefix)+'.state').read_text())
    except (OSError,ValueError):return {}

def wait(condition,label,timeout=60):
    end=time.monotonic()+timeout
    while time.monotonic()<end:
        if condition():return
        time.sleep(.15)
    raise RuntimeError(label+' timed out')

def command(prefix,action,target='',index=0):
    destination=Path(str(prefix)+'.command');wait(lambda:not destination.exists(),'previous command',10)
    draft=Path(str(prefix)+'.temp');draft.write_text(json.dumps({'action':action,'target':target,'index':index}));draft.replace(destination)
    wait(lambda:not destination.exists(),action,10);time.sleep(.35)

def key(prefix,name):command(prefix,'key-down',name);command(prefix,'key-up')
def shutter(prefix):command(prefix,'mouse-left');command(prefix,'mouse-up')
def launch(prefix,is_host=False):
    log=open(str(prefix)+'.stdout','w')
    processes.append(subprocess.Popen([str(exe),'--local-host' if is_host else '--local-join','--port','7798',
        '--profile','albums-'+prefix.name,'--nickname','Album'+prefix.name.title(),'--session-test-path',str(prefix),
        '-screen-width','1280','-screen-height','720','-windowed','-logFile',str(prefix)+'.log'],stdout=log,stderr=subprocess.STDOUT))
    log.close()

def album(prefix,name):return next((a for a in state(prefix).get('sharedAlbums',[]) if a['name']==name),{})
def entry_index(prefix,name):return next(i for i,e in enumerate(state(prefix)['snapshot']['results']['entries']) if e['nickname']==name)
def screenshot(prefix,name):
    image=Path(str(prefix)+'-capture.png');image.unlink(missing_ok=True);command(prefix,'capture');wait(image.exists,'capture',15);shutil.copy2(image,out/name)

checks=[]
try:
    launch(host,True);wait(lambda:state(host).get('snapshot',{}).get('phase')==1,'host lobby',120)
    launch(guest);wait(lambda:len(state(host).get('snapshot',{}).get('players',[]))==2,'guest lobby',120)
    command(host,'ready');command(guest,'ready')
    wait(lambda:all(p['ready'] for p in state(host)['snapshot']['players']),'both ready')
    command(host,'start');wait(lambda:state(host).get('avatars')==2 and state(guest).get('avatars')==2,'round',120)
    for prefix in (host,guest):
        command(prefix,'calm-npcs');assert state(prefix)['aggressors']==8
        key(prefix,'K');wait(lambda:state(prefix)['photo']['equipped'],'camera')
    for number in range(13):
        time.sleep(1.05);shutter(host);wait(lambda:state(host)['photo']['count']==min(number+1,12),'host shot')
        if number<2:
            shutter(guest);wait(lambda:state(guest)['photo']['count']==number+1,'guest shot')
    time.sleep(2)
    command(host,'mannequin-hit-player','AlbumGuest');wait(lambda:state(guest).get('hits')==1,'first mannequin hit')
    command(host,'recover-player','AlbumGuest');time.sleep(1.5)
    command(host,'mannequin-hit-player','AlbumGuest');wait(lambda:state(guest).get('eliminated'),'elimination')
    command(host,'mouse-right');command(host,'mouse-up');command(host,'warp-finish');command(host,'end')
    wait(lambda:state(host).get('snapshot',{}).get('phase')==5 and state(guest).get('snapshot',{}).get('phase')==5,'results')
    for prefix in (host,guest):
        wait(lambda:album(prefix,'AlbumHost').get('loaded')==12 and album(prefix,'AlbumGuest').get('loaded')==2,'all shared images')
        command(prefix,'runway-skip');command(prefix,'results-albums');wait(lambda:state(prefix).get('resultsAlbums'),'album button')
    for name in ('AlbumHost','AlbumGuest'):
        assert album(host,name)['hashes']==album(guest,name)['hashes']
    assert len(set(album(host,'AlbumHost')['hashes']))==12
    assert next(e for e in state(host)['snapshot']['results']['entries'] if e['nickname']=='AlbumGuest')['eliminated']
    checks.append('Two clients receive identical images: latest 12 host photos and both photos of the eliminated guest. Eight aggressive mannequins on each client.')
    command(host,'album-player',index=entry_index(host,'AlbumHost'))
    command(host,'album-next');assert state(host)['albumPhoto']==1
    command(host,'album-previous');assert state(host)['albumPhoto']==0
    screenshot(host,'shared-album.png')
    command(host,'album-layout');layout=Path(str(host)+'-album-layout.txt').read_text();(out/'ui-layout.txt').write_text(layout);assert 'OVERFLOW' not in layout and 'EventSystems=1' in layout and 'GraphicRaycaster=True' in layout,layout
    command(host,'album-player',index=entry_index(host,'AlbumGuest'));screenshot(host,'ghost-album.png')
    command(host,'album-scores');assert not state(host)['resultsAlbums'] and state(host)['snapshot']['phase']==5
    checks.append('Score-screen album button, player selection, next/previous and return to scores all work.')
    launch(spectator);wait(lambda:state(spectator).get('snapshot',{}).get('phase')==5,'late spectator',120)
    wait(lambda:album(spectator,'AlbumHost').get('loaded')==12 and album(spectator,'AlbumGuest').get('loaded')==2,'late spectator albums')
    assert album(spectator,'AlbumHost')['hashes']==album(host,'AlbumHost')['hashes']
    assert album(spectator,'AlbumGuest')['hashes']==album(host,'AlbumGuest')['hashes']
    checks.append('A spectator joining after the round receives the same complete photo albums.')
    (out/'multiplayer-albums.json').write_text(json.dumps({p.name:state(p)['sharedAlbums'] for p in (host,guest,spectator)},indent=2))
    command(host,'continue');wait(lambda:state(host).get('avatars')==0 and state(host).get('snapshot',{}).get('phase')==1,'round unload')
    assert not state(host)['sharedAlbums'] and state(host)['photo']['count']==0
    command(host,'leave');wait(lambda:state(host).get('snapshot',{}).get('phase')==0,'leave')
    command(host,'solo');wait(lambda:state(host).get('offline') and state(host).get('avatars')==1,'solo',120)
    command(host,'calm-npcs');assert state(host)['aggressors']==8
    command(host,'warp-finish');command(host,'end');wait(lambda:bool(state(host).get('sharedAlbums')),'solo results')
    assert state(host)['sharedAlbums'][0]['loaded']==0
    command(host,'runway-skip');command(host,'results-albums');wait(lambda:state(host).get('resultsAlbums'),'empty album')
    screenshot(host,'empty-album.png')
    command(host,'album-scores');command(host,'continue');wait(lambda:not state(host)['menuVisible'],'resume solo')
    key(host,'K');wait(lambda:state(host)['photo']['equipped'],'solo camera')
    shutter(host);wait(lambda:state(host)['photo']['count']==1,'solo photograph')
    command(host,'mouse-right');command(host,'mouse-up');command(host,'end');wait(lambda:bool(state(host).get('sharedAlbums')),'solo photograph results')
    assert state(host)['sharedAlbums'][0]['loaded']==1
    command(host,'runway-skip');command(host,'results-albums');screenshot(host,'solo-album.png')
    checks.append('Round unload clears shared and personal photos. Solo supports both empty albums and real photographs in results.')
    (out/'validation.txt').write_text('\n'.join('PASS: '+c for c in checks)+'\n')
    (out/'album-evidence.json').write_text(json.dumps({'solo':state(host),'checks':checks},indent=2))
    for c in checks:print('PASS:',c,flush=True)
except Exception as error:
    (out/'failure.json').write_text(json.dumps({'error':str(error),'host':state(host),'guest':state(guest),'spectator':state(spectator)},indent=2))
    raise
finally:
    for prefix in (host,guest,spectator):
        if Path(str(prefix)+'.state').exists():
            try:command(prefix,'quit')
            except Exception:pass
    for process in processes:
        try:process.wait(timeout=5)
        except subprocess.TimeoutExpired:process.terminate();process.wait(timeout=5)
    subprocess.run(['defaults','import','unity.DefaultCompany.Svinki',str(preferences)],check=True,stdout=subprocess.DEVNULL)
    if saved_checkpoint is None:checkpoint.unlink(missing_ok=True)
    else:checkpoint.parent.mkdir(parents=True,exist_ok=True);checkpoint.write_bytes(saved_checkpoint)
    for name in ('host.log','guest.log','spectator.log'):
        p=temporary/name
        if p.exists():shutil.copy2(p,out/name)
    shutil.rmtree(temporary)
