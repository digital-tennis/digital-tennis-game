#!/usr/bin/env python3
"""Copy exact game-camera PNGs and build labeled thumbnail sheets, never OS screenshots."""
import argparse,html,json,shutil
from pathlib import Path
from PIL import Image,ImageDraw
p=argparse.ArgumentParser();p.add_argument('--roster',type=Path,required=True);p.add_argument('--stadium',type=Path,required=True);p.add_argument('--out',type=Path,required=True);a=p.parse_args();a.out.mkdir(parents=True,exist_ok=True);art=a.out/'art';art.mkdir(exist_ok=True)
ids=['KOR','JPN','CHN','USA','RUS','IND','DEU','GBR','ESP','FRA','AUS','NLD','BRA','MAR','ZAF']
for source in [a.roster,a.stadium]:
 for file in source.glob('*.png'):shutil.copyfile(file,art/file.name)
def sheet(suffix,name):
 result=Image.new('RGB',(1600,690),(15,26,37));d=ImageDraw.Draw(result)
 for i,id in enumerate(ids):
  image=Image.open(art/(id+suffix+'.png')).convert('RGB');image.thumbnail((316,200));x=(i%5)*320;y=(i//5)*230;result.paste(image,(x+(316-image.width)//2,y));d.text((x+8,y+205),id,fill=(233,241,246))
 result.save(a.out/name)
sheet('-face','roster-faces.png');sheet('-phase-50','roster-skills.png');sheet('-venue','court-venues.png')
parts=['<!doctype html><meta charset="utf-8"><title>Digital Tennis review</title><style>body{background:#0e1c28;color:#e9f2f4;font:17px system-ui;margin:32px}a{color:#a9dc72}img{max-width:100%;height:auto}.grid{display:grid;grid-template-columns:repeat(auto-fit,minmax(300px,1fr));gap:18px}article{background:#203240;padding:12px;border-radius:8px}audio{width:100%}</style><h1>Digital Tennis · game quality review</h1><p>Actual Unity game-camera PNGs. Skill gallery uses explicitly staged stock; motion/online tests separately verify real charging and play. English synthesized voices are draft candidates. No OS screenshots or microphone/system audio.</p>']
parts+=['<h2>Live opening camera edit</h2><p>12.5-second game-camera preview, silent export. In-game menu music and cut SFX remain available; live tennis is muted.</p><video controls style="max-width:100%" src="opening-camera-preview.mp4"></video>']
parts+=['<h2>15 faces / skills / venues</h2>']+['<a href="'+file+'"><img src="'+file+'"></a>' for file in ['roster-faces.png','roster-skills.png','court-venues.png']]
parts+=['<h2>Audio candidates</h2><p>Menu → game → result → deuce → pressure; A then B for each. A is provisional for menus/results. Game/deuce/pressure cues are audition-only; active play has no BGM.</p><audio controls src="audio/music-ab.wav"></audio><p>KOR/JPN ready, skill, win, lose: base then alternate.</p><audio controls src="audio/voice-kor-jpn.wav"></audio><h2>Full game-camera captures</h2><div class="grid">']
for id in ids:
 parts+=['<article><h3>'+id+'</h3>']
 for suffix,label in [('-face','Face'),('-phase-18','Skill preparation'),('-phase-50','Skill accent'),('-phase-80','Skill recovery'),('-venue','Venue intro'),('-near','Near court'),('-far','Far court')]:
  file=id+suffix+'.png';parts+=['<a href="art/'+file+'">'+label+'<img loading="lazy" src="art/'+file+'"></a>']
 parts+=['</article>']
parts+=['</div>'];(a.out/'review.html').write_text('\n'.join(parts))
(a.out/'review-manifest.json').write_text(json.dumps({'mode':'staged art review from real Unity camera; separate ordinary-input motion/online evidence','characters':15,'venues':15,'cameraPngs':len(list(art.glob('*.png'))),'audioSelection':'A provisional'},indent=2)+'\n')
print('Prepared '+str(a.out/'review.html'))
