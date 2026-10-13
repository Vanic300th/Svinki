"""Native host/client/late join regression. Requires the social lobby development build."""
import json, time, subprocess, shutil, wave, struct, math, threading, http.server, functools, re, os
from pathlib import Path
root=Path.cwd(); out=root/'ArtSource/SocialLobby'; out.mkdir(parents=True,exist_ok=True)
exe=root/'Builds/SocialLobby-Test/Svinki.app/Contents/MacOS/Svinki'
assert exe.is_file()
media=Path('/tmp/svinki-social-media');media.mkdir(exist_ok=True)
with wave.open(str(media/'test.wav'),'wb') as wav:
 wav.setparams((1,2,22050,0,'NONE','not compressed'))
 wav.writeframes(b''.join(struct.pack('<h',int(math.sin(i*2*math.pi*330/22050)*3000)) for i in range(22050*90)))
class QuietHandler(http.server.SimpleHTTPRequestHandler):
 def log_message(self,*args):pass
 def send_head(self):
  self.remaining=None
  path=self.translate_path(self.path)
  if self.headers.get('Range') and os.path.isfile(path):
   size=os.path.getsize(path);m=re.match(r'bytes=(\d+)-(\d*)',self.headers['Range'])
   if m:
    start=int(m[1]);end=min(size-1,int(m[2]) if m[2] else size-1)
    if start>=size:self.send_error(416);return None
    f=open(path,'rb');f.seek(start);self.remaining=end-start+1
    self.send_response(206);self.send_header('Content-Type',self.guess_type(path));self.send_header('Accept-Ranges','bytes')
    self.send_header('Content-Range',f'bytes {start}-{end}/{size}');self.send_header('Content-Length',str(self.remaining));self.end_headers();return f
  return super().send_head()
 def copyfile(self,source,output):
  if self.remaining is None:return super().copyfile(source,output)
  while self.remaining>0:
   part=source.read(min(65536,self.remaining))
   if not part:break
   output.write(part);self.remaining-=len(part)
server=http.server.ThreadingHTTPServer(('127.0.0.1',18987),functools.partial(QuietHandler,directory=str(media)))
threading.Thread(target=server.serve_forever,daemon=True).start()
H='/tmp/svinki-social-host';G='/tmp/svinki-social-guest';L='/tmp/svinki-social-late';procs=[];passed=[]
prefs=out/'test-original-prefs.plist'
subprocess.run(['defaults','export','unity.DefaultCompany.Svinki',str(prefs)],check=True,stdout=subprocess.DEVNULL)
checkpoint=Path.home()/'Library/Application Support/DefaultCompany/Svinki/round-checkpoint.json'
old_checkpoint=checkpoint.read_bytes() if checkpoint.exists() else None

def state(p):
 try:return json.loads(Path(p+'.state').read_text())
 except (ValueError,OSError):return {}
def wait(pred,label,timeout=45):
 start=time.time()
 while time.time()-start<timeout:
  if pred():return
  time.sleep(.15)
 raise RuntimeError(label+' timed out: '+json.dumps({p:state(p).get('status') for p in [H,G,L]}))
def cmd(p,action,target='',index=0):
 wait(lambda:not Path(p+'.command').exists(),'previous command',10)
 f=Path(p+'.temp');f.write_text(json.dumps(dict(action=action,target=target,index=index)));f.replace(p+'.command')
 wait(lambda:not Path(p+'.command').exists(),action+' consumption',10);time.sleep(.75)
def mark(text):
 passed.append('PASS: '+text);print(passed[-1],flush=True);(out/'network-validation.txt').write_text('\n'.join(passed)+'\n')
def launch(p,host,profile=None):
 for suffix in ['.state','.command']:Path(p+suffix).unlink(missing_ok=True)
 proc=subprocess.Popen([str(exe),'--local-host' if host else '--local-join','--port','7798','--profile',profile or p.split('-')[-1], '--nickname',p.split('-')[-1], '--session-test-path',p,'-screen-width','1280','-screen-height','720','-windowed','-logFile',p+'.log'],stdout=open(p+'.stdout','w'),stderr=subprocess.STDOUT)
 procs.append(proc);return proc
def social(p):return state(p).get('social',{})
def phase(p):return state(p).get('snapshot',{}).get('phase')
def capture(p,name):
 f=Path(p+'-capture.png');f.unlink(missing_ok=True);cmd(p,'capture');wait(f.exists,'capture',15);shutil.copy2(f,out/name)
