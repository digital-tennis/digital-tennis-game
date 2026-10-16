#!/usr/bin/env python3
"""Original Digital Tennis music/SFX. No samples, copied melodies, cloud calls or API keys.
Requires numpy; optional --flite points to a locally built official Flite binary.
"""
import argparse, csv, json, math, subprocess, wave
from pathlib import Path
import numpy as np
SR=22050
rng=np.random.default_rng(4016)

def save(path,data):
    data=np.asarray(data,dtype=np.float64);data-=data.mean()
    peak=max(.0001,float(np.abs(data).max()));data*=.78/peak
    pcm=np.int16(np.clip(data,-1,1)*32767)
    with wave.open(str(path),'wb') as f:f.setnchannels(1);f.setsampwidth(2);f.setframerate(SR);f.writeframes(pcm.tobytes())
    return {'file':path.name,'seconds':round(len(data)/SR,3),'peak_db':round(20*math.log10(max(abs(data))),2),'rms_db':round(20*math.log10(max(.00001,np.sqrt(np.mean(data**2)))),2),'boundary_delta':round(float(abs(data[0]-data[-1])),6)}

def add(buffer,signal,start):
    at=int(start*SR);indices=(np.arange(len(signal))+at)%len(buffer);np.add.at(buffer,indices,signal)

def tone(note,duration,kind='bell'):
    t=np.arange(int(duration*SR))/SR;f=440*2**((note-69)/12)
    attack=np.minimum(t/.012,1);release=np.minimum((duration-t)/.07,1)
    if kind=='bass':sound=np.sin(2*np.pi*f*t)+.15*np.sin(4*np.pi*f*t);env=np.exp(-t*2.5)
    elif kind=='pad':sound=np.sin(2*np.pi*f*t)+.30*np.sin(4*np.pi*f*t);attack=np.minimum(t/.22,1);release=np.minimum((duration-t)/.35,1);env=np.ones(len(t))
    elif kind=='lead':sound=np.sin(2*np.pi*f*t)+.22*np.sin(6*np.pi*f*t);env=np.exp(-t*3)
    else:sound=np.sin(2*np.pi*f*t)+.35*np.sin(2*np.pi*f*2.003*t)+.12*np.sin(2*np.pi*f*3*t);env=np.exp(-t*5)
    return sound*attack*release*env

def drum(kind):
    duration={'kick':.25,'snare':.16,'hat':.055}[kind];t=np.arange(int(duration*SR))/SR
    if kind=='kick':return np.sin(2*np.pi*(48*t+26*(1-np.exp(-t*30))/30))*np.exp(-t*22)
    noise=rng.normal(0,.5,len(t));noise=np.r_[0,np.diff(noise)]
    return noise*np.exp(-t*(35 if kind=='snare' else 100))

def music(cue,bpm,variant,result=False):
    beat=60/bpm;bars=4 if result else 16;out=np.zeros(round(bars*4*beat*SR));chords=[(53,57,60),(48,52,55),(50,53,57),(46,50,53)]
    melody=[0,4,7,9,7,4,2,0, 7,9,12,9,7,4,2,4] if variant==0 else [7,4,2,0,2,4,9,7, 4,7,12,11,9,7,4,2]
    for bar in range(bars):
        chord=chords[bar%4]
        for note in chord:add(out,tone(note+12,4.2*beat,'pad')*.075,bar*4*beat)
        for b in range(4):
            at=(bar*4+b)*beat
            add(out,tone(chord[0]-12,.7*beat,'bass')*.25,at)
            add(out,drum('kick')*(.24 if cue in ['game','deuce','pressure'] else .14),at)
            if b%2:add(out,drum('snare')*.11,at)
            for half in range(2):add(out,drum('hat')*.045,at+half*beat/2)
            note=chord[0]+24+melody[(bar*4+b)%len(melody)]
            add(out,tone(note,beat*.82,'lead' if cue in ['game','deuce','pressure'] else 'bell')*(.12 if cue=='game' else .11),at)
            if variant==1:add(out,tone(chord[b%3]+24,beat*.4,'bell')*.07,at+beat*.5)
    if cue=='deuce':
        for bar in range(bars):
            for half in range(8):add(out,tone(77+(half%2)*5,beat*.22,'bell')*.045,(bar*4+half*.5)*beat)
    if cue=='pressure':
        for bar in range(bars):
            for quarter in range(16):add(out,tone(41+(bar%4)*2,beat*.16,'bass')*.09,(bar*4+quarter*.25)*beat)
    if result:out*=np.minimum(1,np.arange(len(out))/SR/.02)*np.minimum(1,(len(out)-np.arange(len(out)))/SR/.8)
    return out

