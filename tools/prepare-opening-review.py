#!/usr/bin/env python3
"""Encode only Unity camera PNGs using their measured frame times. No desktop capture."""
import argparse,json,subprocess,shutil
from pathlib import Path
p=argparse.ArgumentParser();p.add_argument('--source',type=Path,required=True);p.add_argument('--out',type=Path,required=True);p.add_argument('--audio',type=Path,help='Actual AudioListener WAV from the same opening run');a=p.parse_args();a.out.mkdir(parents=True,exist_ok=True)
r=json.loads((a.source/'result.json').read_text());assert r['passed'];times=r['times'];lines=[]
for i,t in enumerate(times):
 file=(a.source/f'frame-{i:04}.png').resolve();assert file.is_file();lines+=['file '+"'"+str(file).replace("'","'\\''")+"'",'duration '+str((times[i+1] if i+1<len(times) else 12.5)-t)]
concat=a.source/'frames.txt';concat.write_text('\n'.join(lines)+'\n')
audio_args=[]
if a.audio:
 assert a.audio.resolve()==(a.source/'intro-listener.wav').resolve() and r['audioRecorded'], 'Use the actual listener capture from this opening run'
 assert a.audio.is_file()
 audio_args=['-i',str(a.audio),'-c:a','aac','-shortest']
subprocess.run(['ffmpeg','-y','-loglevel','error','-f','concat','-safe','0','-i',str(concat),*audio_args,'-vf','scale=1280:720,fps=30','-c:v','libx264','-pix_fmt','yuv420p','-movflags','+faststart',str(a.out/'opening-camera-preview.mp4')],check=True)
shutil.copyfile(a.source/'result.json',a.out/'opening-result.json');print(a.out/'opening-camera-preview.mp4')
