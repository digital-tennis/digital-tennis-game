# Digital Tennis Unity PC 테니스 게임

공·코트·서브·점수, 자동 이동, 1인 플레이, 온라인 2인 플레이를 담당하는 독립 제품 저장소입니다. 전체 프로젝트는 **외장 IMU를 별도로 부착한 NU40-DK**, Unity PC 게임, 1인 AI와 온라인 2인 대전을 12일·3명으로 개발합니다.

## 현재 상태

Unity **6.3 LTS / 6000.3.26f1** 기반의 3D 프로젝트입니다. 기본 `FrontEnd`에서 조직 로고→타이틀→메뉴→세계지도 선수/상대/코트 선택→대진 소개→경기로 이어집니다. `MatchCourt`에서도 서브→랠리→득점→다음 서브를 실행하고 0·15·30·40·듀스·어드밴티지·승리를 계산합니다. `DevelopmentCourt`는 60Hz 공 이동과 바운스 실험용으로 유지합니다. 포핸드·백핸드·스매시·발리·슬라이스·로브 입력과 캐릭터 자동 이동을 지원합니다. 15개 국가의 가상 성인 캐릭터, 국가별 코트 테마와 고유 스킬을 선택할 수 있습니다. 상·중·하 AI는 별도 저장소의 고정 UPM 패키지로 연결했습니다. 키보드를 공통 입력 어댑터에 연결했고, 방 코드와 WebSocket 릴레이를 통한 온라인 2인 플레이를 지원합니다. 실물 IMU/BLE 입력 소스와 직접 P2P는 후속 과제입니다.

Built-in Render Pipeline과 Mono를 사용합니다. 팀원 모두 같은 Editor 버전으로 프로젝트를 엽니다.

## 캐릭터와 게임 디자인 개선 계획

[캐릭터·코트·카메라·UI 개선 계획](docs/art-and-design-plan.md)에 제작 방법, 국가별 개성, 공통 골격과 테니스 동작, 우선순위와 3인 작업 분담을 정리했습니다. 첫 제작 범위는 한국·일본 선수 2명과 서울 코트 1개에서 품질 기준을 확정하는 안입니다. [음성·BGM 제작 및 청음 작업안](docs/audio-production-research.md)은 음악 후보 비교, 장면별 전환, Unity 연결과 외부 CSV·음원 교체 계획을 다룹니다. 두 문서는 향후 제작 계획이며 현재 게임에 새 모델이나 음원을 추가한 변경은 아닙니다.

## 처음 실행하기

1. Unity Hub에서 **6000.3.26f1**을 설치합니다. Apple Silicon Mac은 ARM64 Editor를 선택합니다. Windows 실행 파일을 만들 때는 **Windows Build Support (Mono)** 모듈도 설치합니다.
2. 저장소를 복제한 뒤 Unity Hub의 **Add → Add project from disk**로 저장소의 루트 폴더를 추가합니다. `Assets` 폴더의 상위 폴더를 선택합니다.
3. 프로젝트를 열고 처음 가져오기가 끝날 때까지 기다립니다. `Assets/Scenes/FrontEnd.unity`를 열고 상단 ▶ 버튼으로 실행합니다.
4. VS Code에 Microsoft의 **Unity** 확장(`visualstudiotoolsforunity.vstuc`)을 설치합니다. Unity의 **Settings/Preferences → External Tools → External Script Editor**에서 VS Code를 선택합니다. C# 파일을 더블 클릭하면 편집기가 열립니다.
5. VS Code의 **Run and Debug → Attach to Unity**로 연결하면 실행 중인 코드에 중단점을 걸 수 있습니다.

이 Mac에는 Editor, Windows Mono 모듈과 VS Code의 Unity·C# 확장을 설치했습니다. Unity Hub에 프로젝트도 등록했습니다. [환경과 협업 안내](docs/development.md)를 참고하세요.

## 시작 화면과 세계지도 선택

실행하면 조직 로고와 **DIGITAL TENNIS** 타이틀이 나타납니다. Enter/Space로 시작해 **1인 / 온라인 / 연습 / 설정**을 고릅니다. 세계지도의 국가 마커 또는 하단 선수 카드를 선택하고, 상대·난이도·코트를 확정하면 대진 소개 후 경기에 들어갑니다. 경기 HUD는 점수·판정·스킬·체력을 표시하고, Esc로 경기 메뉴를 엽니다. 경기 결과에서 재경기 또는 메인 메뉴를 고릅니다.

