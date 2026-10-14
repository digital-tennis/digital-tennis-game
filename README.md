# Digital Tennis Unity PC 테니스 게임

공·코트·서브·점수, 자동 이동, 1인 플레이, 온라인 2인 플레이를 담당하는 독립 제품 저장소입니다. 전체 프로젝트는 **외장 IMU를 별도로 부착한 NU40-DK**, Unity PC 게임, 1인 AI와 온라인 2인 대전을 12일·3명으로 개발합니다.

## 현재 상태

개발 계획과 초기 문서가 준비된 단계입니다. 제품 코드는 아직 구현되지 않았습니다. 실행 명령은 D1 도구 선택과 실제 구현 뒤에 추가합니다. 모의 입력으로 먼저 검증하고 실물/다른 제품과 통합합니다.

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

## 개발 환경과 예상 구조

초기 담당 역할은 B입니다. 실제 IMU·배선·펌웨어 SDK·Unity·Windows·런타임 버전은 D1에 [환경표](https://github.com/digital-tennis/digital-tennis-docs/blob/main/docs/project/overview.md)와 결정 기록에 남깁니다.

예상 폴더는 `Assets/ · Packages/ · ProjectSettings/ · Tests/`입니다. 아직 존재하지 않는 폴더와 실행 명령을 구현 완료로 해석하지 않습니다. 채택 도구와 실제 구현에 맞게 갱신합니다.

## 검증과 협업

- [12일 WBS](https://github.com/digital-tennis/digital-tennis-docs/blob/main/docs/planning/wbs.md)
- [컨트롤러 계약](https://github.com/digital-tennis/digital-tennis-docs/blob/main/docs/contracts/controller-protocol.md)
- [온라인 계약](https://github.com/digital-tennis/digital-tennis-docs/blob/main/docs/contracts/online-protocol.md)
- [수용 테스트](https://github.com/digital-tennis/digital-tennis-docs/blob/main/docs/testing/acceptance.md)
- [브랜치·PR 작업 방법](https://github.com/digital-tennis/digital-tennis-docs/blob/main/docs/guides/contributing.md)
- [모든 제품과 의존 관계](https://github.com/digital-tennis/digital-tennis-docs/blob/main/docs/project/repositories.md)

라이선스는 코드와 제3자 자산 조건을 검토한 뒤 팀이 선택합니다.
