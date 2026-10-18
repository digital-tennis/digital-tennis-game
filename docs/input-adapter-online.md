# 키보드 입력 어댑터와 온라인 2인 플레이

## 입력이 게임에 도착하는 순서

키보드 또는 수신된 보드 동작 → `IPlayerInputSource` → `PlayerInputRouter` → `PlayerInputAdapter` → 로컬 경기 또는 온라인 경로 → `TennisMatchController.ApplyPlayerInput` → `SubmitInput` → 타격 검사 → 공 발사 순서입니다. 점수와 타격 수학은 입력 장치에 의존하지 않습니다. 화면 버튼도 같은 어댑터로 들어갑니다. AI와 코트 실험용 데모는 기존 정규화 `RacketInput` 경계를 사용합니다.

기본 소스는 `KeyboardPlayerInputSource`입니다. Space는 서브, Q/W는 **자신의** 포핸드/백핸드, R은 새 경기입니다. Practice에서만 E/D로 반대편을 조작할 수 있습니다. F/L은 폴트/렛 실험입니다. 1인 모드에서는 Near 사용자/Far AI, 온라인에서는 host=Near/guest=Far이며 양쪽 모두 Q/W를 사용합니다. 현재 서버는 게임마다 교대하고 타이브레이크 서비스 순서와 엔드 교대를 적용합니다. 세트·매치 진행은 [경기 규칙](match-rules.md)을 따릅니다. 이동은 게임이 자동으로 합니다.

`PlayerInputSample`은 동작, 0이 아닌 eventId, PC 수신 시각, 세기 0~1, 좌우/높낮이 -1~1을 가집니다. 장치가 플레이어·점수·공 좌표를 지정하지 않습니다. 어댑터가 로컬 역할과 현재 rallyId/shotId를 붙이고, 호스트는 거리·높이·타이밍·중복·문맥을 검사합니다. 0.25초보다 오래된 로컬 입력과 미래 시각, NaN/Infinity, 범위 밖 값은 거부합니다. 큐는 32개이며 오래 쌓인 입력부터 버립니다.

## 외장 IMU 보드를 나중에 연결하기

실물 구성은 **NU40-DK + 별도로 부착한 외장 IMU**입니다. 현재 SDK와 네이티브 backend가 OS 연결·본딩·패킷 검증을 처리합니다. 실제 연결 상태는 [본딩 검수](controller-bonding.md)를 따릅니다. 아래는 최초 어댑터 경계의 개발 예이며, 일반 사용자 설정은 [기기별 설정·저장 흐름](controller-setup-flow.md)과 현재 InputDeviceManager를 사용합니다. 보드의 보정·판정 결과는 정규화 입력으로 연결합니다. 패킷 규격과 BLE SDK 코드는 각 제품 저장소에 둡니다.

```csharp
// Unity 메인 스레드에서 연결이 준비됐을 때 한 번 실행합니다.
var boardSource = new BufferedPlayerInputSource("NU40-DK external IMU");
controller.InputAdapter.UseSource(boardSource);

// 디코딩된 새 동작을 받을 때 실행합니다. 값은 보정/정규화된 값입니다.
boardSource.Enqueue(new PlayerInputSample(
    PlayerAction.Forehand, eventId, Time.realtimeSinceStartupAsDouble,
    power01, aimX, aimZ));

// 키보드로 돌아가기
controller.InputAdapter.UseKeyboard();
```

- MCU 시각을 `ReceivedAt`에 넣지 않습니다. PC의 동일한 시계에서 수신한 시각을 사용합니다. BLE 콜백이 작업 스레드라면 Unity API를 호출하지 말고 원시 이벤트와 PC 수신 시간을 메인 스레드로 넘겨 같은 시계로 변환합니다. 대기 중 경과 시간도 보존해 오래된 이벤트가 새 입력이 되지 않게 합니다.
- eventId는 소스 내에서 중복되지 않는 uint32이며 0을 건너뜁니다. 재전송은 동일 ID를 유지합니다. 소스 교체는 이전/새 소스 큐를 모두 비우고 별도 sessionId를 발급하므로, 연결 전 스윙이 나중에 실행되지 않습니다.
- 보드가 준비되면 소스 구현만 교체합니다. `TennisMatch`, `TennisScore`, `RacketShot`에 BLE 분기를 추가하지 않습니다.
- `BufferedPlayerInputSource`는 연결 경계입니다. 실제 센서의 캘리브레이션·스윙 분류 정확도나 Windows BLE 성공을 입증하지 않습니다.

