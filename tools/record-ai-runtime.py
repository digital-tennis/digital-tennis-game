"""Collect this integration's real test, build and native player receipts; never invent visual inspection."""
import argparse,hashlib,json,shutil,xml.etree.ElementTree as ET
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1];OUT=ROOT/'docs/evidence/ai-full-match'
p=argparse.ArgumentParser();p.add_argument('--window-inspected',action='store_true');p.add_argument('--batch-input',action='store_true');a=p.parse_args()
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
results={}
for name in ['test-edit','test-play','test-ai-runtime']:
 src=ROOT/'.work'/(name+'.xml');r=ET.parse(src).getroot()
 assert r.get('failed')=='0' and int(r.get('passed'))>0
 dest=OUT/('runtime-'+name+'.xml');shutil.copyfile(src,dest)
 results[name]={k:r.get(k) for k in ['total','passed','failed','skipped']};results[name]['sha256']=sha(dest)
scene=json.loads((ROOT/'.work/ai-runtime/scene.json').read_text());assert scene['passed'] and scene['reset'] and scene['pauseHeld'] and scene['onlineIsolated']
assert max(scene['nearSets'],scene['farSets'])==2 and scene['points']==len(scene['trajectory'])
shutil.copyfile(ROOT/'.work/ai-runtime/scene.json',OUT/'runtime-scene.json')
assert not (a.window_inspected and a.batch_input),'Batch evidence cannot stand in for ordinary window inspection'
native=ROOT/'.work/ai-runtime'/('mac-batch' if a.batch_input else 'mac');r=json.loads((native/'result.json').read_text());assert r['passed'] and r['runtimeErrors']==0 and r['pauseHeld'] and r['rematchReset']
assert max(r['completedGame']['score']['sets'])==2
shutil.copyfile(native/'result.json',OUT/'runtime-mac.json')
events=[line for line in (native/'player.log').read_text().splitlines() if line.startswith(('TENNIS_AI_MODEL','TENNIS_AI ','TENNIS_PHASE','TENNIS_POINT','FRONT_END_CAPTURE','FRONT_END_VALIDATION'))]
(OUT/'runtime-events.txt').write_text('\n'.join(events)+'\n')
for image,target in [('10-playing.png','gameplay.png'),('12-results.png','runtime-results.png'),('13-rematch.png','runtime-rematch.png')]:
 src=native/image;assert src.read_bytes().startswith(b'\x89PNG');shutil.copyfile(src,OUT/target)
frames={}
for name in ['10-playing','12-results','13-rematch']:
 frames[name]=json.loads((native/(name+'.json')).read_text())
model=ROOT/'Assets/Resources/FullMatchAI/resumed-ppo.json';assert sha(model)==sha(OUT/'resume-ppo.json')
source=['Assets/Scripts/Runtime/TennisAiOpponent.cs','Assets/Scripts/Runtime/FullMatchPolicyAdapter.cs','Assets/Scripts/Runtime/FullMatchObservation.cs','Assets/Tests/EditMode/ResumedActorTests.cs','Assets/Tests/PlayMode/FullMatchRuntimeTests.cs','Packages/manifest.json','Packages/packages-lock.json']
assembly=ROOT/'Builds/mac/DigitalTennis.app/Contents/Resources/Data/Managed/DigitalTennis.Runtime.dll'
assert assembly.is_file()
report=dict(schema=1,passed=a.window_inspected,automatedPassed=True,unityVersion='6000.3.26f1',modelSha256=sha(model),modelSteps=json.loads(model.read_text())['steps'],sourceSha256={s:sha(ROOT/s) for s in source},buildAssemblySha256=sha(assembly),gameConfigSha256={str(p.relative_to(ROOT)):sha(p) for p in (ROOT/'Assets/StreamingAssets/Balance').glob('*.csv')},tests=results,scene=scene,native=r,frames=frames,windowInspection=dict(passed=a.window_inspected,blocker=None if a.window_inspected else 'Computer-use tool reports Mac locked. Manual unlock required; ordinary-window visual inspection has not been performed.'),nativeBatch=a.batch_input,scope='Frozen full-match tactical actor; ordinary authoritative inputs; native GPU callback/adapter fixture. Automated camera captures are distinct from ordinary-window visual inspection.',limitations=['No physical NU40-DK/external IMU, Windows or WAN validation.','Timing, movement, serving and skill execution remain deterministic adapters.','No performance improvement or human win-rate claim.','Only AC5 integration receipts; no repository delivery/merge claim.'])
report['artifactSha256']={n:sha(OUT/n) for n in ['runtime-scene.json','runtime-mac.json','runtime-events.txt','gameplay.png','runtime-results.png','runtime-rematch.png']}
(OUT/'runtime.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n')
print('Recorded verified automated runtime evidence; ordinary-window inspected:',a.window_inspected)
