#!/usr/bin/env python3
"""Build offline factory voice/CC0 foley upgrades without rewriting legacy user files.
Input archives must be the official free Kenney downloads; hashes are pinned below.
"""
import argparse,csv,hashlib,json,shutil,subprocess,zipfile
from pathlib import Path
P=argparse.ArgumentParser();P.add_argument('--sources',type=Path,required=True);P.add_argument('--flite',type=Path,required=True);a=P.parse_args()
bank=Path('Assets/StreamingAssets/Audio');provenance=[]
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
expected={'impact-sounds':'029d734af1582474edf3a694d1b0cebc97c1c152f2f39fa34d4c2bafc5de77f8','rpg-audio':'6dbeaf8544da958d8f2adcb4a4a4b76c1ade34a05f8ab9edccd327da7375f38b'}
for pack,h in expected.items():
 archive=a.sources/(pack+'.zip');assert sha(archive)==h
 with zipfile.ZipFile(archive) as z:z.extractall(a.sources/pack)
 provenance.append({'pack':pack,'source':'https://kenney.nl/assets/'+pack,'archive_url':{'impact-sounds':'https://kenney.nl/media/pages/assets/impact-sounds/87b4ddecda-1677589768/kenney_impact-sounds.zip','rpg-audio':'https://kenney.nl/media/pages/assets/rpg-audio/8e99002d76-1677590336/kenney_rpg-audio.zip'}[pack],'archive_sha256':h,'license':'CC0-1.0'})
 shutil.copyfile(a.sources/pack/'License.txt',bank/('KENNEY-'+pack+'-LICENSE.txt'))
