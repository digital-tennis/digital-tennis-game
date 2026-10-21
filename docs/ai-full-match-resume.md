# 전체 학습 상태 저장과 재개

AC 4는 실제 self-play가 끝난 모델에서 시작한다. 기존 actor-only checkpoint는
완료된 match 뒤의 finish-only 실행과 PPO 관측이 달라질 수 있으므로, 먼저 새
정상 match에서 4096 step을 학습한 다음 새 complete-state checkpoint를 만든다.
이 준비 구간과 재개 구간은 구분해서 기록한다.

`tools/run-ai-resume.py`는 Unity 브리지를 시작하고 첫 Python 학습 프로세스가
rollout 최적화를 마친 경계에서 저장하고 **종료하도록** 한다. 종료가 확인된 뒤
다른 Python 프로세스를 실행해 저장 상태를 읽고 8192 step을 추가 학습한다.
부모의 단순 TCP 중계는 연결만 보관한다. action, 관측, 보상, 점수는 만들지 않는다.
Unity는 동기 요청 사이에 yield하지 않으므로 같은 partial match가 정지된 채 남는다.
복원 시 lease와 Unity 상태가 정확히 일치해야 하며, 다르면 초기화 없이 거부한다.

체크포인트는 PPO actor/critic 및 Adam optimizer, global timestep/update/episode
관련 SB3 상태와 마지막 관측, Monitor/VecEnv 버퍼, Python/NumPy/Torch 난수,
양쪽 선수의 별도 난수, Gym 난수, 고정 관측 normalization 설명, 관측/action/reward
버전, CSV 설정 및 실행 코드/scene/rules/dependency 해시를 저장한다. frozen 상대
모델의 실제 bytes와 순서, 현재 상대, cohort schedule 및 snapshot callback 이력도
포함한다. 끝난 rollout만 허용해 보류 중인 transition/gradient를 버리지 않는다.
정규화는 Unity v2의 고정 scale/clip이므로 학습 중인 running mean/variance는 없다.

저장은 immutable ZIP generation을 fsync한 뒤 current pointer를 원자적으로 교체한다.
각 payload의 SHA-256을 deserialization 전에 검증한다. 현재 generation이 손상되면
자동으로 과거 상태를 고르지 않는다. 명시적 `previous=True`로 검증된 직전 generation을
선택할 수 있다. 경로 순회, symlink, 예상하지 않은 archive entry와 설정/환경/schema
불일치를 거부한다. pickle/SB3 archive는 이 로컬 writer가 만든 신뢰된 파일만 읽는다.
해시는 공격자가 다시 서명한 악성 pickle을 안전하게 실행해 주는 보안 경계가 아니다.

제한: Unity 자체를 종료한 후의 world 복원은 지원하지 않는다. Unity가 사라지면
복원을 명시적으로 실패시킨다. 이번 검증은 학습 프로세스 종료·재시작을 다루며,
Unity scene을 재실행한 bitwise 재현성을 주장하지 않는다. Unity 외부의 실제 시간과
프레임 순서에 따른 차이가 있을 수 있다. 짧은 uninterrupted/resumed PPO 비교는
제어된 Gym 테스트에서 weights, optimizer, counters, RNG가 정확히 같음을 확인한다.
이 합성 fixture는 Unity match 결과를 대신하지 않는다.

실제 실행에서 global step은 137216 → 145408, optimizer epoch 카운터는
396 → 412로 증가했다. Adam의 tensor별 step은 6336 → 6592였으며, 상대 풀은
기존 17개를 유지하고 새 snapshot 1개를 추가했다. ordinary-input 원시 기록의
실제 point 변화는 46회였다. 이 짧은 추가 구간은 같은 partial match 안에서 끝났고,
완료 match 수를 늘렸다고 주장하지 않는다.

실제 결과는 `evidence/ai-full-match/resume.json`과 `resume-checkpoints.json`, 압축한
ordinary-input/score/gate 기록 및 Unity XML에 보관한다. 전체 체크포인트와 큰 로그는
AI checkout의 무시되는 `.work/complete-checkpoints/`에 남기고, compact 추론 모델과
재현용 Python 원본은 증거 디렉터리에 보관한다. partial match를 완료로 세지 않는다.
이 작업은 Mac 창 검증, 장치/Windows/WAN 검사나 PR 발행을 주장하지 않는다.

```sh
python3 tools/run-ai-resume.py --python /path/to/pinned-venv/bin/python --ai .work/ai-full-match
/path/to/pinned-venv/bin/python -m unittest discover -s tools -p test_ai_resume.py
/path/to/pinned-venv/bin/python tools/verify-ai-resume.py
bash tools/unity.sh check
```
