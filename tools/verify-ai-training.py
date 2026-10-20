"""Verify recorded genuine Unity training, source/model hashes and full-match counters."""
import hashlib
import json
import subprocess
from pathlib import Path
import xml.etree.ElementTree as ET
ROOT=Path(__file__).resolve().parents[1]
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def verify():
 folder=ROOT/'docs/evidence/ai-full-match';r=json.loads((folder/'training.json').read_text())
 assert r['passed'] and r['steps']>=65536 and r['rolloutUpdates']>=4 and r['matches']>=2
 assert r['initialWeightsSha256']!=r['finalWeightsSha256']
 assert r['rawSha256']==sha(folder/'training-raw.jsonl')
 assert r['baselineSha256']==sha(folder/'baseline.json')
 assert r['unityTestsSha256']==sha(folder/'training-tests.xml')
 assert ET.parse(folder/'training-tests.xml').getroot().get('result')=='Passed'
 points=games=sets=completed=0;previous=None;episode=0;resets=0;last_step=-1;episode_start=0;episode_reward=0.;last_point_rally=0
 for line in (folder/'training-raw.jsonl').read_text().splitlines():
  e=json.loads(line);s=e['state']
  assert last_step<=e['step']<=r['steps'];last_step=e['step']
  assert s['rules']==[3,6,6,7] and (s['observationVersion'],s['actionVersion'],s['rewardVersion'])==(2,1,1)
  assert s['attempts']==s['hits']+s['rejected'] and s['decisions']==s['attempts']
  if e['kind']=='reset':
   assert previous is None or previous['done'];previous=s;episode=e['episode'];resets+=1
   assert s['nearPoints']+s['farPoints']==0
   episode_start=e['step'];episode_reward=0.;last_point_rally=0
   continue
  assert e['episode']==episode and previous is not None
  dp=s['nearPoints']+s['farPoints']-previous['nearPoints']-previous['farPoints'];assert dp in (0,1);points+=dp
  episode_reward+=s['reward']
  if dp:assert s['rally']>last_point_rally;last_point_rally=s['rally']
  assert s['physicsTicks']>=previous['physicsTicks'] and s['rally']>=previous['rally']
  if s['done']:
   assert s['winner'] in ('Near','Far') and max(s['nearSets'],s['farSets'])==2 and s['nearServes']>0 and s['farServes']>0 and s['physicsTicks']>0 and s['endChanges']>0
   measured=r['completedMatches'][completed]
   assert measured['final']=={k:v for k,v in s.items() if k not in ('action','reward')}
   assert measured['steps']==e['step']-episode_start and abs(measured['reward']-episode_reward)<1e-6
   assert measured['seed']==r['config']['seed']+episode and measured['near']==(episode-1)%15 and measured['far']==(episode+1)%15
   completed+=1;games+=s['nearGameWins']+s['farGameWins'];sets+=s['nearSets']+s['farSets']
  previous=s
 assert completed==r['matches']==len(r['completedMatches']) and games==r['games'] and sets==r['sets']
 assert resets in (completed,completed+1)
 import math
 assert r['finalLosses'] and all(math.isfinite(v) for v in r['finalLosses'].values())
 # Training predates normal-game integration. Verify changed files against the exact
 # immutable pre-integration Git object, never rewrite historical training hashes.
 for relative,digest in r['sourceSha256'].items():
  if sha(ROOT/relative)!=digest:
   original=subprocess.check_output(['git','show','8ae5cd8fa203b1b1c6583ec8904150814dd2c9a3:'+relative],cwd=ROOT)
   assert hashlib.sha256(original).hexdigest()==digest,relative
 for relative,digest in r['gameConfigSha256'].items():assert sha(ROOT/relative)==digest,relative
 config_path=folder/'training-config.csv';model=folder/'full-match-ppo.json'
 for name,digest in r['evidenceSha256'].items():assert sha(folder/name)==digest,name
 assert r['configSha256']==sha(config_path) and r['modelSha256']==sha(model)
 manifest=json.loads((ROOT/'Packages/manifest.json').read_text())
 published=manifest['dependencies']['com.digital-tennis.ai'].rsplit('#',1)[1]
 subprocess.run(['git','merge-base','--is-ancestor',r['aiFeatureCommit'],published],cwd=ROOT/'.work/ai-full-match',check=True)
 # The package may advance for self-play/checkpoint docs; the original actor and
 # training sources below must still match the historical execution hashes.
 packages=list((ROOT/'Library/PackageCache').glob('com.digital-tennis.ai@*'))
 matching=[p for p in packages if (p/'Runtime/Resources/DigitalTennisAI/full-match-ppo.json').exists() and sha(p/'Runtime/Resources/DigitalTennisAI/full-match-ppo.json')==r['modelSha256']]
 assert matching,'Published compact actor must resolve in Unity package cache'
 for name,digest in r['aiSourceSha256'].items():assert sha(matching[0]/name)==digest,name
 print(json.dumps(dict(passed=True,steps=r['steps'],matches=completed,games=games,sets=sets,points=points)))
if __name__=='__main__':verify()
