# 학습 전 실제 Unity 코호트

이 단계는 기존 번들 배치 PPO를 교체하기 전에 실제 `SinglePlayerCourt`에서 측정한다. 기존 `software-followup`과 학습 AI 검수 자료는 그대로 보존한다. 모델·캐릭터·게임플레이·규칙 CSV를 조정하지 않는다.

```sh
python3 -m unittest discover -s tools -p test_ai_full_match.py
bash tools/unity.sh check
python3 tools/verify-ai-full-match.py --run-baseline
python3 tools/verify-ai-full-match.py
```

Unity 6000.3.26f1이 필요하며 실행 전에 해당 프로젝트의 Editor를 닫는다. `tools/unity.sh`가 사용자 설정과 분리된 `.work/test-*.csv`를 복사해 사용한다. 로컬 모델 override가 있거나 기본 CSV와 다르면 측정을 중단한다. 일반 사용자 플레이에서는 opt-in 테스트가 실행되지 않는다.

15명의 원래 순서에서 Near `i`와 Far `(i+1)%15`를 짝짓는다. 각 선수는 Near와 Far 역할에 각각 등장하고, 각 역할마다 Easy/Medium/Hard × 고정 시드17/4016의6사례를 가진다. 총90사례이며225개 순서쌍 전체를 대표한다고 주장하지 않는다.

각 사례는 실제 두 서비스 게임까지 진행한다. 기본 best-of-3,6게임 세트,6–6에서7점 타이브레이크 규칙을 유지한다. 이 측정에서 세트나 매치 완료를 주장하지 않는다. Near는 기존 검수와 같은 중앙 지향 정규화 타격 fixture이며 Far는 기존 번들 모델이다. 서브·타격·스킬은 기존 컨트롤러 입력과 승인 gate를 거치고, 실제 authoritative BallFlight와 TennisMatchScore만 공·포인트·게임을 진행한다. 점수·재고·공 상태를 주입하지 않는다. timeScale8은 테스트 실행 속도만 변경한다. 프레임 타이밍에 따른 차이가 있어 고정 시드를 프레임 단위 결정성 보증으로 해석하지 않는다.

`docs/evidence/ai-full-match/baseline.json`은 선수별 Near/Far·매치업별·난이도별 집계와 소스·모델·CSV SHA-256을 담는다. `baseline-raw.json`은 각 사례의 실제 포인트 누적, 게임/세트, 서브 주체, 물리적 코트 끝, 교대 수, 양측 서브·타격, AI 승인/거절,6개 모델 액션 분포, gate 결과 분포, 공 이벤트 분포와 물리 tick을 보존한다. `swingResults` 순서는 `SwingResult` enum(Hit,Duplicate,Invalid,Stale,WrongPhase,OutOfReach,WrongSide,Height,NoBall,NotAuthority), `ballEvents`는 Bounce/Out/Net/예약0이다. `baseline-tests.xml`과 `baseline-unity.log`는 실제 Unity 테스트 결과와 사례 완료 로그다. 전체 로컬 로그는 `.work/logs/test-ai-baseline.log`다.

검증기는90개 유일한 예상 cell과 양쪽 서비스 게임, 교대,1점 단위 누적, 중복 rally, 입력/액션 합계, 유한·정수 카운터, 증거 해시, 측정 전후 소스·모델·밸런스 불변성을 검사한다. 검증기 자체의 음성 테스트는 합성 fixture임을 명시하며 실제 경기 증거로 합산하지 않는다. 해시는 증거의 변경 검출용이며 사람 승률·AI 개선·전체 경기 강화학습의 증명이 아니다.

하드웨어·Windows·WAN 검수는 하지 않는다. NU40-DK+별도 외장 IMU와 최초12일·3명 계획을 유지한다. 새로운 학습, 자기대전, 재개 학습, Mac 일반 창 검수와 PR 생성은 이 baseline 단계의 결과에 포함하지 않는다.
