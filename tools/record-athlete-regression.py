#!/usr/bin/env python3
"""Bind completed real Unity test XMLs to current production source bytes."""
import hashlib,json,subprocess,xml.etree.ElementTree as E
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]

def digest(path):return hashlib.sha256(path.read_bytes()).hexdigest()

def record():
    reports=[]
    for name,minimum in [('test-edit.xml',169),('test-play.xml',37)]:
        p=ROOT/'.work'/name;r=E.parse(p).getroot()
        assert r.get('result')=='Passed' and int(r.get('total','0'))>=minimum
        assert int(r.get('failed','-1'))==0 and int(r.get('skipped','0'))==0
        reports.append({'path':str(p.relative_to(ROOT)),'sha256':digest(p),'start':r.get('start-time'),'end':r.get('end-time'),'total':int(r.get('total'))})
    sources=[]
    names=subprocess.check_output(['git','ls-files','-z','Assets','Packages','ProjectSettings'],cwd=ROOT).decode().split('\0')
    for name in filter(None,names):sources.append({'path':name,'sha256':digest(ROOT/name)})
    data={'sources':sources,'reports':reports,'output':'REGRESSION_OK','scope':'Real EditMode/PlayMode from final project; source bytes recorded after execution. No build/test simulation.'}
    (ROOT/'.work/regression-receipt.json').write_text(json.dumps(data,indent=2)+'\n')
    print('REGRESSION_OK',flush=True)

if __name__=='__main__':record()
