# 실제 Unity 전체 경기 PPO 학습

학습 전 90사례는 `ai-full-match-baseline.md`와 기존 증거에 보존한다. 이 단계는 그 측정 후 실행하며 `software-followup` 자료를 덮어쓰지 않는다.

`FullMatchTrainingTests.AuthoritativeGymBridge`는 명시적인 `-dt-gym-port` 인자가 있을 때만 loopback TCP를 연다. 일반 실행·빌드에는 테스트 브리지가 없다. Python Gymnasium은 관측값을 받고 여섯 샷 배치 액션을 전송한다. Python에는 공 물리·착지 판정·점수 계산 환경이 없다. 실제 `SinglePlayerCourt`, authoritative `BallFlight`, `TennisMatch`, `TennisMatchScore`가 포인트·게임·세트·매치를 만든다. 기본 best-of-3, 6게임 세트, 6–6에서 7점 타이브레이크를 검사한다. `Winner`가 있어야 terminal이며 완료되지 않은 경기를 reset할 수 없다. 예산 끝의 부분 경기는 완료 수에서 제외한다.

관측 버전2는 32개 값이다. 공 위치/속도, 상대/본인 위치, 샷 배율/중력, 본인/상대 포인트·게임·세트, 서버, 타이브레이크, phase, bounce, 양측 에너지·재고·buff 시간·선수 index, 본인의 물리적 Far 여부, fault가 포함된다. 좌표는 본인 baseline이 +z인 정규화 좌표이며 점수·에너지 등은 본인/상대 순이다. `FullMatchActor.Fields`와 normalization scale이 계약이다. 입력은 유한한 float32, 정확히32개이며 고정 scale과 [-2,2] clamp를 사용한다. 통계 기반 normalization의 평가 중 갱신은 없다.

액션 버전1은 폭 {-1,0,+1} × 깊이 {.55,1}의 6개 배치다. PPO는 시뮬레이션 0.1초마다 배치를 선택하며 실제 타격 시점에 최신 액션을 사용한다. 이동은 기존 AutomaticCourtMovement, 반응·forehand/backhand·속도 해법은 기존 seeded AiPolicy, 서브와 스킬은 기존 일반 PlayerAction이다. 학습 대상은 경기 상태에 조건화된 샷 전술이며 모든 motor action을 학습했다고 주장하지 않는다. Near의 타격도 RacketInput/SubmitInput gate를 통과하며 시도·승인·거절을 그대로 기록한다. stock·ball·score를 주입하지 않는다. Far는 기존 번들 placement PPO와 Medium 실행기를 고정해 사용한다. 훈련 중 Far 업데이트는 없다.

보상 버전1은 실제 본인−상대 포인트, 완료 게임, 세트 증가와 최종 승패 및 거절 시도 수의 변화다. 가중치·학습률·gamma·GAE·PPO clip·epoch·rollout·batch·seed·예산은 AI 저장소의 `Training/full-match.csv`에서 읽으며 알 수 없는 key, 중복/누락, NaN/Inf, 범위·정수·batch 불일치를 거절한다. gamma=.995로 장기 경기를 학습한다. 65536 step, 적어도4 rollout update와2 실제 완료 매치가 초기 실행 조건이다. 완료가 부족하면 rollout 단위로 연장한다. 승률 향상 임계치는 없다.

```sh
# --ai는 feature branch의 독립 AI checkout이다. venv는 Git에 넣지 않는다.
python3 tools/train-ai-full-match.py --python <pinned-venv-python> --ai <ai-checkout>
python3 tools/finalize-ai-training.py --ai <ai-checkout>
python3 tools/verify-ai-training.py
bash tools/unity.sh check
bash tools/unity.sh test-edit
bash tools/unity.sh test-play
```

AI checkout에서 pinned `Training/requirements-lock.txt`를 설치한다. 독립 검사 명령은 `dotnet run --project tests/DigitalTennis.AI.Checks.csproj --configuration Release`와 `python -m unittest discover -s tests -p test_full_match.py`다. 소형 actor는 `Runtime/Resources/DigitalTennisAI/full-match-ppo.json`, Python/C# 64사례 정규화·logit parity는 `full-match-parity.json`이다. C# actor는 netstandard2.1이며 Python·network·UnityEngine 의존성이 없다. 기존 placement 모델은 유지한다. 훈련 bridge의 Near 실행은 전달된 액션을 사용하고 추론 actor와 같은 placement 계약을 사용한다.

`docs/evidence/ai-full-match/training.json`은 실제 step·경기별 seed/선수·포인트·게임·세트·경기 완료·거절·physics tick·loss·reward·update·library version·초기/최종 weight hash를 기록한다. `training-raw.jsonl`은 reset과 실제 포인트/타격/terminal 변화를 보존한다. `training-tests.xml`은 실제 Unity 브리지 테스트 결과다. checkpoint·venv·전체 Unity 로그는 `.work`에만 둔다. 결과는 학습 궤적이며 held-out 평가나 사람 승률·개선의 증거가 아니다.

이 단계는 자기대전·재개 학습·일반 Mac 창 검수·PR 생성을 포함하지 않는다. NU40-DK+별도 외장 IMU, 최초12일/3명 계획, Apache-2.0와 외부 라이선스를 유지한다. 물리 장치·Windows·WAN 검증은 별도다.

실행 결과: 65,536 step, 32 rollout update(128 PPO epoch), 실제 기본 규칙 매치8개/게임96개/세트16개를 완료했다. 완료8경기는 Near가 모두2–0으로 이겼다. Near는 Hard 실행기, Far는 고정 Medium이므로 이 결과는 학습 성능 개선이나 대등한 난이도 승률의 증거가 아니다. 기록된 전체500포인트에는 마지막 부분 경기의 포인트도 포함된다. 원본에서 완료 매치의 점수·reward·seed·terminal·step 수를 별도로 대조한다. 모델 초기/최종 weight hash가 다르고 loss는 유한하다. 실제 입력 거절은 training.json의 각 final 및 부분 경기 상태를 참조한다.

Unity 의존성은 공개 feature SHA `550f04ab831165b79ec8cb1f936d838400862b24`에 고정했다. 아직 main merge는 없다. 후속 PR이 승인되어 squash-merge되면 최종 squash SHA로 게임 pin을 다시 갱신해야 한다.

검증: Git feature pin에서 EditMode208개 통과, PlayMode46개 통과/실패0/의도적 제외4개(렌더링 pointer UI, baseline, 학습 bridge,9사례 opt-in 평가), 독립 .NET30개 검사와 Python 계약5개·증거2개 검사가 통과했다. 학습 bridge는 별도 실제 실행의 `training-tests.xml`에서1개 통과했다. PlayMode 전체 명령은 종료코드0이며 NUnit 최상위 결과는 제외 사례 때문에 `Skipped:Ignored`다. 일반 Mac 창이나 pointer UI를 이번 결과로 검증했다고 주장하지 않는다.
