#!/usr/bin/env python3
"""Make labeled, game-asset-only audition playlists; no microphone, system recording or network."""
import argparse,array,csv,hashlib,html,json,math,shutil,wave
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
rows=list(csv.DictReader((a.bank/'audio.csv').open(encoding='utf-8-sig')))
by_cue={r['cue']:r for r in rows}
report=[]
report.append(playlist('music-ab',[(cue.upper()+' '+variant.upper(),by_cue[cue+'_'+variant]['file']) for cue in ['menu','game','result','deuce','pressure'] for variant in ['a','b']],7.5))
report.append(playlist('voice-kor-jpn',[(id+' '+cue+' '+('alternate' if alternate else 'base'),by_cue[id+'.'+cue+('_alt' if alternate else '')]['file']) for id in ['KOR','JPN'] for cue in ['ready','skill','win','lose'] for alternate in [False,True]],5))
assets=a.out/'assets';assets.mkdir(exist_ok=True)
items=[]
for row in rows:
 source=(a.bank/row['file']).resolve()
 if not source.is_relative_to(a.bank.resolve()):raise ValueError('Audio path leaves bank: '+row['file'])
 target=assets/row['file'];target.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(source,target)
 items.append({'cue':row['cue'],'group':row['group'],'file':'assets/'+row['file'],'sha256':hashlib.sha256(source.read_bytes()).hexdigest(),'source':row['source']})
(a.out/'audition.json').write_text(json.dumps({'selection':'Undecided by user; current A defaults are provisional','playlists':report,'fullAssets':items},ensure_ascii=False,indent=2)+'\n')
cards=[]
for item in items:
 cards.append('<article data-search="'+html.escape(item['cue'].lower(),quote=True)+'"><strong>'+html.escape(item['cue'])+'</strong> <small>'+html.escape(item['group'])+'</small><audio controls preload="none" src="'+html.escape(item['file'],quote=True)+'"></audio></article>')
page='''<!doctype html><html lang="ko"><meta charset="utf-8"><meta name="viewport" content="width=device-width"><title>Digital Tennis 청음</title><style>body{background:#101b28;color:#edf4ff;font:17px system-ui;max-width:1100px;margin:40px auto;padding:20px}article{display:inline-block;vertical-align:top;width:310px;margin:8px;padding:16px;background:#1b2b3e;border-radius:12px}audio{display:block;width:100%;margin-top:12px}input{font:inherit;padding:12px}small{color:#acc2d8}[hidden]{display:none!important}h2{margin-top:38px}</style><h1>Digital Tennis 음향 검수</h1><p>자동 재생하지 않습니다. 한 항목을 재생하면 다른 항목은 정지합니다. 음악 A/B와 음성의 최종 선택은 미정입니다. 경기 중 BGM은 음소거되며 game/deuce/pressure 음악은 청음 전용입니다.</p><p>'''+str(len(items))+'''개 현재 CSV 음원에는 15명 선수의 기본·대체 대사와 심판/환경/효과음이 포함됩니다. 음성은 Flite 영어 초안이며 특정 국적의 발음 재현을 주장하지 않습니다. 기본/대체 cue가 같은 파일을 지정한 경우 동일하게 들립니다. 아래는 파일 원음으로, 게임 내 혼합·루프 검증 결과와 구별합니다.</p><h2>짧은 비교 모음</h2><p>메뉴·경기·결과·듀스·중요 포인트 순서로 A 다음 B</p><audio controls preload="none" src="music-ab.wav"></audio><p>한국·일본 대사 기본/대체</p><audio controls preload="none" src="voice-kor-jpn.wav"></audio><h2>전체 음원</h2><label>검색 <input id="filter" placeholder="KOR, JPN, hit, step, menu …"></label><main>'''+''.join(cards)+'''</main><script>document.querySelectorAll('audio').forEach(a=>a.addEventListener('play',()=>document.querySelectorAll('audio').forEach(b=>{if(a!==b)b.pause()})));document.querySelector('#filter').addEventListener('input',e=>document.querySelectorAll('article').forEach(a=>a.hidden=!a.dataset.search.includes(e.target.value.trim().toLowerCase())));</script></html>'''
(a.out/'index.html').write_text(page,encoding='utf-8')
print('Prepared '+str(a.out))
