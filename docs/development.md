# 개발 환경과 Codex 협업

## 고정한 환경

| 항목 | 설정 |
| --- | --- |
| Unity Editor | 6000.3.26f1, Unity 6.3 LTS |
| Mac Editor 아키텍처 | Apple Silicon ARM64 |
| 렌더링 | Built-in Render Pipeline |
| 데스크톱 스크립팅 | Mono |
| 시작 씬 | Assets/Scenes/MatchCourt.unity |
| 코드 편집기 | VS Code + Microsoft Unity 확장 |
| Unity IDE 패키지 | com.unity.ide.visualstudio 2.0.22 |
| Windows 빌드 모듈 | Windows Build Support (Mono) |

Editor 버전은 `ProjectSettings/ProjectVersion.txt`에서 관리합니다. 개인이 Editor나 패키지를 바꾸기 전에 팀과 변경을 정하고 저장소에도 기록합니다. 이 초기 선택은 BLE·P2P 패키지 호환성 검증을 완료했다는 뜻은 아닙니다.

## 이 Mac에서 사용하는 폴더

프로젝트는 `/Users/kevin/Documents/Codex/digital-tennis/digital-tennis-game`에 있습니다. Unity Hub와 VS Code, Codex 모두 이 **같은 폴더**를 사용합니다.

Codex에서 별도 프로젝트로 관리하려면 프로젝트 추가에서 이 폴더를 선택합니다. Codex에는 구체적인 작업과 완료 기준을 전달합니다. 예를 들어 “GM-02의 공 이동을 구현하고, 한 번 바운스할 때 속도와 방향이 바뀌는지 검증해 줘”처럼 요청합니다. `AGENTS.md`에 Editor 버전·폴더·검증 명령이 정리되어 있습니다.

Unity Editor는 화면 배치·실행 확인에, VS Code는 C# 편집·디버깅에 사용합니다. Codex가 일괄 빌드를 실행할 때는 동일 프로젝트를 연 Editor를 먼저 저장하고 닫습니다. 편집기가 열려 있는 동안에는 Unity가 변경한 파일을 다시 가져오는 것을 기다리고 Console의 컴파일 오류를 확인합니다.

## 첫 실행 확인

1. Unity Hub에서 `digital-tennis-game`을 엽니다.
2. `Assets/Scenes/MatchCourt.unity`를 엽니다.
3. Game 탭에서 코트, 네트, 두 플레이어의 임시 모델이 보이는지 확인합니다.
4. ▶ 버튼을 누릅니다. Console에 `Digital Tennis development scene ready. Unity 6000.3.26f1`이 표시되어야 합니다.
5. 자동 서브·리턴·득점 후 점수가 15–0으로 바뀌고, 서버가 반대 서브 위치에서 다음 서브를 시작하는지 확인합니다. [서브·랠리·점수](serve-rally-score.md)의 조작표로 수동 폴트·렛도 확인합니다.
6. ▶ 버튼을 다시 눌러 실행을 멈춥니다. 독립적인 공 이동 실험은 `DevelopmentCourt`와 [공 이동과 바운스](ball-motion.md)를 사용합니다.

## 코드 편집과 디버깅

Unity의 External Tools에서 VS Code를 선택합니다. Microsoft의 Unity 확장은 C# 및 C# Dev Kit 확장과 함께 사용합니다. 기존의 폐기된 `com.unity.ide.vscode` 패키지는 추가하지 않습니다.

Unity 메뉴의 **Assets → Open C# Project** 또는 C# 파일 더블 클릭으로 프로젝트를 엽니다. 필요하면 External Tools의 **Regenerate project files**로 C# 솔루션을 다시 생성합니다. VS Code에서 중단점을 지정하고 **Attach to Unity**를 실행한 다음 Unity에서 ▶ 버튼을 누릅니다.

## 결과를 구분하기

2026-10-15 초기 환경 검증에서 C# 컴파일·시작 씬 검증, Unity 카메라 렌더링, Mac Universal(ARM64/x86_64) 빌드와 Windows x64 Mono 빌드가 통과했습니다. 로그는 해당 Mac의 `.work/logs/`에, 실행 파일은 `Builds/`에 있습니다. 생성 파일은 Git에 포함하지 않습니다.

시작 씬의 C# 컴파일과 데스크톱 빌드는 개발 환경 확인입니다. 완성된 테니스 플레이의 수용 테스트는 별도로 수행합니다. Windows 실행 파일이 생성되어도 Windows에서 실행했다는 의미는 아닙니다.

BLE 컨트롤러는 외장 IMU를 부착한 NU40-DK로 실물 검증합니다. Windows BLE 연결, 두 PC 사이의 P2P·릴레이와 온라인 동기화는 실제 Windows 테스트 장비가 필요합니다. AI·컨트롤러 프로토콜·BLE 어댑터·매치 서버는 계획한 별도 저장소의 책임을 유지합니다. 전체 개발 범위는 12일·3명 계획을 따릅니다.

공식 참고: [Unity Editor 명령행](https://docs.unity3d.com/6000.3/Documentation/Manual/EditorCommandLineArguments.html), [VS Code Unity 설정](https://code.visualstudio.com/docs/other/unity).
