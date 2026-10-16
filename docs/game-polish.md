# 게임 완성도 개선 구현

2026-10-16의 개선 작업입니다. 최초 조건인 12일·3명과 NU40-DK에 외장 IMU를 별도 연결하는 제품 경계를 유지합니다.

## 선수와 경기장

`AthleteRigSetup`의 기존 관절/GUID 위에 `AthleteSkinSetup`이15개의 원본 스킨 메시를 생성합니다. `appearance.json`은 개인별 어깨·허리·골반·얼굴·턱·눈·코·입 비율, 헤어15종과 의상/봉제선/하의 실루엣을 정의합니다.15본 가중치로 팔꿈치·무릎을 연결하고 Focus/Smile/Shout/Blink 표정을 사용합니다. `BakeMesh` 검사로 팔 변형이 머리까지 이동시키지 않는지 확인합니다. 성인 페르소나의 외형 설정은 개인의 디자인이며 국가 전체의 신체/성격을 일반화하지 않습니다.

실제 타격이 인정된 순간의 `contactPoint`를 스윙 ID에 묶습니다. 표시용2본 IK가 스트링 중심을 그 좌표에 맞추고 손-그립을 연결합니다. 팔의 도달 길이는 기존 아케이드 타격 범위에 맞춘 양식화된 표현입니다. 준비/팔로스루/회복과 양발의 접지 앵커/무릎/골반 보정은 모델에만 적용하고 선수 루트·공·판정을 옮기지 않습니다. 게스트도 위치와 스윙/접촉을 같은 보간 시간선에 표시하며 같은 ID의 접촉 좌표 변경과 오래된 체크포인트 재생을 거절합니다.

`skill-art.json`은15명 각자의 준비/강조/분출/복귀5키, 표정, 이동량·점프, 카메라/FOV, 서로 다른 리본 경로를 정의합니다. `SkillCameraDirector`가1.2초 종료 시 원래 시점을 정확히 복원합니다. 공/규칙/이동은 기존 권위 측에서 동결하고 global timeScale을 바꾸지 않습니다. 움직임 줄이기는 카메라를 유지하고 자세/효과 움직임을 줄입니다. 연출 캡처의 미리보기 stock 부여는 실제 게임의 충전/소비 검증과 구분합니다.

`court-art.json`의15개 시간대·태양/주변광·하늘/안개와 원본 도시/자연 배경을 연결했습니다. 코트 셰이더는 하드 코트의 미세한 입자, 클레이 입자, 잔디 줄무늬를 표현합니다. 원본 기하 문양은 코트 밖에 배치하고 배경은 양쪽 관중석 너머에만 생성합니다. 한국 강변 지붕·다리, 일본 정원 파빌리온, 중국 테라스, 미국 도시, 러시아 겨울 다면체, 인도 격자, 독일 트러스, 영국 정원, 스페인 회랑, 프랑스 맨사드, 호주 해안 돛, 네덜란드 계단 박공, 브라질 언덕, 모로코 아치/모래, 남아공 단형 산을 창작 모티프로 사용했습니다. 실제 랜드마크/문화의 재현이나 역사 고증을 주장하지 않습니다. 대진 소개의 첫1.3초는 낮은 시점의 경기장 전경을 보여주고2.2초까지 원래 경기 카메라로 복귀합니다. 움직임 줄이기는 이 이동도 생략합니다. 경기 크기·라인·볼 물리·콜라이더는 동일합니다.

624석·502명 관중은 기존 결합 메시를 유지하고 shader에서 작은 움직임과 실제 득점 반응을 표현합니다. 낮음에서는 관중 렌더링과 그림자를 끄고, 보통은 2x AA/단순 그림자, 높음은 4x AA/부드러운 그림자를 사용합니다. 모두 물리·밸런스·온라인 상태를 바꾸지 않습니다. 점수 HUD는 좌측 위, 입력 안내는 아래로 옮겨 중앙 전광판과 경기 시야를 확보했습니다.

## 음향과 청음

`tools/generate-audio.py`가 NumPy 합성으로 메뉴/경기/결과/듀스/중요 포인트 음악 A/B10곡과 효과음/환경음11개를 생성합니다. 추가 의존성은 NumPy 1.26.4이며 `--flite /path/to/flite`는 영어 합성 음성130개와 청음용3테이크를 생성합니다. 캐릭터15명 × ready/skill/win/lose 기본/대체8개와 심판10개입니다. 대체 대사는 준비/자신감·외침·승리 감사·패배 후 재도전의 감정 방향을 구분하고 문장/발화 속도를 바꿉니다. 총151cue입니다. 사람 배우나 각국 언어/억양의 완성을 뜻하지 않습니다. 현재 한국어 자막이 있으며 음원 파일명과 자막은 외부 CSV로 바꿀 수 있습니다.

