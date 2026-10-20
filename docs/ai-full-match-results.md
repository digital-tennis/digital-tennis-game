# 실제 전체 경기 AI 결과와 남은 검수

2026-10-21 실행 결과다. 기본 규칙은 best-of-3, 6게임 세트와 6–6의 7점 타이브레이크(마지막 세트 포함)다. 점수·게임·세트·완료는 Unity authoritative BallFlight/TennisMatch에서만 발생한다.

| 단계 | 실제 측정 | 해석·제한 |
| --- | --- | --- |
| 학습 전 확장 baseline | 15명 cyclic offset+1 × 3난이도 × 2시드 = 90사례, 972점, AI 승인 1,608/거부 0 | 사례별 2서비스게임, 225개 전체 대진이나 완료 매치 아님 |
| 전체 경기 PPO | 65,536 step, 32 rollout update/128 optimizer epoch, 완료 8경기·96게임·16세트, 부분 경기 포함 500점 | Hard Near 대 Medium Far, 완료 8경기 모두 Near 2–0. 대등한 승률·개선 증거 아님 |
| 동결 상대 self-play | 133,120 학습 step, 65 rollout update/260 epoch, 학습 중 2경기 완료 | 완료를 위해 초기 32,768 예산 연장. 추가 45,729 finish-only step은 학습 아님. 전체 3경기·523점 |
| 분리 평가 | 6경기·952점, scripted 1/2승, random 0/2승, frozen 2/2승 | 역할 교대, 학습과 분리된 seed/대진, update 0·전후 가중치 동일. 인간 승률·개선 보장 아님 |
| complete-state 재개 | 준비 4,096 step 후 trainer 종료·새 프로세스에서 8,192 step/4 rollout update/16 epoch 추가 | global step 137,216→145,408, 실제 46점. 같은 partial match이며 추가 완료 경기 0 |
| 일반 runtime 자동 검사 | EditMode 210, PlayMode 48통과/6제외, 별도 scene 1통과. scene 79점·AI 승인150/거부0·Far 2세트승 | 기존 fixture 상대이며 인간 평가 아님 |
| Mac GPU batch | 빌드·실제 카메라/UI/일반 입력, 86점·2–6/2–6, 오류0·pause/rematch 통과 | **일반 창 육안 검수는 시스템 잠금으로 미완료**. batch를 ordinary window로 세지 않음 |

학습은 관측 v2의32개 경기 상태에 따른 action v1의6개 코스·깊이 전술이다. actor는 32→32→32→6 tanh이며 고정 scale/clip 정규화를 사용한다. 이동·서브·반응·타격 시점/세기·스킬 안전 실행은 결정적 코드다. 모든 타격은 SubmitInput, 서브/Skill은 일반 PlayerAction과 실제 재고 gate를 통과한다. Near/Far identity는 end change에도 고정된다. pause/menu/online/비권한 상태에서 정책을 진행하지 않는다. 정상 추론은 독립 netstandard2.1 C#이며 Python/네트워크/API를 요구하지 않는다.

동결 풀은 초기 actor+16개 snapshot이고 경기 시작에 `(episode-1) % poolSize`를 선택해 경기 내내 고정한다. 학습자 Near/Far를 교대하며 평가에서 학습하지 않는다. CSV가 학습량·보상·PPO·snapshot 간격·seed를 검증한다. 모델은 버전/shape/finite/크기 검사 후 적용한다.

체크포인트는 actor/critic·Adam·SB3/Monitor/VecEnv·관측·counter·Python/NumPy/Torch/양쪽 RNG·고정 normalization·버전/설정/소스 hash·실제 상대 풀 bytes·schedule을 저장한다. fsync한 immutable ZIP 뒤 원자 pointer를 교체하고 deserialize 전 hash를 검증한다. 저장된 rollout 경계와 살아 있는 같은 Unity world/lease가 필요하다. **Unity 종료 후 world 복원은 지원하지 않는다**. 손상/환경 불일치는 실패시키고 직전 generation은 명시적으로만 선택한다. 로컬 신뢰 checkpoint만 읽으며 hash가 악성 pickle의 보안 경계라는 주장은 하지 않는다.

기존 software-followup 자료는 보존한다. 새 raw·작은 보고서·모델·캡처는 game의 `docs/evidence/ai-full-match/`에 있다. gzip raw는 손실 없는 기록이며 전체 idle step 생략 범위와 finish-only를 보고서에 구분한다. venv·전체 checkpoint·빌드·큰 로그는 무시되는 `.work/`/`Builds/`에 남긴다. 모델/설정/소스 SHA-256은 각 JSON에 있다. 당시 baseline과 재개의 `unity.sh`를 hash가 정확히 일치하는 감사 사본으로 보존하며, 이후 제품 통합으로 바뀐 파일을 당시 실행 소스로 오인하지 않는다.

