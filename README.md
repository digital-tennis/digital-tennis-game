# Digital Tennis

15개 국가의 오리지널 선수와 코트에서 즐기는 **Unity PC 모션 테니스 게임**이다. 키보드로 시작하고, BLE 라켓 컨트롤러를 같은 입력 어댑터에 연결한다. 1인 AI 대전과 방 코드 기반 온라인2인 플레이를 지원한다.

전체 프로젝트는 **NU40-DK+별도 외장 IMU**, 최초 **12일·3명** 계획을 유지한다. NU-54는 센서 없는 연결 검증을 위한 추가 타깃이다.

## 게임 화면

기존 Mac/Unity 실행 캡처다. [출처와 검증 범위](docs/screenshots/README.md)를 확인한다.

![로고·타이틀과 시작 메뉴](docs/screenshots/title.png)

![국기·국경선이 있는 세계지도 선수 선택](docs/evidence/mac-mouse-2026-10-18/japan-selected.jpg)

![한국·일본 선수의 랠리와 관중석](docs/screenshots/court-rally.png)

## 주요 기능

- 로고/인트로→메뉴→세계지도 선수·상대·코트 선택→경기/결과
- 서브·랠리·폴트/렛·테니스 점수·게임/세트·경기 승리
- 포핸드/백핸드·특수 샷·자동 이동, 난이도 상·중·하 AI
- 국가별15명 페르소나·모델·고유 스킬·코트, 관중/효과음/대사
- CSV 밸런스/규칙/음원 설정, 실제 WebRTC P2P와 WebSocket 릴레이 복구
- 기기별 입력 설정·취소/저장, OS Bluetooth 연결/본딩 및 통신 진단

## 빠른 시작

1. Unity Hub에 **Unity6000.3.26f1**을 설치한다. Apple Silicon은 ARM64 Editor, Windows 빌드는 Windows Build Support(Mono)가 필요하다.
2. 이 저장소 루트를 Unity Hub의 Add project from disk로 연다. Built-in Render Pipeline/Mono를 사용한다.
3. Assets/Scenes/FrontEnd.unity를 열고 Play를 누른다. 일반 Mac 실행 파일은 아래 build-mac으로 만든다.
4. 방향키/Enter/ESC로 메뉴를 조작한다. 기본 경기 키는 Space서브, Q/W포핸드/백핸드, S스매시, V발리, X슬라이스, B로브, K스킬이다. 현재 키 배치는 설정에 표시된다.

```sh
# 같은 프로젝트 Editor를 저장하고 닫은 뒤 실행한다.
bash tools/unity.sh check
bash tools/unity.sh test-edit
bash tools/unity.sh test-play
bash tools/unity.sh test-controller
bash tools/unity.sh build-mac
```

빌드는 Builds/mac/DigitalTennis.app, Windows는 build-windows다. 개발 옵션을 끈 배포는 release-mac/release-windows 후 tools/package-release.py를 사용한다. 생성 파일·실행 로그는 Builds/.work에 보관하고 Git에 넣지 않는다.

## 컨트롤러와 제품 경계

firmware가 외장 IMU를 보정/판정해 protocol의20바이트 이벤트를 보낸다. ble-unity가 OS 연결/암호화/본딩·코덱 검증/중복 제거를 맡고, 게임은 IPlayerInputSource→PlayerInputRouter→PlayerInputAdapter→SubmitInput 경계로 받는다. 게임은 센서 알고리즘을 복사하지 않으며 타격 거리·권한·점수는 게임이 판정한다.

설정→입력 컨트롤러에서 키보드 또는 무선 상세를 연다. 무선은 기기 찾기→OS 페어링/본딩 확인→통신 확인→센서 보정/세 동작 테스트→저장 순서다. 센서 없는 통신 성공으로 보정이나 경기 준비를 만들지 않는다. 원래 NU-54 지갑 펌웨어는 테니스 서비스가 아니므로 호환 테니스 펌웨어가 필요하다. 화면 없는 시험 기기의 SC+Just Works 본딩은 PIN/MITM 인증을 제공하지 않는다. 실물 성공 여부는 [현재 본딩 검수](docs/controller-bonding.md)를 확인한다.

## 밸런스와 온라인 실행

캐릭터·물리/샷/AI·규칙은 Assets/StreamingAssets/Balance의CSV를 사용자 저장 폴더로 복사해 사용한다. 설정에 표시된 외부 파일을 수정하고 안전한 서브 대기에서 F5로 읽는다. 잘못된 파일은 이전 설정을 유지하고 경기 규칙 변경은 새 경기에 적용한다. [CSV 안내](docs/gameplay-balance.md)와 [테니스 규칙](docs/match-rules.md)을 확인한다.

온라인은 별도 digital-tennis-match-server를 실행한 뒤 주소와 방 코드로 연결한다. 같은 Mac의 localhost 예제와 실제 서로 다른 PC의 접근 가능한 wss 주소를 구분한다. [온라인 구조](docs/input-adapter-online.md), [통합 검수](docs/software-integration.md)를 따른다.

## 문서와 검증 범위

- [기기별 설정 흐름](docs/controller-setup-flow.md) · [컨트롤러 검수](docs/controller-readiness.md)
- [선수/동작 품질](docs/roster-athlete-art-review.md) · [경기장](docs/stadium-design.md) · [음향 제작](docs/audio-production-research.md)
- [입력/마우스 검수](docs/mac-mouse-validation.md) · [전체 게임 검수](docs/polish-validation.md) · [개선 이력](docs/polish-backlog.md)
- [제품별 초보자 과제](https://github.com/digital-tennis/digital-tennis-docs/blob/main/docs/products/digital-tennis-game.md)

Assets/는 씬·스크립트·자산, Packages/는 고정 UPM 의존성, ProjectSettings/는 Editor 공통 설정, tools/는 재현 도구다. 게임 규칙/물리는 호스트만 진행하고 게스트는 상태를 표시한다. 실제 Mac 실행·GPU 모의 라디오·Windows 교차 빌드·실물 IMU 결과를 혼동하지 않는다. IMU 정확도·물리 Windows·서로 다른 두 PC/WAN은 별도 검증 대상이다.

## 도움말과 기여

[이슈](https://github.com/digital-tennis/digital-tennis-game/issues)에 환경·커밋·재현 순서·기대/실제 동작을 남긴다. 관리 주체는 [Digital Tennis](https://github.com/digital-tennis)이며 [기여 안내](CONTRIBUTING.md)와 AGENTS.md를 따른다.

## 라이선스

프로젝트 자체 코드·문서는 **[Apache-2.0](LICENSE)**이다. [NOTICE](NOTICE) 및 [외부 자산 고지](THIRD_PARTY_NOTICES.md)를 함께 배포한다. Quaternius/Kenney·폰트·국기·지도·Flite·Unity/WebRTC/.NET은 각각의 원래 조건을 유지한다.