## 온라인 실행

1. 별도 `digital-tennis-match-server`에서 `npm ci --ignore-scripts`, `npm start`를 실행합니다.
2. Unity Editor 또는 Mac/Windows 게임 실행 파일을 두 개 실행합니다. 같은 프로젝트를 Editor 두 개에서 열지 않습니다.
3. 온라인 패널에 `ws://127.0.0.1:8080/ws`를 입력하고 첫 게임에서 Create room을 누릅니다. 다른 게임에서 표시된 6자리 코드를 입력하고 Join room을 누릅니다. LAN의 두 PC에서는 서버의 LAN 주소를 사용합니다.
4. 양쪽의 연결 완료와 선택된 전송 방식(P2P 또는 relay)을 확인하고 현재 서브 차례의 사용자가 Space로 서브합니다. 양쪽에서 Q/W로 자신의 라켓을 휘두릅니다. 포인트 종료 1.25초 뒤 다음 서브 대기로 돌아갑니다. 다시 Space를 누릅니다.
5. Leave online으로 연결을 정리하고 1인 모드로 돌아갑니다. R로 온라인 경기를 새로 시작하는 권한은 호스트에만 있습니다.

호스트의 공 물리는 60Hz, 상태 송신은 최대 30Hz입니다. 게스트는 100ms 버퍼로 상태를 보간해 표시하며 물리·득점을 실행하지 않습니다. 게스트 입력은 합의한 DataChannel 또는 서버 릴레이를 통해 호스트에 도착한 현재 타격 구간에서 처리합니다. rollback/지연 보정은 없으므로 큰 지연에서는 타격이 거절될 수 있습니다. 입력 inputSeq와 상태 tick은 uint32 wrap을 고려하고 이전 epoch·중복/오래된 메시지를 버립니다.

연결이 끊기면 양쪽 입력과 호스트 물리를 멈춥니다. 동일 실행 프로세스가 15초 유예 안에 돌아오면 메모리에 보관한 토큰으로 재접속하고, 서버가 토큰을 교체하고, 새 epoch를 합의한 다음 호스트 상태를 다시 보냅니다. 호스트 프로세스 종료 후 다른 프로세스로 상태 복원하거나 호스트를 이전하지 않습니다. 토큰은 로그·URL·파일에 쓰지 않습니다. 서버가 재시작되면 방은 사라집니다.

이 문서의 초기 검증은 **WebSocket 릴레이**를 사용했습니다. 이후 main에서 실제 WebRTC DataChannel·8초 탐색 제한·새 epoch 릴레이 복구를 구현하고 같은 Mac 두 프로세스로 확인했습니다. 최신 동작과 증거는 [소프트웨어 통합](software-integration.md)을 따릅니다. TURN 운영과 서로 다른 두 물리 Windows PC/WAN 검수는 별도입니다.

## A와 B의 화면 기준

A(호스트)는 Near 선수, B(게스트)는 Far 선수입니다. 외형과 색은 각각 선택한 캐릭터를 따릅니다. 이것은 네트워크상의 고정된 선수 ID입니다. 각 화면에서는 **자신이 화면 아래, 상대가 화면 위**에 보입니다. B는 코트 중심을 기준으로 카메라를 180도 돌립니다. 선수 좌표나 공·점수·입력 패킷을 다시 뒤집지 않습니다.

| 실제 동작 | A 화면 | B 화면 |
| --- | --- | --- |
| A의 포핸드/백핸드 타격 | 자기 캐릭터가 같은 종류로 스윙 | 상대 캐릭터가 같은 타격을 상대 방향에서 보여줌 |
| A가 자기 기준 오른쪽 이동 | 자기 캐릭터가 오른쪽 이동 | 상대 캐릭터가 왼쪽 이동 |
| B가 자기 기준 오른쪽 이동 | 상대 캐릭터가 왼쪽 이동 | 자기 캐릭터가 오른쪽 이동 |

양쪽 UI 점수는 YOU : OPPONENT 순서이며 자기 캐릭터의 색을 표시합니다. 현재 이동은 기존 자동 이동을 사용합니다. aimX의 +1은 각자의 화면에서 오른쪽입니다. 이미 `RacketShot`이 선수 방향을 적용하므로 입력을 추가로 반전시키지 않습니다. 온라인을 나가면 기존 1인 플레이 카메라로 복원합니다.