[전체 시나리오·조작·참고 영상·검증 방법](docs/front-end.md)을 참고하세요. 메뉴는 실제 경기 입력을 막으며, 오프라인 일시정지는 공과 점수를 멈춥니다. 온라인 메뉴는 상대 경기를 멈추지 않습니다. 새 시작 흐름의 선택 화면을 사용하며, 기존 C 디버그 메뉴는 경기 씬을 직접 실행할 때 사용할 수 있습니다.

## AI와 1인 플레이

서브·타격·바운스·스킬 충전/발동에 파티클 효과를 표시합니다. 설정에서 효과 밀도를 고르며, 외부 `Effects/effects.csv`를 수정하고 F5로 빌드 없이 강도·색·수량·수명을 조절합니다. [효과 구성·CSV·온라인 검증](docs/gameplay-effects.md)을 참고하세요.

기본 메뉴의 **1인 플레이**에서 본인·상대 선수와 AI 난이도 하/중/상을 고릅니다. 실제 경기 씬인 `SinglePlayerCourt`에서 Near는 사용자, Far는 AI입니다. **Space**로 서브, **Q/W**로 포핸드/백핸드를 입력합니다. 이동은 자동이며 득점 후 다음 서브도 Space로 시작합니다. 경기 메뉴는 Esc, 종료 후 재경기는 결과 화면에서 선택합니다. 개발용으로 `SinglePlayerCourt`만 직접 열면 기존 C 선택 메뉴와 Easy/Medium/Hard·R 초기화 도구를 사용할 수 있습니다.

AI는 반응 시간·이동 오차·실수 확률이 다른 독립 정책으로 동작하며, 사람과 동일한 타격 검사를 통과합니다. 1인 모드에서는 기존 자동 타격 데모와 Far용 E/D 입력을 막습니다. [1인 플레이·AI 패키지·검증 안내](docs/single-player-ai.md)를 참고하세요. Practice 버튼으로 기존 가상 입력 실험을 실행할 수 있습니다.

## 캐릭터·스킬·CSV 밸런스

한국·일본·중국·미국·러시아·인도·독일·영국·스페인·프랑스·호주·네덜란드·브라질·모로코·남아공의 가상 선수 15명과 코트 테마 15개를 제공합니다. 기본 시작 화면의 세계지도에서 본인/상대/코트를 고릅니다. 경기 씬을 직접 실행할 때는 기존 **C** 개발 메뉴를 사용할 수 있습니다. 온라인에서는 각자 본인 캐릭터를, 호스트가 코트를 선택합니다.

서브·스매시·발리·수비·체력·스피드의 초기 합계는 모두 420입니다. 성공한 공격/수비로 게이지가 100이 되면 스킬 사용권을 얻으며 최대 3개입니다. **K** 또는 화면 버튼으로 사용하면 캐릭터별 1.2초 포즈·카메라 줌·리본 연출 후 짧은 능력 강화가 적용됩니다. 연출 중 경기 시계를 멈추고 같은 공에서 재개합니다. 스킬이 네트·라인·타격 판정이나 점수를 우회하지 않습니다. **S/V/X/B**는 스매시/발리/슬라이스/로브입니다.

[기본 밸런스 CSV](Assets/StreamingAssets/Balance/characters.csv)는 최초 실행 시 사용자 저장 폴더의 `Balance/characters.csv`로 복사됩니다. 설정 화면(개발 씬에서는 C 메뉴)에 표시된 경로의 파일을 수정하고 서브 대기 중 **F5**로 읽으면 **다시 빌드하지 않고 적용**합니다. 진행 중 스킬이 없어야 하며, 잘못된 CSV는 이전 값으로 유지합니다. 온라인은 호스트 값과 revision을 양쪽에 동기화합니다.

