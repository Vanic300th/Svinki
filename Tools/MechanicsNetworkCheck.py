"""Native host/guest integration scenarios; run only against a development build."""
import json, subprocess, tempfile, time, sys, shutil
from pathlib import Path
root=Path.cwd();work=Path(tempfile.mkdtemp(prefix='svinki-mechanics-network-'));out=root/'ArtSource/Mechanics';out.mkdir(parents=True,exist_ok=True)
host=work/'host';guest=work/'guest';procs=[];checks=[];exe=Path(sys.argv[1]).resolve();prefs=work/'prefs.plist'
subprocess.run(['defaults','export','unity.DefaultCompany.Svinki',str(prefs)],check=True,stdout=subprocess.DEVNULL)
def state(p):
 try:return json.loads(Path(str(p)+'.state').read_text())
 except (OSError,ValueError):return {}
def wait(test,label,seconds=35):
 end=time.monotonic()+seconds
 while time.monotonic()<end:
  try:
   if test():return
  except (KeyError,IndexError,TypeError):pass
  time.sleep(.15)
 raise RuntimeError(label+' '+json.dumps({p.name:state(p).get('mechanics') for p in [host,guest]}))
def command(p,action,target='',index=0):
 f=Path(str(p)+'.command');wait(lambda:not f.exists(),'pending command',10)
 temp=Path(str(p)+'.tmp');temp.write_text(json.dumps({'action':action,'target':target,'index':index}));temp.replace(f)
 wait(lambda:not f.exists(),action,10);time.sleep(.15)
def key(p,name):command(p,'key-down',name);time.sleep(.15);command(p,'key-up')
def warp(p,x,y,z):command(p,'mechanics-position',json.dumps({'x':x,'y':y,'z':z}));time.sleep(.3)
def player(p,name):return next((q for q in state(p).get('mechanics',{}).get('players',[]) if q['name']==name),{})
def avatar(p,name):return next((q for q in state(p).get('players',[]) if q['name']==name),{})
def passed(text):checks.append('PASS: '+text);print(checks[-1],flush=True);(out/'network-validation.txt').write_text('\n'.join(checks)+'\n')
def capture(p,label):
 f=Path(str(p)+'-capture.png');f.unlink(missing_ok=True);command(p,'capture');wait(f.exists,'capture');shutil.copy2(f,out/(label+'.png'))