rows=list(csv.DictReader((bank/'audio.csv').open()));subs=list(csv.DictReader((bank/'subtitles.csv').open()))
# English synthesized speech, original scripts based on each existing adult's personal arc.
# Korean captions are translations, not claims of Korean/Japanese voice acting.
scripts=[
('KOR', 'Leave a beat. The path continues.|한 박자 비워도 길은 이어져.', 'Make room for the next choice!|다음 선택의 자리를 남겨!', 'A shared court. A shared rhythm. Thank you.|함께 쓴 코트, 함께 찾은 리듬. 고마워요.', 'My plan needs another path. I will listen.|계획에 다른 길이 필요하네요. 귀 기울일게요.'),
('JPN','Start again at the crease.|접힌 자리에서 다시 시작해.', 'Step back. Fold a new path!|한 발 물러서, 새 길을 접어!', 'These small pieces made a fine match.|작은 조각들이 좋은 경기를 만들었네요.', 'Keep the crease. Try the next fold.|접힌 자국은 두고, 다음을 접어 보자.'),
('CHN','An empty space is also a stroke.|빈자리도 한 획이야.', 'Leave the circle open!|원의 끝을 열어 둬!', 'Your choices completed this picture. Thank you.|당신의 선택으로 그림이 완성됐어요. 고마워요.', 'I closed the space too soon. I see it now.|너무 일찍 여백을 닫았네요. 이제 보여요.'),
('USA','A failed test is my next design.|실패 기록도 다음 설계야.', 'Restart. Find another answer!|다시 시동 걸고, 다른 답을 찾아!', 'That return goes in my class notebook!|그 리턴은 수업 노트에 적어야겠어!', 'No hiding this test. Back to the return drill.|이번 실험도 숨기지 않을게. 리턴 연습부터 다시.'),
('RUS','This time, I will turn on the light.|지금, 내가 불을 켤게.', 'Give the signal. Go!|신호를 보내고, 간다!', 'We kept the stage bright together.|함께 무대를 밝게 지켰네요.', 'I waited past my cue. Next time, I begin.|내 신호를 지나쳤네요. 다음엔 먼저 시작할게요.'),
('IND','Rest leaves room for the next pattern.|쉬어야 다음 무늬가 이어져.', 'Breathe. We can continue!|숨을 고르면, 이어 갈 수 있어!', 'A lasting rhythm needs both of us. Thank you.|오래가는 박자에는 둘이 필요하네요. 고마워요.', 'Time to rest, then weave another pattern.|이젠 쉬고, 다음 무늬를 엮을 시간이에요.'),
('DEU','After the numbers, tell me how it feels.|수치 다음엔 네 감각을 듣자.', 'Adjust the vector. Now!|방향을 다시 맞춰. 지금!', 'Your return taught me a new adjustment.|당신의 리턴으로 새 조정을 배웠어요.', 'An unexpected result. Let me ask, then measure.|예상 밖의 결과네요. 질문하고 다시 측정할게요.'),
('GBR','No jokes this time. I will hear your call.|이번엔 농담 빼고, 네 콜을 들을게.', 'Our call. Our timing!|우리의 콜, 우리의 박자!', 'A fine match. And I mean that seriously.|좋은 경기였어요. 이번 말은 진심이에요.', 'I rushed the net. Your call was clearer.|제가 네트를 서둘렀네요. 당신의 콜이 더 분명했어요.'),
('ESP','Still hot. Give it one more beat.|아직 뜨거워, 한 박자 더.', 'Wait for it. Now, fire!|기다렸다가, 지금 불꽃처럼!', 'A little patience made this piece shine.|조금 기다리니 이 작품이 빛나네.', 'I opened the kiln too soon. Keep the pieces.|가마를 너무 일찍 열었네. 조각은 간직할게.'),
('FRA','Take something away. My color remains.|덜어 내도 내 색은 남아.', 'One clear gesture!|선명한 동작 하나!', 'We gave each other room to shine.|서로 빛날 자리를 내줬네요.', 'Too much flourish. I will find the quiet gesture.|너무 꾸몄네요. 조용한 동작을 찾아볼게요.'),
('AUS','Ready? We go together.|준비됐지? 함께 간다.', 'Check the team. Catch the wave!|함께 있는지 보고, 파도를 잡아!', 'Everyone back on deck. What a rally!|모두 데크로 돌아왔네. 멋진 랠리였어!', 'I ran ahead. Next time, I check beside me.|혼자 앞서갔네. 다음엔 옆부터 살필게.'),
('NLD','Unexpected flow. Let us follow it.|예상 밖이네, 따라가 보자.', 'Open a channel beyond the map!|지도 밖으로 물길을 열어!', 'Your unexpected angle belongs in my notes.|예상 못 한 그 각도는 기록해야겠어요.', 'That was no error. I have more to observe.|오류가 아니었네요. 더 관찰해야겠어요.'),
('BRA','Let me hear your beat first.|네 박자부터 들려줘.', 'Leave a beat. Let it flow!|한 박자 비우고, 흘려보내!', 'Your rhythm made this rally sing.|네 박자가 이 랠리를 노래하게 했어.', 'I filled every silence. Next time, I listen.|빈 박자를 다 채웠네. 다음엔 먼저 들을게.'),
('MAR','Look again. The path can change.|다시 보자, 길은 바뀌니까.', 'Read this moment, not the memory!|기억 대신, 지금을 읽어!', 'We made room for a different performance.|다른 공연이 펼쳐질 자리를 만들었네요.', 'My old plan missed your new path.|오래된 도면으로 새 길을 놓쳤네요.'),
('ZAF','I want to reach this horizon too.|나도 이 길 끝에 가고 싶어.', 'My next step. Our open horizon!|내 다음 발걸음, 함께 열린 지평선!', 'I reached my goal, and heard your steps.|내 목표에 닿고, 네 발걸음도 들었어.', 'I need a rest too. Then I will choose my path.|나도 쉬어야겠어. 그런 다음 내 길을 고를게.')]
voices={'woman':'slt','man':'rms','nonbinary':'awb'};roster={c['id']:c for c in json.load(open('Assets/Resources/Characters/roster.json'))['characters']}
voice_manifest=[]
for index,(id,*lines) in enumerate(scripts):
 for cue,line in zip(['ready','skill','win','lose'],lines):
  en,ko=line.split('|');key=id+'.'+cue;file=id+'_'+cue+'_v2.wav';voice=voices[roster[id]['gender']]
  dest=bank/file
  subprocess.run([str(a.flite.resolve()),'-voice',voice,'-setf','duration_stretch='+str(.94+(index%4)*.035),'-t',en,'-o',str(dest)],check=True,stdout=subprocess.DEVNULL)
  for row in rows:
   # Retire repeated alternate scripts from gameplay by resolving both takes to the new persona line.
   # Existing alternate custom files remain eligible for customization/migration by their own hash.
   if row['cue'] in [key,key+'_alt']:row.update(file=file,source='Synthesized English / Flite CMU '+voice+' / original persona script v2')
  for row in subs:
   if row['cue'] in [key,key+'_alt']:row['text']=ko if row['language']=='ko' else en
  voice_manifest.append({'cue':key,'language':'en','caption_language':'ko','text':en,'caption':ko,'voice':voice,'file':file,'sha256':sha(dest)})