[15명 페르소나·능력치·스킬·코트와 CSV 사용법](docs/world-characters.md), [음성/BGM 조사와 청음 제작 절차](docs/audio-production-research.md), [청음 목록 CSV](docs/audio-audition-plan.csv)를 참고하세요. 모델과 연출은 원본 절차적 3D 프로토타입이며, 음원은 아직 만들거나 삽입하지 않았습니다. 경기 입력 피드백 변경 후 Unity EditMode 110개·PlayMode 29개와 Mac 빌드를 통과했습니다. 기본 키보드의 포핸드/백핸드 리턴과 두 클라이언트 온라인 4–1 종료도 확인했습니다. [시작 흐름의 화면·검증 한계](docs/front-end.md), [캐릭터 검증 기록](docs/world-characters.md#검증-기록)을 함께 확인하세요.

## 입력 어댑터와 온라인 2인 플레이

키보드와 화면 버튼은 `PlayerInputAdapter`를 통해 경기로 전달됩니다. 이후 **외장 IMU를 부착한 NU40-DK** 컨트롤러가 준비되면 `IPlayerInputSource`만 교체합니다. 하드웨어 입력 때문에 경기·점수 코드를 수정하지 않습니다.

경기는 **Space로 서브, Q/W로 포핸드/백핸드**를 입력하고 캐릭터 이동은 자동입니다. HUD에서 타격 가능한 순간과 마지막 입력의 성공/헛스윙 이유를 확인할 수 있습니다. S 스매시는 높은 공에 사용합니다. 헛스윙도 라켓 모션으로 보이며, 공 반환·점수·체력/사용권은 기존 성공 판정에서만 변합니다.

별도 `digital-tennis-match-server`를 실행한 뒤 오른쪽 온라인 패널에서 Create room으로 만든 6자리 코드를 다른 클라이언트의 Join room에 입력합니다. 양쪽이 WebSocket relay 모드를 합의한 뒤 호스트는 Space로 서브하고, 양쪽 모두 Q/W로 자신의 라켓을 조작합니다. 호스트는 Near, 게스트는 Far입니다. 각 화면은 자신의 캐릭터를 아래쪽에 표시합니다. A가 자기 기준 오른쪽으로 움직이면 B 화면의 상대 캐릭터는 왼쪽으로 움직이며, 상대 라켓도 전달된 실제 포핸드·백핸드 모션으로 재생합니다. 온라인 모드에서는 AI가 실행되지 않으며 호스트만 공/점수를 계산합니다. [소스 교체 코드, 방 접속, 네트워크 책임과 검증 안내](docs/input-adapter-online.md)를 참고하세요.

## 서브·랠리·득점 확인

코트 옆 의자에 **심판 캐릭터**, 네트 옆에 **볼보이**가 있습니다. 심판은 폴트·더블 폴트·렛·아웃·득점과 듀스/어드밴티지/게임을 화면 하단에 알리고 팔로 신호합니다. **N**은 네트에 걸리는 서브를 실제로 발사합니다. 판정 후 볼보이가 접근→줍기→공을 들고 코트 밖으로 복귀하며, 회수가 끝나야 다음 서브를 받습니다. 네트에 닿아도 살아 있는 랠리 공은 회수하지 않습니다. [적용 규칙과 심판·볼보이 확인 절차](docs/court-officials.md)를 참고하세요.

심판·볼보이 변경은 Unity EditMode **82개**, PlayMode **19개**, 프로젝트 check와 Mac 빌드를 통과했습니다. 두 실제 Mac 게임 프로세스에서 네트 폴트→렛→더블 폴트→회수 3회→다음 랠리→**4–1** 게임 종료와 epoch 2 재접속을 확인했습니다. 양쪽 통신/실행 오류 0개, 게스트 물리 tick 0입니다. `--offscreen` 실행에서는 Mac 잠금을 유지한 채 실제 GPU 게임 카메라 PNG를 저장해 심판, 볼보이의 줍기·복귀 및 양쪽 랠리 시점을 확인했습니다. 이 카메라 이미지는 점수·버튼(IMGUI)을 포함하지 않으며 창 모드 UI 확인과 구분합니다.

`MatchCourt`의 기본 데모는 자동 서브→라켓으로 네 번 리턴→두 번째 바운스로 득점→다음 서브를 반복합니다. 득점 표시 후 1.25초 뒤 공을 다음 서브 위치로 옮기고, 자동 서브가 켜져 있으면 0.75초 뒤 다시 발사합니다. 네트 접촉으로 끝난 공은 회수 완료 후 이 대기를 시작합니다. 첫 서버는 Near로 유지하고 포인트마다 서브 좌우를 바꿉니다. 현재 경기는 **한 게임**이며 세트·타이브레이크는 포함하지 않습니다.

수동 확인은 **Demo: auto serve**와 **Demo: racket swings**를 끈 뒤 Space(서브), Q/W(Near 포핸드/백핸드), E/D(Far 포핸드/백핸드), F(잘못된 서비스 박스), L(네트 상단을 건드리는 렛 서브), R(경기 재시작)을 사용합니다. 같은 기능을 화면 버튼으로도 실행합니다. 서브 대기·랠리·득점·승리 상태에 따라 허용된 입력만 처리합니다.

첫 폴트에는 두 번째 서브를 기다리고, 더블 폴트에는 상대가 득점합니다. 유효 박스에 떨어진 네트 접촉은 렛으로 재서브하며 첫 폴트 횟수를 유지합니다. [규칙·점수·조작과 검증](docs/serve-rally-score.md)을 참고하세요. 타격은 기본 거리 1.5 m·높이 0.2~2.5 m 조건을 검사하며, 기본 자동 이동은 5 m/s입니다. 캐릭터 수비/스피드, 체력과 활성 스킬이 거리·속도를 보정합니다. `Continuous rally`로 네 번 타격 제한을 해제할 수 있습니다. [가상 라켓 조작·입력 계약·자동 이동 과제](docs/racket-input-movement.md)를 참고하세요. 데모는 재현 가능한 가상 입력이며 AI 플레이어는 아닙니다.

## 공 이동과 바운스 확인

Unity에서 `DevelopmentCourt`를 열고 ▶를 누르면 공이 자동으로 발사됩니다. 왼쪽 패널의 버튼이나 **1~6 키**로 코트 안, 라인 위, 코트 밖, 네트, 서비스 박스 안/밖 사례를 선택합니다. **R**은 같은 사례 재실행입니다. 반복 관찰을 멈추려면 **Replay every 6 seconds**를 해제합니다.

첫 착지의 **IN/OUT**, 바운스 횟수와 네트 충돌 횟수가 화면에 표시됩니다. 착지 표시는 초록색(in), 빨간색(out)이며 Console에는 사건별 기록이 남습니다. 두 번째 바운스까지 구분하지만 이번 모듈이 점수를 올리지는 않습니다. [물리 설정·사건 계약·검증 방법](docs/ball-motion.md)을 참고하세요.

## Mac에서 실행·검증·빌드

저장소 루트에서 실행합니다. **일괄 검증이나 빌드 전에는 같은 프로젝트를 연 Unity Editor를 저장하고 닫습니다.**

```bash
bash tools/unity.sh open           # Unity Editor에서 프로젝트 열기
bash tools/unity.sh check          # C# 컴파일, 시작 씬과 공통 설정 검증
bash tools/unity.sh build-mac      # Builds/mac/DigitalTennis.app
bash tools/unity.sh build-windows  # Builds/windows/DigitalTennis.exe
bash tools/unity.sh preview        # .work/development-preview.png
bash tools/unity.sh test-edit      # 물리·점수·서브/랠리 규칙·중복 사건 검증
bash tools/unity.sh test-play      # 실제 공 실험·경기·득점 후 다음 서브 검증
```

`setup`은 기본 코트와 공 실험 도구를 준비하고, 별도 `MatchCourt`에 경기 컴포넌트를 연결합니다. 기존 코트 전체를 재생성하지 않습니다. 기본 빌드 시작 씬은 FrontEnd이며 실제 경기는 SinglePlayerCourt를 함께 불러옵니다. 기존 MatchCourt와 DevelopmentCourt도 포함합니다. Unity 메뉴의 **Digital Tennis**에서도 준비와 검증을 실행할 수 있습니다. 실행 로그와 테스트 결과는 `.work/`에 저장됩니다.

Editor가 기본 설치 위치에 없으면 `UNITY_EDITOR_PATH`에 Editor 실행 파일의 경로를 지정합니다. Windows에서는 Unity의 **File → Build Profiles**에서 Windows 플랫폼을 선택해 빌드합니다.

## 플레이어별 시점과 상대 모션 검증

A와 B는 선택한 캐릭터의 색과 복장으로 표시되며 각자의 캐릭터가 화면 아래에 보입니다. 상대가 자기 기준 오른쪽으로 이동하면 내 화면에서는 왼쪽으로 보입니다. 월드 좌표는 공유하고 B의 카메라만 코트 반대편으로 전환합니다. 점수와 YOU/OPPONENT 표시도 자기 기준입니다. 현재 캐릭터 이동은 자동 이동입니다.

상대 모션은 타격 횟수의 홀짝 대신 각 선수의 `swingId/stroke/swingAge`로 동기화하고, 공·선수 위치와 같은 보간 시점에서 재생합니다. 서브 모션도 포함하며 오래된 모션을 재접속 후 다시 시작하지 않습니다. 양쪽 클라이언트 모두 이 업데이트를 사용해야 합니다.

Unity EditMode 75개·PlayMode 16개와 Mac 빌드를 통과했습니다. 두 실제 실행 프로세스 검증은 기존 경기/점수 검사에 더해 양쪽 카메라, 상대 좌우 이동 반전, 수신 위치와 실제 캐릭터 위치, 정확한 상대 포핸드·백핸드를 검사합니다. `node tools/verify-online.mjs`는 `host-view.png`와 `guest-view.png`도 게임 자체에서 저장합니다.

## 기본 기능 검증 기록 (2026-10-15)

- 서버 테스트 15개 및 GitHub Node 22 CI, Unity EditMode 69개·PlayMode 13개, 프로젝트 check와 Mac 빌드 통과.
- 기본 키보드 소스로 Unity Editor에서 직접 Space/Q/W를 입력해 사용자 6회·AI 7회, 총 13회 랠리 후 득점·다음 서브 실행을 확인. 실행 중 경고/오류 0개.
- 서로 다른 Mac Unity 실행 프로세스 2개에서 같은 방에 입장하고, 양쪽 12회씩 타격·최대 6회 랠리·4번 서브 후 양쪽 점수 **4–0**, 같은 승자 확인. 호스트 물리 3,021 tick, 게스트 **0 tick**, 프로토콜/실행 오류 0개.
- 두 번째 실행에서는 게스트 소켓을 경기 중 한 번 강제로 끊어 pause → 토큰 재접속 → epoch 2 합의 → 동일 경기 종료(4–0)를 확인. 양쪽 오류 0개, 게스트 물리 0 tick.

온라인 반복 검증은 검증용 입력 소스를 실제 어댑터에 연결한 결과입니다. 기본 사용자 실행은 키보드입니다. 같은 Mac 두 프로세스 검증이며 **두 물리 Windows PC, WAN, 직접 P2P 또는 실물 BLE 검증은 수행하지 않았습니다.**

## 개발할 기능과 제출 기준

[초보 개발자용 상세 과제](https://github.com/digital-tennis/digital-tennis-docs/blob/main/docs/products/digital-tennis-game.md)에서 상황·입력·출력·예제·예외·완료 기준을 확인합니다.

- [GM-01 공통 Unity 환경과 테스트 코트](https://github.com/digital-tennis/digital-tennis-docs/blob/main/docs/products/digital-tennis-game.md#gm-01)
- [GM-02 공 이동과 바운스 판정](https://github.com/digital-tennis/digital-tennis-docs/blob/main/docs/products/digital-tennis-game.md#gm-02)
- [GM-03 서브와 랠리 상태기계](https://github.com/digital-tennis/digital-tennis-docs/blob/main/docs/products/digital-tennis-game.md#gm-03)
- [GM-04 순수 점수 함수와 중복 득점 방지](https://github.com/digital-tennis/digital-tennis-docs/blob/main/docs/products/digital-tennis-game.md#gm-04)
- [GM-05 라켓 입력으로 샷과 자동 이동](https://github.com/digital-tennis/digital-tennis-docs/blob/main/docs/products/digital-tennis-game.md#gm-05)
- [GM-06 1인 모드와 메뉴·상태 UI](https://github.com/digital-tennis/digital-tennis-docs/blob/main/docs/products/digital-tennis-game.md#gm-06)
- [GM-07 온라인 로비와 P2P·릴레이 전송 어댑터](https://github.com/digital-tennis/digital-tennis-docs/blob/main/docs/products/digital-tennis-game.md#gm-07)
- [GM-08 호스트 권위와 상태 동기화·재접속](https://github.com/digital-tennis/digital-tennis-docs/blob/main/docs/products/digital-tennis-game.md#gm-08)
- [GM-09 Windows 릴리스와 재현 가능한 플레이 검증](https://github.com/digital-tennis/digital-tennis-docs/blob/main/docs/products/digital-tennis-game.md#gm-09)

## 프로젝트 구조

초기 담당 역할은 B입니다. 실제 IMU·배선·펌웨어 SDK·Windows 버전은 D1에 [환경표](https://github.com/digital-tennis/digital-tennis-docs/blob/main/docs/project/overview.md)와 결정 기록에 남깁니다. 컨트롤러는 **NU40-DK에 외장 IMU를 별도로 부착**합니다.

| 경로 | 역할 |
| --- | --- |
| `Assets/Scenes/FrontEnd.unity` | 기본 시작 씬, 로고·메뉴·세계지도·결과 흐름 |
| `Assets/Scenes/SinglePlayerCourt.unity` | 사용자 vs 상·중·하 AI, 실제 1인 경기 씬 |
| `Assets/Scenes/MatchCourt.unity` | 가상 입력과 서브·랠리·점수 실험 씬 |
| `Assets/Scenes/DevelopmentCourt.unity` | 독립적인 공 이동·바운스 실험 씬 |
| `Assets/Materials/` | 코트·네트·임시 모델의 머티리얼 |
| `Assets/Scripts/Runtime/` | 공 이동, 경기 상태, 순수 점수와 Unity 실행 코드 |
| `Assets/Scripts/Editor/` | 씬 준비·검증·빌드 도구 |
| `Assets/Tests/` | EditMode 수치 검증과 PlayMode 씬 검증 |
| `Packages/` | Unity 패키지와 잠금 파일 |
| `ProjectSettings/` | 팀 공통 설정과 Editor 버전 |
| `tools/unity.sh` | Mac에서 사용하는 실행·검증 명령 |
| `AGENTS.md` | Codex가 따를 프로젝트 작업 규칙 |

`Assets`, `Packages`, `ProjectSettings`와 모든 `.meta` 파일을 함께 커밋합니다. Unity가 생성하는 `Library`, `Temp`, `UserSettings`, 빌드 파일과 C# 솔루션은 커밋하지 않습니다. 파일을 옮길 때는 Unity의 Project 창을 사용해 `.meta`의 식별자를 유지합니다.

## 검증과 협업

- [12일 WBS](https://github.com/digital-tennis/digital-tennis-docs/blob/main/docs/planning/wbs.md)
- [컨트롤러 계약](https://github.com/digital-tennis/digital-tennis-docs/blob/main/docs/contracts/controller-protocol.md)
- [온라인 계약](https://github.com/digital-tennis/digital-tennis-docs/blob/main/docs/contracts/online-protocol.md)
- [수용 테스트](https://github.com/digital-tennis/digital-tennis-docs/blob/main/docs/testing/acceptance.md)
- [브랜치·PR 작업 방법](https://github.com/digital-tennis/digital-tennis-docs/blob/main/docs/guides/contributing.md)
- [모든 제품과 의존 관계](https://github.com/digital-tennis/digital-tennis-docs/blob/main/docs/project/repositories.md)

라이선스는 코드와 제3자 자산 조건을 검토한 뒤 팀이 선택합니다.

PR은 스쿼시 머지하고, 병합한 원격·로컬 기능 브랜치는 삭제합니다.

### CSV로 밸런스 조절

공 물리·샷·이동·체력·서브 진행·AI 난이도는 `gameplay.csv`, 캐릭터 능력치/고유 스킬은 `characters.csv`에서 조절합니다. 외부 CSV 수정 후 안전한 서브 대기에서 F5로 함께 적용합니다. 오류 시 두 파일 모두 이전 값을 유지합니다. [수정 예제·전체 키·범위·온라인 적용 안내](docs/gameplay-balance.md)를 확인하세요.
