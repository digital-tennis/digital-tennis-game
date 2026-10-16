# 15선수 업그레이드 Unity 검증

Unity 6000.3.26f1, Built-in/Mono, native Mac/Metal에서 실제 게임을 실행했다. 한국·일본을 먼저 검토한 뒤 15명 전체에 확장했다. 비교 이미지는 게임 스킨의 동일한 포즈·카메라·조명으로 만든 원본이며 경기 사진을 보정하지 않았다.

## 결과

| 검사 | 실제 결과 |
|---|---|
| 최종 project check / Mac 검증 빌드 | 통과 |
| 최종 EditMode | 166/166, 실패 0 |
| 키보드·메뉴 PlayMode 회귀 | 6/6, C 이야기 열기/페이지/닫기와 확정·서브 차단 포함 |
| 리그·모션 PlayMode 회귀 | 4/4 |
| 전체 PlayMode 기준 검사 | 37/37; 이후 입력 변경은 front, 메시 변경은 EditMode/motion 및 새 native 실행으로 재검증 |
| 실제 어댑터 타격 | 7서브, 28접촉, 6종 타격, 오류 0 |
| 최종 native 증거 검사 | 447개 실제 파일, 현재 빌드/보고서/이미지/녹음 해시 확인 |
| native 메뉴 전체 경기 | 15명 선택, 51서브, 결과·일시정지·리매치·메뉴 복귀 통과 |
| 실제 오디오 | fresh 60개 + 기존 설치 8개, 원본 대비 최소 상관도 0.99999880 |
| 온라인 | 같은 Mac의 두 프로세스, 2–0 세트, 7–6(타이브레이크 8–6)/6–0, epoch 2 재접속 |

최대 손–그립 오차 0.000000968m, 스트링–공 0.000001025m, 접지 높이 0.000000957m, 디딘 발의 프레임 이동 0.000001908m였다. 실제 서브·이동·포핸드·백핸드 이미지를 열어 확인했다.

호스트 물리는 20,740틱, 게스트는 0틱이었다. 게스트의 버퍼 위치 20,969회, 스트로크 41,569회, 캐릭터 42,300회 검사에서 오류 0이었다. 릴레이 메시지는 8,770건이며 양쪽 승자와 점수가 일치했다.

오프닝은 실제 카메라 144프레임과 AudioListener 1,132,544샘플로 영상과 소리를 함께 저장했다. physics tick은 0→0이므로 오프닝 연출이 실제 매치를 진행하지 않았다.

## 일반 창 입력과 검증 범위

사용자에게 1분간 검증을 허용받고 CUA로 정상 앱을 Raise한 뒤 C 이야기 열기, Right 페이지 전환, Esc 닫기, 선수·상대·코트 확정과 Space 서브를 직접 관찰했다. GBR 테오 베넷과 JPN 렌의 경기 화면에 공이 날아가고 ‘서브 입력됨’이 표시됐다. 이후 Q/W 시도는 사용자 상태 변경으로 중지했으므로 타격 성공으로 주장하지 않는다. CUA 이미지가 대화에 있고 `.work/ac4/keyboard/observations.md`에 관찰 범위를 기록했다.

자동 검증의 메뉴는 버튼 콜백/정규화 입력이다. 실제 OS 포인터 성공이나 사람이 타이밍을 맞춰 랠리한 것으로 표시하지 않는다. 15명 스킬 사진은 명시적 연출 미리보기 스톡을 사용하며 실제 충전의 증거가 아니다. 실제 타격·이동 검증은 일반 입력 어댑터를 거쳐 공·점수·스톡을 직접 쓰지 않는다.

Windows 실물 PC, BLE·별도 부착 외장 IMU, 두 PC/WAN 시험은 수행하지 않았다. 기존 NU40-DK + 별도 외장 IMU와 12일·3인 계획을 유지한다.

## 소리가 반복되던 자동 검증 수정

`AudioOutputLimiter`는 실제 게임 믹스를 제한·녹음한 뒤 batchmode의 스피커 모니터만 0으로 만든다. 두 설치 모두 `monitorMuted=true`인 상태에서 무음이 아닌 WAV와 원본 상관관계를 확인했다. 일반 창의 볼륨과 게임 소리는 유지된다. 경기/서브/랠리/스킬/일시정지 BGM은 0, 실제 포인트 함성은 1회이며 다음 서브 전에 멈춘다. 기존 사용자 CSV·자막·수정 녹음·볼륨 0.19 보존도 통과했다. 이는 재생/파형 검증이며 주관적 성우 청음 평가는 아니다.

검증 전용 앱은 `com.digitaltennis.validation`, 일반 앱은 `com.Digital-Tennis.Digital-Tennis`를 사용한다. `BuildVerificationMac`은 별도 폴더에 만들고 일반 설정을 복구한다. 사용자가 플레이 중인 앱을 재빌드하거나 닫지 않았다. 검증 실행은 순서대로 진행했다.

## 재현

프로젝트를 Unity Editor에서 닫고 아래를 순서대로 실행한다. Git 추적 대상은 source·license·provenance이며 `.work`, `Builds`, `Library`, 로컬 평가 파일은 제외한다. 무료 원본 ZIP은 `.work/athlete-assets`에 준비한다. 서버 저장소는 옆에 clone한 경로로 지정한다.

