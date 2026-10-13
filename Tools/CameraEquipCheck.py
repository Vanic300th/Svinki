"""Opt-in native test for permanent cameras, K equipment, capture and background music."""
import json
import shutil
import subprocess
import sys
import tempfile
import time
from pathlib import Path

root = Path.cwd()
exe = Path(sys.argv[1])
output = root / 'ArtSource/CameraAndMusic'
output.mkdir(parents=True, exist_ok=True)
temporary = Path(tempfile.mkdtemp(prefix='svinki-camera-input-'))
host, guest = (temporary / name for name in ('host', 'guest'))
processes = []
checkpoint = Path.home() / 'Library/Application Support/DefaultCompany/Svinki/round-checkpoint.json'
checkpoint_data = checkpoint.read_bytes() if checkpoint.exists() else None
prefs = temporary / 'preferences.plist'
subprocess.run(['defaults', 'export', 'unity.DefaultCompany.Svinki', str(prefs)], check=True, stdout=subprocess.DEVNULL)

def state(prefix):
    try:
        return json.loads(Path(str(prefix) + '.state').read_text())
    except (OSError, ValueError):
        return {}

def wait(condition, label, timeout=45):
    end = time.monotonic() + timeout
    while time.monotonic() < end:
        if condition():
            return
        time.sleep(.15)
    raise RuntimeError(label + ': ' + json.dumps({'host': state(host).get('photo'), 'guest': state(guest).get('photo')}))

def command(prefix, action, target=''):
    destination = Path(str(prefix) + '.command')
    wait(lambda: not destination.exists(), 'previous command', 10)
    draft = Path(str(prefix) + '.temp')
    draft.write_text(json.dumps({'action': action, 'target': target}))
    draft.replace(destination)
    wait(lambda: not destination.exists(), action, 10)
    time.sleep(.35)

def key(prefix, name):
    command(prefix, 'key-down', name)
    command(prefix, 'key-up')

def shutter(prefix):
    command(prefix, 'mouse-left')
    command(prefix, 'mouse-up')

def snapshot(prefix, name):
    image = Path(str(prefix) + '-capture.png')
    image.unlink(missing_ok=True)
    command(prefix, 'capture')
    wait(image.exists, 'screenshot', 10)
    shutil.copy2(image, output / name)

