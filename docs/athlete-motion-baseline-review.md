# 기존 버전과 현재 버전의 실제 경기 동작 비교

기준판은 PR18의 `69bdf9ca797fb8396a7b25bc040f0e75bcf8e5e0`다. `.work/baseline-motion-project`의 독립적인 로컬 clone에서 빌드한다. 현재 실행 중인 사용자 앱과 현재 검증 앱은 수정하지 않는다. Assets의 모델·애니메이션 등 원본 958개를 유지하며 촬영용 `MotionValidationDriver.cs`와 검증 앱 식별자만 수정하며, 나머지 모델·머티리얼·애니메이션·물리·점수·입력 코드는 Git 원본 blob과 바이트 단위로 대조한다. `.work/baseline-motion/source-receipt.json`에 SHA-256과 실제 빌드 파일 해시를 보관한다.

촬영 드라이버는 현재의 16프레임 카메라 기록 기능을 이식한 것이다. 기존 리그에 없는 팔 길이/속도/그립 관측 API는 사용하지 않는다. 원래 손 앵커와 실제 루트 이동량으로 관측한다. 임의 포즈·공·점수 설정을 추가하지 않으며, 두 선수의 서비스 교대는 정상 포인트 진행으로 발생한다. 이 수정은 비교 자료 생성용 복사본에만 있으며 제품 baseline의 애니메이션을 변경하지 않는다. 앱 ID는 `com.digitaltennis.baseline`이고, batchmode/격리 CSV/스피커 모니터 뮤트를 유지한다.

양 버전의 촬영은 초점 높이 +0.4m, 선수 정면 4m·오른쪽 1.5m·위 0.6m, FOV 48도, 1200×900 PNG로 같다. 각기 정상 입력 경기를 실행하므로 공 궤적·프레임 시각·매 프레임의 자세가 동일한 동기 리플레이는 아니다. 같은 동작 시작과 회복의 상대 시점으로 비교한다. 서브는 승인 접촉부터의 기록이며 전체 토스 준비 시퀀스가 아니다.

한국·일본의 Running/Serve/Forehand/Backhand 8개 비교표는 왼쪽 BEFORE, 오른쪽 AFTER다. 각 행은 실제 연속 촬영의 0/4/8/15 프레임이다. 원본 1200×900 이미지의 전체 픽셀을 크롭·리사이즈·보정 없이 붙이고 바깥에 라벨만 추가한다. 타임라인 16개 샘플과 모든 원본 PNG, 해시를 함께 연결한다. `.work/athlete-comparison/index.html`에서 중립 외형 전후 비교와 실제 경기 동작 전후 비교를 모두 연다.

재현은 Unity 프로젝트를 닫고 아래 순서대로 실행한다. 기존 baseline 폴더가 있으면 보관한 뒤 새로 준비한다. 원본·캡처·빌드·로그·로컬 clone은 Git에 포함하지 않는다.

```sh
./.work/verification-venv/bin/python3 tools/capture-baseline-athlete-motion.py prepare
./.work/verification-venv/bin/python3 tools/capture-baseline-athlete-motion.py build
./.work/verification-venv/bin/python3 tools/capture-baseline-athlete-motion.py capture
./.work/verification-venv/bin/python3 tools/review-baseline-athlete-motion.py
./.work/verification-venv/bin/python3 tools/review-athlete-comparison.py
```

최종 전체 회귀는 `bash tools/unity.sh test-edit`와 `bash tools/unity.sh test-play`의 실제 Unity 결과다. `tools/record-athlete-regression.py`는 통과 XML과 현재 Assets/Packages/ProjectSettings 전체 해시를 연결하고 `REGRESSION_OK`를 출력한다. 최종 커밋·푸시 후 `tools/record-athlete-delivery.py`는 실제 GitHub API와 원격 브랜치를 읽고 clean/head 일치가 확인됐을 때만 `PR_READY`를 출력한다. 둘 다 `.work` receipt를 보존하며, 검수 도구의 테스트 성공 주장만으로 원본 경기나 Unity 실행을 대체하지 않는다.
