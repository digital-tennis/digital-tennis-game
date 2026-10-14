# Digital Tennis Unity PC 테니스 게임

공·코트·서브·점수, 자동 이동, 1인 플레이, 온라인 2인 플레이를 담당하는 독립 제품 저장소입니다. 전체 프로젝트는 **외장 IMU를 별도로 부착한 NU40-DK**, Unity PC 게임, 1인 AI와 온라인 2인 대전을 12일·3명으로 개발합니다.

## 현재 상태

Unity **6.3 LTS / 6000.3.26f1** 기반의 3D 프로젝트입니다. 기본 `SinglePlayerCourt`에서 AI와 1인 플레이를 시작합니다. `MatchCourt`에서도 서브→랠리→득점→다음 서브를 실행하고 0·15·30·40·듀스·어드밴티지·승리를 계산합니다. `DevelopmentCourt`는 60Hz 공 이동과 바운스 실험용으로 유지합니다. 포핸드·백핸드 가상 입력과 캐릭터 자동 이동도 지원합니다. 상·중·하 AI는 별도 저장소의 고정 UPM 패키지로 연결했습니다. 실물 입력·온라인 기능은 다음 과제입니다.

Built-in Render Pipeline과 Mono를 사용합니다. 팀원 모두 같은 Editor 버전으로 프로젝트를 엽니다.

## 처음 실행하기

1. Unity Hub에서 **6000.3.26f1**을 설치합니다. Apple Silicon Mac은 ARM64 Editor를 선택합니다. Windows 실행 파일을 만들 때는 **Windows Build Support (Mono)** 모듈도 설치합니다.
2. 저장소를 복제한 뒤 Unity Hub의 **Add → Add project from disk**로 저장소의 루트 폴더를 추가합니다. `Assets` 폴더의 상위 폴더를 선택합니다.
3. 프로젝트를 열고 처음 가져오기가 끝날 때까지 기다립니다. `Assets/Scenes/SinglePlayerCourt.unity`를 열고 상단 ▶ 버튼으로 실행합니다.
4. VS Code에 Microsoft의 **Unity** 확장(`visualstudiotoolsforunity.vstuc`)을 설치합니다. Unity의 **Settings/Preferences → External Tools → External Script Editor**에서 VS Code를 선택합니다. C# 파일을 더블 클릭하면 편집기가 열립니다.
5. VS Code의 **Run and Debug → Attach to Unity**로 연결하면 실행 중인 코드에 중단점을 걸 수 있습니다.

이 Mac에는 Editor, Windows Mono 모듈과 VS Code의 Unity·C# 확장을 설치했습니다. Unity Hub에 프로젝트도 등록했습니다. [환경과 협업 안내](docs/development.md)를 참고하세요.

## AI와 1인 플레이

`SinglePlayerCourt`에서 Near(청록색)는 사용자, Far(주황색)는 AI입니다. 오른쪽 Easy/Medium/Hard로 난이도를 선택하고 **Space**로 서브, **Q/W**로 포핸드/백핸드를 입력합니다. 이동은 자동이며 `Near: HIT READY`가 표시될 때 타격합니다. 득점 후 다음 서브도 Space로 시작합니다. 난이도 변경과 R은 새 경기를 시작합니다.

AI는 반응 시간·이동 오차·실수 확률이 다른 독립 정책으로 동작하며, 사람과 동일한 타격 검사를 통과합니다. 1인 모드에서는 기존 자동 타격 데모와 Far용 E/D 입력을 막습니다. [1인 플레이·AI 패키지·검증 안내](docs/single-player-ai.md)를 참고하세요. Practice 버튼으로 기존 가상 입력 실험을 실행할 수 있습니다.

## 서브·랠리·득점 확인

`MatchCourt`의 기본 데모는 자동 서브→라켓으로 네 번 리턴→두 번째 바운스로 득점→다음 서브를 반복합니다. 득점 후 1.25초 뒤 공을 다음 서브 위치로 옮기고, 자동 서브가 켜져 있으면 0.75초 뒤 다시 발사합니다. 첫 서버는 Near로 유지하고 포인트마다 서브 좌우를 바꿉니다. 현재 경기는 **한 게임**이며 세트·타이브레이크는 포함하지 않습니다.

수동 확인은 **Demo: auto serve**와 **Demo: racket swings**를 끈 뒤 Space(서브), Q/W(Near 포핸드/백핸드), E/D(Far 포핸드/백핸드), F(잘못된 서비스 박스), L(네트 상단을 건드리는 렛 서브), R(경기 재시작)을 사용합니다. 같은 기능을 화면 버튼으로도 실행합니다. 서브 대기·랠리·득점·승리 상태에 따라 허용된 입력만 처리합니다.

첫 폴트에는 두 번째 서브를 기다리고, 더블 폴트에는 상대가 득점합니다. 유효 박스에 떨어진 네트 접촉은 렛으로 재서브하며 첫 폴트 횟수를 유지합니다. [규칙·점수·조작과 검증](docs/serve-rally-score.md)을 참고하세요. 타격은 거리 1.5 m·높이 0.2~2.5 m 안에서만 성공하며, 캐릭터는 예상 타격 위치를 향해 5 m/s로 자동 이동합니다. `Continuous rally`로 네 번 타격 제한을 해제할 수 있습니다. [가상 라켓 조작·입력 계약·자동 이동 과제](docs/racket-input-movement.md)를 참고하세요. 데모는 재현 가능한 가상 입력이며 AI 플레이어는 아닙니다.

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

`setup`은 기본 코트와 공 실험 도구를 준비하고, 별도 `MatchCourt`에 경기 컴포넌트를 연결합니다. 기존 코트 전체를 재생성하지 않습니다. 기본 빌드 시작 씬은 SinglePlayerCourt입니다. 기존 MatchCourt와 DevelopmentCourt도 포함합니다. Unity 메뉴의 **Digital Tennis**에서도 준비와 검증을 실행할 수 있습니다. 실행 로그와 테스트 결과는 `.work/`에 저장됩니다.

Editor가 기본 설치 위치에 없으면 `UNITY_EDITOR_PATH`에 Editor 실행 파일의 경로를 지정합니다. Windows에서는 Unity의 **File → Build Profiles**에서 Windows 플랫폼을 선택해 빌드합니다.

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
| `Assets/Scenes/SinglePlayerCourt.unity` | 사용자 vs 상·중·하 AI, 기본 시작 씬 |
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
