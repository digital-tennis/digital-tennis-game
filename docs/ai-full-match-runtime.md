# 전체 경기 actor의 일반 게임 연결

`TennisAiOpponent`가 `FullMatchPolicyAdapter`를 통해 실제 경기의 32개 관측을
독립 `DigitalTennis.AI.FullMatchActor`에 전달한다. 번들 `FullMatchAI/resumed-ppo`
모델은 실제 학습·고정 상대 self-play·재개 후 145408 step의 actor와 동일하다.
게임 실행에 Python, 서버 또는 학습 프로세스는 필요하지 않다.

학습 범위는 경기 상태에 따른 6개 코스·깊이 전술이다. 이동 예측, 서브,
반응 지연, 타격 시점·세기 계산, 스킬 사용 결정은 기존 결정적 실행 계층이
맡는다. 모든 타격은 SubmitInput, 스킬과 서브는 일반 PlayerAction으로 처리한다.
도달 거리·중복·오래된 입력·스킬 재고 검사를 우회하지 않는다.

`persistentDataPath/AI/full-match-ppo.json`은 다음 AI 생성 시 적용한다.
버전, 모양, 유한성, 값 범위, 최대 파일 크기를 검증한다. 잘못된 교체 파일은
번들 actor로, 잘못되거나 없는 번들은 명시적으로 표시된 기본 AI로 대체한다.
이전 placement 모델 교체 파일은 전체 경기 모델로 해석하지 않는다.

`ResumedActorTests`는 고정 64개 Torch 추론과 C# logits/정규화/행동 일치 및
변형 모델 거부를 검사한다. `FullMatchRuntimeTests`는 별도 점수/공/재고 쓰기 없이
일반 입력 어댑터로 기본 규칙 전체 경기, 실제 서브/반환/득점/코트 교대/종료와
재시작, pause·online 격리를 검사한다. 자동 Near fixture는 사람 승률 증거가 아니다.
기존 입력 설정·menu·스킬·온라인 회귀 검사는 전체 Unity suite로 확인한다.

Mac 일반 창은 수동 키보드 입력과 수동 관찰로 별도 확인한다. 화면은 게임만
캡처한다. 하드웨어, Windows, WAN 검사는 수행하지 않는다. NU40-DK와 별도 외장
IMU, 최초 12일·3명 계획과 라이선스는 유지한다.

## 검증과 실행 증거

이번 통합에서 독립 AI 30검사, Python AI 환경/모델/checkpoint 20검사와 게임
증거/checkpoint 41검사, EditMode 210검사를 통과했다. PlayMode는 48통과,
0실패, 별도 실행이 필요한 opt-in 검사 6개 제외다. 추가 전체 경기 scene 검사는
기본 규칙으로 79점, AI 승인 타격 150회/거부 0회, AI 서브 40회, end change
7회 후 Far의 2세트 승리와 일반 어댑터 재시작을 확인했다. 이 결과는 지정된
자동 Near fixture의 결과이며 인간 상대 승률이 아니다.

Mac 빌드를 완료했다. 일반 창을 시작했지만 컴퓨터 사용 도구가 시스템 잠금을
보고했고 validation frame도 생성되지 않아 그 프로세스를 종료했다. 시스템 잠금은
해제하거나 우회하지 않았다. 별도 GPU batch 실행은 실제 게임 카메라와 UI callback,
일반 입력 어댑터로 실행한다. 그 화면을 ordinary window 검수로 취급하지 않는다.
최종 수치와 캡처는 [runtime.json](evidence/ai-full-match/runtime.json)에 기록한다.
`passed: false`이면 자동 검사가 통과했더라도 전체 AC 검증 완료를 뜻하지 않는다.

```sh
bash tools/unity.sh check
bash tools/unity.sh test-edit
bash tools/unity.sh test-play
bash tools/unity.sh test-ai-runtime
bash tools/unity.sh build-mac
python3 tools/run-ai-runtime-mac.py --batch
python3 tools/record-ai-runtime.py --batch-input
python3 tools/verify-ai-runtime.py --automated-only
```

잠금 해제 뒤 일반 창은 `python3 tools/run-ai-runtime-mac.py`로 다시 실행한다.
실제 창을 보고 정상 표시와 진행을 확인한 뒤에만
`python3 tools/record-ai-runtime.py --window-inspected`와
`python3 tools/verify-ai-runtime.py`를 사용한다. 기본 verifier는 batch-only나
미완료 ordinary-window 증거를 거부한다.

별도 Mac GPU batch의 실제 완료 점수는 2–6, 2–6(세트 0–2), 총 86점이었다.
런타임 오류 0회, pause 유지와 rematch 초기화를 통과했다. Near는 첫 5점까지만
반환을 시도하는 기존 front-end fixture이므로 위 Medium scene fixture의 79점
결과와 비교해 실력 개선을 추론할 수 없다. `gameplay.png`, `runtime-results.png`,
`runtime-rematch.png`는 실제 카메라 캡처이며 합성 화면이나 점수 주입이 아니다.
