# 실제 Unity 동결 상대 풀 자기대전

이 단계는 `baseline.json`의 확장 pre-training 측정과 `training.json`의 전체 경기 PPO 학습을 읽고 시작한다. 이전 측정과 `software-followup` 자료는 수정하지 않는다. 제품의 AI 모델 pin과 일반 사용자 플레이는 바꾸지 않는다.

`FullMatchSelfPlayTests.AuthoritativeSelfPlayBridge`는 `-dt-self-play-port`가 있어야 실행되는 테스트 전용 loopback 브리지다. 실제 SinglePlayerCourt의 authoritative BallFlight/TennisMatch가 기본 best-of-3, 6게임 세트, 6–6의 7점 타이브레이크로 경기를 진행한다. Winner가 있어야 done이며 완료 전 reset을 거절한다. 점수·공·재고를 주입하지 않는다. 두 선수의 서브/Skill은 일반 PlayerAction, 타격은 SubmitInput이다. 시도·승인·거절을 양쪽 모두 기록한다.

테스트 fixture는 Practice에서 autoServe/demoReturns를 끄고 양쪽에 기존 AutomaticCourtMovement와 동일한 Hard AiPolicy 실행기를 사용한다. 일반 Practice의 데모 자동 타격은 사용하지 않는다. 학습 대상은 관측 버전2의32개 경기 상태 값에 따른 액션 버전1의6개 샷 배치다. 이동·서브·반응/스트로크 시점·power 해법·스킬 안전 실행기는 결정적 코드로 남는다. motor action 전체를 학습했다고 주장하지 않는다. 기존 SinglePlayerCourt Far AI의 이동 실행기와 다르므로 기존 비대칭 Hard/Medium 훈련 승률과 직접 비교하지 않는다.

AI 저장소의 `Training/self_play.py`는 기존 학습 checkpoint의 weight hash를 검사하고 PPO를 계속 학습한다. `Training/self-play.csv`는 초기32,768 step,8,192 step별 snapshot, 학습 및 평가 seed를 검증한다. PPO/보상 설정은 검증된 `Training/full-match.csv`를 재사용한다. 완료 경기2개, 양 역할 및 서로 다른 상대 해시가 확보될 때까지 rollout 단위로 연장한다. 예산 끝에 남은 경기는 동결 actor로 완료하며 finish-only step은 학습 step과 구분한다.

상대 풀은 학습 전 전체 경기 모델과 주기별 compact actor snapshot이다. snapshot은 독립 복사한 읽기 전용 numpy 배열이며 optimizer·통계 갱신·recurrent state가 없다. 경기 시작에 당시 풀의 `(episode-1) % poolSize`를 선택하고 경기 내내 고정한다. 다음 경기마다 학습자 Near/Far를 교대한다. 일정에는 사용 가능한 풀, 선택 상대 해시, seed, 선수, 역할과 step이 있다. 물리적 end change가 있어도 Near/Far identity와 wire 좌표는 바꾸지 않는다.

양쪽 AiPolicy는 서로 다른 seed의 random/반응/shot/tick 상태, session/event ID를 쓴다. 실제 완료 뒤 새 scene과 새 policy로 reset한다. random baseline도 물리적 양쪽에 별도 numpy Generator를 둔다. pause·cinematic·online·비권한·match done 상태에서는 fixture의 실행기를 호출하지 않는다. 브리지 자체의 reset 검사와 별도로 `FullMatchSelfPlayIsolationTests`는 진행 중 공의 pause/resume, 실제 타격 거절, 두 policy의 pause/online 비활성 상태를 검사한다.

held-out 평가는 학습을 호출하지 않는다. 최종 actor를 동결하고 학습 seed와 분리된913001 계열 seed, 학습 cyclic offset+2에 없는 선수 offset+7 조합으로 scripted/random/frozen 각2경기(학습자 Near/Far 각1회), 총6개 실제 전체 경기를 실행한다. scripted는 상대 X 부호에 따른 width와 deep placement를 쓰는 고정 heuristic이다. random은6개 배치를 균일하게 고른다. frozen baseline은 self-play 전 전체 경기 PPO다. 모든 상대의 이동·서브·반응·스킬 실행기는 동일하다. 평가 전후 weight hash와 상대 파일 해시를 대조한다. 표본 승패를 개선 보장·인간 승률로 해석하지 않는다.

재현 명령(아래 Python은 pinned requirements-lock.txt가 설치된 venv):

```sh
python3 tools/run-ai-self-play.py --python <venv-python> --ai <ai-feature-checkout>
python3 tools/compact-ai-self-play.py
bash tools/unity.sh test-play
python3 tools/finalize-ai-self-play.py
python3 tools/verify-ai-self-play.py
<venv-python> -m unittest discover -s <ai-feature-checkout>/tests -p test_self_play.py
dotnet run --project <ai-feature-checkout>/tests/DigitalTennis.AI.Checks.csproj --configuration Release
bash tools/unity.sh check
```

`self-play.json`과 `held-out.json`은 결과, 모델/소스/설정/원시 기록 hash를 포함한다. `self-play-pool/`은 동결 상대 compact 모델, `self-play-ppo.json`은 최종 모델이다. `*-raw.jsonl.gz`는 reset과 실제 point/attempt/terminal 전이를 손실 없이 압축한 기록이며 조용한 idle step은 생략한다. 보고서의 step은 전체 bridge step이다. `self-play-tests.xml`은 실제 브리지 NUnit 결과다. 증거의 `self_play.py`/`self-play.csv`는 실행한 AI 소스의 감사용 사본이며 정본은 AI feature checkout이다. venv/checkpoint/대형 Unity 로그는 `.work`에만 둔다. 기존 증거를 재실행으로 덮어쓰지 말고 별도로 보존해야 한다.

NU40-DK+별도 외장 IMU, 최초12일/3명 계획, Apache-2.0와 외부 라이선스를 유지한다. 이 AC에는 complete-state 재개 학습·일반 Mac 창 검증·PR 발행을 포함하지 않는다. 물리 장치·Windows·WAN 검증은 수행하지 않았다.

실행 결과: PPO133,120 step/65 rollout update/260 optimizer epoch에서 실제 전체 경기2개가 완료됐다. 진행 중 경기의45,729 step은 추가 학습 없이 완료해 전체 trajectory178,849 step/3경기/523포인트를 보존했다. 동결 풀에는 초기 모델과16개 snapshot이 있고 실제 완료 경기에서 서로 다른3개 상대 해시를 사용했다. held-out343,955 step/6경기/952포인트의 결과는 scripted1승/2경기, random0승/2경기, frozen2승/2경기다. 평가 optimizer update는0이며 전후 weight hash가 같다. 이 작은 표본은 개선의 보장이 아니다. 원시 기록은 gzip 손실 없는 왕복 대조 후 약1.7MB/3.0MB로 보존했다.

검증 결과: 실제 브리지 NUnit1개 통과, 전체 PlayMode47개 통과/실패0/의도적 제외5개다. 제외에는 opt-in 브리지와 렌더링 UI/코호트가 포함되며 최상위 NUnit 결과는 `Skipped:Ignored`다. 진행 중 공 pause/resume 및 양쪽 policy online 격리 테스트는 실제 통과했다. Python self-play 계약4개·증거 감사/손상 거절6개와 독립 .NET30개 검사가 통과했다. `self-play-regression-tests.xml`과 `self-play-verification.json`에 전체 회귀 및 소스 hash를 보존했다. 일반 Mac 창이나 물리 장치 검증으로 해석하지 않는다.
