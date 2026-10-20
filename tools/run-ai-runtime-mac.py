"""Run the opt-in normal Mac player fixture with isolated CSVs and real game captures.
This is callback/adapter automation; a separate ordinary-window inspection is required.
"""
import argparse,json,shutil,subprocess
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
p=argparse.ArgumentParser();p.add_argument('--timeout',type=int,default=1000);p.add_argument('--batch',action='store_true');args=p.parse_args()
out=ROOT/'.work/ai-runtime'/('mac-batch' if args.batch else 'mac');out.mkdir(parents=True,exist_ok=True)
player=ROOT/'Builds/mac/DigitalTennis.app/Contents/MacOS/Digital Tennis'
assert player.is_file(),'Run build-mac first'
cmd=[str(player),'-screen-fullscreen','0','-screen-width','1280','-screen-height','800','-logFile',str(out/'player.log'),'-dt-frontend-verify',str(out)]
if args.batch:cmd.insert(1,'-batchmode')
for name,flag in [('characters','balance'),('gameplay','gameplay'),('rules','rules')]:
 dest=out/(name+'.csv');shutil.copyfile(ROOT/'Assets/StreamingAssets/Balance'/dest.name,dest);cmd+=['-dt-'+flag+'-file',str(dest)]
dest=out/'effects.csv';shutil.copyfile(ROOT/'Assets/StreamingAssets/Effects/effects.csv',dest);cmd+=['-dt-effects-file',str(dest)]
(out/'result.json').unlink(missing_ok=True)
subprocess.run(cmd,check=True,timeout=args.timeout,start_new_session=True)
r=json.loads((out/'result.json').read_text());assert r['passed'],r
assert r['completedGame']['winner'] in ['host','guest']
assert r['completedGame']['nearHits']>0 and r['completedGame']['farHits']>0
assert r['pauseHeld'] and r['rematchReset'] and r['runtimeErrors']==0
print('PASS native Mac automated buttons/input adapter, default full match, pause, rematch and real game captures; batch:',args.batch)