NU40-DK+별도 외장 IMU, 최초12일·3명·36PD 계획은 그대로다. 이번 확장에서 하드웨어·Windows 실기·두 PC/WAN 검증은 수행하지 않았다. 자체 코드/문서/학습 가중치는 Apache-2.0, SB3/Gymnasium MIT 및 Torch/NumPy BSD 계열과 기존 자산 라이선스·NOTICE는 유지한다. Python 3.10.12, SB3 2.3.2, Gymnasium 0.29.1, Torch 2.2.2, NumPy 1.26.4와 전이 의존성은 AI `Training/requirements-lock.txt`로 고정한다.

## 재현·증거 감사

각 단계의 [baseline](ai-full-match-baseline.md), [학습](ai-full-match-training.md), [self-play/평가](ai-full-match-self-play.md), [재개](ai-full-match-resume.md), [runtime](ai-full-match-runtime.md)를 순서대로 읽는다. 기준 평가 재실행은 기존 보고서가 있으면 거부한다. 새 실험은 원본을 보존한 별도 checkout/output에서 한다. 납품 감사는 새 학습 없이 원시 전이·보상·terminal·hash·NUnit과 문서를 대조한다.

```sh
python3 tools/verify-ai-delivery.py
python3 tools/verify-ai-training.py
python3 tools/verify-ai-self-play.py
<venv-python> tools/verify-ai-resume.py
python3 tools/verify-ai-runtime.py --automated-only
```

최종 [delivery.json](evidence/ai-full-match/delivery.json)에 PR, 정확한 검증 commit, 모델/설정/증거 hash, CI 상태와 미완료를 기록한다. 전체 목표의 완료 판정은 ordinary-window 검수가 남아 있어 false다. main merge는 하지 않는다. AI feature SHA를 먼저 push하고 game manifest/lock에 pin한다. 승인 후 AI를 squash-merge하면 새 squash SHA로 다시 pin하고 Unity package resolve/check/runtime 회귀를 재검증해야 한다. feature SHA와 squash SHA는 같다고 가정하지 않는다.

## 검토 중인 PR

[AI draft #7](https://github.com/digital-tennis/digital-tennis-ai/pull/7), [Game draft #30](https://github.com/digital-tennis/digital-tennis-game/pull/30), [Docs #10](https://github.com/digital-tennis/digital-tennis-docs/pull/10)은 모두 미병합이다. CI의 소스/.NET/Python/문서 검사와 Unity 실행·일반 창 검수는 구분한다. game CI의 licensed Unity job은 자동 PR에서 제외되며 로컬 실행 XML을 참조한다.

## 단계별 검증과 완료 판정

원래 단계별 진입점을 다음과 같이 사용한다. baseline은 당시 source/model/balance의
정확한 바이트와 원시 90사례를 검사한다. 현재 HEAD가 달라졌다는 이유로 과거 결과를
무효화하거나 다시 측정한 값으로 바꾸지 않는다. 당시 verifier와 model 사본의 해시도
원래 보고서에 기록된 값과 일치해야 한다. `--strict-current-source`는 별도로 현재
checkout까지 완전히 동일한지 확인할 때 사용하며, 정상적인 후속 수정 뒤에는 실패한다.

```sh
python3 tools/verify-ai-full-match.py --phase baseline
python3 tools/verify-ai-full-match.py --phase training
python3 tools/verify-ai-full-match.py --phase self-play
python3 tools/verify-ai-full-match.py --phase resume
python3 tools/verify-ai-full-match.py --phase runtime
python3 tools/verify-ai-full-match.py --phase delivery
```

resume은 설치된 고정 학습 환경에서 실제 checkpoint의 Adam/RNG/pool과 원시 전이를
검사한다. 자동으로 찾을 수 없으면 `--python /path/to/venv/bin/python` 또는
`DIGITAL_TENNIS_TRAINING_PYTHON`을 지정한다. 검사 중 의존성을 설치하지 않는다.

runtime과 delivery의 기본 검사는 일반 창 확인이 없으면 **종료 코드 3(검수 대기)**을
반환한다. `verify-ai-runtime.py --automated-only`와 `verify-ai-delivery.py`는 자동 실행과
증거 무결성의 범위를 확인하는 별도 검사다. 이 검사들의 성공을 전체 완료로 세지 않는다.
창 확인 후에는 runtime/delivery의 완료 필드를 실제 결과에 맞춰 갱신해야 하며,
일반 창이 미완료여야만 감사가 통과하는 조건은 없다. 회귀 검사는 잘못된 batch 완료
주장과 손상된 보관 소스, 기존 기준 결과의 덮어쓰기를 거부한다.
