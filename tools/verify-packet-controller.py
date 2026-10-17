#!/usr/bin/env python3
"""Actual GPU packet→SDK→UI→game check. The radio is explicitly a MOCK fixture."""
import argparse,hashlib,html,json,pathlib,shutil,struct,subprocess
p=argparse.ArgumentParser();p.add_argument('--out',type=pathlib.Path,default=pathlib.Path('.work/packet-controller'));p.add_argument('--player',type=pathlib.Path);a=p.parse_args();root=pathlib.Path(__file__).resolve().parents[1];out=a.out.resolve();out.mkdir(parents=True,exist_ok=True)
player=(a.player or root/'Builds/verification/mac/DigitalTennis.app/Contents/MacOS/Digital Tennis').resolve()
for old in out.glob('*.png'):old.unlink()
for name in ('result.json','failure.txt'):
    old=out/name
    if old.exists():old.unlink()
shutil.copytree(root/'Assets/StreamingAssets/Audio',out/'audio',dirs_exist_ok=True,ignore=shutil.ignore_patterns('*.meta'))
command=[str(player),'-batchmode','-force-metal','-screen-width','1600','-screen-height','900','-dt-controller-packet-verify',str(out),'-dt-audio-file',str(out/'audio/audio.csv'),'-dt-audio-validation','-logFile',str(out/'player.log')]
for name,source in [('rules','Balance/rules.csv'),('gameplay','Balance/gameplay.csv'),('balance','Balance/characters.csv'),('effects','Effects/effects.csv')]:
    destination=out/(name+'.csv');shutil.copyfile(root/'Assets/StreamingAssets'/source,destination);command+=['-dt-'+name+'-file',str(destination)]
run=subprocess.run(command,timeout=180,stdout=subprocess.DEVNULL,stderr=subprocess.DEVNULL)
assert run.returncode==0,('Player failed',run.returncode,(out/'failure.txt').read_text() if (out/'failure.txt').exists() else 'See player.log')
report=json.loads((out/'result.json').read_text());images=sorted(out.glob('*.png'));log=(out/'player.log').read_text(errors='replace')
assert report['passed'] and report['errors']==0 and report['captures']==14 and len(images)==14
assert report['nearHits']>0 and report['farHits']>0 and report['points']>0 and report['nextServe'] and report['pauseOnDisconnect']
assert 'Metal' in log,'Actual Metal GPU proof required'
for path in images:
    data=path.read_bytes();assert data[:8]==b'\x89PNG\r\n\x1a\n' and struct.unpack('>II',data[16:24])==(1600,900)
paths=[*root.glob('Assets/Scripts/**/*.cs'),root/'Packages/manifest.json',root/'Packages/packages-lock.json']
receipt={'sourceHashes':{str(path.relative_to(root)):hashlib.sha256(path.read_bytes()).hexdigest() for path in paths},'playerHash':hashlib.sha256(player.read_bytes()).hexdigest(),'imageHashes':{path.name:hashlib.sha256(path.read_bytes()).hexdigest() for path in images},'scope':report['scope']}
(out/'receipt.json').write_text(json.dumps(receipt,indent=2)+'\n')
(out/'index.html').write_text('<!doctype html><meta charset="utf-8"><title>Controller packet review</title><style>body{font:18px system-ui;background:#101b25;color:white;max-width:1200px;margin:40px auto}img{width:100%}figure{margin:30px 0}</style><h1>컨트롤러 패킷·화면 검증</h1><p>'+html.escape(report['scope'])+'</p>'+''.join('<figure><figcaption>'+html.escape(path.stem)+'</figcaption><img src="'+html.escape(path.name)+'"></figure>' for path in images))
print(json.dumps(report,ensure_ascii=False))
