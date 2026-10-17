#!/usr/bin/env python3
"""Opt-in actual Unity camera and input-port survey; mock pairing is explicitly labelled."""
import argparse,hashlib,html,json,plistlib,shutil,subprocess,time
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
p=argparse.ArgumentParser(description=__doc__);p.add_argument('--player',type=Path,default=ROOT/'Builds/verification/mac/DigitalTennis.app/Contents/MacOS/Digital Tennis');p.add_argument('--out',type=Path,default=ROOT/'.work/controller-ui');a=p.parse_args()
app=a.player.resolve();out=a.out.resolve();out.mkdir(parents=True,exist_ok=True)
with (app.parents[1]/'Info.plist').open('rb') as stream:assert plistlib.load(stream)['CFBundleIdentifier']=='com.digitaltennis.validation','Keep the human game separate'
for old in out.glob('*.png'):old.unlink()
(out/'result.json').unlink(missing_ok=True)
audio=out/'audio';shutil.copytree(ROOT/'Assets/StreamingAssets/Audio',audio,dirs_exist_ok=True,ignore=shutil.ignore_patterns('*.meta'))
args=[str(app),'-batchmode','-screen-fullscreen','0','-screen-width','1600','-screen-height','900','-dt-controller-ui-verify',str(out),'-dt-audio-validation','-dt-audio-file',str(audio/'audio.csv'),'-logFile',str(out/'player.log')]
for name,flag in [('characters','balance'),('gameplay','gameplay'),('rules','rules')]:
 target=out/(name+'.csv');shutil.copyfile(ROOT/('Assets/StreamingAssets/Balance/'+name+'.csv'),target);args+=['-dt-'+flag+'-file',str(target)]
shutil.copyfile(ROOT/'Assets/StreamingAssets/Effects/effects.csv',out/'effects.csv');args+=['-dt-effects-file',str(out/'effects.csv')]
started=time.time();child=subprocess.Popen(args,stdout=subprocess.DEVNULL,stderr=subprocess.STDOUT)
try:code=child.wait(timeout=180)
except subprocess.TimeoutExpired:
 child.terminate()
 try:child.wait(timeout=10)
 except subprocess.TimeoutExpired:child.kill();child.wait()
 raise SystemExit('Controller UI survey timed out; only its isolated player was stopped')
assert code==0,(code,str(out/'player.log'))
r=json.loads((out/'result.json').read_text());assert r['passed'] and r['errors']==0 and r['rosterFlags']==15 and r['nearHits']>0,r
images=sorted(out.glob('*.png'));assert len(images)==r['captures']==24
assert 'Initializing Metal device caps:' in (out/'player.log').read_text()
for path in images:assert path.stat().st_mtime>started
sha=lambda path:hashlib.sha256(path.read_bytes()).hexdigest()
# Unity Test Runner may be active independently; its ignored bootstrap scene is not project source.
sources=[p for folder in ['Assets','Packages','ProjectSettings'] for p in (ROOT/folder).rglob('*') if p.is_file() and not p.name.startswith('InitTestScene')]
receipt=dict(scope=r['scope'],sources=[dict(path=str(p.relative_to(ROOT)),sha256=sha(p)) for p in sorted(sources)],images=[dict(path=p.name,sha256=sha(p)) for p in images],reportSha256=sha(out/'result.json'),player=[dict(path=str(p),sha256=sha(p)) for p in [app,app.parents[1]/'Resources/Data/Managed/DigitalTennis.Runtime.dll',app.parents[1]/'Resources/Data/resources.assets']])
(out/'receipt.json').write_text(json.dumps(receipt,indent=2)+'\n')
page=['<!doctype html><meta charset="utf-8"><title>Flags, country borders and input controller settings</title><style>body{background:#071520;color:#edf5ee;font:18px system-ui;max-width:1400px;margin:auto;padding:30px}img{max-width:100%}a{color:#d8f879}section{margin:45px 0}</style><h1>국기 · 국가 경계 · 컨트롤러 설정</h1><p>Unity 6000.3.26f1의 실제 게임 카메라 원본입니다. MOCK 표시는 연결 서비스와 입력 경로의 모의 검증이며 실제 BLE/OS 페어링 성공이 아닙니다. 정상 게임에서는 센서 연결 준비 중 상태를 표시합니다.</p>']
for image in images:page.append(f'<section><h2>{html.escape(image.stem)}</h2><a href="{image.name}"><img loading="lazy" src="{image.name}"></a></section>')
(out/'index.html').write_text('\n'.join(page))
print('PASS: 15 country flags, geographic borders, settings/selection, labelled mock discovery/connect/disconnect and actual adapter-driven rally; runtime errors0')
