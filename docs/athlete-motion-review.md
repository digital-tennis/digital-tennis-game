# KOR/JPN 전신 모션 검토 (AC2)

기존 사인파 이동과 `Idle_Loop` 머리 트랙만 사용하던 경로에서, 한국·일본 선수는 보관된 Quaternius Universal Animation Library **Standard FREE / CC0-1.0**의 실제 전신 `Idle_Loop`, `Walk_Loop`, `Jog_Fwd_Loop`, `Sprint_Loop`을 사용한다. 원본은 `Assets/ThirdParty/Quaternius/1/Unreal-Godot/UAL1_Standard.glb`이며, 기존 `provenance.json`의 원본 배포본 출처와 `1/License.txt`를 유지했다. 추가 `motion-bindings.json`은 실제 읽은 원본·라이선스·리타깃 설정·생성 트랙의 SHA-256을 기록한다.

`tools/adapt-athlete-motion.py`는 glTF 전체 관절 계층을 보간하고, 부모의 월드 프레임과 authored 축 변환으로 몸통·머리·양어깨·양팔꿈치·양허벅지·양무릎의 10개 트랙을 기존 접촉 리그에 맞춘다. 발목 궤적도 원본 Jog에서 추출한다. `AcquiredAthleteMotion`이 이 데이터를 런타임에 샘플링한다. 원본의 루프 끝 차이는 마지막 0.08초를 블렌드하여 닫는다. 루트 모션은 추출하지 않는다.

이동 위상은 실제 플레이어 이동 거리에 비례하며 속도에 따라 Idle/Walk/Jog/Sprint를 블렌드한다. 가감속과 방향 전환은 시각 모델에만 적용한다. 디딘 발은 월드 앵커에 두고, 공중 발은 취득한 발목 궤적을 사용한다. 타격은 승인된 기존 입력 시점에서 접촉하고 몸통·반대팔·회복을 블렌드한다. **취득한 테니스 모캡이라고 주장하지 않는다.** Standard FREE에 테니스 클립은 없으며, 서브·포핸드·백핸드는 기존 authored 타격과 접촉 IK를 전신 이동 트랙에 결합한 것이다. 입력/도달/중복/오래된 입력 검사나 물리·점수는 바꾸지 않았다.

화면에서 펼친 손과 늘어나는 팔을 발견해 수정했다. 취득한 오른손 손가락 표면을 authored 원통 그립으로 굽히고 손목 소켓 방향과 손바닥 오프셋을 라켓에 맞춘다. 이는 손가락별 실시간 애니메이션이 아닌 정지 그립 변형이다. 손목 대신 실제 손바닥 기준으로 그립 오차를 검사한다. 도달은 몸 전체의 런지로 처리하며 팔 연장은 설정의 1.08배 이내로 제한한다. 접지 후 IK를 다시 풀 때도 원래 팔 길이를 사용한다. 설정은 `motion-retarget.json`과 `body-retarget.json`에 있고, 다른 13명의 프로필·메시 및 경기 능력치는 변경하지 않는다.

## 이번 작업 트리의 실제 실행

Unity **6000.3.26f1**, Built-in/Mono, native Mac/Metal의 새 `Builds/verification/mac` 앱을 실행했다. ID는 `com.digitaltennis.validation`이다. 사용자 일반 앱은 닫거나 덮어쓰지 않았다. 자동 native 프로세스는 `-batchmode`이며 기존 AudioOutputLimiter가 녹음 후 스피커 모니터를 뮤트한다. 사용자 오디오/밸런스 파일은 사용하지 않고 각 실행 폴더로 격리했다.

스트로크별로 정상 연습게임을 새로 시작하며, 마지막 이동·서비스 교대 검사는 한 연습게임을 계속 진행한다. 최종 motion 실행은 정상 입력 소스 → 라우터 → 어댑터를 통해 **접촉 75회, 서브 11회, 스트로크 6종, 런타임 오류 0**을 기록했다. 한국·일본 각각 달리기·서브·포핸드·백핸드와 실제 회복 프레임을 저장했다. 일본 서브는 점수나 서버를 설정하지 않고, 정상적으로 리턴하지 않은 포인트들이 첫 게임을 끝내 서비스가 교대된 뒤 촬영했다.

| 측정 | 실제 최대값 |
|---|---:|
| 승인 시 스트링 중심–공 | 0.00034465 m |
| 손바닥–손잡이 | 0.0000009611 m |
| 디딘 발바닥 높이 오차 | 0.0001112 m |
| 디딘 발의 프레임간 보정 | 0.0300564 m |
| 팔 길이 비율 | 1.076750 |

발 보정은 0이 아니다. 큰 런지 등 도달 한계에서 앵커 보정이 가능한 접지 솔버이며, 무오차 접지나 모든 포즈의 해부학적 정확성을 주장하지 않는다. 로우폴리 스킨·고정 손가락·짧은 승인 후 타격이라는 표현상의 한계도 남는다. 토스 준비를 위해 실제 서브 접촉을 지연시키지 않았다.