def effect(name):
    durations={'hit':.11,'bounce':.09,'serve':.22,'net':.18,'skill':.85,'charge':.40,'point':.70,'move':.045,'confirm':.22,'cheer':1.3,'ambience':8}
    duration=durations[name];t=np.arange(int(duration*SR))/SR
    if name in ['hit','bounce','net']:
        f={'hit':460,'bounce':185,'net':95}[name];return (.7*np.sin(2*np.pi*f*t*np.exp(-t*2))+.3*rng.normal(0,1,len(t)))*np.exp(-t*50)
    if name in ['serve','skill']:
        rise=np.sin(2*np.pi*(120*t+850*t*t))*.5+rng.normal(0,.18,len(t));return rise*np.sin(np.pi*t/duration)**2
    if name in ['move','confirm','charge','point']:
        out=np.zeros(len(t));notes={'move':[76],'confirm':[72,79],'charge':[72,76,79,84],'point':[67,72,76]}[name]
        for i,n in enumerate(notes):add(out,tone(n,duration/len(notes)*1.4,'bell')*.4,i*duration/len(notes))
        return out*np.minimum(1,(duration-t)/.025)
    white=rng.normal(0,1,len(t));freq=np.fft.rfftfreq(len(t),1/SR)
    spectrum=np.fft.rfft(white);spectrum*=np.exp(-((freq-650)/900)**2)*(freq>100)
    noise=np.fft.irfft(spectrum,n=len(t));noise/=max(1,np.max(np.abs(noise)))
    envelope=.35+.10*np.sin(t*np.pi*.5)+.10*np.sin(t*np.pi*.75)
    if name=='cheer':envelope=np.sin(np.pi*t/duration)**.7
    return noise*envelope

