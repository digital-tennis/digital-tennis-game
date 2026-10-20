"""Verify integration receipts; default requires the ordinary-window inspection too."""
import argparse,hashlib,json,subprocess,xml.etree.ElementTree as ET
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1];F=ROOT/'docs/evidence/ai-full-match'
p=argparse.ArgumentParser();p.add_argument('--automated-only',action='store_true');a=p.parse_args()
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
r=json.loads((F/'runtime.json').read_text());assert r['automatedPassed']
for field in ['sourceSha256','gameConfigSha256']:
 for path,h in r[field].items():
  if sha(ROOT/path)!=h:
   assert path in ('Packages/manifest.json','Packages/packages-lock.json'),path
   assert sha(F/('runtime-'+Path(path).name))==h,path
   # Publishing adds training/checkpoint documentation; inference bytes must match.
   current=json.loads((ROOT/'Packages/manifest.json').read_text())['dependencies']['com.digital-tennis.ai'].rsplit('#',1)[1]
   previous=json.loads((F/'runtime-manifest.json').read_text())['dependencies']['com.digital-tennis.ai'].rsplit('#',1)[1]
   for source in ('Runtime/FullMatchActor.cs','Runtime/AiPolicy.cs','Runtime/LearnedShotSelector.cs'):
    versions=[subprocess.check_output(['git','show',commit+':'+source],cwd=ROOT/'.work/ai-full-match') for commit in (previous,current)]
    assert versions[0]==versions[1],source
for path,h in r['artifactSha256'].items():assert sha(F/path)==h,path
assert sha(ROOT/'Assets/Resources/FullMatchAI/resumed-ppo.json')==r['modelSha256']==sha(F/'resume-ppo.json')
assert r['modelSteps']==145408
for name,v in r['tests'].items():
 path=F/('runtime-'+name+'.xml');assert sha(path)==v['sha256']
 xml=ET.parse(path).getroot();assert xml.get('failed')=='0' and int(xml.get('passed'))>0
scene=r['scene'];assert scene['passed'] and scene['pauseHeld'] and scene['onlineIsolated'] and scene['reset']
assert scene['points']==len(scene['trajectory']) and max(scene['nearSets'],scene['farSets'])==2
assert scene['attempts']==scene['farHits']+scene['rejected'] and scene['decisions']==scene['attempts']
for i,point in enumerate(scene['trajectory'],1):
 assert point['nearPoints']+point['farPoints']==i and point['nearEnd']!=point['farEnd']
n=r['native'];assert n['passed'] and n['runtimeErrors']==0 and n['pauseHeld'] and n['rematchReset']
assert n['completedGame']['score']['rules']==[3,6,6,7] and max(n['completedGame']['score']['sets'])==2
assert n['completedGame']['nearHits']>0 and n['completedGame']['farHits']>0
assert r['frames']['10-playing']['aiDecisions']>0 and r['frames']['10-playing']['aiHits']>0
assert r['frames']['13-rematch']['points']==0
if not a.automated_only:
 assert r['passed'] and r['windowInspection']['passed'] and not r['nativeBatch'],r['windowInspection']
print('PASS runtime automated model/scene/native GPU receipts'+('; ordinary-window inspection excluded' if a.automated_only else '; ordinary-window inspection included'))
