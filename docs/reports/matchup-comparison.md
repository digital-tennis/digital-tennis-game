# 전체 조합 밸런스 측정과 CSV 조정

225개 순서쌍 × 난이도3 × 시드3(17/4016/9001) × 입력 전략3 × 선공2. 전후 각각12,150사례·48,600개 실제 판정 포인트. 동일 조건 재실행 일치, 제한시간 초과0.

| 지표 | 변경 전 | 변경 후 |
| --- | --- | --- |
| 종료 체력1 미만 비중(양쪽 합산) | 21.4% | 14.7% |
| 평균 종료 체력 | 46.62 | 55.31 |
| AI Easy의 포인트 점유율 | 3.3% | 3.3% |
| AI Medium의 포인트 점유율 | 18.1% | 18.1% |
| AI Hard의 포인트 점유율 | 59.7% | 59.7% |

체력이 네 포인트 이내에 바닥에 도달하는 사례를 줄이기 위해 `gameplay.csv`의 `energy.hit_cost`를4→3.2, `energy.move_cost`를0.8→0.72로 조정했습니다. 캐릭터 능력치 합420, 실력/스킬/난이도 계수는 유지했습니다. 강한 캐릭터를 추정해서 임의로 약화시키지 않았습니다.

로컬 입력 전략은 배치(시도 확률0.92/대기0.05초), 전진 혼합(0.96/0.02초), 신중 혼합(0.84/0.16초)입니다. 누락은 해당 공에 실제로 입력하지 않는 것이며 점수는 공/규칙 이벤트만이 만듭니다. 입력 시드와 정책은 비교 전후 동일합니다. CSV를 바꾼 뒤 실게임 회귀도 별도로 수행합니다.

## 해석 범위

- Scripted input cases, not human win rates or a proof of competitive balance.
- External AI policy remains pinned; its shot choices are forehand/backhand. The local fixtures cover volleys, slices and lobs. Smash is verified separately in the live motion survey.
- Fixed-step detached models omit frame/transport timing and ball-boy presentation; native integration is a separate check.
- Six-rating character budgets remain 420 because this sample does not show a reliable reason to redistribute them.

원시 사례는 `.work/quality/balance-controlled-{baseline,tuned}/`에 저장합니다. 공개 요약 CSV는675행입니다. 재현은 `BalanceExperiment.Run`을 전후 CSV에서 각각 실행하고 `tools/compare-matchups.py --before .../experiment.json --after .../experiment.json --out docs/reports`로 비교합니다.
