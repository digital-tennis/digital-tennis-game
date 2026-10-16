# 게임 완성도 개선 구현

2026-10-16의 개선 작업입니다. 최초 조건인 12일·3명과 NU40-DK에 외장 IMU를 별도 연결하는 제품 경계를 유지합니다.

## 선수와 경기장

`AthleteRigSetup`이 기존 GUID를 유지하면서 얼굴·눈·코·귀·헤어 5종·손·상의·하의·양말·신발을 갖춘 자체 도형 모델을 구성합니다. 15명은 기존 성인 페르소나와 개별 배색/키/체형을 사용합니다. `AthleteMotion`은 실제 이동량과 확정된 swingId를 읽어 10개 관절의 이동·서브·6종 타격·득점 포즈를 표현합니다. 라켓은 막힌 판 대신 열린 테두리와 스트링 메시이며 손의 그립에 고정합니다. 모델에 공 판정 collider를 추가하지 않습니다. 사람형 스킨 메시나 모션 캡처 자산을 구매한 결과는 아닙니다.

`SkillCameraDirector`는 1.2초 연출의 클로즈업→측면 이동→원래 경기 카메라 복귀를 처리합니다. 실제 공/규칙은 기존 권위 측에서 동결하고 global timeScale은 바꾸지 않습니다. 움직임 줄이기를 선택하면 원래 시점을 유지합니다. 유니폼 글자는 깊이 검사를 적용해 몸 뒤쪽 글자가 얼굴/가슴에 겹쳐 보이지 않게 했습니다.

경기장은 한국 지붕, 일본 접힌 면, 중국 문루, 미국 도시 보, 러시아 다면체, 인도 격자, 독일 트러스, 영국 파빌리온, 스페인 회랑, 프랑스 조형 핀, 호주 돛, 네덜란드 계단 박공, 브라질 곡선, 모로코 아치, 남아공 단형 그늘막을 원형으로 한 외곽 실루엣을 사용합니다. 공유 코트 크기와 공 판정은 동일합니다. 국가는 개인의 능력을 결정하는 인종/국민성 주장이 아닙니다.

624석·502명 관중은 기존 결합 메시를 유지하고 shader에서 작은 움직임과 실제 득점 반응을 표현합니다. 낮음에서는 관중 렌더링과 그림자를 끄고, 보통은 2x AA/단순 그림자, 높음은 4x AA/부드러운 그림자를 사용합니다. 모두 물리·밸런스·온라인 상태를 바꾸지 않습니다. 점수 HUD는 좌측 위, 입력 안내는 아래로 옮겨 중앙 전광판과 경기 시야를 확보했습니다.

## 음향과 청음

`tools/generate-audio.py`가 NumPy 합성으로 음악 A/B 6곡과 효과음/환경음 11개를 생성합니다. 추가 의존성은 NumPy 1.26.4이며 `--flite /path/to/flite`는 영어 합성 음성 68개와 청음용 3테이크를 생성합니다. 캐릭터 15명 × ready/skill/win/lose 4개와 심판 8개입니다. 사람 배우나 각국 언어/억양의 완성을 뜻하지 않습니다. 현재 한국어 자막이 있으며 음원 파일명과 자막은 외부 CSV로 바꿀 수 있습니다.

