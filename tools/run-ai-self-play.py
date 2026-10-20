"""Execute the opt-in Unity bridge and independent AI snapshot-pool trainer."""
import argparse
import hashlib
import json
from pathlib import Path
import shutil
import subprocess
import xml.etree.ElementTree as ET

ROOT=Path(__file__).resolve().parents[1]
def sha(path):return hashlib.sha256(path.read_bytes()).hexdigest()
def main():
    p=argparse.ArgumentParser();p.add_argument('--python',required=True);p.add_argument('--ai',type=Path,required=True);a=p.parse_args();ai=a.ai.resolve()
    folder=ROOT/'docs/evidence/ai-full-match'
    for name in ('baseline.json','training.json'):
        assert json.loads((folder/name).read_text())['passed'],name
    # Source hash before execution, not retroactively inferred after training.
    sources=[ROOT/'Assets/Tests/PlayMode/FullMatchSelfPlayTests.cs',ROOT/'Assets/Scripts/Runtime/FullMatchObservation.cs',ROOT/'tools/run-ai-self-play.py']
    source_hashes={str(f.relative_to(ROOT)):sha(f) for f in sources}
    ai_hashes={str(f.relative_to(ai)):sha(f) for f in [ai/'Training/self_play.py',ai/'Training/self-play.csv',ai/'Training/full_match.py',ai/'Training/full-match.csv',ai/'Training/requirements-lock.txt']}
    xml=ROOT/'.work/test-ai-self-play.xml';xml.unlink(missing_ok=True)
    with (ROOT/'.work/logs/self-play-launch.log').open('w') as log:
        unity=subprocess.Popen(['bash','tools/unity.sh','test-ai-self-play'],cwd=ROOT,stdout=log,stderr=subprocess.STDOUT)
        try:
            subprocess.run([a.python,str(ai/'Training/self_play.py'),'--root',str(ai),'--output',str(folder)],cwd=ROOT,check=True)
            if unity.wait(timeout=120)!=0:raise RuntimeError('Unity self-play test failed')
        finally:
            if unity.poll() is None:unity.terminate();unity.wait(timeout=30)
    assert ET.parse(xml).getroot().get('result')=='Passed'
    shutil.copy2(xml,folder/'self-play-tests.xml')
    shutil.copy2(ai/'Training/self-play.csv',folder/'self-play-config.csv')
    # Retain compact reproducible source alongside AI feature checkout until delivery.
    for name in ('self_play.py','self-play.csv'):
        shutil.copy2(ai/'Training'/name,folder/name)
    configs={str(f.relative_to(ROOT)):sha(f) for f in (ROOT/'Assets/StreamingAssets/Balance').glob('*.csv')}
    for name in ('self-play.json','held-out.json'):
        path=folder/name;r=json.loads(path.read_text());r.update(sourceSha256=source_hashes,aiSourceSha256=ai_hashes,gameConfigSha256=configs,unityTestsSha256=sha(folder/'self-play-tests.xml'),unityVersion='6000.3.26f1')
        r['evidenceSha256']={n:sha(folder/n) for n in ('self_play.py','self-play.csv','self-play-config.csv')}
        path.write_text(json.dumps(r,indent=2)+'\n')
    print('Actual Unity self-play and frozen held-out evaluation passed')
if __name__=='__main__':main()
