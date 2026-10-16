#!/usr/bin/env python3
"""Run only the game's GPU cameras/audio and real adapter inputs. Never captures the OS."""
import argparse,json,shutil,subprocess
from pathlib import Path
p=argparse.ArgumentParser();p.add_argument('--out',type=Path,required=True);p.add_argument('--only',choices=['roster','stadium','audio','survey','motion','all'],default='all');a=p.parse_args()
root=Path(__file__).resolve().parents[1];app=root/'Builds/mac/DigitalTennis.app/Contents/MacOS/Digital Tennis';out=a.out.resolve();out.mkdir(parents=True,exist_ok=True)
if not app.exists():raise SystemExit('Build Mac first')
for mode in (['roster','stadium','audio','motion','survey'] if a.only=='all' else [a.only]):
 d=out/mode;d.mkdir(parents=True,exist_ok=True)
 for name in ['result.json','survey.json','audio-result.json']:(d/name).unlink(missing_ok=True)
 audio=d/'audio';shutil.copytree(root/'Assets/StreamingAssets/Audio',audio,dirs_exist_ok=True,ignore=shutil.ignore_patterns('*.meta'))
 args=[str(app),'-batchmode','-screen-fullscreen','0','-screen-width','1600','-screen-height','900','-logFile',str(d/'player.log'),'-dt-audio-validation','-dt-audio-file',str(audio/'audio.csv'),'-dt-metrics-dir',str(d/'metrics')]
 for name,flag in [('characters','balance'),('gameplay','gameplay'),('rules','rules')]:
  target=d/(name+'.csv');shutil.copyfile(root/('Assets/StreamingAssets/Balance/'+name+'.csv'),target);args+=['-dt-'+flag+'-file',str(target)]
 target=d/'effects.csv';shutil.copyfile(root/'Assets/StreamingAssets/Effects/effects.csv',target);args+=['-dt-effects-file',str(target)]
 if mode=='roster':args+=['-dt-roster-preview',str(d)]
 elif mode=='survey':args+=['-dt-balance-survey',str(d)]
 elif mode=='motion':args+=['-dt-motion-verify',str(d)]
 else:
  args+=['-dt-stadium-verify',str(d)]
  if mode=='audio':args+=['-dt-stadium-front']
 print('Starting '+mode,flush=True)
 child=subprocess.Popen(args,stdout=subprocess.DEVNULL,stderr=subprocess.STDOUT)
 try:code=child.wait(timeout=1200 if mode=='survey' else 360 if mode=='motion' else 180)
 except subprocess.TimeoutExpired:
  child.terminate()
  try:child.wait(timeout=10)
  except subprocess.TimeoutExpired:child.kill();child.wait()
  raise SystemExit(mode+' timed out; only this validation process was stopped')
 if code:raise SystemExit(mode+' failed, exit '+str(code)+': '+str(d/'player.log'))
 report=json.loads((d/('survey.json' if mode=='survey' else 'motion.json' if mode=='motion' else 'result.json')).read_text())
 if mode=='roster':assert report['runtimeErrors']==0 and report['characters']==15 and report['points']==0
 else:assert report['passed'],report
 print('PASS '+mode,flush=True)
