#!/usr/bin/env python3
"""Run actual Mac GPU/audio player against fresh and legacy local installs, entirely isolated."""
import argparse,csv,hashlib,json,shutil,subprocess,wave
from pathlib import Path
p=argparse.ArgumentParser();p.add_argument('--out',type=Path,required=True);p.add_argument('--player',type=Path);p.add_argument('--mode',choices=['all','fresh','existing'],default='all');a=p.parse_args();root=Path(__file__).resolve().parents[1];out=a.out.resolve();out.mkdir(parents=True,exist_ok=True)
player=a.player.resolve() if a.player else root/'Builds/mac/DigitalTennis.app/Contents/MacOS/Digital Tennis';assert player.is_file(),'Build Mac first'
bundle=player.parents[2]
assemblies=list(bundle.rglob('DigitalTennis.Runtime.dll'));assert len(assemblies)==1
build_id=hashlib.sha256(assemblies[0].read_bytes()).hexdigest()[:12]
isolated=out/'player'/build_id/'DigitalTennis.app'
if not isolated.exists():shutil.copytree(bundle,isolated,symlinks=True)
player=isolated/'Contents/MacOS/Digital Tennis'
bank=root/'Assets/StreamingAssets/Audio';legacy=json.loads((bank/'factory-legacy.json').read_text())['entries']
def digest(p):return hashlib.sha256(p.read_bytes()).hexdigest()
for mode in (['fresh','existing'] if a.mode=='all' else [a.mode]):
 d=out/mode;d.mkdir(parents=True,exist_ok=True);local=d/'audio'
 if mode=='fresh' and local.exists():
  backup=d/('previous-audio-'+str(__import__('time').time_ns()));local.rename(backup)
 local.mkdir(exist_ok=True)
 for result in ['result.json','audio-result.json','upgrade-result.json']:(d/result).unlink(missing_ok=True)
 protected={}
 if mode=='existing':
  shutil.copytree(bank,local,dirs_exist_ok=True,ignore=shutil.ignore_patterns('*.meta'))
  rows=list(csv.DictReader((bank/'audio.csv').open()));byid={e['id']:e for e in legacy}
  rows=[r for r in rows if not r['cue'].startswith(('step_','skid_','hit_'))]
  for r in rows:
   if r['cue'] in byid:r['file']=byid[r['cue']]['file']
   if r['cue']=='KOR.ready':r['volume']='0.19'
  with (local/'audio.csv').open('w',newline='') as f:w=csv.DictWriter(f,fieldnames=rows[0].keys(),lineterminator='\n');w.writeheader();w.writerows(rows)
  with (local/'subtitles.csv').open('w',newline='') as f:
   w=csv.writer(f);w.writerow(['cue','language','text'])
   for e in legacy:
    if e['caption']:w.writerow([e['id'],'ko','사용자 편집 대사' if e['id']=='JPN.ready' else e['caption']])
  import array
  file=local/'JPN_ready.wav'
  with wave.open(str(file)) as f:params=f.getparams();data=array.array('h',f.readframes(f.getnframes()))
  data=array.array('h',(round(x*.8) for x in data))
  with wave.open(str(file),'wb') as f:f.setparams(params);f.writeframes(data.tobytes())
  protected={p:digest(p) for p in [local/'audio.csv',local/'subtitles.csv',local/'KOR_ready.wav',file]}
 args=[str(player),'-batchmode','-screen-fullscreen','0','-screen-width','1600','-screen-height','900','-logFile',str(d/'player.log'),'-dt-stadium-verify',str(d),'-dt-stadium-front','-dt-audio-validation','-dt-audio-upgrade-verify','-dt-audio-file',str(local/'audio.csv')]
 if mode=='existing':args+=['-dt-audio-existing-verify']
 for name,flag in [('characters','balance'),('gameplay','gameplay'),('rules','rules')]:
  file=d/(name+'.csv');shutil.copyfile(root/'Assets/StreamingAssets/Balance'/file.name,file);args+=['-dt-'+flag+'-file',str(file)]
 file=d/'effects.csv';shutil.copyfile(root/'Assets/StreamingAssets/Effects/effects.csv',file);args+=['-dt-effects-file',str(file)]
 print('Native Mac playback: '+mode,flush=True)
 subprocess.run(args,check=True,timeout=900,stdout=subprocess.DEVNULL,stderr=subprocess.STDOUT,start_new_session=True)
 assert (d/'upgrade-result.json').is_file(),'Native player exited before final audio report; partial captures are not a pass'
 assert all(digest(p)==h for p,h in protected.items()),'Local customizations changed'
 if mode=='existing':
  report=json.loads((d/'upgrade-result.json').read_text());assert next(v['caption'] for v in report['voices'] if v['cue']=='JPN.ready')=='사용자 편집 대사'
 print('PASS '+mode+' player and unchanged local files',flush=True)
if all((out/m/'result.json').exists() for m in ['fresh','existing']):subprocess.run(['python3',str(root/'tools/verify-court-audio.py'),'--native',str(out)],check=True)
