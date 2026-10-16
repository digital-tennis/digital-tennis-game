# 선수 외형·동작 변경 회귀 검증 (AC 5)

이 작업 트리에서 Unity 6000.3.26f1로 직접 실행했다. 기존 사용자 게임은
종료하거나 덮어쓰지 않았다. 테스트 CSV는 `tools/unity.sh`가 `.work`에
복사한 격리 파일을 사용했다.

| 실행 명령 | 관측 결과 |
| --- | --- |
| `bash tools/unity.sh check` | 통과 |
| `bash tools/unity.sh test-edit` | 169개 통과, 실패·건너뜀 0 |
| `bash tools/unity.sh test-play` | 37개 통과, 실패·건너뜀 0 |
| `./.work/verification-venv/bin/python3 -B -m unittest discover -s tools -p test_athlete_regression.py -v` | 4개 통과 |

이번 EditMode XML의 UTC 실행 구간은 2026-10-16 21:44:47–21:44:50,
PlayMode는 21:45:17–21:51:18이다. 결과는 `.work/test-edit.xml`,
`.work/test-play.xml`, 로그는 `.work/logs/`에 있으며 Git에는 포함하지 않는다.
최초 PlayMode 시도는 직전 Editor 종료 중 프로젝트 잠금으로 시작하지 못했다.
Editor 프로세스가 종료된 것을 확인한 뒤 재실행한 전체 결과가 위 표다.

검증 범위:

- `BallSimulationTests`, `TennisRulesTests`, `MatchScoringTests`, 씬 테스트:
  공 이벤트, 점수→게임→세트→매치, 서비스 순서와 엔드 변경.
- `PlayerInputAdapterTests`, `RacketPlayTests`, `PlayerInputSceneTests`:
  중복·오래된 입력 거부, 소스 전환, 실제 입력 경로와 도달/타격 게이트.
- `OnlineProtocolTests`, `OnlineRematchTests`, `PlayerPerspectiveSceneTests`:
  epoch/시퀀스 검증, 스냅샷 버퍼, 명시적 스트로크, 종료된 동작 재생 방지,
  재접속 시 선수 정체성과 물리적 엔드 보존. 게스트 물리 진행 금지도 검사한다.
- `CharacterSystemTests`, `CharacterSceneTests`, `GameplayBalanceTests`:
  15명 성인 선수, CSV 원자적 갱신, 스킬 최대 3개, 실제 타격으로 충전,
  시네마틱 중 물리 정지와 점수 분리.
- `TennisAudioPolicyTests`, `AudioCatalogTests`, `AudioUpgradeTests`:
  경기·일시정지 중 음악 차단, 음원 참조, 큐 우선순위, 사용자 자막 보존.
- `FrontEndSceneTests`, `SinglePlayerSceneTests`, `PresentationPolishSceneTests`:
  메뉴 입력 차단, AI 모드 분리, 실제 랠리의 그립·스트링 접촉·발 접지.
- 추가한 Python 검사는 PR18 기준 `69bdf9ca797fb8396a7b25bc040f0e75bcf8e5e0`과
  모든 선수·게임플레이·규칙·제스처 CSV 값을 비교한다. 15명 로스터 전체와
  나머지 13명 외형 프로필도 비교하며, 기존 Audio 트리 460개 파일의 Git
  blob 해시로 SFX·합성 영문 음성·한국어 자막·출처 파일의 바이트 보존을 확인한다.

이 문서는 자동 회귀 검사 결과다. 외형 품질의 직접 관찰, 네이티브 빌드/비교
이미지, 실제 온라인 시연은 다른 AC의 증거를 사용해야 한다. Windows BLE,
외부 IMU 하드웨어 및 물리적 두 PC 검증은 이 실행으로 입증하지 않는다.
