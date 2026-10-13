"""Run the isolated host/guest camera regression against a native development build."""
import json, subprocess, tempfile, time, sys
from pathlib import Path
root=Path.cwd(); work=Path(tempfile.mkdtemp(prefix="svinki-lobby-bugfix-")); output=root/"ArtSource/Bugfix"; output.mkdir(parents=True,exist_ok=True)
exe=Path(sys.argv[1]).resolve(); host=work/"host"; guest=work/"guest"; processes=[]; checks=[]
prefs=work/"preferences.plist"
subprocess.run(["defaults","export","unity.DefaultCompany.Svinki",str(prefs)],check=True,stdout=subprocess.DEVNULL)
def state(p):
 try:return json.loads(Path(str(p)+".state").read_text())
 except (OSError,ValueError):return {}
def wait(test,label,seconds=60):
 deadline=time.monotonic()+seconds
 while time.monotonic()<deadline:
  if test():return
  time.sleep(.2)
 raise RuntimeError(label+": "+json.dumps({"host":state(host).get("photo"),"guest":state(guest).get("photo")}))
def command(p,action,target="",index=0):
 path=Path(str(p)+".command");wait(lambda:not path.exists(),"previous command",10)
 draft=Path(str(p)+".tmp");draft.write_text(json.dumps({"action":action,"target":target,"index":index}));draft.replace(path)
 wait(lambda:not path.exists(),action,10);time.sleep(.3)
def key(p,name):command(p,"key-down",name);command(p,"key-up")
try:
 for p,is_host in [(host,True),(guest,False)]:
  log=open(str(p)+".stdout","w")
  processes.append(subprocess.Popen([str(exe),"--local-host" if is_host else "--local-join","--port","7896","--profile",work.name+"-"+p.name,"--nickname","Bugfix"+p.name,"--session-test-path",str(p),"-screen-width","1280","-screen-height","720","-windowed","-logFile",str(p)+".log"],stdout=log,stderr=subprocess.STDOUT));log.close()
  if is_host:wait(lambda:state(host).get("snapshot",{}).get("phase")==1,"host lobby",120)
 wait(lambda:state(host).get("avatars")==2 and state(guest).get("avatars")==2,"both lobby players",120)
 for p in (host,guest):
  if state(p).get("menuVisible"):key(p,"Escape")
  wait(lambda:state(p).get("inputAllowed"),"lobby input")
  key(p,"K");wait(lambda:state(p).get("photo",{}).get("equipped"),"K equips in lobby")
  assert state(p)["snapshot"]["phase"]==1
  command(p,"mouse-left");command(p,"mouse-up");wait(lambda:state(p)["photo"]["count"]==1,"lobby photograph")
  key(p,"K");wait(lambda:not state(p)["photo"]["equipped"],"K stows in lobby")
 checks.append("PASS: host and guest equip, photograph and stow cameras in the lobby without starting a round.")
 command(host,"social-basket-shot");wait(lambda:state(host).get("social",{}).get("basketball",{}).get("Last")==1 and state(guest).get("social",{}).get("basketball",{}).get("Last")==1,"basketball score")
 checks.append("PASS: an ordinary shot through the hoop still scores and replicates to the guest.")
 command(host,"capture");wait(lambda:Path(str(host)+"-capture.png").exists(),"lobby screenshot")
 import shutil;shutil.copy2(Path(str(host)+"-capture.png"),output/"native-lobby.png")
 (output/"native-lobby-validation.txt").write_text("\n".join(checks)+"\n")
 print("\n".join(checks),flush=True)
 print("Evidence:",output,"Logs:",work,flush=True)
finally:
 for p in (guest,host):
  try:command(p,"quit")
  except Exception:pass
 for process in processes:
  try:process.wait(timeout=8)
  except subprocess.TimeoutExpired:process.terminate();process.wait(timeout=8)
 subprocess.run(["defaults","import","unity.DefaultCompany.Svinki",str(prefs)],check=True,stdout=subprocess.DEVNULL)
