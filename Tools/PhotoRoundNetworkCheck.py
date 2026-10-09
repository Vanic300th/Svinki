import json,time,subprocess,shutil,os
from pathlib import Path
root=str(Path.cwd()); exe=root+'/Builds/ExpandedRound-Test/Svinki.app/Contents/MacOS/Svinki'
out=Path.cwd()/'ArtSource/PhotoRound';out.mkdir(parents=True,exist_ok=True);procs=[]
assert Path(exe).is_file(), 'Build the native development app first'
subprocess.run(['defaults','export','unity.DefaultCompany.Svinki','/tmp/svinki-photo-original-prefs.plist'],check=True,stdout=subprocess.DEVNULL)
checkpoint=Path.home()/'Library/Application Support/DefaultCompany/Svinki/round-checkpoint.json'
Path('/tmp/svinki-photo-checkpoint-exists').write_text(str(checkpoint.exists()))
if checkpoint.exists():shutil.copy2(checkpoint,'/tmp/svinki-photo-original-checkpoint')
H='/tmp/svinki-photo-host';G='/tmp/svinki-photo-guest'
def state(p):
 try:return json.loads(Path(p+'.state').read_text())
 except (OSError,ValueError):return {}
def wait(pred,label,timeout=45):
 start=time.time()
 while time.time()-start<timeout:
  if pred():return
  time.sleep(.15)
 raise RuntimeError(label+' timed out: '+json.dumps({'host':state(H).get('status'),'guest':state(G).get('status'),'Hphoto':state(H).get('photo'),'Gphoto':state(G).get('photo')}))
def command(p,action,target='',index=0):
 wait(lambda:not Path(p+'.command').exists(),'previous command',10)
 temp=Path(p+'.temp');temp.write_text(json.dumps({'action':action,'target':target,'index':index}));temp.replace(p+'.command')
 wait(lambda:not Path(p+'.command').exists(),action+' consumption',10);time.sleep(.3)
def key(p,k):command(p,'key-down',k);command(p,'key-up')
def mouse(p):command(p,'mouse-left');command(p,'mouse-up')
def capture(p,name):
 f=Path(p+'-capture.png');f.unlink(missing_ok=True);command(p,'capture');wait(f.exists,'capture',10);shutil.copy2(f,out/name)
def launch(p,host):
 for suffix in ('.state','.command'):Path(p+suffix).unlink(missing_ok=True)
 args=[exe,'--local-host' if host else '--local-join','--port','7797','--profile','photo-host' if host else 'photo-guest','--nickname','PhotoHost' if host else 'PhotoGuest','--session-test-path',p,'-screen-width','1280','-screen-height','720','-windowed','-logFile',p+'.log']
 procs.append(subprocess.Popen(args,stdout=open(p+'.stdout','w'),stderr=subprocess.STDOUT))