try:
 launch(H,True);wait(lambda:phase(H)==1,'host lobby',120)
 guest=launch(G,False);wait(lambda:len(state(H).get('snapshot',{}).get('players',[]))==2,'two lobby members',90)
 wait(lambda:state(H).get('avatars')==2 and state(G).get('avatars')==2 and len(social(H).get('lockers',[]))==6,'playable lobby avatars')
 cmd(H,'ready');cmd(G,'ready');cmd(H,'start');time.sleep(2);assert phase(H)==1
 mark('separate Friendslop lobby spawns participants; legacy Ready/Start broadcasts cannot bypass the lift')
 cmd(H,'calm-npcs');cmd(G,'calm-npcs')
 wait(lambda:len(social(H).get('lockers',[]))==6 and social(H)['lockers'][1]['owner'],'locker assignment')
 assert len(state(H)['pickups'])==0 and len(state(H)['carts'])==0 and len(social(H)['props'])==17
 assert not social(H).get('navInLobby') and not social(H).get('navAtSpawn')
 mark('independent safe scene has no factory enemies/clothes/carts, 17 lobby props and six assigned lockers')
 cmd(G,'social-place',index=0);start_z=state(G)['localPosition']['z'];cmd(G,'key-down','W');cmd(G,'key-up');assert state(G)['localPosition']['z']>start_z+.4
 cmd(H,'chat','Friendslop hello');wait(lambda:any('Friendslop hello' in line for line in state(G).get('chat',[])),'lobby chat')
 cmd(G,'emote',index=1);wait(lambda:any(p['name']=='guest' and p['emote'] not in ('None','') for p in state(H).get('players',[])),'lobby gesture')
 cmd(G,'social-place',index=0);cmd(H,'social-place',index=0);cmd(G,'voice-tone',index=2);wait(lambda:state(H).get('relayedVoice',0)>0 and state(H).get('receivedVoice',0)>0,'lobby voice relay');cmd(G,'voice-stop')
 mark('normal movement, chat, gestures and existing proximity voice relay work in lobby')
 cmd(H,'social-locker-door',index=0);wait(lambda:abs(social(G)['lockers'][0]['door']['y'])>.785,'door synchronization')
 before=social(H)['lockers'][0]['door']['y'];cmd(G,'social-place',index=1);cmd(G,'social-locker-door',index=0);time.sleep(1)
 assert abs(social(H)['lockers'][0]['door']['y']-before)<.05
 mark('owner opens locker; other player cannot toggle it')
 cmd(H,'social-board-draw');cmd(G,'social-place',index=2);cmd(G,'social-board-draw')
 wait(lambda:social(H).get('boardCommands')==2 and social(G).get('boardCommands')==2,'simultaneous board strokes')
 cmd(G,'social-board-clear');cmd(G,'social-board-invalid');time.sleep(1);assert social(H)['boardCommands']==2
 mark('both players draw; guest global-clear and invalid points rejected')
 cmd(H,'social-prop','Rubber ball');wait(lambda:social(H).get('held')=='Rubber ball' and any(x['holder']=='host' for x in social(G)['props']),'prop pickup')
 cmd(H,'social-release',index=1);wait(lambda:not social(H).get('held') and all(not x['holder'] for x in social(G)['props']),'prop throw')
 cmd(H,'social-prop-lost','Rubber ball');wait(lambda:abs(next(x for x in social(G)['props'] if x['name']=='Rubber ball')['position']['z']+27)<.6,'lost prop reset to authored home')
 mark('pickup/throw replicated; lost prop restored by server')
 cmd(G,'social-prop-approach','Rubber ball');cmd(G,'social-prop','Rubber ball');wait(lambda:social(G).get('held')=='Rubber ball' and any(x['holder']=='guest' for x in social(H)['props']),'client prop pickup')
 cmd(G,'social-release',index=1);wait(lambda:not social(G).get('held') and all(not x['holder'] for x in social(H)['props']),'client prop throw')
 mark('remote client can pick up and throw with server-owned physics')
 cmd(H,'social-basket-shot');wait(lambda:social(H)['basketball']['Last']==1 and social(G)['basketball']['Last']==1,'basketball physical downward score')
 cmd(H,'social-dart-shot');wait(lambda:social(H)['darts']['Last']>0 and social(G)['darts']['Last']==social(H)['darts']['Last'],'dart impact score')
 cmd(H,'social-hockey');cmd(H,'social-hockey-move');cmd(H,'social-hockey-goal');wait(lambda:'0 : 1' in social(H).get('hockey','') and '0 : 1' in social(G).get('hockey',''),'hockey physical goal')
 mark('authoritative physical basketball, dart impact and hockey goal replicate to guest')
 cmd(H,'social-media-load','file:///etc/passwd',0);assert not social(H)['tv'].get('Url')
 cmd(H,'social-media-load','http://127.0.0.1:18987/test.mp4',0)
 wait(lambda:social(H)['tv'].get('Duration',0)>0 and social(G)['tv'].get('Url'), 'TV prepared',40)
 cmd(H,'social-media-pause',index=0);wait(lambda:not social(G)['tv']['Playing'],'TV pause')
 cmd(H,'social-media-pause',index=0);wait(lambda:social(G)['tv']['Playing'],'TV resume')
 mark('TV validates URLs, loads direct MP4 locally and synchronizes play/pause')
 cmd(H,'social-media-load','http://127.0.0.1:18987/test.wav',1)
 wait(lambda:social(H)['music'].get('Duration',0)>0,'jukebox prepared',30)
 cmd(G,'social-place',index=4);cmd(G,'social-media-load','http://127.0.0.1:18987/test.wav?track=2',1)
 wait(lambda:social(H).get('queue')==1 and social(G).get('queue')==1,'queue sync')
 time.sleep(2);cmd(G,'social-media-load','http://127.0.0.1:18987/test.wav?track=2',1);assert social(H)['queue']==1
 mark('jukebox local WAV playback, shared queue and duplicate rejection')
 launch(L,False);wait(lambda:phase(L)==1 and len(social(L).get('lockers',[]))==6,'late join scene',90)
 wait(lambda:social(L).get('boardCommands')==2  and social(L).get('queue')==1 and social(L)['tv'].get('Url'),'late join state')
 wait(lambda:social(L).get('tvActual',0)>1 and social(L).get('musicActual',0)>1,'late join playback starts')
 wait(lambda:abs(social(L)['tvActual']-social(H)['tvActual'])<2 and abs(social(L)['musicActual']-social(H)['musicActual'])<2,'late join drift correction',25)
 mark('late join restores board, locker door, current TV, track and queue, with timestamps within two seconds; lobby participant spawns immediately')
 cmd(G,'social-media-pause',index=1);wait(lambda:not social(H)['music']['Playing'] and not social(L)['music']['Playing'],'jukebox pause')
 cmd(G,'social-media-pause',index=1);wait(lambda:social(H)['music']['Playing'] and social(L)['music']['Playing'],'jukebox resume')
 cmd(G,'social-media-skip',index=1);wait(lambda:social(H)['queue']==0 and 'track=2' in social(L)['music']['Url'],'jukebox skip')
 cmd(H,'social-media-stop',index=0);wait(lambda:not social(L)['tv'].get('Url'),'TV stop')
 mark('jukebox pause/resume/skip and TV stop synchronize to late client')
 cmd(G,'disconnect');wait(lambda:phase(G)==1 and state(G).get('avatars')==3 and all(p['connected'] for p in state(H)['snapshot']['players']),'automatic reconnect',90)
 wait(lambda:social(G)['boardCommands']==2,'reconnect social state')
 mark('automatic reconnect restores existing lobby player, board and media state')
 cmd(H,'social-lift-in',index=0);time.sleep(1);assert phase(H)==1 and not social(H)['lift']['Closing']
 cmd(L,'social-lift-in',index=2);cmd(G,'social-lift-in',index=1);wait(lambda:social(H)['lift']['Closing'],'all inside countdown');cmd(G,'social-lift-out')
 wait(lambda:not social(H)['lift']['Closing'],'countdown cancellation');time.sleep(3.4);assert phase(H)==1
 mark('one player cannot start; exit cancels the server countdown')
 cmd(G,'social-lift-in',index=1);wait(lambda:phase(H)==3 and phase(G)==3 and phase(L)==3 and state(H).get('avatars')==3,'factory run starts',90)
 assert len(state(H)['pickups'])==96 and len(state(H)['carts'])==3 and not social(H)['lockers']
 cmd(H,'calm-npcs');cmd(G,'calm-npcs');cmd(L,'calm-npcs')
 mark('all three enter lift -> factory scene; original 96 clothes and three carts retained; social scene unloaded')
 cmd(H,'key-down','K');cmd(H,'key-up');wait(lambda:state(H).get('photo',{}).get('equipped'),'camera equip')
 cmd(H,'mouse-left');cmd(H,'mouse-up');wait(lambda:state(H)['photo']['count']==1,'camera photo');cmd(H,'key-down','K');cmd(H,'key-up');time.sleep(1)
 for player in [H,G,L]:cmd(player,'end')
 time.sleep(1);assert phase(H)==3
 cmd(H,'social-lift-in',index=0);time.sleep(3.5);assert phase(H)==3
 cmd(G,'social-lift-in',index=1);cmd(L,'social-lift-in',index=2);time.sleep(.6)
 cmd(G,'social-lift-out');time.sleep(3.5);assert phase(H)==3
 cmd(G,'social-lift-in',index=1)
 wait(lambda:phase(H)==5 and phase(G)==5 and phase(L)==5,'return lift final outfit show')
 mark('return lift: manual finish rejected; one player cannot end; exit cancels; all living participants depart to synchronized show')
 capture(H,'final-show.png')
 cmd(H,'continue');wait(lambda:phase(H)==1 and state(H).get('avatars')==3 and len(social(G).get('lockers',[]))==6,'return Friendslop lobby',90)
 assert state(H)['photo']['count']==1
 cmd(H,'social-locker-photo','0',index=0);wait(lambda:social(G)['lockers'][0]['photos']==1,'match photograph pinned after returning to lobby')
 assert social(H)['lockers'][0]['hashes'][0]==social(G)['lockers'][0]['hashes'][0]==social(L)['lockers'][0]['hashes'][0]
 mark('match photo survives return to lobby and pins with identical image hashes on all clients')
 cmd(L,'leave');wait(lambda:phase(L)==0,'late player leaves');cmd(L,'quit');time.sleep(1);launch(L,False)
 wait(lambda:phase(L)==1 and state(L).get('avatars')==3 and len(social(L).get('lockers',[]))==6 and social(L)['lockers'][0]['photos']==1,'new lobby join restores pinned match photo',90)
 assert social(L)['lockers'][0]['hashes'][0]==social(H)['lockers'][0]['hashes'][0]
 mark('new lobby connection receives the retained match photograph after scene return')
 assert social(H)['boardCommands']==0 and not social(H)['tv'].get('Url') and social(H)['queue']==0
 for player,index in [(H,0),(G,1),(L,2)]:cmd(player,'social-lift-in',index=index)
 wait(lambda:phase(H)==3 and state(H).get('avatars')==3,'second factory run',90)
 for player,index in [(H,0),(G,1),(L,2)]:cmd(player,'calm-npcs');cmd(player,'social-lift-in',index=index)
 wait(lambda:phase(H)==5,'second results');cmd(H,'continue')
 wait(lambda:phase(H)==1 and len(social(G).get('lockers',[]))==6 and social(G)['lockers'][0]['photos']==1,'pins persist between runs',90)
 mark('repeat scene transitions retain pinned photos and reset transient board/media/props')
 cmd(H,'leave');wait(lambda:phase(H)==0,'session leave');cmd(H,'solo');wait(lambda:state(H).get('offline') and state(H).get('avatars')==1,'solo',60)
 cmd(H,'calm-npcs');cmd(H,'social-place',index=0);cmd(H,'look-forward');capture(H,'lobby-first-person.png')
 cmd(H,'social-locker-approach',index=0);cmd(H,'social-locker-door',index=0);wait(lambda:abs(social(H)['lockers'][0]['door']['y'])>.785,'solo locker')
 cmd(H,'social-prop-approach','Rubber ball');cmd(H,'social-prop','Rubber ball');wait(lambda:social(H).get('held')=='Rubber ball','solo prop pickup');time.sleep(1);assert social(H).get('held')=='Rubber ball';cmd(H,'social-release',index=1)
 cmd(H,'social-place',index=2);cmd(H,'social-board-draw');wait(lambda:social(H)['boardCommands']==1,'solo drawing')
 cmd(H,'social-lift-in');wait(lambda:len(state(H).get('pickups',[]))==96 and not social(H).get('lockers'),'solo lift starts factory run',90)
 cmd(H,'calm-npcs');cmd(H,'social-lift-in');wait(lambda:state(H).get('menuVisible')==True,'solo final show');cmd(H,'continue');wait(lambda:len(social(H).get('lockers',[]))==6,'solo returns to lobby',90)
 mark('offline social interactions, lift run start, factory results and return to lobby work')
 (out/'network-failure.json').unlink(missing_ok=True)
except Exception as error:
 (out/'network-failure.json').write_text(json.dumps({'error':str(error),'host':state(H),'guest':state(G),'late':state(L)},indent=2));raise
finally:
 for p in [H,G,L]:
  try:cmd(p,'quit')
  except Exception:pass
 for proc in procs:
  try:proc.wait(timeout=5)
  except subprocess.TimeoutExpired:proc.terminate();proc.wait(timeout=5)
 server.shutdown();server.server_close()
 subprocess.run(['defaults','import','unity.DefaultCompany.Svinki',str(prefs)],check=True,stdout=subprocess.DEVNULL)
 prefs.unlink(missing_ok=True)
 if old_checkpoint is None:checkpoint.unlink(missing_ok=True)
 else:checkpoint.write_bytes(old_checkpoint)
