"""Independently audit full checkpoint state, optimizer continuity and Unity records."""
import gzip
import importlib.util
import io
import json
import math
from pathlib import Path
import pickle
import sys
import subprocess
import hashlib
import xml.etree.ElementTree as ET
from stable_baselines3 import PPO

ROOT=Path(__file__).resolve().parents[1];FOLDER=ROOT/'docs/evidence/ai-full-match'
sys.path.insert(0,str(ROOT/'.work/ai-full-match/Training'))
import checkpoint as cp
from full_match import weights_hash,sha


def verify():
    r=json.loads((FOLDER/'resume.json').read_text());assert r['passed']
    assert r['interruptedTrainerExited'] and r['interruptedProcessPid']!=r['resumedProcessPid']
    assert r['checkpointStep']==r['initialSelfPlayStep']+4096
    assert r['finalStep']-r['checkpointStep']==r['additionalSteps']==8192
    assert r['additionalRolloutUpdates']==4 and r['finalUpdates']-r['checkpointUpdates']==r['additionalOptimizerEpochs']>=2
    assert r['loaded']['globalStep']==r['checkpointStep'] and r['loaded']['updates']==r['checkpointUpdates']
    assert r['optimizerBefore']==r['loaded']['optimizerSha256']!=r['optimizerAfter']
    assert r['weightsBefore']==r['loaded']['weightsSha256']!=r['weightsAfter']
    assert r['rngRestored'] and r['sideRngBeforeSha256']==r['sideRngAfterLoadSha256']
    assert r['environmentBeforeSha256']==r['environmentAfterLoadSha256']
    assert r['normalization']['runningStatistics'] is None
    assert r['poolAfter'][:len(r['poolBefore'])]==r['poolBefore']==r['loaded']['pool']
    assert r['scheduleAfter'][:len(r['scheduleBefore'])]==r['scheduleBefore']
    for name,digest in r['contract']['hashes'].items():
        base=ROOT/'.work/ai-full-match' if name.startswith(('Training/','Runtime/')) else ROOT
        if sha(base/name)!=digest:
            # Runtime integration follows this immutable training receipt.
            assert base==ROOT,name
            archive={'tools/unity.sh':'resume-unity.sh','Packages/manifest.json':'runtime-manifest.json'}.get(name)
            original=(FOLDER/archive).read_bytes() if archive else subprocess.check_output(['git','show','8ae5cd8fa203b1b1c6583ec8904150814dd2c9a3:'+name],cwd=ROOT)
            assert hashlib.sha256(original).hexdigest()==digest,name
    assert sha(FOLDER/'resume-ppo.json')==r['modelSha256']
    assert json.loads((FOLDER/'resume-ppo.json').read_text())['steps']==r['finalStep']
    assert sha(FOLDER/'resume-tests.xml')==r['unityTestsSha256']
    tree=ET.parse(FOLDER/'resume-tests.xml').getroot();assert tree.get('result')=='Passed' and tree.get('failed')=='0'
    assert any(c.get('fullname')=='DigitalTennis.Tests.FullMatchResumeTests.AuthoritativeResumeBridge' and c.get('result')=='Passed' for c in tree.iter('test-case'))
    summaries=[];checkpoint=ROOT/'.work/ai-full-match/.work/complete-checkpoints'
    pointer=json.loads((checkpoint/'current.json').read_text())
    assert pointer['current']==r['finalGeneration'] and pointer['previous']==r['checkpointGeneration']
    for previous,step,updates,opt,weights,pool in [
        (True,r['checkpointStep'],r['checkpointUpdates'],r['optimizerBefore'],r['weightsBefore'],r['poolBefore']),
        (False,r['finalStep'],r['finalUpdates'],r['optimizerAfter'],r['weightsAfter'],r['poolAfter'])]:
        manifest,data=cp.read(checkpoint,r['contract'],previous=previous)
        s=pickle.loads(data['state.pkl'])['state'];model=PPO.load(io.BytesIO(data['model.zip']),device='cpu')
        assert s['globalStep']==model.num_timesteps==step and s['updates']==model._n_updates==updates
        assert cp.optimizer_hash(model)==s['optimizerSha256']==opt and weights_hash(model)==weights
        assert [cp.digest(data[f'pool/{i}.json']) for i in range(manifest['poolCount'])]==pool
        assert s['environment']['schedule']==(r['scheduleBefore'] if previous else r['scheduleAfter'])
        assert s['normalization']==r['normalization'] and s['snapshots']==r['snapshots'][:len(s['snapshots'])]
        assert not s['monitor']['needs_reset'] and s['vector']['buf_obs'][None].shape==(1,32)
        adam_steps=sorted({int(v['step'].item()) for v in model.policy.optimizer.state_dict()['state'].values()})
        assert adam_steps and min(adam_steps)>0
        summaries.append(dict(globalStep=step,updates=updates,optimizerSha256=opt,adamSteps=adam_steps,
            manifest=manifest,environment=s['environment'],sideRngSha256=cp.fingerprint(s['sideRng']),
            monitorTotalSteps=s['monitor']['total_steps'],snapshotCount=len(s['snapshots'])))
    assert min(summaries[1]['adamSteps'])>max(summaries[0]['adamSteps'])
    assert summaries[0]['environment']['last']['done'] is False
    assert summaries[1]['monitorTotalSteps']-summaries[0]['monitorTotalSteps']==8192
    assert cp.fingerprint(summaries[0]['environment']['last'])==r['environmentBeforeSha256']
    assert summaries[1]['environment']['last']==r['partialFinalState']
    raw=FOLDER/r['rawFile'];assert sha(raw)==r['rawSha256']
    cfg=r['contract']['config'];prior=None;points=0;transitions=0;resets=0
    for line in gzip.decompress(raw.read_bytes()).decode().splitlines():
        e=json.loads(line);s=e['state'];entry=r['scheduleAfter'][e['episode']-1]
        assert s['rules']==[3,6,6,7] and s['isolationChecks']==2
        assert [s[k] for k in ('observationVersion','actionVersion','rewardVersion')]==[2,1,1]
        for key in ('observation','farObservation'):assert len(s[key])==32 and all(math.isfinite(v) and abs(v)<=2 for v in s[key])
        assert s['attempts']==s['hits']+s['rejected'] and s['farAttempts']==s['farHits']+s['farRejected']
        if e['kind']=='reset':
            assert prior is None or prior['done'];resets+=1
            assert e['step']==entry['step'] and s['nearPoints']+s['farPoints']==0
        else:
            assert prior is not None and not prior['done'] and e['kind']=='transition';transitions+=1
            dp=s['nearPoints']+s['farPoints']-prior['nearPoints']-prior['farPoints'];assert dp in (0,1);points+=dp
            assert s['physicsTicks']>=prior['physicsTicks']
            for k in ('attempts','farAttempts','rejected','farRejected'):assert s[k]>=prior[k]
            side=entry['learnerSide'];sign=1 if side=='Near' else -1
            def delta(a,b):return ((s[a]-prior[a])-(s[b]-prior[b]))*sign
            expected=cfg['reward_point']*delta('nearPoints','farPoints')+cfg['reward_game']*delta('nearGameWins','farGameWins')+cfg['reward_set']*delta('nearSets','farSets')
            reject='rejected' if side=='Near' else 'farRejected';expected+=cfg['reward_rejected']*(s[reject]-prior[reject])
            assert s['nearAction'] in range(6) and s['farAction'] in range(6)
            if s['done']:
                assert max(s['nearSets'],s['farSets'])==2
                expected+=cfg['reward_match']*(1 if s['winner']==side else -1)
            assert abs(expected-s['reward'])<1e-6
        prior=s
    assert transitions>0 and points>0 and resets>=1
    # This run cannot hide a reset at the process boundary.
    assert summaries[0]['environment']['episode']==summaries[1]['environment']['episode']
    assert resets==1 and len(r['scheduleAfter'])==len(r['scheduleBefore'])
    (FOLDER/'resume-checkpoints.json').write_text(json.dumps(dict(schema=1,passed=True,checkpoints=summaries,actualUnityTransitions=transitions,actualUnityPoints=points,actualUnityResets=resets),indent=2)+'\n')
    print(json.dumps(dict(passed=True,checkpointStep=r['checkpointStep'],finalStep=r['finalStep'],additionalSteps=8192,rolloutUpdates=4,optimizerEpochs=r['additionalOptimizerEpochs'],actualUnityPoints=points,processRestart=True)))

if __name__=='__main__':verify()
