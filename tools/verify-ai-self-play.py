"""Audit actual two-sided Unity trajectories, snapshot schedule and frozen evaluation."""
import gzip
import hashlib
import json
import math
from pathlib import Path
import xml.etree.ElementTree as ET

ROOT=Path(__file__).resolve().parents[1]
FOLDER=ROOT/'docs/evidence/ai-full-match'
def sha(path):return hashlib.sha256(path.read_bytes()).hexdigest()
def read(name):return json.loads((FOLDER/name).read_text())

def trajectory(report,name,cfg):
    path=FOLDER/report.get('rawFile',name)
    assert report['rawSha256']==sha(path)
    raw=gzip.decompress(path.read_bytes()) if path.suffix=='.gz' else path.read_bytes()
    if path.suffix=='.gz':assert hashlib.sha256(raw).hexdigest()==report['uncompressedRawSha256']
    previous=None;completed=[];resets=0;last_step=0;start=0;reward=0.;point_total=0
    for line in raw.decode().splitlines():
        e=json.loads(line);s=e['state'];step=e['step']
        assert last_step<=step;last_step=step
        assert (s['observationVersion'],s['actionVersion'],s['rewardVersion'])==(2,1,1)
        assert s['rules']==[3,6,6,7] and s['isolationChecks']==2
        for prefix in ('','far'):
            key=lambda x:prefix+x[0].upper()+x[1:] if prefix else x
            assert s[key('attempts')]==s[key('hits')]+s[key('rejected')]
            assert s[key('decisions')]==s[key('attempts')]
        for key in ('observation','farObservation'):
            assert len(s[key])==32 and all(math.isfinite(v) and abs(v)<=2 for v in s[key])
        if e['kind']=='reset':
            assert previous is None or previous['done'],'Nonterminal reset'
            assert s['nearPoints']+s['farPoints']==0 and s['attempts']+s['farAttempts']==0
            assert e['episode']==resets+1;start=step;reward=0.;resets+=1
            entry=report['schedule'][resets-1]
            assert entry['step']==step and entry['episode']==resets
            if entry['opponent']=='frozen':assert entry['opponentSha256'] in entry['availablePool']
        else:
            assert e['kind']=='transition' and e['episode']==resets and previous is not None and not previous['done']
            assert step>0
            dp=s['nearPoints']+s['farPoints']-previous['nearPoints']-previous['farPoints'];assert dp in (0,1);point_total+=dp
            assert s['physicsTicks']>=previous['physicsTicks'] and s['rally']>=previous['rally']
            assert s['attempts']>=previous['attempts'] and s['farAttempts']>=previous['farAttempts']
            entry=report['schedule'][resets-1];side=entry['learnerSide'];own='near' if side=='Near' else 'far';other='far' if side=='Near' else 'near'
            assert s['nearAction'] in range(-1,6) and s['farAction'] in range(-1,6)
            assert s[own+'Action'] in range(6)
            if entry['opponent']=='scripted':assert s[other+'Action']==-1
            else:assert s[other+'Action'] in range(6)
            def delta(a,b):return ((s[a]-previous[a])-(s[b]-previous[b]))*(1 if side=='Near' else -1)
            expected=cfg['reward_point']*delta('nearPoints','farPoints')+cfg['reward_game']*delta('nearGameWins','farGameWins')+cfg['reward_set']*delta('nearSets','farSets')
            reject_key='rejected' if side=='Near' else 'farRejected'
            expected+=cfg['reward_rejected']*(s[reject_key]-previous[reject_key])
            if s['done']:expected+=cfg['reward_match']*(1 if s['winner']==side else -1)
            assert abs(s['reward']-expected)<1e-6,'Reward must come from actual Unity score/gates'
            reward+=s['reward']
            if s['done']:
                assert s['winner'] in ('Near','Far') and max(s['nearSets'],s['farSets'])==2
                assert s['physicsTicks']>0 and s['endChanges']>0 and s['nearServes']>0 and s['farServes']>0
                assert s['hits']>0 and s['farHits']>0,'Both sides must really play'
                final={k:v for k,v in s.items() if k not in ('nearAction','farAction','reward')}
                match=report['completedMatches'][len(completed)]
                assert final==match['final'] and match['steps']==step-start and abs(match['reward']-reward)<1e-6
                for k in ('seed','near','far','learnerSide','opponent','opponentSha256'):assert match[k]==entry[k]
                completed.append(match)
        previous=s
    assert previous['done'] and len(completed)==report['matches']==resets==len(report['schedule'])
    return completed,last_step,point_total