def main():
    p=argparse.ArgumentParser();p.add_argument('--output',type=Path,default=Path('.work/game-polish/audio'));p.add_argument('--flite',type=Path);p.add_argument('--roster',type=Path,default=Path('Assets/Resources/Characters/roster.json'));a=p.parse_args();a.output.mkdir(parents=True,exist_ok=True)
    stats=[];rows=[];captions=[]
    for cue,bpms in [('menu',[108,96]),('game',[132,144]),('result',[120,108]),('deuce',[132,144]),('pressure',[132,144])]:
        for variant,bpm in enumerate(bpms):
            key=f'{cue}_{"ab"[variant]}';file=key+'.wav';stats.append(save(a.output/file,music(cue,bpm,variant,cue=='result')))
            rows.append([key,'Music',file,.65,0 if cue=='result' else 1,.8,'original-synthesis'])
    for name in ['hit','bounce','serve','net','skill','charge','point','move','confirm','cheer','ambience']:
        file=name+'.wav';stats.append(save(a.output/file,effect(name)));rows.append([name,'Ambience' if name=='ambience' else 'Sfx',file,.16 if name=='ambience' else .6,1 if name=='ambience' else 0,.5,'original-synthesis'])
    if a.flite:
        voices={'woman':'slt','man':'rms','nonbinary':'awb'}
        roster=json.loads(a.roster.read_text())['characters']
        skill_lines=['Flow like the river!','One step ahead!','Stand strong!','Time to shine!','Find the opening!','Follow the rhythm!','Right on target!','Hold the line!','Here comes the fire!','A touch of brilliance!','Ride the momentum!','Turn the tide!','Keep it moving!','Every angle counts!','Rise to the challenge!']
        lines={'ready':'Ready to play.','skill':'This is my moment!','win':'A great match. Thank you.','lose':'Well played. Next time.'}
        for index,character in enumerate(roster):
            for cue,text in lines.items():
                if cue=='skill':text=skill_lines[index]
                file=f'{character["id"]}_{cue}.wav';dest=a.output/file
                subprocess.run([str(a.flite.resolve()),'-voice',voices[character['gender']],'-t',text,'-o',str(dest)],check=True,stdout=subprocess.DEVNULL)
                temporary=dest.with_suffix('.resampled.wav')
                with wave.open(str(dest),'rb') as f:original_rate=f.getframerate()
                pitch=.96+(index%7)*.016
                subprocess.run(['ffmpeg','-v','error','-y','-i',str(dest),'-af',f'asetrate={original_rate*pitch},aresample=22050,atempo={1/pitch}','-c:a','pcm_s16le',str(temporary)],check=True)
                temporary.replace(dest)
                korean={'ready':'준비됐어요!','win':'멋진 경기였어요. 고마워요.','lose':'잘했어요. 다음을 기대해요.','skill':['강물처럼 흐르게!','한 걸음 먼저!','흔들리지 않아!','빛날 시간이야!','빈틈을 찾았어!','리듬을 따라!','정확히 겨냥해!','이 자리를 지켜!','뜨겁게 간다!','빛나는 한 수!','기세를 이어가!','흐름을 바꿔!','멈추지 않아!','모든 각도를 노려!','한계를 넘어!'][index]}[cue]
                captions.extend([[f'{character["id"]}.{cue}','en',text],[f'{character["id"]}.{cue}','ko',korean]])
                rows.append([f'{character["id"]}.{cue}','Voice',file,.75,0,.04,'Flite-CMU-'+voices[character['gender']]])
        alternate_ready=['Find the rhythm. Let us begin.','Calm steps. Clear focus.','Steady hands. Ready.','Big court. Big opportunity!','Patient. Solid. Ready.','Breathe in. Play freely.','A clear plan. Let us play.','Good tennis starts here.','Let us bring the energy!','A little flair today.','A fresh rally awaits!','Change the angle. Ready.','Feel the beat. Play!','See the space. Ready.','Rise together. Ready.']
        alternate_skill=['Now, release the rhythm!','Fold the distance!','This line is mine!','Light up the court!','Unbroken focus!','Breathe, and turn!','One precise moment!','Own the net!','Let the rally burn!','The perfect flourish!','Catch this wave!','Open a new angle!','Keep the rhythm alive!','Find every opening!','Stand tall, move free!']
        for index,character in enumerate(roster):
            text_by_cue={'ready_alt':alternate_ready[index],'skill_alt':alternate_skill[index],'win_alt':['We found our rhythm. Thank you!','What a rally. I enjoyed that!','A fine contest. Thank you.'][index%3],'lose_alt':['That taught me something. Again!','Strong play. I will be ready.','A close lesson. Thank you.'][index%3]}
            for cue,text in text_by_cue.items():
                file=f'{character["id"]}_{cue}.wav';dest=a.output/file
                stretch=.92 if cue=='skill_alt' else 1.06 if cue=='lose_alt' else .98
                subprocess.run([str(a.flite.resolve()),'-voice',voices[character['gender']],'-setf',f'duration_stretch={stretch}','-t',text,'-o',str(dest)],check=True,stdout=subprocess.DEVNULL)
                captions.extend([[f'{character["id"]}.{cue}','en',text],[f'{character["id"]}.{cue}','ko',{'ready_alt':['리듬을 찾고, 시작해요.','차분한 발걸음, 맑은 집중.','흔들림 없이, 준비됐어요.','넓은 코트, 큰 기회!','침착하고 단단하게, 준비.','숨을 들이쉬고, 자유롭게.','계획은 선명해요. 시작해요.','좋은 테니스는 여기서 시작해요.','열기를 끌어올려요!','오늘은 약간의 멋을 더해요.','새로운 랠리가 기다려요!','각도를 바꿔요. 준비.','박자를 느끼고, 플레이!','공간을 보고, 준비.','함께 일어서요. 준비.'][index],'skill_alt':['지금, 리듬을 터뜨려!','거리를 접는다!','이 라인은 내 거야!','코트를 밝혀라!','흔들리지 않는 집중!','숨을 고르고, 회전!','정확한 한순간!','네트를 장악해!','랠리를 불태워!','완벽한 마무리!','이 파도를 잡아!','새 각도를 연다!','리듬을 이어가!','모든 빈틈을 찾아!','당당하게, 자유롭게!'][index],'win_alt':['우리의 리듬을 찾았네요. 고마워요!','멋진 랠리였어요. 즐거웠어요!','좋은 승부였어요. 고마워요.'][index%3],'lose_alt':['많이 배웠어요. 다시 해봐요!','멋진 플레이네요. 다음엔 준비할게요.','좋은 배움이었어요. 고마워요.'][index%3]}[cue]]])
                rows.append([f'{character["id"]}.{cue}','Voice',file,.75,0,.04,'Flite-CMU-'+voices[character['gender']]+'-alternate-script'])
        for cue,text in {'fault':'Fault.','double_fault':'Double fault.','let':'Let. Serve again.','point':'Point.','out':'Out.','game':'Game.','set':'Game and set.','match':'Game, set and match.','deuce':'Deuce.','match_point':'Match point.'}.items():
            file=f'umpire_{cue}.wav';subprocess.run([str(a.flite.resolve()),'-voice','awb','-t',text,'-o',str(a.output/file)],check=True,stdout=subprocess.DEVNULL)
            captions.extend([['umpire.'+cue,'en',text],['umpire.'+cue,'ko',{'fault':'폴트','double_fault':'더블 폴트','let':'렛. 다시 서브하세요.','point':'포인트','out':'아웃','game':'게임','set':'게임 앤드 세트','match':'게임, 세트 앤드 매치','deuce':'듀스','match_point':'매치 포인트'}[cue]]])
            rows.append(['umpire.'+cue,'Voice',file,.8,0,.02,'Flite-CMU-awb'])
        for candidate,voice in [('a','slt'),('b','rms'),('c','awb')]:
            subprocess.run([str(a.flite.resolve()),'-voice',voice,'-t','Ready to play. This is my moment! A great match. Thank you.','-o',str(a.output/f'voice_{candidate}.wav')],check=True,stdout=subprocess.DEVNULL)
    with (a.output/'audio.csv').open('w',newline='') as f:
        writer=csv.writer(f,lineterminator='\n');writer.writerow(['cue','group','file','volume','loop','fade','source']);writer.writerows(rows)
    with (a.output/'subtitles.csv').open('w',newline='') as f:
        writer=csv.writer(f,lineterminator='\n');writer.writerow(['cue','language','text']);writer.writerows(captions)
    (a.output/'synthesis-report.json').write_text(json.dumps(stats,indent=2))
    print('Generated',len(rows),'cues at',a.output)
if __name__=='__main__':main()
