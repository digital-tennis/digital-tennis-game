# 개발 환경과 빌드

## 준비 환경

- Unity **6000.3.26f1**, Built-in Render Pipeline, 데스크톱 Mono
- macOS Apple Silicon: ARM64 Editor. Windows 빌드: Windows Build Support (Mono)
- 네이티브 컨트롤러 빌드: macOS Command Line Tools/Swift, Windows helper용 .NET 10 SDK
- Git. 코드 편집기는 팀이 사용하는 IDE를 선택하며 개인 경로는 저장소에 넣지 않습니다.

Editor 버전은 `ProjectSettings/ProjectVersion.txt`, 패키지 버전은 `Packages/manifest.json`과 잠금 파일이 기준입니다. protocol·controller·AI의 Git 참조는 검토한 커밋을 고정합니다.

## 시작 씬과 개발 씬

| 씬 | 목적 |
| --- | --- |
| `Assets/Scenes/FrontEnd.unity` | 일반 사용자 시작 화면. 메뉴·선수 선택 후 경기 진입 |
| `SinglePlayerCourt` | 1인 경기 개발 실험실. 일반 실행은 FrontEnd 경유 |
| `MatchCourt` | 서브·랠리·규칙 실험 |
| `DevelopmentCourt` | 공 궤적·바운스 독립 실험 |

실제 플레이 경로는 [루트 README](../README.md)의 시작하기를 따릅니다. 실험 씬의 자동 입력을 일반 플레이와 혼동하지 않습니다.

## 검사

같은 프로젝트의 Editor에서 저장하고 닫은 뒤 저장소 루트에서 실행합니다. 다른 경로에 설치한 Editor는 `UNITY_EDITOR_PATH`로 지정합니다.

```sh
# Unity 라이선스가 필요 없는 소스·CSV·GUID 검사
python3 tools/check-source.py

# Unity 씬·코드 구성 및 규칙/동작 검사
bash tools/unity.sh check
bash tools/unity.sh test-edit
bash tools/unity.sh test-play

# 필요한 영역을 선택해 추가 확인
bash tools/unity.sh test-controller
bash tools/unity.sh test-mouse
```

로그는 `.work/logs/`, 테스트 결과는 `.work/test-*.xml`입니다. GPU 없는 검사에서 제외된 화면 항목은 실제 GPU·일반 창에서 별도 확인합니다. 자동 검사 통과를 실제 사용자의 키보드·마우스 또는 실물 BLE 성공으로 대신하지 않습니다.

## 개발 실행 파일

```sh
bash tools/unity.sh build-mac
open Builds/mac/DigitalTennis.app
```

Windows는 `bash tools/unity.sh build-windows`이며 `Builds/windows/DigitalTennis.exe`를 생성합니다. 빌드 후처리는 고정 SDK 소스로 플랫폼별 Bluetooth helper와 필수 고지를 포함합니다. Windows 교차 빌드는 실제 Windows 실행 검수와 다릅니다.

## 배포 파일

커밋한 깨끗한 소스에서 실행합니다.

```sh
bash tools/unity.sh release-mac
python3 tools/package-release.py --platform mac --out .work/releases
```

Windows는 각각 `release-windows`, `--platform windows`를 사용합니다. 개발·디버깅 옵션, 필수 라이선스 고지, 빌드 메타데이터와 Mac 서명을 확인하고 패키징합니다. 배포 명세의 소스 커밋과 체크섬을 함께 보관합니다.

## 공동 작업

Unity 원본은 `Assets/`, `Packages/`, `ProjectSettings/`와 대응 `.meta`입니다. 이동·이름 변경 시 GUID를 보존합니다. `Library/`, `Temp/`, `UserSettings/`, `Builds/`, `.work/`와 개인 설정은 커밋하지 않습니다. 실행 중 Editor를 강제 종료해 변경을 잃지 않도록 합니다.

입력·AI·프로토콜·BLE·매칭 서버의 책임은 분리합니다. [CONTRIBUTING.md](../CONTRIBUTING.md)와 `AGENTS.md`에서 변경 영역의 검증 조건을 확인하세요. 원래 12일·3명 계획은 [초기 WBS](https://github.com/digital-tennis/digital-tennis-docs/blob/main/docs/planning/wbs.md)에 보존합니다.

[문서 목차로 돌아가기](README.md)
