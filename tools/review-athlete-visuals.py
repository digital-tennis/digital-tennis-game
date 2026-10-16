#!/usr/bin/env python3
"""Validate/composite actual game-camera captures. Does not generate or retouch athletes."""
from pathlib import Path
import argparse,hashlib,json,subprocess
import numpy as np
from PIL import Image,ImageDraw
ROOT=Path(__file__).resolve().parents[1]
folder=ROOT/'.work/athlete-visuals'
ids='KOR JPN CHN USA RUS IND DEU GBR ESP FRA AUS NLD BRA MAR ZAF'.split()
parser=argparse.ArgumentParser();parser.add_argument('--prepare-originals',metavar='GIT_REF');args=parser.parse_args()
if args.prepare_originals:
    commit=subprocess.check_output(['git','rev-parse',args.prepare_originals],cwd=ROOT,text=True).strip()
    originals=folder/'original-assets';originals.mkdir(parents=True,exist_ok=True);rows=[]
    for ident in ids:
        source='Assets/Resources/Athletes/'+ident+'.asset'
        data=subprocess.check_output(['git','show',commit+':'+source],cwd=ROOT)
        (originals/(ident+'.asset')).write_bytes(data)
        rows.append({'path':source,'sha256':hashlib.sha256(data).hexdigest()})
    (folder/'baseline.json').write_text(json.dumps({'commit':commit,'originals':rows},indent=2)+'\n')
    print('Prepared 15 original Git meshes for native Before capture: '+commit)
    raise SystemExit(0)

report={'capture':'Unity 6000.3.26f1 native Mac Metal / Apple M2 Max, Camera.Render','views':[],'baseline':json.loads((folder/'baseline.json').read_text())}
for view in ['face','body']:
    sheet=Image.new('RGB',(5*320,3*220),(20,24,32));draw=ImageDraw.Draw(sheet)
    for i,ident in enumerate(ids):
        images=[];records=[]
        for phase in ['before','after']:
            p=folder/f'{phase}-{ident}-{view}.png';im=Image.open(p).convert('RGB');a=np.array(im)
            assert im.size==(800,1000),p
            background=a[0,0];visible=np.any(np.abs(a.astype(int)-background)>10,axis=2).sum()
            assert visible>60000,f'Blank/incomplete capture: {p}'
            records.append({'phase':phase,'path':str(p.relative_to(ROOT)),'sha256':hashlib.sha256(p.read_bytes()).hexdigest(),'visiblePixels':int(visible)})
            images.append(a);thumb=im.resize((152,190));x=(i%5)*320+(0 if phase=='before' else 160);y=(i//5)*220+24;sheet.paste(thumb,(x,y))
        changed=int(np.any(abs(images[0].astype(int)-images[1].astype(int))>10,axis=2).sum())
        assert changed>5000,f'No visible geometry change: {ident} {view}'
        report['views'].append({'athlete':ident,'view':view,'changedPixels':changed,'captures':records})
        draw.text(((i%5)*320+4,(i//5)*220+5),ident+'   BEFORE                  AFTER',fill='white')
    sheet.save(folder/f'comparison-{view}.png')
(folder/'review.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n')
print('Verified 60 nonblank native captures; 30 matched before/after comparisons; generated face/body contact sheets.')