# Short real material transients are adapted for shoe steps, shoe scrapes and racket contact.
foley=[]
for i in range(3):
 for cue,pack,source,filter in [
  ('step_'+str(i),'impact-sounds',f'footstep_concrete_00{i}.ogg','atrim=duration=0.28,afade=t=out:st=0.22:d=0.06'),
  ('skid_'+str(i),'rpg-audio',f'cloth{i+1}.ogg','atempo=0.8,atrim=duration=0.38,highpass=f=600,afade=t=out:st=0.28:d=0.1'),
  ('hit_'+str(i),'impact-sounds',f'impactWood_light_00{i}.ogg','atrim=duration=0.20,highpass=f=160,afade=t=out:st=0.14:d=0.06')]:
  src=a.sources/pack/'Audio'/source;dest=bank/('court_'+cue+'_v2.wav')
  subprocess.run(['ffmpeg','-v','error','-y','-i',str(src),'-af',filter+',loudnorm=I=-22:TP=-3:LRA=7','-ar','22050','-ac','1','-c:a','pcm_s16le',str(dest)],check=True)
  row={'cue':cue,'group':'Sfx','file':dest.name,'volume':'0.65' if cue.startswith('hit') else '0.42','loop':'0','fade':'0','source':'Kenney '+pack+' CC0-1.0; '+source+' adapted'}
  rows=[r for r in rows if r['cue']!=cue];rows.append(row)
  foley.append({'cue':cue,'pack':pack,'archive_member':'Audio/'+source,'source_sha256':sha(src),'file':dest.name,'sha256':sha(dest),'adaptation':filter+',loudnorm=I=-22:TP=-3:LRA=7; mono PCM16 22050Hz','role':'cloth scrape used as shoe skid approximation' if cue.startswith('skid') else 'contact transient' if cue.startswith('hit') else 'concrete shoe step'})
for row in rows:
 if row['cue'] in ['hit','serve']:row.update(file='court_hit_'+('0' if row['cue']=='hit' else '1')+'_v2.wav',source='Kenney impact-sounds CC0-1.0; adapted wood contact')
for name,data,fields in [('audio.csv',rows,['cue','group','file','volume','loop','fade','source']),('subtitles.csv',subs,['cue','language','text'])]:
 with (bank/name).open('w',newline='') as f:w=csv.DictWriter(f,fieldnames=fields,lineterminator='\n');w.writeheader();w.writerows(data)
(bank/'factory-provenance.json').write_text(json.dumps({'packs':provenance,'voices':voice_manifest,'foley':foley,'synthesis':'Flite official festvox/flite 6c9f20dc915b17f5619340069889db0aa007fcdc; offline English synthetic voices, not actors or native-language performances','synthesis_source':'https://github.com/festvox/flite/tree/6c9f20dc915b17f5619340069889db0aa007fcdc','synthesis_license':'FLITE-COPYING.txt'},ensure_ascii=False,indent=2)+'\n')
print(f'Produced {len(voice_manifest)} persona recordings and {len(foley)} CC0 foley variants')
