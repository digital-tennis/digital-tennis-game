# Digital Tennis 문서

게임 사용, 개발, 표현 설계와 검증 자료를 목적별로 안내합니다. 처음 실행한다면 [게임 README](../README.md)의 시작하기부터 읽으세요.

## 플레이·설정

| 주제 | 문서 |
| --- | --- |
| 시작 메뉴·선수 선택·경기 흐름 | [프런트엔드](front-end.md) |
| 포인트·게임·세트·매치 | [테니스 규칙](match-rules.md) |
| 키보드·무선 상세·저장과 취소 | [컨트롤러 설정 흐름](controller-setup-flow.md) |
| CSV 파일·값·안전한 다시 읽기 | [밸런스 조정](gameplay-balance.md) |
| 1인 AI와 난이도 | [AI 연결](single-player-ai.md) |
| 온라인 방·입력·재접속·화면 방향 | [온라인 연결](input-adapter-online.md) |

## 개발

| 주제 | 문서 |
| --- | --- |
| Editor·폴더·검사·데스크톱 빌드 | [개발 환경과 빌드](development.md) |
| 전체 제품의 책임과 입력 흐름 | [통합 아키텍처](https://github.com/digital-tennis/digital-tennis-docs/blob/main/docs/project/architecture.md) |
| 게임 입력과 자동 이동 | [라켓 입력·이동](racket-input-movement.md) |
| 공 궤적·바운스 | [공 시뮬레이션](ball-motion.md) |
| 심판·볼보이 | [코트 진행 연출](court-officials.md) |
| 처음 참여하는 개발자의 구현 과제 | [게임 과제](https://github.com/digital-tennis/digital-tennis-docs/blob/main/docs/products/digital-tennis-game.md) |

## 캐릭터·경기장·음향

[15명 페르소나와 이야기](athlete-stories.md) · [캐릭터·스킬](world-characters.md) · [전신 비율과 동작](roster-athlete-art-review.md) · [무료 자산 출처](athlete-cc0-assets.md) · [경기장](stadium-design.md) · [효과](gameplay-effects.md) · [인트로와 코트 음향](cinematic-and-court-audio.md) · [음향 제작 조사](audio-production-research.md)

## 지원 상태·검증 기록

- [실제 Bluetooth 본딩과 메시지 검수](controller-bonding.md): Mac 일반 창의 실제 본딩·왕복·재시작 통과와 남은 실기를 안내합니다. NU-54는 센서 없는 시험용이고 제품은 NU40-DK+별도 외장 IMU입니다.
- [소프트웨어 통합](software-integration.md): 패키지·온라인·배포 검증입니다.
- [Mac 마우스 검수](mac-mouse-validation.md): 일반 창 입력 확인과 제한을 기록합니다.
- [게임 완성도 검수](polish-validation.md) · [개선 작업 이력](polish-backlog.md): 해당 시점의 범위와 증거입니다.
- [README 화면 출처](screenshots/README.md): 공개 화면의 원본 실행 기준입니다.

날짜·커밋이 있는 검수와 초기 기능 문서는 당시의 기록입니다. 현재 경기 형식은 [match-rules.md](match-rules.md), 현재 기기 연결 상태는 [controller-bonding.md](controller-bonding.md)를 기준으로 읽습니다. 예전 한 게임 MVP 기록이나 모의 검사 결과를 현재 전체 경기·실물 성공으로 해석하지 않습니다.

## 후속 개발

- [외장 IMU 인수 가이드](https://github.com/digital-tennis/digital-tennis-firmware/blob/a0a5b6c5d4c0f51f863da57ec85a217086315ebe/docs/imu-developer-handoff.md): 센서 보유 개발자가 수정할 파일·함수·검증 기준.
- [강화학습 AI 학습과 모델 교체](https://github.com/digital-tennis/digital-tennis-ai/blob/d2e1f004041cd35773fc423c5be65b071d7e9cd8/Training/README.md): PPO 배치 모델과 기존 실행 계층의 역할.

- [학습 AI 게임 연결과 실제 검수](learned-ai.md)
