#!/usr/bin/env python3
"""Inspect shipped waveforms/provenance and optionally actual native listener captures."""
import argparse,csv,hashlib,json,math,wave
from pathlib import Path
import numpy as np
p=argparse.ArgumentParser();p.add_argument('--native',type=Path);a=p.parse_args();bank=Path('Assets/StreamingAssets/Audio')
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def samples(p):
 with wave.open(str(p)) as f:
  assert f.getsampwidth()==2,p
  data=np.frombuffer(f.readframes(f.getnframes()),dtype='<i2').astype(float)/32768
  data=data.reshape(-1,f.getnchannels()).mean(axis=1);rate=f.getframerate()
 assert len(data)>rate*.025 and np.sqrt(np.mean(data**2))>.0001,p
 assert np.max(np.abs(data))<1,p
 return data,rate
pro=json.loads((bank/'factory-provenance.json').read_text());rows={r['cue']:r for r in csv.DictReader((bank/'audio.csv').open())};subs={(r['cue'],r['language']):r['text'] for r in csv.DictReader((bank/'subtitles.csv').open())}
assert len(pro['voices'])==60 and len({v['sha256'] for v in pro['voices']})==60
assert len({v['text'] for v in pro['voices']})==60
for record in pro['voices']+pro['foley']:
 assert sha(bank/record['file'])==record['sha256'],record['file']
 samples(bank/record['file']);assert rows[record['cue']]['file']==record['file']
for voice in pro['voices']:
 assert subs[voice['cue'],'en']==voice['text'] and subs[voice['cue'],'ko']==voice['caption']
 assert voice['language']=='en'
for pack in pro['packs']:assert 'CC0' in (bank/('KENNEY-'+pack['pack']+'-LICENSE.txt')).read_text()
assert 'Permission is hereby granted' in (bank/'FLITE-COPYING.txt').read_text()
for e in json.loads((bank/'factory-legacy.json').read_text())['entries']:assert sha(bank/e['file'])==e['sha256']
if a.native:
 audit=[]
 for mode in ['fresh','existing']:
  d=a.native/mode;report=json.loads((d/'result.json').read_text());assert report['passed'],report
  upgrade=json.loads((d/'upgrade-result.json').read_text());audio=json.loads((d/'audio-result.json').read_text())
  assert upgrade['monitorMuted'],'Offscreen speaker output was not muted'
  assert upgrade['nativePlatform']=='OSXPlayer' and upgrade['graphics']!='Null'
  assert len(upgrade['voices'])==(60 if mode=='fresh' else 8) and min(upgrade[k] for k in ['steps','skids','contacts'])>0
  assert audio['musicMuted'] and audio['musicVolume']==0 and audio['pointCheers']==1 and audio['crowdStoppedAtNextServe']
  for voice in upgrade['voices']:
   played,rate=samples(d/(voice['cue']+'-playback.wav'));source,sr=samples(d/'audio'/voice['file'])
   expected=np.interp(np.arange(round(len(source)*rate/sr))*sr/rate,np.arange(len(source)),source)
   size=1<<(len(played)+len(expected)-1).bit_length()
   correlation=np.fft.irfft(np.fft.rfft(played,size)*np.conj(np.fft.rfft(expected,size)),size).max()
   similarity=correlation/(np.linalg.norm(played)*np.linalg.norm(expected))
   assert similarity>.80,(mode,voice['cue'],float(similarity))
   audit.append({'install':mode,'cue':voice['cue'],'file':voice['file'],'correlation':float(similarity),'capture_sha256':sha(d/(voice['cue']+'-playback.wav'))})
  samples(d/'game-mix.wav')
  assert len(list(d.glob('*-speech-audition.png')))>=(15 if mode=='fresh' else 2)
  if mode=='existing':assert abs(upgrade['korVolume']-.19)<.00001 and upgrade['korFile']=='KOR_ready_v2.wav' and upgrade['jpnFile']=='JPN_ready.wav'
  else:assert upgrade['korFile']=='KOR_ready_v2.wav' and upgrade['jpnFile']=='JPN_ready_v2.wav'
 (a.native/'waveform-audit.json').write_text(json.dumps(audit,indent=2)+'\n')
 print('PASS native fresh/existing: 68 audible listener recordings (all 15 fresh; KOR/JPN migration), movement/contact, silent BGM, one cheer, preserved custom edits')
print('PASS audio assets: 60 distinct English persona recordings, Korean subtitles, 9 CC0 adaptations, legacy hashes/licenses')
