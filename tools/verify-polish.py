#!/usr/bin/env python3
"""Run only the game's GPU cameras/audio and real adapter inputs. Never captures the OS."""
import argparse,json,shutil,subprocess
from pathlib import Path
p=argparse.ArgumentParser();p.add_argument('--player',type=Path);p.add_argument('--out',type=Path,required=True);p.add_argument('--only',choices=['roster','stadium','audio','survey','motion','intro','all'],default='all');p.add_argument('--baseline-reference',action='store_true',help='Record original baseline quality honestly, including failed anatomical thresholds; requires isolated baseline bundle');p.add_argument('--motion-pair',help='Two real roster selections through the input adapter, e.g. CHN,USA');p.add_argument('--roster-ids',help='Optional staged preview subset, e.g. KOR,JPN');a=p.parse_args()
root=Path(__file__).resolve().parents[1];app=a.player.resolve() if a.player else root/'Builds/mac/DigitalTennis.app/Contents/MacOS/Digital Tennis';out=a.out.resolve();out.mkdir(parents=True,exist_ok=True)
if not app.exists():raise SystemExit('Build Mac first')
if a.baseline_reference:
 import plistlib
 with (app.parents[1]/'Info.plist').open('rb') as stream:bundle=plistlib.load(stream)['CFBundleIdentifier']
 assert bundle=='com.digitaltennis.baseline' and a.only=='motion', 'Reference mode is restricted to the isolated original baseline'
for mode in (['roster','stadium','audio','motion','intro','survey'] if a.only=='all' else [a.only]):
 d=out/mode;d.mkdir(parents=True,exist_ok=True)
 for name in ['result.json','survey.json','audio-result.json','motion.json']:(d/name).unlink(missing_ok=True)
 if mode=='motion':
  for old in d.iterdir():
   if old.is_file() and (old.suffix=='.png' or old.name.endswith('-timeline.json')):old.unlink()
 audio=d/'audio';shutil.copytree(root/'Assets/StreamingAssets/Audio',audio,dirs_exist_ok=True,ignore=shutil.ignore_patterns('*.meta'))
 args=[str(app),'-batchmode','-screen-fullscreen','0','-screen-width','1600','-screen-height','900','-logFile',str(d/'player.log'),'-dt-audio-validation','-dt-audio-file',str(audio/'audio.csv'),'-dt-metrics-dir',str(d/'metrics')]
 for name,flag in [('characters','balance'),('gameplay','gameplay'),('rules','rules')]:
  target=d/(name+'.csv');shutil.copyfile(root/('Assets/StreamingAssets/Balance/'+name+'.csv'),target);args+=['-dt-'+flag+'-file',str(target)]
 target=d/'effects.csv';shutil.copyfile(root/'Assets/StreamingAssets/Effects/effects.csv',target);args+=['-dt-effects-file',str(target)]
 if mode=='roster':
  args+=['-dt-roster-preview',str(d)]
  if a.roster_ids:args+=['-dt-roster-ids',a.roster_ids]
 elif mode=='survey':args+=['-dt-balance-survey',str(d)]
 elif mode=='motion':
  args+=['-dt-motion-verify',str(d)]
  if a.motion_pair:args+=['-dt-motion-pair',a.motion_pair]
 elif mode=='intro':args+=['-dt-intro-verify',str(d)]
 else:
  args+=['-dt-stadium-verify',str(d)]
  if mode=='audio':args+=['-dt-stadium-front']
 print('Starting '+mode,flush=True)
 child=subprocess.Popen(args,stdout=subprocess.DEVNULL,stderr=subprocess.STDOUT)
 try:code=child.wait(timeout=1200 if mode=='survey' else 600 if mode=='motion' else 180)
 except subprocess.TimeoutExpired:
  child.terminate()
  try:child.wait(timeout=10)
  except subprocess.TimeoutExpired:child.kill();child.wait()
  raise SystemExit(mode+' timed out; only this validation process was stopped')
 if code and not (a.baseline_reference and code==1):raise SystemExit(mode+' failed, exit '+str(code)+': '+str(d/'player.log'))
 report=json.loads((d/('survey.json' if mode=='survey' else 'motion.json' if mode=='motion' else 'result.json')).read_text())
 if mode=='roster':
  ids=a.roster_ids.split(',') if a.roster_ids else 'KOR JPN CHN USA RUS IND DEU GBR ESP FRA AUS NLD BRA MAR ZAF'.split()
  assert report['runtimeErrors']==0 and report['characters']==len(set(ids)) and report['points']==0
  for ident in ids:
   for suffix in ['', '-face','-detail','-phase-18','-phase-50','-phase-80']:assert (d/(ident+suffix+'.png')).is_file()
 elif a.baseline_reference:
  assert report['errors']==0 and report['strokes']==6 and report['contacts']>=26,report
  for ident in a.motion_pair.split(','):
   for action in ['Running','Serve','Forehand','Backhand']:
    samples=json.loads((d/f'{ident}-{action}-timeline.json').read_text())['samples']
    assert len(samples)==16
  print('BASELINE_OBSERVED; original quality guards passed='+str(report['passed']),flush=True)
 else:assert report['passed'],report
 if mode=='stadium':assert len(list(d.glob('*.png')))>=48
 if not a.baseline_reference:print('PASS '+mode,flush=True)
