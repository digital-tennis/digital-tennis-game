# 15선수 업그레이드 네이티브 검증

이 문서는 AC4의 새 Mac 빌드 검증을 기록한다. 이전 버전의 `polish-validation.md`나 에셋 제작 단계의 성공을 이번 실행 결과로 대체해 인용하지 않는다. Unity 6000.3.26f1, Built-in/Mono를 사용한다. 검증 캡처·빌드·로그는 `.work/ac4`와 `Builds` 아래에 두고 Git에 넣지 않는다.

## 재현 순서

Unity Editor에서 프로젝트를 닫은 뒤 아래 batch 작업을 한 번에 하나씩 실행한다. `setup`으로 기존 씬을 재생성하지 않는다.

```sh
bash tools/unity.sh check
bash tools/unity.sh test-edit
bash tools/unity.sh test-play
bash tools/unity.sh test-motion
bash tools/unity.sh test-front
bash tools/unity.sh build-mac
python3 tools/verify-native-athlete-review.py --out .work/ac4 --begin
python3 tools/verify-polish.py --only roster --roster-ids KOR,JPN --out .work/ac4/kor-jpn
```

위 KOR/JPN의 얼굴·전신·스킬 준비/강조/회복 프레임을 먼저 열어 검토한 뒤 전체 로스터를 실행한다.

```sh
python3 tools/verify-polish.py --only roster --out .work/ac4/all
python3 tools/verify-polish.py --only motion --out .work/ac4/all
python3 tools/verify-polish.py --only intro --out .work/ac4/all
node tools/verify-front-end.mjs --out .work/ac4/front
python3 tools/verify-native-court-audio.py --out .work/ac4/audio
node tools/verify-online.mjs --server-repo /Users/kevin/Documents/Codex/digital-tennis/digital-tennis-match-server --menus --offscreen --exercise-characters --exercise-reconnect --out .work/ac4/online
python3 tools/verify-native-athlete-review.py --out .work/ac4
```

마지막 검증기는 실제 Unity XML의 성공 테스트 개수, 현재 Mac Runtime DLL 해시, 실행 시작보다 새로운 PNG/WAV/JSON을 확인한다. 빈 이미지·무음/클리핑 WAV·누락 파일·오래된 캡처·실패 보고서는 거절한다. `verified.json`은 검증한 실제 파일의 경로와 SHA-256을 남긴다. 단순한 `passed` 문자열만으로 통과시키지 않는다. 음성은 별도 파형 검증에서 재생 원본과 AudioListener 녹음의 상관관계도 대조한다.

## 검증 경계

- 로스터는 명시적 연출 미리보기다. 준비/강조/복귀 포즈를 위해 스톡을 부여하며 실제 경기 충전의 증거가 아니다.
- 모션은 opt-in CLI 소스 → 정상 입력 어댑터 → `SubmitInput` 경로로 서브와 실제 6종 타격, 추가 이동 랠리를 진행한다. 공·점수·충전량을 직접 쓰지 않는다. 손–그립·스트링–공·접지 높이·디딘 발 미끄러짐을 실제 리그에서 측정한다. `Serve.png`와 `Running.png`도 실제 플레이에서 찍는다.
- 메뉴 검증은 Unity 버튼 콜백과 자동 normalized input이다. 키보드 경계는 PlayMode `KeyboardAndReplaceableSourceUseTheSameRacketGate`에서 검사한다. 자동 입력을 사람의 물리 키보드 플레이로 표시하지 않는다.
- 온라인은 같은 Mac의 서로 다른 두 네이티브 프로세스와 로컬 WebSocket 서버다. 호스트만 물리/규칙을 진행하고 게스트는 버퍼에 맞춘 위치·명시적 스트로크·캐릭터 상태를 표시한다. 재접속 epoch와 카메라 방향도 검사한다. 두 PC/WAN 시험이 아니다.
- 이미지는 게임 카메라, 음원은 게임 AudioListener 출력이다. 마이크·OS 음성·다른 앱의 화면을 수집하지 않는다. 오프닝 녹음은 `all/intro/intro-listener.wav`, 실제 랠리/득점/다음 서브 녹음은 `audio/*/game-mix.wav`, 15명 60개 고유 대사는 `audio/fresh/*-playback.wav`다. 대사는 Flite 영어 합성 음성이며 한국어 자막을 제공한다.
- 경기 BGM 볼륨 0, 포인트 함성 한 번과 다음 서브 전 종료를 확인한다. 기존 설치 검증은 사용자 CSV/자막/수정 음원의 해시와 커스텀 볼륨을 보존한다.
- Mac 잠금을 우회하지 않는다. Windows 실물 PC, BLE와 별도 외장 IMU, 두 PC 온라인은 이 검증 범위 밖이다. 기존 NU40-DK + 별도 외장 IMU 및 12일·3인 계획을 유지한다.