```sh
bash tools/unity.sh check
bash tools/unity.sh test-edit
bash tools/unity.sh test-front
bash tools/unity.sh test-motion
python3 tools/verify-athlete-upgrade.py narrative
python3 tools/verify-athlete-upgrade.py assets
python3 tools/test-upgrade-verifier.py
/Applications/Unity/Hub/Editor/6000.3.26f1/Unity.app/Contents/MacOS/Unity -batchmode -quit -projectPath "$PWD" -executeMethod DigitalTennis.Editor.ProjectSetup.BuildVerificationMac -logFile .work/logs/build-verification-mac.log
verify_player="$PWD/Builds/verification/mac/DigitalTennis.app/Contents/MacOS/Digital Tennis"
python3 tools/verify-native-athlete-review.py --out .work/ac4 --build Builds/verification/mac --begin
python3 tools/verify-polish.py --player "$verify_player" --only roster --roster-ids KOR,JPN --out .work/ac4/kor-jpn
python3 tools/verify-polish.py --player "$verify_player" --only roster --out .work/ac4/all
python3 tools/compose-athlete-review.py --source .work/ac4/all/roster
python3 tools/verify-polish.py --player "$verify_player" --only motion --out .work/ac4/all
python3 tools/verify-polish.py --player "$verify_player" --only intro --out .work/ac4/all
python3 tools/prepare-opening-review.py --source .work/ac4/all/intro --audio .work/ac4/all/intro/intro-listener.wav --out .work/ac4
python3 tools/verify-native-court-audio.py --player "$verify_player" --out .work/ac4/audio
node tools/verify-front-end.mjs --player "$verify_player" --out .work/ac4/front
node tools/verify-online.mjs --player "$verify_player" --server-repo ../digital-tennis-match-server --menus --offscreen --exercise-characters --exercise-reconnect --out .work/ac4/online
python3 tools/verify-athlete-upgrade.py audio
python3 tools/verify-athlete-upgrade.py native
```

검증 진입점은 narrative/assets/audio/native를 각각 실제 검사로 보낸다. 누락 파일·변조·오래된 캡처·무음·클리핑·잘못된 인자는 실패한다. 현재 native DLL SHA-256은 `17a8ac37f519221a43acef1a353d8798e64131c69c4a2b3c7752bb2f2e502411`이며 영수증의 buildDirectory는 `Builds/verification/mac`다. 개발 빌드이며 릴리스 인증으로 표시하지 않는다.

초기 실행·재시도 원본은 `.work/ac4-first-pass`, `.work/ac4-visual-pass`, `.work/parent-probe`에 보존했다. 현재 통과 증거는 새 빌드의 `.work/ac4/verified.json`과 `review.html`이다. 실패 자료를 통과 근거로 재사용하지 않는다. 최초 전체 PlayMode의 프레임 타이밍 문제는 물리 단계 입력 확인으로, 메뉴 래퍼의 짧은 제한 시간은 정규 전체 매치에 맞춘 제한 시간으로 수정했다. 규칙이나 승리 조건을 줄이지 않았다.

## 읽기 전용 종합 평가 재검증

평가기의 Stage 1은 일반 쉘·스크립트 호출을 허용하지 않으므로 표준 `unittest` 진입점을 사용한다. 로컬 워크플로 상태는 Git에 넣지 않고, 추적하는 템플릿을 복사한다. 평가 서버의 기본 Python과 개발용 Python이 달라도 같은 NumPy·Pillow 버전을 사용하도록 의존성을 고정한 프로젝트 전용 가상 환경을 명시한다. Python 3.11을 사용하며 ffprobe도 PATH에 필요하다.

```sh
python3 -m venv .work/verification-venv
./.work/verification-venv/bin/python3 -m pip install -r tools/requirements-verification.txt
mkdir -p .ouroboros
cp tools/athlete-mechanical.toml .ouroboros/mechanical.toml
./.work/verification-venv/bin/python3 -B -m unittest discover -s tools -p test_athlete_acceptance.py -v
```

이 5개 검사는 15명 서사, 실제 CC0 ZIP·모델·애니메이션, 실제 68개 녹음의 파형, Unity XML·빌드·447개 native 증거, 잘못된 인자·누락 증거 거부를 각각 실행한다. 저장한 `verified.json`과 `waveform-audit.json`을 원본 증거에서 다시 계산해 대조한다. 음원 검사와 native 검사에 `--read-only`를 전달하므로 보고서나 검수 HTML을 다시 쓰지 않으며 파일 해시와 수정 시각이 보존되는지도 확인한다. 새 게임이나 오디오 테스트를 시작하지 않는다.

```sh
python3 tools/verify-athlete-upgrade.py audio --read-only
python3 tools/verify-athlete-upgrade.py native --read-only
```

`--begin`과 `--read-only`는 함께 사용할 수 없다. 기존 보고서가 없거나 원본 증거·해시가 달라지면 실패한다. `.work/ac4`와 원본 ZIP은 로컬 검증 자료이므로 새 clone에서는 위의 실제 게임 검증을 먼저 실행해야 한다. 기존 개발 빌드의 native 결과와 현재 테스트 실행을 구분한다.
