# 최종 회귀 검증 방식

사용자 요청은 한국·일본 선수의 얼굴·헤어·의상·이동/타격 개선, 실제 Unity 확인, 전후 비교와 커밋/PR 전달이다. 자동 생성된 최초 Seed의 AC5 설명도 보존 동작 회귀를 요구하며, 별도의 예시 `verify_command`는 프로젝트 전체를 임시 폴더로 복사한 뒤 CSV 경로 옵션 없이 Unity를 실행하도록 생성됐다. 원래 Seed 파일이나 보호된 평가 스크립트는 수정하지 않았다.

이 프로젝트의 `GameplayBalanceSceneTests`는 실제 `c.Balance.Path`의 CSV를 변경하고 복원한다. 경로를 지정하지 않으면 `BalanceBundleStore.Open()`이 사용자 `Application.persistentDataPath/Balance`를 사용한다. 따라서 실행 중인 사용자 앱을 보호하기 위해 기존 `tools/unity.sh`의 격리 CSV 옵션을 유지한다. `-nographics`는 Editor 테스트용 렌더링 옵션이며, 실제 외형·동작은 별도 Metal/GPU Mac 앱에서 따로 검증한다.

최종 동등 검증 계약은 다음과 같다.

1. 최종 형상 이후 동일 Unity 6000.3.26f1에서 전체 EditMode 169개 이상·전체 PlayMode 37개 이상을 실제 실행한다. 실패·스킵은 0이어야 한다. 네 가지 사용자 CSV 경로는 `.work` 복사본으로 격리한다.
2. 실제 XML 두 개와 모든 추적 Assets/Packages/ProjectSettings의 SHA-256을 연결한다. 테스트 시작은 최종 선수 메시 생성 이후여야 한다. `REGRESSION_OK`는 이 원본 결과 검증 후에만 출력한다.
3. 기존 입력 거부·자동 이동·호스트 권한·좌우 시점·온라인 재접속·테니스 규칙/점수·15명 신원·CSV 값·3개 스킬 상한·경기 BGM 정책·음성/자막 회귀를 보존한다. 실제 native 경기와 두 클라이언트 GPU 실행도 별도로 확인한다.
4. 원본 baseline 애니메이션은 독립 clone과 별도 앱에서 실행한다. Git 원본 파일, 실제 패키지 해시, 같은 카메라의 원본 전후 픽셀과 타임라인을 검사한다.
5. clean인 최종 커밋·원격 브랜치·실제 열린 초안 PR head가 같아야 한다. `PR_READY`는 실제 인증 GitHub API/원격 조회가 통과했을 때만 출력한다.

검사 수나 어설션을 낮추거나 결과 XML을 수동 생성하지 않는다. 사용자 요구의 회귀 범위는 그대로이며, 자동 생성 예시의 명령 문자열 일치 대신 안전하게 격리된 실제 동일 테스트 실행과 최종 소스 일치를 판단한다. `tools/athlete-current-mechanical.toml`과 `tools/test_athlete_current.py`가 이 전달 검사를 실행한다. 최종 독립 평가는 같은 여섯 가지 결과 요건을 대상으로 하며, 생성된 임시 복사 예시를 사용자 지시로 취급하지 않는다.
