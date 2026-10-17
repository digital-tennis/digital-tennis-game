#!/usr/bin/env python3
"""Label actual native camera frames; never repaint game pixels or autoplay sound."""
import argparse,hashlib,json,re,time
from pathlib import Path
from PIL import Image,ImageDraw,ImageFont
ROOT=Path(__file__).resolve().parents[1];out=ROOT/'.work/ac2-motion';folder=out/'motion'
parser=argparse.ArgumentParser();parser.add_argument('--receipt',action='store_true');args=parser.parse_args()
if args.receipt:
 dll=ROOT/'Builds/verification/mac/DigitalTennis.app/Contents/Resources/Data/Managed/DigitalTennis.Runtime.dll'
 paths=[*ROOT.glob('Assets/Scripts/Runtime/Athlete*Motion.cs'),ROOT/'Assets/Scripts/Runtime/AcquiredAthleteMotion.cs',ROOT/'Assets/Scripts/Runtime/AthleteSkin.cs',ROOT/'Assets/Resources/Characters/Acquired/FullBodyMotion.json',ROOT/'Assets/Resources/Characters/motion-retarget.json',ROOT/'Assets/Resources/Athletes/KOR.asset',ROOT/'Assets/Resources/Athletes/JPN.asset',ROOT/'Assets/Scripts/Runtime/MotionValidationDriver.cs']
 # Asset-only builds legitimately reuse the compiled DLL. Verify the actual
 # packaged resources and successful build receipt, never touch timestamps.
 paths += [ROOT/'Assets/Resources/Characters/appearance.json',ROOT/'Assets/Shaders/AthleteSurface.shader']
 bodies=[ROOT/f'Assets/Resources/Characters/Acquired/Body{ident}.json' for ident in ['KOR','JPN']]
 paths += bodies
 build=ROOT/'Builds/verification/mac/build-info.json'
 resources=ROOT/'Builds/verification/mac/DigitalTennis.app/Contents/Resources/Data/resources.assets'
 assert all(p.stat().st_mtime<build.stat().st_mtime for p in paths),'Source newer than successful player build; rebuild required'
 assert all(p.stat().st_mtime<dll.stat().st_mtime for p in paths if p.suffix=='.cs'),'C# source newer than compiled runtime; rebuild required'
 packaged=resources.read_bytes();names={}
 for ident,body in zip(['KOR','JPN'],bodies):
  name=re.search(r'  m_Name: (.+)',(ROOT/f'Assets/Resources/Athletes/{ident}.asset').read_text()).group(1)
  assert name.encode() in packaged and body.read_bytes() in packaged,'Current body absent from packaged resources: '+ident
  names[ident]=name
 out.mkdir(parents=True,exist_ok=True)
 receipt=dict(buildTime=build.stat().st_mtime,runtimeSha256=hashlib.sha256(dll.read_bytes()).hexdigest(),meshNames=names,playerFiles=[dict(path=str(p.relative_to(ROOT)),sha256=hashlib.sha256(p.read_bytes()).hexdigest()) for p in [build,resources]],sources=[dict(path=str(p.relative_to(ROOT)),sha256=hashlib.sha256(p.read_bytes()).hexdigest()) for p in paths])
 (out/'build-receipt.json').write_text(json.dumps(receipt,indent=2)+'\n')
 print('Saved fresh runtime/source receipt');raise SystemExit(0)
font=ImageFont.load_default(size=24)
items=[]
for name in ['KOR-Running','JPN-Running','KOR-Serve','JPN-Serve','KOR-Forehand','JPN-Forehand','KOR-Backhand','JPN-Backhand']:
 paths=[folder/f'{name}-{i:02}.png' for i in [0,4,8,15]]
 if not all(p.exists() for p in paths):continue
 board=Image.new('RGB',(2400,1880),'#101b29');draw=ImageDraw.Draw(board)
 for k,p in enumerate(paths):
  x=k%2*1200;y=k//2*940
  board.paste(Image.open(p).convert('RGB'),(x,y+40));timeline=json.loads((folder/f'{name}-timeline.json').read_text())['samples'];sample=timeline[[0,4,8,15][k]]
  label=f"{name} | sample {sample['frame']} | swing {sample['swingId']} | age {sample['swingAge']:.3f}s"
  draw.text((x+12,y+6),label,font=font,fill='white')
 target=out/f'{name}-sequence.png';board.save(target);items.append(target.name)
html='<!doctype html><meta charset="utf-8"><title>AC2 actual Unity motion</title><style>body{background:#101b29;color:white;font-family:sans-serif}img{max-width:100%}</style><h1>Actual Unity / native Metal motion</h1><p>Ordinary practice input adapter, no ball/score/pose writes. Sequential live samples; sheet labels are outside original camera pixels. No audio autoplay.</p>'
for item in items:html+=f'<h2>{item}</h2><a href="{item}"><img src="{item}"></a>'
(out/'review.html').write_text(html)
print('Saved native sequence review: '+str(out/'review.html'))

manifest=dict(build=json.loads((out/'build-receipt.json').read_text()),report=json.loads((folder/'motion.json').read_text()),images=[dict(path=str(p.relative_to(out)),sha256=hashlib.sha256(p.read_bytes()).hexdigest()) for p in sorted(folder.glob('*.png'))])
(out/'review.json').write_text(json.dumps(manifest,indent=2)+'\n')