Flite는 공식 festvox/flite의 `6c9f20dc915b17f5619340069889db0aa007fcdc`에서 도구만 로컬 빌드했습니다. 배포되는 음원과 함께 `Assets/StreamingAssets/Audio/FLITE-COPYING.txt`를 유지하세요. 엔진 바이너리는 저장소에 넣지 않습니다. 음악·효과음에는 다른 게임이나 상용 녹음 샘플을 넣지 않았습니다. 출처와 조건: [공식 Flite COPYING](https://github.com/festvox/flite/blob/6c9f20dc915b17f5619340069889db0aa007fcdc/COPYING).

`TennisAudio`는 음악·음성·효과·환경음의 논리 버스, 두 음악 소스 교차 페이드, 음성 중 BGM duck, 전체/그룹 음량 저장, 자막, 제한된 효과 소스 풀을 제공합니다. Unity AudioMixer 자산을 생성한 것은 아닙니다. 설정→오디오에서 A/B를 직접 비교할 수 있고 기본은 A입니다. 음성은 최대 6개 대기열에서 한 번에 하나씩 재생해 심판과 선수 음성이 겹치지 않도록 했습니다. 2026-10-17 사용자 요청으로 경기 음악 정책을 변경했습니다. 서브 대기·랠리·스킬·일시정지 동안 BGM 소스 두 개를 정지하고 볼륨을0으로 유지합니다. 듀스/중요 포인트 음악은 오디오 설정의 청음 자료로만 남습니다. 메뉴/오프닝/결과에는 음악을 허용합니다. 득점 횟수 증가에만 함성을1회 재생하고, 다음 서브/랠리로 넘어가면 함성과 환경음을 정지합니다. 심판 콜은 양쪽에서 표시 중인 같은 점수 상태를 읽습니다. 새 포인트의 듀스/매치 포인트 콜만 재생해 재접속 스냅샷에서 콜을 반복하지 않습니다. 사람 청음 전에는 기본A를 임시 선택으로 표시합니다.

음원은 첫 실행 때 사용자 폴더로 복사하며 기존 파일은 덮어쓰지 않습니다. `audio.csv`의 cue/group/file/volume/loop/fade/source를 편집하고 F5 또는 오디오 다시 읽기를 누릅니다. WAV/OGG 파일만 같은 폴더에서 읽으며 경로 탈출·URL·중복·비정상 수치를 거절합니다. 음성/단발 효과의 loop=1은 거절합니다. 환경음 루프도 유효한 새 파일로 교체합니다. 이전85cue 외부 CSV는 사용자 행/음량을 유지하고 새 대체 음성/긴장 음악만 배포 기본값으로 보충합니다. 새 파일도 기존 파일을 덮어쓰지 않습니다. 소리를 끄려면 해당 행 volume=0으로 둡니다. 모든 파일의 비동기 로드/검증이 성공한 뒤 교체하며 실패하면 이전 음원을 유지합니다. 한 파일 32MB, 전체 128MB, 120초 이하로 제한합니다. 설정 화면에 실제 외부 경로를 표시합니다.

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

초기6능력치 합계는420으로 유지합니다. 전체225순서쌍·3난이도·3시드·3입력전략·양쪽 선공을 비교하는 `DigitalTennis.Editor.BalanceExperiment.Run`과 `tools/compare-matchups.py`를 추가했습니다. 조건부 자동 입력 결과로 사람의 승률을 추정하지 않습니다. [전후 실측 보고서](reports/matchup-comparison.md)에 실제 CSV 조정 근거를 기록합니다. 기존 외부 gameplay.csv는 덮어쓰지 않으므로 새 기본 체력 비용을 적용하려면 energy.hit_cost=3.2, energy.move_cost=0.72로 수정하고 다음 안전한 시점에 F5로 읽으세요. 스크립트 입력 성공률을 사람 대 사람 승률로 해석하거나 15인 매치업 균형이 증명되었다고 주장하지 않습니다. 분석은 CSV 수정을 위한 관측 자료입니다.

## 빌드와 배포

개발 검증은 `build-mac` / `build-windows`, 비개발 배포는 `release-mac` / `release-windows`로 분리합니다. 개발/배포 전환 중 이전 스크립트 레이아웃 캐시가 재사용되지 않게 배포는 [CleanBuildCache](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/BuildOptions.CleanBuildCache.html)로 전체 재빌드합니다. 배포 출력은 `Builds/release/{mac,windows}`이고 BuildReport의 실제 Development/AllowDebugging 비트가 모두 false인지 확인하여 `build-info.json`에 소스 커밋/Unity/장면과 함께 기록합니다. 검증 입력/캡처는 명시적인 CLI 플래그가 있는 실행에만 설치되며 일반 사용자 실행에서는 동작하지 않습니다.

깨끗한 커밋에서 배포 빌드를 만든 뒤 `python3 tools/package-release.py --platform mac --out /absolute/release`를 실행합니다. 기본 configuration은 release이며, dirty 소스·HEAD와 다른 빌드·개발/디버깅 플래그를 거절합니다. 개발 ZIP은 별도 `--configuration development`로만 생성합니다. Mac은 임시 복사본의 Finder/ResourceFork 메타데이터만 제거하고 codesign 검사를 통과한 뒤 압축합니다. 보안/격리 속성은 제거하지 않습니다. ZIP/SHA256/manifest는 Git 밖에 둡니다. Developer ID 공증/스토어 배포는 하지 않으며 Windows 교차 빌드는 실물 실행을 대신하지 않습니다.

## 실시간 오프닝과 경기 음향 정책

[오프닝 연출·음향 상세](cinematic-and-court-audio.md)를 참고하세요. 기존 선수·라켓·공·경기장을 직접 렌더하는12.5초 편집 시퀀스입니다. `opening-edit.json`에서9개 컷의 시점/주시점/FOV/기울기/시간을 편집합니다. 게임 모델과 복제된 연출 모델을 분리하고 종료·스킵 때 원래 렌더러를 복원합니다. 경기 입력·물리·점수·stock에는 쓰지 않습니다. Enter/ESC/화면 버튼으로 건너뛰고 타이틀에서 다시 볼 수 있습니다. 움직임 줄이기에서는 스튜디오 카드만 표시합니다.
