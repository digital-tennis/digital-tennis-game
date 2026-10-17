# KOR/JPN 전후 비교 열기 (AC4)

로컬 검토 시작점은 [통합 Unity 비교 페이지](../.work/athlete-comparison/index.html)다. 한국·일본 각각 정면 전신·측면 전신·얼굴/헤어/칼라의 BEFORE/AFTER 6개 비교표와 달리기·서브·포핸드·백핸드/회복 8개 동작표를 연결한다. 모든 원본 PNG를 원래 크기로 열 수 있으며, 동작별 16개 연속 프레임과 실제 시각·스윙 ID·나이·속도 기록도 연결한다. 회복은 실제 `swinging=false`, `swingAge>=.349`인 샘플에 표시한다. 소리와 영상 자동 재생은 없다.

비교 기준은 `69bdf9ca797fb8396a7b25bc040f0e75bcf8e5e0`의 보관 메시다. `originals/{KOR,JPN}.asset`가 해당 Git 객체와 바이트 단위로 일치하는지 검사한다. 원본 메시를 Unity에서 재현한 중립 자세는 **staged art preview**다. 같은 카메라·조명·6도 팔 자세와 900×1100 프레임을 사용하며, 기존 외부 보관 사진을 수정하거나 새 경기 캡처로 오인하게 하지 않는다. 현재 메시 파일 해시는 `after-assets.txt`와 새 native 빌드 receipt의 소스 해시에 연결한다.

동작은 AC2가 이 작업 트리의 새 검증 앱으로 생성한 **실제 CLI 입력 어댑터 경기** 자료를 참조한다. 과거 실행의 다른 메시 증거를 복사하지 않는다. AC4는 기존 승인된 의존 자료를 보존하고 읽어 비교표·색인을 생성한다. 과거 버전의 동작을 같은 시간·공 궤적으로 다시 촬영한 전후 비교 영상이라고 주장하지 않는다. 접촉부터 0.35초 이후 실제 회복과 달리기·방향 전환을 검토할 수 있다. DLL과 사용 소스의 해시가 달라지면 검사가 실패하며, 새 빌드와 실제 native 촬영 후 다시 생성해야 한다.

검수 중 한국 정면의 연결된 어깨·팔다리와 허리선, 일본 측면의 목·어깨·종아리 윤곽, 두 얼굴의 줄어든 폭·헤어 맞춤, 경기 중 손잡이·라켓·볼·발을 이미지로 열어 확인했다. 로우폴리 얼굴/헤어의 면, 정지 손가락, 의상 셰이더 경계와 발 앵커 보정은 여전히 보인다. 서브는 승인 접촉부터의 짧은 시퀀스이며 토스 전체 준비 모션의 증거가 아니다. 스크린샷과 기계 검사는 모든 포즈에서의 해부학적 정확성·무오차 접지·OS 키보드 실사용을 증명하지 않는다. 일반 Mac 창에서는 Enter로 시작·메뉴·선수 선택에 진입하고 방향키로 한국·일본 선수를 전환한 뒤 한국 선수를 확정하는 동작을 실제 확인했다. 일반 창의 경기 타격 키 입력 검증과 실제 CLI 어댑터 경기 검증은 구분한다.

재생성에는 `tools/requirements-verification.txt`가 설치된 로컬 venv와 먼저 생성한 AC1/AC2 Unity 증거가 필요하다. 임의 대체 이미지나 빈 결과로 통과하지 않는다.

```sh
./.work/verification-venv/bin/python3 tools/review-athlete-proportions.py
./.work/verification-venv/bin/python3 tools/review-athlete-motion.py
./.work/verification-venv/bin/python3 tools/review-athlete-comparison.py
./.work/verification-venv/bin/python3 -B -m unittest discover -s tools -p test_athlete_comparison.py -v
```

이번 AC4 검사 4개가 통과했다. Git 원본 메시·현재 메시·현재 DLL/소스 해시, 같은 카메라 receipt, 모든 비교표 이미지 영역과 원본 PNG의 **전체 픽셀 일치**, 동작의 실제 진행 시각/회복, 168개 참조의 해시와 HTML 링크를 검사했다. 브라우저는 화면에 맞춰 축소 표시하지만 PNG 자체는 리사이즈·크롭·리터칭하지 않는다. `.work`의 PNG/HTML/manifest/로그는 로컬 검토 자료이며 Git에 추가하지 않는다. 실행 중인 사용자 일반 앱은 종료하거나 덮어쓰지 않았다. Windows·BLE·외장 IMU·두 PC 검증 결과는 이 자료에 포함되지 않는다.
