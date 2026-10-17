#!/usr/bin/env python3
"""Pair original and final real gameplay frames without resizing or retouching."""
import hashlib,json
from pathlib import Path
from PIL import Image,ImageDraw,ImageFont
ROOT=Path(__file__).resolve().parents[1]
OUT=ROOT/'.work/baseline-motion'
BASE='69bdf9ca797fb8396a7b25bc040f0e75bcf8e5e0'


def digest(p):return hashlib.sha256(p.read_bytes()).hexdigest()


def generate():
    font=ImageFont.truetype('/System/Library/Fonts/Supplemental/Arial.ttf',24)
    receipt=json.loads((OUT/'source-receipt.json').read_text())
    assert receipt['baselineRevision']==BASE
    records=[]
    for athlete in ('KOR','JPN'):
        for action in ('Running','Serve','Forehand','Backhand'):
            name=f'{athlete}-{action}'
            sheet=Image.new('RGB',(2400,3760),(16,24,32));draw=ImageDraw.Draw(sheet)
            for column,(phase,folder) in enumerate([('BEFORE',OUT/'motion'),('AFTER',ROOT/'.work/ac2-motion/motion')]):
                timeline_path=folder/f'{name}-timeline.json'
                samples=json.loads(timeline_path.read_text())['samples']
                assert len(samples)==16
                records.append({'path':str(timeline_path.relative_to(ROOT)),'sha256':digest(timeline_path)})
                for sample in samples:
                    p=folder/f'{name}-{sample["frame"]:02}.png'
                    records.append({'path':str(p.relative_to(ROOT)),'sha256':digest(p)})
                for row,frame in enumerate((0,4,8,15)):
                    s=samples[frame];p=folder/f'{name}-{frame:02}.png'
                    with Image.open(p) as image:
                        assert image.size==(1200,900)
                        sheet.paste(image.convert('RGB'),(column*1200,row*940+40))
                    label=f'{name} {phase} frame {frame} | age {s["swingAge"]:.3f}s'
                    if action!='Running' and not s['swinging'] and s['swingAge']>=.349:label+=' | RECOVERY'
                    draw.text((column*1200+10,row*940+5),label,font=font,fill='white')
            p=OUT/f'comparison-{name}.png';sheet.save(p)
            records.append({'path':str(p.relative_to(ROOT)),'sha256':digest(p)})
    for p in [OUT/'source-receipt.json',OUT/'motion/motion.json']:
        records.append({'path':str(p.relative_to(ROOT)),'sha256':digest(p)})
    data={'baselineRevision':BASE,'camera':{'focusUp':.4,'forward':4,'up':.6,'right':1.5,'fov':48,'size':[1200,900]},
          'scope':'Matched camera and event-relative samples from independent real adapter-driven games; not identical ball trajectories or full serve toss. No source pixels resized/retouched.', 'files':records}
    (OUT/'comparison-manifest.json').write_text(json.dumps(data,indent=2)+'\n')
    print('BASELINE_MOTION_COMPARISONS_READY',len(records),flush=True)

if __name__=='__main__':generate()
