#!/usr/bin/env python3
"""Make labeled, game-asset-only audition playlists; no microphone, system recording or network."""
import argparse,array,json,math,wave
from pathlib import Path
p=argparse.ArgumentParser();p.add_argument('--bank',type=Path,default=Path('Assets/StreamingAssets/Audio'));p.add_argument('--out',type=Path,required=True);a=p.parse_args();a.out.mkdir(parents=True,exist_ok=True)
rate=22050

def playlist(name,files,limit):
 output=array.array('h');timeline=[]
 for title,file in files:
  with wave.open(str(a.bank/file),'rb') as f:
   assert f.getsampwidth()==2 and f.getnchannels()==1
   source_rate=f.getframerate();samples=array.array('h',f.readframes(f.getnframes()))
  count=min(round(len(samples)*rate/source_rate),round(limit*rate));start=len(output)/rate
  for k in range(count):
   pos=k*source_rate/rate;i=min(len(samples)-1,int(pos));j=min(len(samples)-1,i+1);value=samples[i]+(samples[j]-samples[i])*(pos-i)
   fade=min(1,k/(rate*.015),(count-1-k)/(rate*.03));output.append(round(value*max(0,fade)))
  timeline.append({'title':title,'start':round(start,2),'end':round(len(output)/rate,2),'source':file});output.extend([0]*round(rate*.45))
 with wave.open(str(a.out/(name+'.wav')),'wb') as f:f.setnchannels(1);f.setsampwidth(2);f.setframerate(rate);f.writeframes(output.tobytes())
 return {'file':name+'.wav','timeline':timeline,'source':'original game synthesis / Flite English draft voices; short edits for comparison, not loop QA'}
report=[]
report.append(playlist('music-ab',[(cue.upper()+' '+variant.upper(),cue+'_'+variant+'.wav') for cue in ['menu','game','result','deuce','pressure'] for variant in ['a','b']],7.5))
report.append(playlist('voice-kor-jpn',[(id+' '+cue+' '+('alternate' if alternate else 'base'),id+'_'+cue+('_alt' if alternate else '')+'.wav') for id in ['KOR','JPN'] for cue in ['ready','skill','win','lose'] for alternate in [False,True]],5))
(a.out/'audition.json').write_text(json.dumps({'selection':'A provisional; human choice pending','playlists':report},ensure_ascii=False,indent=2)+'\n')
print('Prepared '+str(a.out))
