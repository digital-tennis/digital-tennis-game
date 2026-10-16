#!/usr/bin/env python3
"""Compare paired production-model experiments. Never changes tuning or infers human win rates."""
import argparse,csv,json,statistics
from collections import defaultdict
from pathlib import Path
p=argparse.ArgumentParser();p.add_argument('--before',type=Path,required=True);p.add_argument('--after',type=Path,required=True);p.add_argument('--out',type=Path,required=True);a=p.parse_args()
before=json.loads(a.before.read_text());after=json.loads(a.after.read_text());a.out.mkdir(parents=True,exist_ok=True)
key=lambda r:tuple(r[k] for k in ['near','far','difficulty','strategy','seed','firstServer'])
assert [key(r) for r in before['results']]==[key(r) for r in after['results']]
assert all(d['deterministic'] and d['timeouts']==0 and d['cases']==12150 and d['points']==48600 for d in [before,after])
def summary(d):
 rs=d['results'];result={'cases':d['cases'],'points':d['points'],'timeouts':d['timeouts'],'deterministic':d['deterministic'],'gameplayHash':d['gameplayHash'],'characterHash':d['characterHash'],'hits':sum(r['hits'] for r in rs),'skills':sum(r['skills'] for r in rs),'strokeCounts':{str(s):sum(r['strokes'][s] for r in rs) for s in range(1,7)},'endingEnergyUnderOne':sum(r[k]<1 for r in rs for k in ['nearEnergy','farEnergy'])/(len(rs)*2),'meanEndingEnergy':statistics.mean(r[k] for r in rs for k in ['nearEnergy','farEnergy'])}
 result['aiPointShare']={level:sum(r['farWins'] for r in rs if r['difficulty']==level)/sum(r['points'] for r in rs if r['difficulty']==level) for level in ['Easy','Medium','Hard']}
 return result
b,c=summary(before),summary(after);report={'scope':after['scope'],'before':b,'after':c,'changes':{'energy.hit_cost':[4,3.2],'energy.move_cost':[.8,.72]},'limitations':['Scripted input cases, not human win rates or a proof of competitive balance.','External AI policy remains pinned; its shot choices are forehand/backhand. The local fixtures cover volleys, slices and lobs. Smash is verified separately in the live motion survey.','Fixed-step detached models omit frame/transport timing and ball-boy presentation; native integration is a separate check.','Six-rating character budgets remain 420 because this sample does not show a reliable reason to redistribute them.']}
(a.out/'matchup-comparison.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n')
rows=[]
for near in dict.fromkeys(r['near'] for r in after['results']):
 for far in dict.fromkeys(r['far'] for r in after['results']):
  for difficulty in ['Easy','Medium','Hard']:
   cells=[r for r in after['results'] if r['near']==near and r['far']==far and r['difficulty']==difficulty];rows.append([near,far,difficulty,len(cells),sum(r['points'] for r in cells),sum(r['nearWins'] for r in cells),sum(r['farWins'] for r in cells),sum(r['hits'] for r in cells),sum(r['skills'] for r in cells)])
with (a.out/'matchup-summary.csv').open('w',newline='') as f:w=csv.writer(f,lineterminator='\n');w.writerow(['near','far','difficulty','cases','points','near_points','far_points','hits','skills']);w.writerows(rows)
lines=['# 전체 조합 밸런스 측정과 CSV 조정','','225개 순서쌍 × 난이도3 × 시드3(17/4016/9001) × 입력 전략3 × 선공2. 전후 각각12,150사례·48,600개 실제 판정 포인트. 동일 조건 재실행 일치, 제한시간 초과0.','','| 지표 | 변경 전 | 변경 후 |','| --- | --- | --- |',f"| 종료 체력1 미만 비중(양쪽 합산) | {b['endingEnergyUnderOne']:.1%} | {c['endingEnergyUnderOne']:.1%} |",f"| 평균 종료 체력 | {b['meanEndingEnergy']:.2f} | {c['meanEndingEnergy']:.2f} |"]
for level in ['Easy','Medium','Hard']:lines.append(f"| AI {level}의 포인트 점유율 | {b['aiPointShare'][level]:.1%} | {c['aiPointShare'][level]:.1%} |")
lines+=['','체력이 네 포인트 이내에 바닥에 도달하는 사례를 줄이기 위해 `gameplay.csv`의 `energy.hit_cost`를4→3.2, `energy.move_cost`를0.8→0.72로 조정했습니다. 캐릭터 능력치 합420, 실력/스킬/난이도 계수는 유지했습니다. 강한 캐릭터를 추정해서 임의로 약화시키지 않았습니다.','','로컬 입력 전략은 배치(시도 확률0.92/대기0.05초), 전진 혼합(0.96/0.02초), 신중 혼합(0.84/0.16초)입니다. 누락은 해당 공에 실제로 입력하지 않는 것이며 점수는 공/규칙 이벤트만이 만듭니다. 입력 시드와 정책은 비교 전후 동일합니다. CSV를 바꾼 뒤 실게임 회귀도 별도로 수행합니다.','','## 해석 범위','']+['- '+x for x in report['limitations']]+['','원시 사례는 `.work/quality/balance-controlled-{baseline,tuned}/`에 저장합니다. 공개 요약 CSV는675행입니다. 재현은 `BalanceExperiment.Run`을 전후 CSV에서 각각 실행하고 `tools/compare-matchups.py --before .../experiment.json --after .../experiment.json --out docs/reports`로 비교합니다.']
(a.out/'matchup-comparison.md').write_text('\n'.join(lines)+'\n');print(json.dumps(report,ensure_ascii=False))
