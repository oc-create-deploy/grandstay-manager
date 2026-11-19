"""Drive normal native touch input; QA snapshots are read-only observations."""
import json, os, pathlib, subprocess, time
out = pathlib.Path('screenshots'); out.mkdir(exist_ok=True)
udid = os.environ['UDID']; bundle = os.environ['APP_BUNDLE_ID']
container = pathlib.Path(subprocess.check_output(['xcrun','simctl','get_app_container',udid,bundle,'data'],text=True).strip())
seq = 0
import importlib.util
spec=importlib.util.spec_from_file_location('touchbridge',pathlib.Path(__file__).with_name('native-touch-bridge.py'))
module=importlib.util.module_from_spec(spec);spec.loader.exec_module(module)
bridge=module.TouchBridge(out,udid,bundle)

def state():
    deadline = time.time()+90
    while time.time()<deadline:
        for p in container.rglob('qa-state.json'):
            try:
                s=json.loads(p.read_text())
                assert not s['errors'],s['errors']
                return s
            except (json.JSONDecodeError,FileNotFoundError): pass
        time.sleep(.5)
    probe_errors=list(container.rglob('qa-recorder-error.txt'))
    if probe_errors:
        detail=probe_errors[0].read_text()
        (out/'qa-recorder-error.txt').write_text(detail)
        raise AssertionError('Native diagnostic recorder failed: '+detail)
    raise AssertionError('Read-only native snapshot missing')

def capture(label):
    (out/(label+'.json')).write_text(json.dumps(state(),indent=2))
    subprocess.run(['xcrun','simctl','io',udid,'screenshot',str(out/(label+'.png'))],check=True)

def point(item):
    assert 0<item['x']<1 and 0<item['y']<1,item
    return f"{round(item['x']*100)}%, {round(item['y']*100)}%"

def swipe_point(item):
    return f"{item['x']*100:.4f}%, {item['y']*100:.4f}%"

def action(body):
    global seq
    seq+=1
    p=out/f'input-{seq:03}.yaml'
    p.write_text(f'appId: {bundle}\n---\n'+body+'\n')
    bridge.action(body)
    time.sleep(1)

def button(prefix):
    return next((i for i in state()['items'] if i['kind']=='button' and i.get('text','').strip().startswith(prefix)),None)

def tap(prefix):
    item=button(prefix);assert item and item['interactable'],('Button unavailable',prefix,item)
    action('- tapOn:\n    point: "'+point(item)+'"')

def texts(): return [i.get('text','') for i in state()['items'] if i['kind']=='text']
def money():
    import re
    return next(tuple(map(int,re.match(r'\$(\d+)  \|  (\d+) room',t).groups())) for t in texts() if re.match(r'\$(\d+)  \|  (\d+) room',t))
def wait_for(predicate,label,seconds=120):
    deadline=time.time()+seconds
    while time.time()<deadline:
        if predicate(): return
        time.sleep(1)
    capture('failure-'+label);raise AssertionError(label)

wait_for(lambda: any('GRANDSTAY' in t for t in texts()),'dashboard')
capture('01-dashboard')
assert money()==(25,1),('Unexpected fresh balance',money())
assert not button('ADD ROOM')['interactable'],'Unaffordable room purchase enabled'
assert not button('BETTER SERVICE')['interactable'],'Unaffordable upgrade enabled'
for visit in range(9):
    wait_for(lambda: button('CHECK IN')['interactable'],'guest-ready')
    before=money()[0];tap('CHECK IN')
    wait_for(lambda: any('Guest staying' in t for t in texts()),'guest-checked-in',30)
    if visit==0:capture('02-stay')
    wait_for(lambda: button('COLLECT')['interactable'],'stay-payment')
    tap('COLLECT');assert money()[0]>before,('Collection did not increase cash',before,money())
    if visit==1:
        capture('03-earned');tap('ADD ROOM');assert money()[1]==2,'Expansion failed';capture('04-expanded')
    if visit==4:
        tap('BETTER SERVICE');assert any('Stay pays $35' in t for t in texts()),'Service upgrade failed';capture('05-upgraded')
    if visit==5:
        tap('CHECK IN');capture('06-concurrent-stays')
# Let all stays finish before taking a stable persistence checkpoint.
time.sleep(15)
if button('COLLECT')['interactable']:tap('COLLECT')
expected=money();capture('07-before-relaunch')
subprocess.run(['xcrun','simctl','terminate',udid,bundle],check=True)
for p in container.rglob('qa-state.json'):p.unlink()
subprocess.run(['xcrun','simctl','launch',udid,bundle,'--playabilityQA'],check=True)
wait_for(lambda: money()==expected,'persisted-hotel')
assert any('Stay pays $35' in t for t in texts()),'Service upgrade did not persist'
capture('08-restored')
(out/'result.json').write_text(json.dumps({'nativeCheckInStayCollect':True,'expansion':True,'serviceUpgrade':True,'persistedCashAndRooms':expected,'nativeRelaunch':True},indent=2))
