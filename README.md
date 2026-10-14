# Digital Tennis Unity PC 테니스 게임

공·코트·서브·점수, 자동 이동, 1인 플레이, 온라인 2인 플레이를 담당하는 독립 제품 저장소입니다. 전체 프로젝트는 **외장 IMU를 별도로 부착한 NU40-DK**, Unity PC 게임, 1인 AI와 온라인 2인 대전을 12일·3명으로 개발합니다.

## 현재 상태

Unity **6.3 LTS / 6000.3.26f1** 기반의 기본 3D 프로젝트가 준비되었습니다. 카메라, 조명, 코트, 네트, 공과 두 플레이어의 임시 모델이 있는 `DevelopmentCourt` 씬으로 시작합니다. 게임 규칙·공 이동·AI·BLE·온라인 기능은 아래 과제로 구현합니다.

Built-in Render Pipeline과 Mono를 사용합니다. 팀원 모두 같은 Editor 버전으로 프로젝트를 엽니다.

## 처음 실행하기

1. Unity Hub에서 **6000.3.26f1**을 설치합니다. Apple Silicon Mac은 ARM64 Editor를 선택합니다. Windows 실행 파일을 만들 때는 **Windows Build Support (Mono)** 모듈도 설치합니다.
2. 저장소를 복제한 뒤 Unity Hub의 **Add → Add project from disk**로 저장소의 루트 폴더를 추가합니다. `Assets` 폴더의 상위 폴더를 선택합니다.
3. 프로젝트를 열고 처음 가져오기가 끝날 때까지 기다립니다. `Assets/Scenes/DevelopmentCourt.unity`를 열고 상단 ▶ 버튼으로 실행합니다.
4. VS Code에 Microsoft의 **Unity** 확장(`visualstudiotoolsforunity.vstuc`)을 설치합니다. Unity의 **Settings/Preferences → External Tools → External Script Editor**에서 VS Code를 선택합니다. C# 파일을 더블 클릭하면 편집기가 열립니다.
5. VS Code의 **Run and Debug → Attach to Unity**로 연결하면 실행 중인 코드에 중단점을 걸 수 있습니다.

이 Mac에는 Editor, Windows Mono 모듈과 VS Code의 Unity·C# 확장을 설치했습니다. Unity Hub에 프로젝트도 등록했습니다. [환경과 협업 안내](docs/development.md)를 참고하세요.

## Mac에서 실행·검증·빌드

저장소 루트에서 실행합니다. **일괄 검증이나 빌드 전에는 같은 프로젝트를 연 Unity Editor를 저장하고 닫습니다.**

```bash
bash tools/unity.sh open           # Unity Editor에서 프로젝트 열기
bash tools/unity.sh check          # C# 컴파일, 시작 씬과 공통 설정 검증
bash tools/unity.sh build-mac      # Builds/mac/DigitalTennis.app
bash tools/unity.sh build-windows  # Builds/windows/DigitalTennis.exe
bash tools/unity.sh preview        # .work/development-preview.png
```

`setup`은 시작 씬이 없는 경우에만 생성합니다. 기존 씬은 덮어쓰지 않습니다. Unity 메뉴의 **Digital Tennis**에서도 준비와 검증을 실행할 수 있습니다. 실행 로그는 `.work/logs/`에 저장됩니다.

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
| `Assets/Scenes/DevelopmentCourt.unity` | 기본 3D 시작 씬 |
| `Assets/Materials/` | 코트·네트·임시 모델의 머티리얼 |
| `Assets/Scripts/Runtime/` | 게임 실행 코드; 현재는 시작 로그만 포함 |
| `Assets/Scripts/Editor/` | 씬 준비·검증·빌드 도구 |
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