상태의 각 `players` 항목에는 `swingId`, `stroke`(1=포핸드/2=백핸드), `swingAge`(초)가 추가됩니다. 서버는 기존 상태 릴레이를 유지합니다. 게스트는 위치에 사용하는 보간 시점에 맞춰 이 모션을 재생하며, 타격 횟수의 홀짝으로 종류를 추정하지 않습니다. 0.35초가 지난 모션은 재접속해도 다시 시작하지 않습니다. 서브도 모션을 전달합니다. 양쪽 클라이언트 모두 같은 업데이트를 사용합니다. 초기에는 실제 접촉만 동기화했으며, 이후 main은 헛스윙도 swingId/종류/나이와 contact/serve 여부로 전달합니다. 오래된 상태를 새 동작으로 재생하지 않습니다.

## 재현 가능한 검증

```sh
# 같은 프로젝트 Editor를 저장하고 닫은 뒤
bash tools/unity.sh check
bash tools/unity.sh test-edit
bash tools/unity.sh test-play
bash tools/unity.sh build-mac
node tools/verify-online.mjs --server-repo ../digital-tennis-match-server
# 실제 게스트 연결을 한 번 끊고 복구하는 추가 검증
node tools/verify-online.mjs --exercise-reconnect --out .work/online-resume
```

마지막 명령은 별도 로컬 서버와 **서로 다른 PID의 Unity Mac 실행 파일 2개**를 실행합니다. 기본 `.work/online/`에 호스트/게스트 로그와 JSON 보고서를 기록합니다. 포핸드/백핸드, 양쪽 타격, 최소 4회 랠리, 4점 이상, 다음 서브, 동일 승자/점수, 게스트 물리 tick=0, 오류=0을 확인합니다. 검증용 입력은 명시적인 `-dt-online-role` 인자가 있을 때만 켜지고 실제 `PlayerInputAdapter`에 들어갑니다. 공·점수·타격 판정을 직접 바꾸지 않으며 일반 사용자 실행에서는 키보드만 사용합니다.

서버의 `npm test`는 별도로 방·시그널링·권한·재접속을 검증합니다. 실제 수행 결과는 [소프트웨어 통합 기록](software-integration.md)에 남깁니다.

온라인 검증 보고서의 `view`에는 실제 카메라 방향, 자기 캐릭터의 아래쪽 배치, 상대의 좌우 이동 반전 횟수, 수신한 위치와 실제 캐릭터 위치의 일치, 상대 포핸드·백핸드 모션 검사를 기록합니다. `--host-player`와 `--guest-player`로 각각 다른 실행 파일 경로를 줄 수 있습니다. 검증용 방 코드는 임시 파일 작성 후 원자적으로 공개해 다른 프로세스가 비어 있는 코드를 먼저 읽지 않게 합니다.

검증 전용 `-dt-view-image` 인자를 사용하면 두 번째 랠리에서 게임의 프레임을 PNG로 저장합니다. 이는 게임 화면만 저장하며 데스크톱의 다른 앱은 촬영하지 않습니다. `verify-online.mjs`가 이 인자를 전달하고 두 PNG가 생성됐는지도 확인합니다.

## 캐릭터·스킬 입력 확장

양쪽 게임과 서버 모두 이번 변경 버전을 사용해야 합니다. 게스트 입력 whitelist에 smash/volley/slice/lob/skill/character를 추가했고, character의 choice는 0~14 정수입니다. 게스트는 자신의 캐릭터만 선택하며 실제 stock/gauge/효과/능력치를 지정할 수 없습니다. 코트 선택과 CSV 재적용은 호스트가 담당합니다. 스냅샷의 characters[2], courtIndex, balanceRevision은 필수이며 게스트는 유효성 검사를 거쳐 같은 버퍼 시점에 표시합니다.

```bash
node tools/verify-online.mjs --offscreen --exercise-characters --exercise-officials --exercise-reconnect --out /private/tmp/digital-tennis-character-proof
```

이 검증은 사용자 파일과 분리된 CSV 복사본을 수정하고 일반 ReloadBalance 입력으로 재적용합니다. 양쪽이 실제 리턴으로 사용권 3개에 도달한 후 일반 Skill 입력을 사용합니다. 메터를 직접 충전하는 아트 미리보기와 다릅니다. 자세한 수치는 [최신 검증 기록](world-characters.md#검증-기록)을 참고하세요.