Flite는 공식 festvox/flite의 `6c9f20dc915b17f5619340069889db0aa007fcdc`에서 도구만 로컬 빌드했습니다. 배포되는 음원과 함께 `Assets/StreamingAssets/Audio/FLITE-COPYING.txt`를 유지하세요. 엔진 바이너리는 저장소에 넣지 않습니다. 음악·효과음에는 다른 게임이나 상용 녹음 샘플을 넣지 않았습니다. 출처와 조건: [공식 Flite COPYING](https://github.com/festvox/flite/blob/6c9f20dc915b17f5619340069889db0aa007fcdc/COPYING).

`TennisAudio`는 음악·음성·효과·환경음의 논리 버스, 두 음악 소스 교차 페이드, 음성 중 BGM duck, 전체/그룹 음량 저장, 자막, 제한된 효과 소스 풀을 제공합니다. Unity AudioMixer 자산을 생성한 것은 아닙니다. 설정→오디오에서 A/B를 직접 비교할 수 있고 기본은 A입니다. 음성은 최대 6개 대기열에서 한 번에 하나씩 재생해 심판과 선수 음성이 겹치지 않도록 했습니다. 사람 청음 승인 전에는 최종 음향 선택으로 표시하지 않습니다.

음원은 첫 실행 때 사용자 폴더로 복사하며 기존 파일은 덮어쓰지 않습니다. `audio.csv`의 cue/group/file/volume/loop/fade/source를 편집하고 F5 또는 오디오 다시 읽기를 누릅니다. WAV/OGG 파일만 같은 폴더에서 읽으며 경로 탈출·URL·중복·비정상 수치를 거절합니다. 음성/단발 효과의 loop=1은 거절합니다. 환경음 루프도 유효한 새 파일로 교체합니다. 모든 파일의 비동기 로드/검증이 성공한 뒤 교체하며 실패하면 이전 음원을 유지합니다. 한 파일 32MB, 전체 128MB, 120초 이하로 제한합니다. 설정 화면에 실제 외부 경로를 표시합니다.

`-dt-audio-validation`은 검증 실행에서만 사용자 설정을 무시하고 게임의 AudioListener 혼합 결과를 최대 20초 PCM으로 저장한 뒤 스피커 출력을 0으로 합니다. 마이크·다른 앱·OS 음성을 녹음하지 않습니다. 일반 게임에서는 캡처하지 않습니다. `AudioOutputLimiter`는 합성 피크를 제한합니다.

## 제스처 입력 경계

`gestures.csv`의 15개 서로 다른 토큰 시퀀스는 게임 측 스킬 발동 템플릿입니다. 실제 IMU 드라이버/방향 분류기는 포함하지 않습니다. 향후 BLE SDK에서 원시 센서를 `GestureToken`으로 분류한 뒤 다음 경계에 연결합니다.

```csharp
var patterns = SkillGestureCatalog.Parse(File.ReadAllText(csvPath));
var source = new GestureSkillInputSource(decodedRacketSource,
    () => controller.Characters.Player(controller.View.LocalPlayer).Index, patterns);
controller.InputAdapter.UseSource(source);
// BLE worker에서 수신한 원시 패킷을 검증/디코딩한 후:
source.PushToken(GestureToken.Left, packetSequence);
```

생성/소스 교체는 Unity 메인 스레드에서 실행합니다. PushToken은 작업 스레드에서도 호출할 수 있습니다. PC 수신 시각을 사용하고 중복·오래된 순번·간격/전체 시간 초과를 버립니다. 최대 큐 64개이며 소스 전환/해제 시 대기 입력을 지웁니다. 끝까지 일치했을 때 일반 `PlayerAction.Skill` 하나를 내보냅니다. 실제 stock/공격 권위/온라인 플레이어 바인딩은 기존 adapter/session이 결정합니다. K 버튼도 같은 입력입니다. CSV를 수정한 패턴은 다음 source 생성 시 적용합니다. 현재 물리 라켓의 인식률을 검증한 것은 아닙니다.

## 온라인 재경기와 헛스윙

종료된 같은 matchId에서 양쪽이 동의해야 새 경기 ID가 발급됩니다. 한쪽만 요청한 동안 결과 화면은 유지하고 취소·연결 끊김은 동의를 해제합니다. 방과 선수/코트는 유지하며 늦은 이전 경기 입력은 host에서 거절하고 snapshot 버퍼도 경기를 섞지 않습니다. 중간 R 입력으로 상대 동의 없이 경기를 초기화할 수 없습니다.

정상적인 헛스윙도 swingId/종류/나이/접촉 여부/서브 여부로 전달합니다. 오래된 재접속 상태는 애니메이션을 재생하지 않습니다. 메뉴에는 잘못된 코드·가득 찬 방·복구 대기 안내와 RTT, 사용자가 누르는 코드 복사 버튼이 있습니다. 전송은 WebSocket 릴레이이며 직접 P2P를 완료했다고 표시하지 않습니다.

## 밸런스와 성능 측정

일반 실행에 `-dt-metrics-dir /absolute/output`을 추가하면 로컬 `points.csv`, `swings.csv`, `performance.json`을 기록합니다. 경기·난이도·두 캐릭터·랠리 길이·체력·입력 결과별 기록이며 주소/계정/재접속 토큰을 쓰지 않습니다. 권위 측만 포인트를 기록하고 게스트는 프레임만 기록합니다. 소스/포인트 기록은 스트리밍으로 쓰고 성능 샘플은 12,000 프레임으로 메모리 사용을 제한합니다. 포인트 간 경과에는 회수/서브 대기가 포함되며 ball_speed는 입력 처리 직후의 공속도입니다. PNG 저장 프레임과 직후 3프레임을 제외하며 Playing 중 wall frame time의 중앙값/P95/P99를 보고합니다. GPU 전용 시간이나 실제 Windows 성능이 아닙니다. GPU batch 측정 실행은 경기 카메라에 1600×900 전용 렌더 타깃을 사용하며 보고서는 실제 카메라 해상도를 기록합니다.

```sh
python3 tools/verify-polish.py --out .work/game-polish/final --only survey
python3 tools/analyze-balance.py --metrics .work/game-polish/online/metrics --out .work/game-polish/balance-report
```

초기 6능력치 합계는 420으로 유지했습니다. 스크립트 입력 성공률을 사람 대 사람 승률로 해석하거나 15인 매치업 균형이 증명되었다고 주장하지 않습니다. 분석은 CSV 수정을 위한 관측 자료입니다.

## 빌드와 배포

`bash tools/unity.sh build-mac` / `build-windows` 후 `python3 tools/package-release.py --platform mac --out /absolute/release`로 로컬 ZIP·SHA256·소스 커밋 manifest를 만듭니다. 깨끗한 커밋 후 패키징하면 dirtyWorkingTree=false를 확인할 수 있습니다. 빌드 산출물은 Git에 추가하지 않습니다. Mac 패키지는 임시 복사본의 Finder/ResourceFork 메타데이터만 제거하고 codesign 검사를 통과한 뒤 압축합니다. 보안/격리 속성은 제거하지 않습니다. 개발용 서명/미공증 패키지이며 외부 게시나 유료 자산 구매는 하지 않습니다. Windows 교차 빌드는 실물 실행을 대신하지 않습니다.
