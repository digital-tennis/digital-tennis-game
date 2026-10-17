#!/usr/bin/env python3
"""Read live GitHub state; retain a PR_READY receipt after commit and push."""
import json,subprocess
from datetime import datetime,timezone
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]

def command(*args):return subprocess.check_output(args,cwd=ROOT,text=True,timeout=60).strip()

def record():
    head=command('git','rev-parse','HEAD');branch=command('git','branch','--show-current')
    assert not command('git','status','--porcelain')
    pr=json.loads(command('gh','pr','view',branch,'--repo','digital-tennis/digital-tennis-game','--json','url,state,isDraft,headRefOid,headRefName,baseRefName'))
    assert pr['state']=='OPEN' and pr['headRefOid']==head and pr['isDraft'] and pr['baseRefName']=='main'
    remote=command('git','ls-remote','--exit-code','origin',f'refs/heads/{branch}').split()[0]
    upstream=command('git','rev-parse','@{upstream}')
    assert remote==upstream==head
    receipt={'observedAt':datetime.now(timezone.utc).isoformat(),'head':head,'branch':branch,'remote':remote,'upstream':upstream,'worktreeClean':True,'pr':pr,'output':'PR_READY','scope':'Live authenticated gh API and git remote query, no PR mutations.'}
    (ROOT/'.work/delivery-receipt.json').write_text(json.dumps(receipt,indent=2)+'\n')
    print('PR_READY\n'+pr['url'],flush=True)

if __name__=='__main__':record()