원본 카메라 PNG와 실제 시각/스윙 ID/스윙 나이/이동 속도는 `.work/ac2-motion/motion`에 있다. 각 시퀀스 16장은 경기를 계속 실행하면서 촬영하며 모델을 임의 포즈로 바꾸지 않는다. `review.html`과 8개 `*-sequence.png`는 이 원본 중 0/4/8/15번 샘플을 보정·크롭하지 않고 바깥쪽에 라벨만 붙인 검수표다. 두 선수의 달리기·회전·서브·포핸드·백핸드·회복 표를 실제 이미지로 열어 확인했다. 일반 OS 키보드 실사용이나 staged art preview라고 표시하지 않는다. **실제 CLI 어댑터 입력 경기**이다. 앞선 중립 전후 비교는 여전히 staged art preview이며 모션 증거와 구분한다.

`build-receipt.json`은 실제 DLL, 성공한 빌드 기록, 포장된 `resources.assets`, 사용한 소스·두 메시의 해시를 연결한다. 에셋만 바꾼 증분 빌드는 기존 DLL을 정상적으로 재사용하므로 DLL 날짜를 새로 만들지 않는다. 두 메시 이름과 Body JSON 전체 바이트가 실제 패키지에 포함되어 있는지도 검사한다. Python 검사는 보관 소스/생성 트랙 해시·전신 키 수·정규화, 현재 DLL/소스 일치, 고유 앱 ID, 빌드 이후 생성한 실제 PNG, 한국·일본의 승인 접촉부터 회복까지의 타임라인과 기존 오차 기준을 검사한다. 이미지 개수나 보고서의 passed 필드만을 시각 품질 증거로 쓰지 않는다. 첫 화면 검토와 접촉 오차 실패는 `.work/ac2-motion-first-pass`, `.work/ac2-motion-grip-pass`에 보존하고 통과 증거로 재사용하지 않았다.

최종 EditMode **169/169**, 모션 PlayMode **4/4**, project check와 검증 Mac 빌드가 통과했다. 전체 PlayMode **37/37**는 전신 이동·그립 변경 후 회귀 실행이다. 마지막 헤어 정수리와 한국 치맛단의 열린 메시/허벅지 웨이트 보정은 별도 재발 검사, 최종 EditMode와 새 native 실행으로 확인했다. 새 모션 Python 검사는 **3/3** 통과했다. Windows 실물·BLE·별도 외장 IMU·두 PC/WAN 검증은 수행하지 않았다. NU40-DK + 별도 부착 외장 IMU와 12일·3인 계획을 유지한다.

같은 새 빌드로 GPU 카메라를 켠 호스트·게스트 두 프로세스도 끝까지 실행했다. 실제 결과는 **2–0 세트, 7–6(타이브레이크 8–6)/6–0**, 재접속 **epoch 2**, 릴레이 **9,438건**이다. 호스트 물리는 **20,998틱**, 게스트는 **0틱**이었다. 게스트의 버퍼 위치 **20,965회**, 스트로크 **41,835회** 비교에서 각각 오류 **0**이었다. 두 선수의 캐릭터 인덱스는 KOR 0/JPN 1을 유지했다. `.work/ac2-online/combined.json`과 양쪽 원본 카메라 PNG가 증거다. 호스트·게스트 및 첫 게임 후 게스트 화면을 실제로 열어 로컬 베이스라인 시점을 확인했다. 이는 같은 Mac의 두 프로세스/로컬 WebSocket 릴레이 검사이며 두 PC 검증은 아니다.

## 재현

이 프로젝트를 Editor에서 닫고 실행한다. 원본 glb와 라이선스는 저장소의 실제 보관본을 읽는다. 프로젝트 로컬 venv는 `tools/requirements-verification.txt`를 사용한다. 로컬 증거/빌드/로그는 Git에 추가하지 않는다.

```sh
./.work/verification-venv/bin/python3 tools/adapt-athlete-bodies.py
./.work/verification-venv/bin/python3 tools/adapt-athlete-motion.py
/Applications/Unity/Hub/Editor/6000.3.26f1/Unity.app/Contents/MacOS/Unity -batchmode -quit -projectPath "$PWD" -executeMethod DigitalTennis.Editor.AthleteMotionSetup.Prepare -logFile .work/logs/ac2-grip-meshes.log
bash tools/unity.sh test-edit
bash tools/unity.sh test-motion
bash tools/unity.sh check
/Applications/Unity/Hub/Editor/6000.3.26f1/Unity.app/Contents/MacOS/Unity -batchmode -quit -projectPath "$PWD" -executeMethod DigitalTennis.Editor.ProjectSetup.BuildVerificationMac -logFile .work/logs/ac2-build-mac.log
./.work/verification-venv/bin/python3 tools/review-athlete-motion.py --receipt
python3 tools/verify-polish.py --player 'Builds/verification/mac/DigitalTennis.app/Contents/MacOS/Digital Tennis' --only motion --out .work/ac2-motion
./.work/verification-venv/bin/python3 tools/review-athlete-motion.py
```

버퍼된 온라인 표현은 같은 검증 앱과 설치한 서버 저장소를 지정해 재현한다. 아래 서버 경로는 실제 설치 위치에 맞춘다. 결과 수집 전에는 새 native 테스트를 동시에 시작하지 않는다.

```sh
node tools/verify-online.mjs --player 'Builds/verification/mac/DigitalTennis.app/Contents/MacOS/Digital Tennis' --server-repo ../digital-tennis-match-server --offscreen --exercise-reconnect --out .work/ac2-online
./.work/verification-venv/bin/python3 -B -m unittest discover -s tools -p test_athlete_motion.py -v
```