try:
 for p,owner in [(host,True),(guest,False)]:
  log=open(str(p)+'.stdout','w');procs.append(subprocess.Popen([str(exe),'--local-host' if owner else '--local-join','--port','7897','--profile',work.name+'-'+p.name,'--nickname',p.name,'--session-test-path',str(p),'-screen-width','1280','-screen-height','720','-windowed','-logFile',str(p)+'.log'],stdout=log,stderr=subprocess.STDOUT));log.close()
  if owner:wait(lambda:state(host).get('snapshot',{}).get('phase')==1,'host lobby',120)
 wait(lambda:state(host).get('avatars')==2 and state(guest).get('avatars')==2,'two players',120)
 for p in [host,guest]:
  if state(p).get('menuVisible'):key(p,'Escape')
 command(host,'mechanics-music',index=0);wait(lambda:state(host).get('mechanics',{}).get('musicVolume')==0,'mute music')
 command(host,'mechanics-music',index=65);wait(lambda:abs(state(host).get('mechanics',{}).get('musicVolume',0)-.65)<.001,'volume')
 key(host,'K');command(host,'mouse-left');command(host,'mouse-up');wait(lambda:state(host).get('photo',{}).get('count')==1,'photo');key(host,'K')
 command(host,'photo-export');wait(lambda:Path(str(host)+'-photo.png').exists(),'photo export');shutil.copy2(Path(str(host)+'-photo.png'),out/'camera-quality.png')
 command(host,'mechanics-photo-view');wait(lambda:state(host)['mechanics']['photoViewer'],'photo window');capture(host,'photo-window')
 command(host,'mechanics-photo-full');capture(host,'photo-full-screen');key(host,'Escape');wait(lambda:not state(host)['mechanics']['photoViewer'],'close photo')
 passed('Music volume changes; new photos open in a window and full screen.')
 command(host,'mechanics-locker');wait(lambda:state(host).get('cargoOpen'),'locker panel');capture(host,'locker-slots');key(host,'Escape')
 for p in [host,guest]:command(p,'social-lift-in',index=0 if p==host else 1)
 wait(lambda:state(host).get('snapshot',{}).get('phase')==3 and state(guest).get('snapshot',{}).get('phase')==3,'round',120)
 wait(lambda:state(host).get('avatars')==2 and state(guest).get('avatars')==2 and state(host).get('mechanics',{}).get('activeHostiles',0)>0,'round avatars and mannequins')
 assert state(host)['mechanics']['activeHostiles']==11,state(host)['mechanics']
 assert state(guest)['mechanics']['activeHostiles']==11,state(guest)['mechanics']
 passed('Eleven aggressive mannequins remain from the authored sixteen on both peers.')
 command(host,'calm-npcs');warp(host,0,.05,5);warp(guest,8,.05,5)
 command(host,'emote',index=7);wait(lambda:player(host,'host').get('ball') and player(guest,'host').get('ball'),'ball form')
 command(guest,'mechanics-ball-grab','host');time.sleep(.4);assert not player(host,'host').get('ballHolder')
 warp(guest,1.5,.05,5);command(guest,'mechanics-ball-grab','host');wait(lambda:player(host,'host').get('ballHolder')=='guest' and player(guest,'host').get('ballHolder')=='guest','ball grab')
 command(guest,'mechanics-ball-throw');wait(lambda:not player(host,'host').get('ballHolder'),'ball throw');key(host,'Space');wait(lambda:not player(host,'host').get('ball') and not player(guest,'host').get('ball'),'jump exits')
 passed('Player ball replicates; distant grabs fail, nearby grabs/throws succeed, and Space exits.')
 time.sleep(1);command(host,'emote',index=7);wait(lambda:player(host,'host').get('ball'),'second ball')
 command(host,'mechanics-ball-shot');wait(lambda:any(q.get('down') for q in state(host).get('stuns',[])) and any(q.get('down') for q in state(guest).get('stuns',[])),'ball stuns mannequin')
 assert any(q.get('remaining',0)>24 for q in state(host)['stuns']);key(host,'LeftCtrl');wait(lambda:not player(host,'host').get('ball'),'crouch exits')
 passed('A physical ball impact knocks a mannequin down for the same 30 seconds as the monkey; Ctrl exits.')
 warp(host,0,.05,5);warp(guest,1.5,.05,5);command(host,'mechanics-prep-cart');time.sleep(.5)
 cart=state(host)['carts'][0]['name'];command(host,'cargo-store',cart,index=0);wait(lambda:len(next(c for c in state(host)['carts'] if c['name']==cart)['cargo'])>0,'loaded cart')
 command(host,'mannequin-hit-player','guest');wait(lambda:avatar(guest,'guest').get('hits')==1,'first hit');command(host,'recover-player','guest');time.sleep(1.3)
 command(host,'mannequin-hit-player','guest');wait(lambda:avatar(host,'guest').get('dead') and player(host,'guest').get('ghost'),'ghost',30)
 before=state(guest)['eyePosition'];corpse=player(host,'guest')['bodyPosition'];command(guest,'key-down','W');time.sleep(1);command(guest,'key-up');after=state(guest)['eyePosition']
 assert sum((after[a]-before[a])**2 for a in ['x','y','z'])>9,(before,after)
 assert state(guest)['mechanics']['scares']>=2
 command(guest,'mechanics-ghost-edge');time.sleep(.5);capture(guest,'ghost-flight')
 passed('Two mannequin hits trigger scares and death; the ghost flies independently of its physical body.')
 pos=player(host,'guest')['bodyPosition'];warp(host,pos['x'],.05,pos['z']-2);command(host,'look-forward');command(host,'mechanics-body-grab','guest')
 wait(lambda:player(host,'guest').get('bodyHolder')=='host' and player(guest,'guest').get('bodyHolder')=='host','body grab')
 capture(host,'carrying-body')
 command(host,'mechanics-body-cart',cart);wait(lambda:player(host,'guest').get('bodyCart')==cart,'body loaded')
 command(host,'mechanics-cart-lift',cart);wait(lambda:not avatar(host,'guest').get('dead') and not avatar(guest,'guest').get('dead') and state(guest).get('inputAllowed'),'revive',30)
 passed('The corpse can be carried, placed in a loaded cart, and revived in the lift on both peers.')
 lift_z=player(host,'guest')['bodyPosition']['z'];warp(host,-1.15,.1,lift_z);command(host,'cargo-wear',cart,index=0);wait(lambda:len(avatar(host,'host').get('outfit',[]))>0,'recover garment for runway')
 for p in [host,guest]:command(p,'warp-finish')
 wait(lambda:state(host).get('snapshot',{}).get('phase')==5 and state(guest).get('snapshot',{}).get('phase')==5,'results',40)
 order=[host if e['nickname']=='host' else guest for e in state(host)['snapshot']['results']['entries']]
 for p in order:
  wait(lambda:state(p).get('mechanics',{}).get('runwayOwn'),'own runway turn',25)
  command(p,'key-down','W');time.sleep(.45);command(p,'key-up');key(p,'Space');command(p,'mechanics-runway-emote',index=1)
  command(p,'mechanics-runway-save');wait(lambda:state(p)['mechanics']['memories']==1,'outfit memory')
  capture(p,'runway-'+p.name)
 wait(lambda:state(host)['mechanics']['runwayElapsed']>=20,'twenty seconds',25)
 passed('Each player controls their 10-second runway turn and explicitly saves their own outfit.')
 command(host,'continue');wait(lambda:state(host).get('snapshot',{}).get('phase')==1,'return lobby',60)
 command(host,'mechanics-locker');capture(host,'locker-memories')
 command(host,'mouse-position',json.dumps({'x':433,'y':116}));command(host,'mouse-left');command(host,'mouse-up')
 wait(lambda:len(avatar(host,'host').get('outfit',[]))>0 and len(avatar(guest,'host').get('outfit',[]))>0,'wear saved nonempty outfit')
 passed('A remembered nonempty outfit can be selected in the locker and its clothes replicate to the other player.')
 command(host,'mouse-position',json.dumps({'x':805,'y':222}));command(host,'mouse-left');command(host,'mouse-up');time.sleep(2);wait(lambda:any(l.get('photos',0)>0 for l in state(host)['social']['lockers']),'occupied photo slot');capture(host,'locker-occupied')
 passed('Saved outfit memories remain available in the locker after returning to the lobby.')
 for p in [host,guest]:(out/(p.name+'-final-state.json')).write_text(json.dumps(state(p),indent=2))
 command(guest,'quit');procs[1].wait(timeout=12)
 command(host,'quit');procs[0].wait(timeout=12)
 Path(str(host)+'.state').unlink(missing_ok=True)
 log=open(str(host)+'.restart.stdout','w');procs.append(subprocess.Popen([str(exe),'--local-host','--port','7897','--profile',work.name+'-host','--nickname','host','--session-test-path',str(host),'-screen-width','1280','-screen-height','720','-windowed','-logFile',str(host)+'.restart.log'],stdout=log,stderr=subprocess.STDOUT));log.close()
 time.sleep(4);wait(lambda:state(host).get('snapshot',{}).get('phase')==1 and state(host).get('avatars')==1 and state(host)['mechanics']['memories']==1,'memory after restart',90)
 passed('Explicitly saved outfit memories reload from disk after restarting the game.')
 print('Evidence:',out,'Logs:',work,flush=True)
except Exception:
 (out/'failed-state.json').write_text(json.dumps({p.name:state(p) for p in [host,guest]},indent=2));raise
finally:
 for p in [guest,host]:
  try:command(p,'quit')
  except Exception:pass
 for process in procs:
  try:process.wait(timeout=8)
  except subprocess.TimeoutExpired:process.terminate();process.wait(timeout=8)
 subprocess.run(['defaults','import','unity.DefaultCompany.Svinki',str(prefs)],check=True,stdout=subprocess.DEVNULL)
