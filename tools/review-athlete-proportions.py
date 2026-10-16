#!/usr/bin/env python3
"""Label matched Unity captures without altering, cropping or resizing their pixels."""
import argparse
import subprocess
import hashlib
import json
from pathlib import Path
from PIL import Image, ImageDraw

ROOT=Path(__file__).resolve().parents[1]
FOLDER=ROOT/'.work/proportion-review'
parser=argparse.ArgumentParser()
parser.add_argument('--prepare-baseline',metavar='GIT_REVISION')
args=parser.parse_args()
if args.prepare_baseline:
    originals=FOLDER/'originals';originals.mkdir(parents=True,exist_ok=True)
    for athlete in ['KOR','JPN']:
        data=subprocess.check_output(['git','show',args.prepare_baseline+':Assets/Resources/Athletes/'+athlete+'.asset'],cwd=ROOT)
        (originals/(athlete+'.asset')).write_bytes(data)
    print('Retained baseline meshes in .work/proportion-review/originals from '+args.prepare_baseline)
    raise SystemExit(0)
records=[]
for athlete in ['KOR','JPN']:
    for view in ['front','side','face']:
        sheet=Image.new('RGB',(1800,1160),(19,24,33));draw=ImageDraw.Draw(sheet)
        for x,phase in [(0,'before'),(900,'after')]:
            path=FOLDER/f'{phase}-{athlete}-{view}.png'
            source=Image.open(path).convert('RGB')
            if source.size!=(900,1100):raise ValueError(f'Unexpected image: {path}')
            sheet.paste(source,(x,60))
            draw.text((x+25,15),f'{athlete} {view.upper()} | {phase.upper()} | staged Unity art preview',fill='white',font_size=22)
            records.append(dict(path=str(path.relative_to(ROOT)),sha256=hashlib.sha256(path.read_bytes()).hexdigest()))
        sheet.save(FOLDER/f'comparison-{athlete}-{view}.png')
(FOLDER/'review.json').write_text(json.dumps(dict(stagedArtPreview=True,sourceRevision='69bdf9ca797fb8396a7b25bc040f0e75bcf8e5e0',method='Original 900x1100 camera pixels pasted unchanged below labels; identical orthographic cameras and lighting.',captures=records),indent=2)+'\n')
print('6 labeled comparisons and hashes of 12 unretouched Unity PNGs: .work/proportion-review')
