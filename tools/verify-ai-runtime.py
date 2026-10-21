"""Verify integration receipts; default requires the ordinary-window inspection too."""
import argparse,hashlib,json,subprocess,sys,xml.etree.ElementTree as ET
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1];F=ROOT/'docs/evidence/ai-full-match'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def completion(report):
    ordinary = bool(report['windowInspection']['passed']) and not report['nativeBatch']
    complete = bool(report['automatedPassed']) and ordinary
    assert report['passed'] == complete, 'Inconsistent runtime completion flags'
    return complete


def verify(automated_only=False):
    r=json.loads((F/'runtime.json').read_text());assert r['automatedPassed']
    for field in ['sourceSha256','gameConfigSha256']:
     for path,h in r[field].items():
      if sha(ROOT/path)!=h:
       assert path in ('Packages/manifest.json','Packages/packages-lock.json'),path
       assert sha(F/('runtime-'+Path(path).name))==h,path
       # Publishing adds training/checkpoint documentation; inference bytes must match.
       current=json.loads((ROOT/'Packages/manifest.json').read_text())['dependencies']['com.digital-tennis.ai'].rsplit('#',1)[1]
       measured=json.loads((F/'training.json').read_text())['aiSourceSha256']
       for source in ('Runtime/FullMatchActor.cs','Runtime/AiPolicy.cs','Runtime/LearnedShotSelector.cs'):
        # The old feature object may be garbage-collected after branch deletion.
        # Its measured digest is immutable; the currently pinned blob must match.
        blob=subprocess.check_output(['git','show',current+':'+source],cwd=ROOT/'.work/ai-full-match')
        assert hashlib.sha256(blob).hexdigest()==measured[source],source
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
    complete = completion(r)
    if not automated_only and not complete:
        print(json.dumps({'status':'blocked','goalComplete':False,
                          'blocker':r['windowInspection'].get('blocker','Ordinary-window inspection required')}))
        return 3
    print(json.dumps({'automatedPassed':True,'ordinaryWindowPassed':complete,
                      'scope':'automated-only' if automated_only else 'complete-runtime'}))
    return 0


if __name__ == '__main__':
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--automated-only',action='store_true')
    sys.exit(verify(parser.parse_args().automated_only))