def verify():
    sp=read('self-play.json');held=read('held-out.json');training=read('training.json')
    assert sp['passed'] and held['passed'] and training['passed']
    assert sp['baselineTrainingSha256']==sha(FOLDER/'training.json')
    assert sp['initialModelSha256']==sha(FOLDER/'full-match-ppo.json')==training['modelSha256']
    assert sp['initialWeightsSha256']==training['finalWeightsSha256']!=sp['finalWeightsSha256']
    assert sp['modelSha256']==sha(FOLDER/'self-play-ppo.json')==held['modelSha256']
    assert sp['steps']>=32768 and sp['rolloutUpdates']>=4 and sp['optimizerEpochs']>=4 and sp['matches']>=2
    assert sp['steps']==read('self-play-ppo.json')['steps']
    assert sp['configSha256']==sha(FOLDER/'self-play-config.csv')
    assert all(math.isfinite(v) for v in sp['finalLosses'].values()) and sp['finalLosses']
    pool={sp['initialModelSha256']}
    for snap in sp['snapshots']:
        assert snap['step']>0 and snap['step']%sp['config']['snapshot_interval']==0
        path=FOLDER/'self-play-pool'/snap['file'];assert sha(path)==snap['sha256']
        assert json.loads(path.read_text())['steps']==snap['step'];pool.add(snap['sha256'])
    matches,steps,points=trajectory(sp,'self-play-raw.jsonl',sp['ppoConfig'])
    assert steps==sp['trajectorySteps']==sp['steps']+sp['finishOnlySteps']
    cumulative=0;training_completed=0
    for match in matches:
        cumulative+=match['steps']
        if cumulative<=sp['steps']:training_completed+=1
    assert training_completed==sp['trainingCompletedMatches'] and training_completed>=2
    assert len({e['opponentSha256'] for e in matches})>=2
    for i,e in enumerate(sp['schedule']):
        assert e['learnerSide']==('Near' if i%2==0 else 'Far') and e['opponent']=='frozen'
        assert e['seed']==sp['config']['seed']+i+1 and e['near']==i%15 and e['far']==(i+2)%15
        available={sp['initialModelSha256']}|{s['sha256'] for s in sp['snapshots'] if s['step']<=e['step']}
        assert set(e['availablePool'])<=available<=pool
        assert e['opponentSha256']==e['availablePool'][i%len(e['availablePool'])]
    assert held['frozen'] and held['evaluationTrainingUpdates']==0 and held['weightsBefore']==held['weightsAfter']==sp['finalWeightsSha256']
    assert held['baselineModelSha256']==sp['initialModelSha256']
    eval_matches,_,eval_points=trajectory(held,'held-out-raw.jsonl',sp['ppoConfig'])
    assert len(eval_matches)==6
    assert not {e['seed'] for e in matches}&{e['seed'] for e in eval_matches}
    assert not {(e['near'],e['far']) for e in matches}&{(e['near'],e['far']) for e in eval_matches}
    for i,k in enumerate(('scripted','random','frozen')):
        cohort=[e for e in eval_matches if e['opponent']==k]
        assert len(cohort)==2 and {e['learnerSide'] for e in cohort}=={'Near','Far'}
        assert {e['seed'] for e in cohort}=={sp['config']['evaluation_seed']+i*101+j for j in (0,1)}
        comparison=next(e for e in held['comparisons'] if e['baseline']==k)
        assert comparison['matches']==2 and comparison['wins']==sum(e['final']['winner']==e['learnerSide'] for e in cohort)
    for report in (sp,held):
        for name,digest in report['sourceSha256'].items():assert sha(ROOT/name)==digest,name
        for name,digest in report['gameConfigSha256'].items():assert sha(ROOT/name)==digest,name
        for name,digest in report['evidenceSha256'].items():assert sha(FOLDER/name)==digest,name
        for name,digest in report['aiSourceSha256'].items():assert sha(ROOT/'.work/ai-full-match'/name)==digest,name
        assert report['unityTestsSha256']==sha(FOLDER/'self-play-tests.xml')
    tree=ET.parse(FOLDER/'self-play-tests.xml').getroot();assert tree.get('result')=='Passed' and tree.get('failed')=='0'
    regression=read('self-play-verification.json');assert regression['passed'] and regression['failedTests']==0
    assert regression['regressionTestsSha256']==sha(FOLDER/'self-play-regression-tests.xml')
    regtree=ET.parse(FOLDER/'self-play-regression-tests.xml').getroot();assert regtree.get('failed')=='0' and int(regtree.get('passed'))==regression['passedTests']
    assert any(x.get('fullname')==regression['isolationTest'] and x.get('result')=='Passed' for x in regtree.iter('test-case'))
    for name,digest in regression['sourceSha256'].items():assert sha(ROOT/name)==digest,name
    print(json.dumps(dict(passed=True,selfPlaySteps=sp['steps'],selfPlayMatches=len(matches),selfPlayPoints=points,heldOutMatches=len(eval_matches),heldOutPoints=eval_points,comparisons=held['comparisons'])))
if __name__=='__main__':verify()
