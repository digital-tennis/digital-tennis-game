"""Bundle compact actual training evidence without changing measured outcomes."""
from pathlib import Path
import hashlib
import json
import re
import shutil
import subprocess
ROOT=Path(__file__).resolve().parents[1]
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def main():
 import argparse
 p=argparse.ArgumentParser();p.add_argument('--ai',type=Path,required=True);a=p.parse_args();ai=a.ai.resolve()
 folder=ROOT/'docs/evidence/ai-full-match';path=folder/'training.json';r=json.loads(path.read_text())
 # These hashes were captured immediately after actual training, before packaging.
 for relative,digest in r['sourceSha256'].items():
  assert sha(ROOT/relative)==digest,relative
 ai_sources=dict(r.get('aiSourceSha256',{}))
 ai_sources.update({str((ROOT/relative).relative_to(ai)):digest for relative,digest in r['sourceSha256'].items() if (ROOT/relative).is_relative_to(ai)})
 r['sourceSha256']={relative:digest for relative,digest in r['sourceSha256'].items() if not (ROOT/relative).is_relative_to(ai)}
 ai_sources.update({str(f.relative_to(ai)):sha(f) for f in [ai/'Training/full-match.csv',ai/'Training/requirements-lock.txt',ai/'Runtime/AiPolicy.cs',ai/'Runtime/LearnedShotSelector.cs']})
 r['aiSourceSha256']=ai_sources
 for src,dst in [(ai/'Runtime/Resources/DigitalTennisAI/full-match-ppo.json','full-match-ppo.json'),(ai/'Runtime/Resources/DigitalTennisAI/full-match-parity.json','full-match-parity.json'),(ai/'Training/full-match.csv','training-config.csv')]:shutil.copy2(src,folder/dst)
 log=(ROOT/'.work/logs/test-ai-training.log').read_text();match=re.search(r'6000\.3\.26f1',log);assert match,'Actual Unity editor version absent'
 r['unityVersion']=match.group(0)
 r['frozenOpponent']={'model':'placement-ppo.json','sha256':sha(ai/'Runtime/Resources/DigitalTennisAI/placement-ppo.json'),'difficulty':'Medium','trainingEnabled':False}
 r['gameConfigSha256']={str(f.relative_to(ROOT)):sha(f) for f in (ROOT/'Assets/StreamingAssets/Balance').glob('*.csv')}
 r['trajectoryScope']='reset and every actual point, attempted shot and terminal transition; quiet idle steps omitted; total step counter is Gym/SB3 steps, not number of shots.'
 r['evidenceSha256']={name:sha(folder/name) for name in ['full-match-ppo.json','full-match-parity.json','training-config.csv','training-raw.jsonl','training-tests.xml']}
 r.setdefault('aiBaseCommit',subprocess.check_output(['git','-C',str(ai),'rev-parse','HEAD'],text=True).strip())
 dependency=json.loads((ROOT/'Packages/manifest.json').read_text())['dependencies']['com.digital-tennis.ai']
 if '#' in dependency:r['aiFeatureCommit']=dependency.rsplit('#',1)[1]
 r['inferenceScope']='Independent frozen C# actor exported and parity-tested. Existing product Far placement actor retained; training Near uses identical six-placement contract.'
 path.write_text(json.dumps(r,indent=2)+'\n');print('Bundled authoritative training evidence')
if __name__=='__main__':main()