checks = []
music_evidence = {}
try:
    for prefix, is_host in ((host, True), (guest, False)):
        log = open(str(prefix) + '.stdout', 'w')
        processes.append(subprocess.Popen([str(exe), '--local-host' if is_host else '--local-join', '--port', '7799',
            '--profile', 'camera-fix-' + prefix.name, '--nickname', 'Camera' + prefix.name.title(),
            '--session-test-path', str(prefix), '-screen-width', '1280', '-screen-height', '720', '-windowed',
            '-logFile', str(prefix) + '.log'], stdout=log, stderr=subprocess.STDOUT))
        log.close()
        if is_host:
            wait(lambda: state(host).get('snapshot', {}).get('phase') == 1, 'host lobby', 120)
    wait(lambda: len(state(host).get('snapshot', {}).get('players', [])) == 2, 'guest lobby', 90)
    command(host, 'ready'); command(guest, 'ready')
    wait(lambda: len(state(host).get('snapshot', {}).get('players', [])) == 2 and all(p['ready'] for p in state(host)['snapshot']['players']), 'both players ready')
    command(host, 'start')
    wait(lambda: state(host).get('avatars') == 2 and state(guest).get('avatars') == 2, 'multiplayer round', 90)
    command(host, 'calm-npcs'); command(guest, 'calm-npcs')
    for prefix in (host, guest):
        wait(lambda: state(prefix)['photo']['owned'], 'camera carried at spawn')
        assert not state(prefix)['photo']['equipped']
        assert not state(prefix)['cameraPickups']
        key(prefix, 'K'); wait(lambda: state(prefix)['photo']['equipped'], 'K equip without pickup')
        key(prefix, 'K'); wait(lambda: not state(prefix)['photo']['equipped'], 'K stow')
        key(prefix, 'K'); wait(lambda: state(prefix)['photo']['equipped'], 'K re-equip')
    checks.append('Host and guest carry cameras at spawn; K equips, stows and re-equips without a pickup. No world cameras spawn.')
    for prefix in (host, guest):
        wait(lambda: state(prefix).get('music', {}).get('playing') and state(prefix)['music']['output'] > .0001, 'audible background music')
        command(prefix, 'music-stop')
        wait(lambda: state(prefix)['music']['playing'] and state(prefix)['music']['output'] > .0001, 'music recovery after stop')
        command(prefix, 'music-rebind')
        wait(lambda: state(prefix)['music']['playing'] and state(prefix)['music']['output'] > .0001, 'music recovery after rebind')
        command(prefix, 'music-pause'); time.sleep(.8)
        assert state(prefix)['music']['paused']
        frozen = state(prefix)['music']['time']; time.sleep(.7)
        assert abs(state(prefix)['music']['time'] - frozen) < .03
        command(prefix, 'music-resume')
        wait(lambda: state(prefix)['music']['playing'] and state(prefix)['music']['output'] > .0001, 'music resumes after listener pause')
    checks.append('Both clients: music produces audio, recovers after stop/rebind, and respects listener pause.')
    music_evidence['multiplayer'] = {p.name: state(p)['music'] for p in (host, guest)}
    snapshot(host, 'k-equipped.png')
    shutter(host); wait(lambda: state(host)['photo']['count'] == 1, 'host photograph')
    assert state(guest)['photo']['count'] == 0
    shutter(guest); wait(lambda: state(guest)['photo']['count'] == 1, 'guest photograph')
    assert state(host)['photo']['count'] == 1
    checks.append('Both clients shoot with LMB; photographs stay in their own albums.')
    command(host, 'photo-rebind')
    assert state(host)['photo']['count'] == 1 and state(host)['photo']['equipped']
    key(host, 'P'); wait(lambda: state(host)['photo']['album'], 'restored album')
    key(host, 'K'); assert state(host)['photo']['equipped'] and state(host)['photo']['album']
    key(host, 'Escape'); wait(lambda: not state(host)['photo']['album'], 'album close')
    assert not state(host)['menuVisible']
    checks.append('Rebinding restores the album singleton, preserves photos and equipment; album blocks K.')
    command(host, 'photo-export')
    image = Path(str(host) + '-photo.png'); wait(image.exists, 'export photo')
    shutil.copy2(image, output / 'verified-photo.png')
    command(host, 'leave'); wait(lambda: state(host).get('avatars') == 0, 'leave multiplayer')
    command(host, 'solo'); wait(lambda: state(host).get('offline') and state(host).get('avatars') == 1, 'single-player round')
    command(host, 'calm-npcs'); command(host, 'photo-rebind')
    assert state(host)['photo']['owned'] and not state(host)['photo']['equipped'] and not state(host)['cameraPickups']
    assert state(host)['photo']['count'] == 0
    key(host, 'K'); wait(lambda: state(host)['photo']['equipped'], 'solo K equip without pickup')
    shutter(host); wait(lambda: state(host)['photo']['count'] == 1, 'solo photo')
    command(host, 'photo-rebind')
    assert state(host)['photo']['count'] == 1
    key(host, 'K'); wait(lambda: not state(host)['photo']['equipped'], 'solo stow')
    wait(lambda: state(host).get('music', {}).get('playing') and state(host)['music']['output'] > .0001, 'music on next scene load')
    checks.append('Solo: camera is always carried, K and LMB work, album survives rebinding. New scene clears photos and starts music.')
    music_evidence['solo'] = state(host)['music']
    (output / 'music-validation.json').write_text(json.dumps(music_evidence, indent=2))
    (output / 'validation.txt').write_text('\n'.join('PASS: ' + item for item in checks) + '\n')
    for item in checks:
        print('PASS:', item, flush=True)
except Exception as error:
    (output / 'failure.json').write_text(json.dumps({'error': str(error), 'host': state(host), 'guest': state(guest)}, indent=2))
    raise
finally:
    for prefix in (host, guest):
        try:
            command(prefix, 'quit')
        except Exception:
            pass
    for process in processes:
        try:
            process.wait(timeout=5)
        except subprocess.TimeoutExpired:
            process.terminate(); process.wait(timeout=5)
    subprocess.run(['defaults', 'import', 'unity.DefaultCompany.Svinki', str(prefs)], check=True, stdout=subprocess.DEVNULL)
    if checkpoint_data is None:
        checkpoint.unlink(missing_ok=True)
    else:
        checkpoint.parent.mkdir(parents=True, exist_ok=True)
        checkpoint.write_bytes(checkpoint_data)
    for name in ('host.log', 'guest.log'):
        if (temporary / name).exists():
            shutil.copy2(temporary / name, output / name)
    shutil.rmtree(temporary)