## 이번 실행에서 발견한 회귀

첫 전체 PlayMode 실행은 36/37이었다. `PhysicalPointsCompleteTwoSetsAndStopWithoutRestarting`은 실제로 6–0 / 6–0, 2–0 세트로 종료했지만, 4배속의 프레임 단위 입력에서 한 리턴을 놓쳐 총 포인트 49가 예상 48과 달랐다. 원시 XML/로그를 `.work/test-play-initial-failure.xml`과 `.work/logs/test-play-initial-failure.log`에 보존했다. 이 테스트의 입력 루프를 `WaitForFixedUpdate`로 바꾸어 렌더 프레임 사이의 물리 진행에서도 정상 리턴 게이트를 확인한다. 프로덕션 물리·점수·도달 범위·CSV 수치는 변경하지 않았다. 48포인트/48서브, 두 6–0세트와 종료 후 추가 서브 불가 assertion은 유지했다.

오프닝 리뷰 동영상에 실제 음원을 포함하려면, 같은 실행에서 생성한 WAV만 지정한다.

```sh
python3 tools/prepare-opening-review.py --source .work/ac4/all/intro --audio .work/ac4/all/intro/intro-listener.wav --out .work/ac4
```

`review.html`은 검증을 통과한 원본 게임 프레임과 AudioListener WAV를 그대로 연결한다. 모델이나 프레임을 생성형 이미지로 보정하지 않는다.

수정 후 전체 PlayMode 재실행은 **37/37, 실패 0**, 실제 XML 실행 시간 364.90초였다. 처음 실패한 검사의 승자/세트/게임/48포인트/48서브/종료 후 정지 assertion을 완화하지 않았다. motion 집중 회귀도 4/4를 통과했다.

네이티브 front 검증의 첫 실행은 기존 Node 래퍼의 210초 제한에 걸려 결과 JSON을 만들지 못했다. 현재 규칙은 한 게임이 아니라 best-of-3 전체 매치를 끝낸 뒤 Results를 표시하므로, 래퍼가 Unity 드라이버의 900초 진단 제한보다 먼저 플레이어를 종료했다. `.work/ac4/front-initial-timeout`에 부분 프레임과 로그를 보존하고, 래퍼만 930초로 맞췄다. 경기 결과·점수·리매치·메뉴 assertion을 줄이거나 규칙 CSV를 짧게 바꾸지 않는다. 부분 실행을 성공 증거로 사용하지 않는다.

## 새 빌드의 실제 네이티브 측정

