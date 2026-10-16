#!/usr/bin/env python3
"""Analyze opt-in match telemetry; never rewrites balance or claims human win rates."""
import argparse,csv,json,statistics
from collections import Counter,defaultdict
from pathlib import Path
p=argparse.ArgumentParser();p.add_argument('--metrics',type=Path,nargs='+',default=[]);p.add_argument('--characters',type=Path,default=Path('Assets/StreamingAssets/Balance/characters.csv'));p.add_argument('--out',type=Path,required=True);a=p.parse_args()
a.out.mkdir(parents=True,exist_ok=True)
ratings=['serve','smash','volley','defense','stamina','speed'];roster=list(csv.DictReader(a.characters.open(encoding='utf-8-sig')))
summary={'scope':'Recorded scripted/actual input only; no estimate of human win rate. Ratings are fictional individuals, not national traits.','ratingBudget':{},'points':0,'rallyMedian':0,'rallyP95':0,'swingResults':{},'perCharacter':{},'performance':[],'warnings':[]}
for r in roster:
 total=sum(float(r[k]) for k in ratings);summary['ratingBudget'][r['id']]=total
 if total!=420:summary['warnings'].append(r['id']+' total differs from initial 420; verify intentional tuning')
rallies=[];results=Counter();by=defaultdict(Counter)
for directory in a.metrics:
 for path in directory.rglob('points.csv'):
  for row in csv.DictReader(path.open()):rallies.append(int(row['rally_hits']))
 for path in directory.rglob('swings.csv'):
  for row in csv.DictReader(path.open()):results[row['result']]+=1;by[row['character']][row['result']]+=1
 for path in directory.rglob('performance.json'):summary['performance'].append({'file':str(path),**json.loads(path.read_text())})
summary['points']=len(rallies)
if rallies:
 ordered=sorted(rallies);summary['rallyMedian']=statistics.median(ordered);summary['rallyP95']=ordered[min(len(ordered)-1,int(len(ordered)*.95))]
summary['swingResults']=dict(results);summary['perCharacter']={k:dict(v) for k,v in by.items()}
if not rallies:summary['warnings'].append('No authoritative point samples; do not infer balance from ratings alone')
if len(by)<15:summary['warnings'].append('Not all 15 characters were played; full roster human balance remains an iteration task')
(a.out/'balance-report.json').write_text(json.dumps(summary,ensure_ascii=False,indent=2)+'\n')
lines=['# 밸런스 측정 결과','','점수 기록 '+str(len(rallies))+'개 · 랠리 타격 중앙값 '+str(summary['rallyMedian'])+' · P95 '+str(summary['rallyP95']),'','| 캐릭터 | 입력 결과별 횟수 |','| --- | --- |']
lines += ['| '+r['id']+' | '+str(dict(by[r['id']]))+' |' for r in roster]
lines += ['','기록은 검증 스크립트/일반 입력의 결과이며 사람 대 사람 승률이 아닙니다. 기본 능력치 합계는 전원 420입니다. 실제 CSV 수치를 변경한 뒤 동일 시드·입력으로 비교하고 사람의 플레이 결과를 함께 검토하세요.','','## 확인할 항목','']+['- '+w for w in summary['warnings']]
(a.out/'balance-report.md').write_text(('\n'.join(lines) if summary['warnings'] else '\n'.join(lines[:-3])).rstrip()+'\n');print(json.dumps({'points':len(rallies),'swings':sum(results.values()),'out':str(a.out)}))
