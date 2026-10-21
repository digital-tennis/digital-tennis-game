"""Attach actual regression-test evidence without rewriting measured outcomes."""
import hashlib
import json
from pathlib import Path
import shutil
import xml.etree.ElementTree as ET

ROOT=Path(__file__).resolve().parents[1]
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def main():
    folder=ROOT/'docs/evidence/ai-full-match';src=ROOT/'.work/test-play.xml';tree=ET.parse(src).getroot()
    assert tree.get('failed')=='0' and int(tree.get('passed'))>0
    name='DigitalTennis.Tests.FullMatchSelfPlayIsolationTests.ActiveBallPauseAndOnlineObservationLeaveBothPoliciesInactive'
    cases=[x for x in tree.iter('test-case') if x.get('fullname')==name]
    assert len(cases)==1 and cases[0].get('result')=='Passed','Active-ball isolation must actually pass'
    dest=folder/'self-play-regression-tests.xml';shutil.copy2(src,dest)
    sources=['Assets/Tests/PlayMode/FullMatchSelfPlayIsolationTests.cs','tools/verify-ai-self-play.py','tools/test_ai_self_play.py','tools/finalize-ai-self-play.py']
    result=dict(schema=1,passed=True,regressionTestsSha256=sha(dest),passedTests=int(tree.get('passed')),skippedTests=int(tree.get('skipped')),failedTests=0,isolationTest=name,
        sourceSha256={p:sha(ROOT/p) for p in sources})
    (folder/'self-play-verification.json').write_text(json.dumps(result,indent=2)+'\n')
    print(json.dumps(result,ensure_ascii=False))
if __name__=='__main__':main()