- KOR/JPN 12장 먼저 확인 후 15명 90장(얼굴/전신/스킬 준비·강조·회복)을 촬영했다. `all/roster/faces-sheet.png`, `skills-sheet.png`는 원본 30장을 축소 배치한 검토 시트다. 두 우선 선수의 원본 4장과 15명 시트를 직접 열어 확인했다.
- 실제 모션은 7서브·28접촉·6종 타격, 오류 0이었다. 최대 스트링–공 오차 0.000001025m, 손–그립 0.000000956m, 접지 높이 0.000001010m, 디딘 발 프레임 미끄러짐 0.000001908m였다. 서브·이동·포핸드·백핸드 원본을 열어 라켓/손/발 표시를 확인했다.
- 오프닝은 159프레임, AudioListener 샘플 1,116,160개, 오류 0, physics tick 0→0이었다. 실제 녹음이 포함된 MP4는 H.264 영상 12.500초와 AAC 음성 12.516초를 가진다. 녹음 파일과 실제 프레임만 인코딩했다.
- 신규 설치 60개(15명×ready/skill/win/lose), 기존 설치 8개(KOR/JPN) AudioListener 녹음 68개를 실제 원본 음원과 파형 상관관계로 대조했다. 이동/스키드/확정 접촉 SFX와 경기 BGM 무음, 포인트 함성 1회·다음 서브 전 정지, 사용자 CSV·음원·자막·볼륨 보존 검증이 통과했다. 이는 자동 파형/재생 검증이며 사람의 주관적 청음 평가라고 표시하지 않는다.

추가 일반 창은 `-dt-input-observe`로 격리 실행했으나 CUA가 실행 프로세스에 연결하지 못했다(`procNotFound`, 동일 bundle ID 경로 모호성). 키 입력은 보내지 않았고 해당 관찰 프로세스만 종료했다. 이번 실행의 CUA/물리 키보드 통과 주장은 없다. 실제 키보드 소스의 게이트/메뉴/플레이 경로는 통과한 PlayMode 자동 입력 테스트로만 입증한다. 잠금을 우회하거나 다른 UI 자동화 도구를 사용하지 않았다.

온라인은 같은 Mac의 두 실제 OSXPlayer와 로컬 서버로 86포인트(56/30), 2–0세트, 7–6(타이브레이크 8–6)/6–0을 끝냈다. 릴레이 8,942건, 재접속 epoch 2, 최대 6접촉 랠리였다. 호스트 물리 20,741틱, 게스트 0틱, 게스트 buffered 위치 검사 20,983회·명시적 스트로크 검사 41,601회와 캐릭터 검사 42,332회에서 오류 0이었다. 호스트/게스트 스킬 화면과 게스트 코트 원본을 직접 열어 HUD의 로컬 선수 순서와 Far 베이스라인 카메라, 동일 월드 스킬 표시를 확인했다. 메뉴는 8/5회 버튼 콜백이며 포인터 클릭 수는 0/0이다. 물리 마우스/키보드나 두-PC/WAN 성공으로 표시하지 않는다.

## 최종 증거 대조

전체 front 재실행은 15명 선택, 36회 버튼 콜백, 51회 사용자측 자동 서브, 실제 74포인트(19/55), 1–6/0–6과 0–2세트의 종료, 일시정지 동결, 리매치 초기화·메뉴 복귀를 통과했다. 14개 원본 UI/카메라 프레임, 런타임 오류 0이다.

최종 `verify-native-athlete-review.py`는 실제 XML과 **462개 새 네이티브 파일**을 검증하고 `verified.json`에 SHA-256을 기록했다. 그 전에 front 결과가 없던 중간 실행은 `Missing native evidence: front/result.json`으로 거절했으며 성공 보고서를 만들지 않았다.

현재 개발 Mac Runtime DLL SHA-256: `dc4b7f1ea440dd8f2015bd60c92c416f2b418042f7c616c66a65c69bb00b30e8`. 빌드는 development/AllowDebugging=true이며 작업 트리의 미커밋 변경을 포함한다. clean release나 배포 검증이라고 표시하지 않는다.

최종 Unity 성공 개수: test-edit 165, test-play 37, test-motion 4, test-front 6; 모두 실패 0. 원본과 갤러리는 `.work/ac4/verified.json`, `.work/ac4/review.html`, `.work/ac4/opening-camera-preview.mp4`에서 확인한다.

작은 접촉/발 오차 수치는 Unity Transform 좌표의 계산 오차다. 외장 IMU나 실물 추적 장치의 정확도를 측정한 수치가 아니다. Windows 실물, 외장 IMU/BLE, 두-PC/WAN 및 이번 실행의 CUA 키보드 관찰은 통과 근거에 포함하지 않는다.