try:
 launch(H,True);wait(lambda:state(H).get('snapshot',{}).get('phase')==1,'host lobby')
 launch(G,False);wait(lambda:len(state(H).get('snapshot',{}).get('players',[]))==2,'guest lobby')
 command(H,'ready');command(G,'ready');command(H,'start')
 wait(lambda:state(H).get('avatars')==2 and state(G).get('avatars')==2 and state(G).get('snapshot',{}).get('phase')==3,'two-player round')
 command(H,'calm-npcs');command(G,'calm-npcs');time.sleep(1)
 a=state(H);b=state(G)
 assert len(a['carts'])==3 and len(a['monkeys'])==2 and len(a['cameraPickups'])==2
 assert len(a['npcs'])==40 and len(a['pickups'])==96
 def poses(items):return {x['name']:x['position'] for x in items}
 import math
 maximum=0
 for category in ('carts','pickups','cameraPickups','npcs'):
  left,right=poses(a[category]),poses(b[category]);assert left.keys()==right.keys(),category
  for name,pos in left.items():
   distance=math.sqrt(sum((pos[k]-right[name][k])**2 for k in ('x','y','z')));maximum=max(maximum,distance);assert distance<.15,(category,name,distance)
 (out/'two-player-layout.json').write_text(json.dumps({'host':a,'guest':b,'maximumPoseDifference':maximum},indent=2))
 print('PASS two players: 3 carts, 40 mannequins, 96 clothes, 2 toys, 2 cameras; shared poses',maximum,flush=True)
 command(H,'photo-warp');time.sleep(.7);key(H,'E');wait(lambda:state(H)['photo']['owned'],'host camera pickup',8)
 command(G,'photo-warp');time.sleep(.7);key(G,'E');wait(lambda:state(G)['photo']['owned'],'guest camera pickup',8)
 command(H,'design-position',index=0);command(H,'look-forward');key(H,'K');wait(lambda:state(H)['photo']['equipped'],'host camera equip',8)
 capture(H,'camera-viewfinder.png')
 mouse(H);wait(lambda:state(H)['photo']['count']==1,'host photograph',8)
 assert state(G)['photo']['count']==0
 assert not any(x['down'] for x in state(H)['stuns'])
 command(G,'design-position',index=2);command(G,'look-forward');key(G,'K');wait(lambda:state(G)['photo']['equipped'],'guest camera equip',8)
 mouse(G);wait(lambda:state(G)['photo']['count']==1,'guest photograph',8)
 assert state(H)['photo']['count']==1
 assert not any(x['down'] for x in state(G)['stuns'])
 print('PASS host and guest camera pickup, equipped state, shutter, separate photo albums, no mannequin stun',flush=True)
 key(H,'P');wait(lambda:state(H)['photo']['album'],'album open')
 old=state(H)['localPosition'];command(H,'key-down','W');time.sleep(.8);command(H,'key-up');new=state(H)['localPosition']
 assert math.dist(list(old.values()),list(new.values()))<.1
 assert not state(H)['inputAllowed']
 key(H,'Escape');wait(lambda:not state(H)['photo']['album'],'album close');assert not state(H)['menuVisible']
 assert state(H)['inputAllowed']
 for i in range(13):command(H,'photo-capture-buffer')
 wait(lambda:state(H)['photo']['count']==12,'bounded album')
 command(H,'photo-export');wait(lambda:Path(H+'-photo.png').exists(),'photo export');shutil.copy2(H+'-photo.png',out/'world-photo.png')
 key(H,'P');capture(H,'photo-album.png');key(H,'Escape')
 command(H,'photo-guide');capture(H,'camera-guide.png');key(H,'Escape')
 command(H,'mouse-right');command(H,'mouse-up');wait(lambda:not state(H)['photo']['equipped'],'camera stow')
 print('PASS album blocks movement, Escape consumes menu input, 12-image cap, stow; screenshot evidence written',flush=True)
 saved=state(H);command(G,'mouse-right');command(G,'mouse-up');command(H,'warp-finish');command(G,'warp-finish');time.sleep(.7);command(H,'end');command(G,'end');wait(lambda:state(H).get('snapshot',{}).get('phase')==5,'fashion show');command(H,'continue');wait(lambda:state(H).get('avatars')==0,'round unload');wait(lambda:state(H)['photo']['count']==0,'album reset')
 (out/'camera-network-validation.txt').write_text('PASS: two native clients; camera pickup with E, equip with K, host and guest photographs with LMB; owner-only albums; no mannequin stun.\nPASS: same random poses for all 96 garments, 40 mannequins, three carts and two cameras; two monkeys for two players. Maximum pose difference '+str(maximum)+'m.\nPASS: album blocks movement; Escape closes album without opening menu; latest 12-image cap; camera stow; album cleared on round unload.\n')
 print('PASS next round unload clears album',flush=True)
 wait(lambda:state(H).get('snapshot',{}).get('phase')==1 and state(G).get('snapshot',{}).get('phase')==1,'return to lobby');command(H,'ready');command(G,'ready');command(H,'start');wait(lambda:state(H).get('avatars')==2 and state(G).get('avatars')==2,'second round');command(H,'calm-npcs');command(G,'calm-npcs');time.sleep(.7);fresh=state(H);assert poses(saved['carts'])!=poses(fresh['carts']) and poses(saved['pickups'])!=poses(fresh['pickups']);assert not fresh['photo']['owned'] and fresh['photo']['count']==0;assert len(fresh['monkeys'])==2 and len(fresh['cameraPickups'])==2;print('PASS next round produces a different layout and fresh equipment',flush=True)
 command(H,'leave');wait(lambda:state(H).get('avatars')==0 and state(H).get('snapshot',{}).get('phase')==0,'session leave');command(H,'solo');wait(lambda:state(H).get('offline') and state(H).get('avatars')==1,'solo');command(H,'calm-npcs');assert len(state(H)['monkeys'])==1 and len(state(H)['cameraPickups'])==1;command(H,'photo-warp');time.sleep(.7);key(H,'E');wait(lambda:state(H)['photo']['owned'],'solo camera pickup');command(H,'design-position',index=0);command(H,'look-forward');key(H,'K');wait(lambda:state(H)['photo']['equipped'],'solo equip');mouse(H);wait(lambda:state(H)['photo']['count']==1,'solo photo');command(H,'leave');wait(lambda:state(H).get('avatars')==0 and state(H)['photo']['count']==0,'solo reset');print('PASS solo spawn, pickup, shutter, album and reset',flush=True)
 with (out/'camera-network-validation.txt').open('a') as report:report.write('PASS: second round changes cart and clothing positions; camera ownership and album reset; two new monkeys and cameras.\nPASS: single-player round, one monkey and camera, E pickup, K equip, LMB photo and album reset.\n')
except Exception as e:
 (out/'camera-test-failure.txt').write_text(str(e)+'\n'+json.dumps({'host':state(H),'guest':state(G)},indent=2))
 raise
finally:
 for p in (H,G):
  try:command(p,'quit')
  except Exception:pass
 for process in procs:
  try:process.wait(timeout=6)
  except subprocess.TimeoutExpired:process.terminate();process.wait(timeout=5)
 subprocess.run(['defaults','import','unity.DefaultCompany.Svinki','/tmp/svinki-photo-original-prefs.plist'],check=True,stdout=subprocess.DEVNULL)
 checkpoint=Path.home()/'Library/Application Support/DefaultCompany/Svinki/round-checkpoint.json'
 if Path('/tmp/svinki-photo-checkpoint-exists').read_text()=='True':shutil.copy2('/tmp/svinki-photo-original-checkpoint',checkpoint)
 else:checkpoint.unlink(missing_ok=True)
