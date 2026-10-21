"""Launch only the opt-in Unity training test and the AI repository's PPO trainer."""
from pathlib import Path
import argparse
import hashlib
import json
import subprocess
import sys

ROOT=Path(__file__).resolve().parents[1]
def main():
 p=argparse.ArgumentParser();p.add_argument('--python',required=True);p.add_argument('--ai',type=Path,required=True);p.add_argument('--collect-existing',action='store_true');a=p.parse_args();a.ai=a.ai.resolve()
 baseline=json.loads((ROOT/'docs/evidence/ai-full-match/baseline.json').read_text())
 if not baseline.get('passed') or baseline.get('phase')!='pre-training':raise SystemExit('Accepted expanded baseline required before training')
 evidence=ROOT/'docs/evidence/ai-full-match';evidence.mkdir(parents=True,exist_ok=True)
 out=a.ai/'Runtime/Resources/DigitalTennisAI/full-match-ppo.json'
 if not a.collect_existing:
  (ROOT/".work/test-ai-training.xml").unlink(missing_ok=True)
  with (ROOT/'.work/logs/training-launch.log').open('w') as log:
   unity=subprocess.Popen(['bash','tools/unity.sh','test-ai-training'],cwd=ROOT,stdout=log,stderr=subprocess.STDOUT)
   try:
    subprocess.run([a.python,str(a.ai/'Training/full_match.py'),'--out',str(out),'--report',str(evidence/'training.json')],check=True,cwd=ROOT)
    if unity.wait(timeout=120)!=0:raise RuntimeError('Unity test failed; inspect test-ai-training.log')
   finally:
    if unity.poll() is None:unity.terminate();unity.wait(timeout=30)
 import xml.etree.ElementTree as ET
 tree=ET.parse(ROOT/'.work/test-ai-training.xml').getroot()
 if tree.get('result')!='Passed':raise RuntimeError('Unity bridge test did not pass')
 import shutil
 shutil.copy2(ROOT/'.work/test-ai-training.xml',evidence/'training-tests.xml')
 report_path=evidence/'training.json';r=json.loads(report_path.read_text())
 files=list((ROOT/'Assets/Scripts/Runtime').glob('*.cs'))+[ROOT/'Assets/Tests/PlayMode/FullMatchTrainingTests.cs',a.ai/'Training/full_match.py',a.ai/'Runtime/FullMatchActor.cs']
 r['sourceSha256']={str(f.relative_to(ROOT)):hashlib.sha256(f.read_bytes()).hexdigest() for f in files}
 r['baselineSha256']=hashlib.sha256((evidence/'baseline.json').read_bytes()).hexdigest()
 r['unityTestsSha256']=hashlib.sha256((evidence/'training-tests.xml').read_bytes()).hexdigest()
 report_path.write_text(json.dumps(r,indent=2)+'\n')
 print(json.dumps({k:r[k] for k in ['passed','steps','matches','games','sets','rolloutUpdates']}))
if __name__=='__main__':main()
