# KOR/JPN 전신 비율·외형 검토 (AC1)

한서윤(KOR, 26)과 아오야마 렌(JPN, 24)의 기존 머리만 교체하던 경로를 두 선수에 한해 전신 경로로 바꿨다. Quaternius Universal Base Characters **Standard FREE / CC0-1.0**의 보관된 성인 남녀 glTF와 bin을 실제 읽고, 몸통·목·얼굴·귀·눈·눈썹·손가락·팔다리의 연속 표면과 원본 스킨 웨이트를 사용한다. 헤어도 같은 원본 좌표계에서 얼굴과 함께 변형하므로 이전처럼 얼굴 폭과 헤어를 별도로 늘리지 않는다. 얼굴에는 완화한 눈썹 두께와 입술 색을 적용했다. 특정 국적의 신체적 특징을 추론하지 않는다.

`tools/adapt-athlete-bodies.py`가 `body-retarget.json`의 연속 높이 변형과 팔 회전·길이 설정으로 기존 접촉 리그의 정지 공간에 베이크한다. 원본의 다중 관절 웨이트는 기존 15본으로 합산·정규화한다. `BodyKOR.json`/`BodyJPN.json`은 실제 생성 메시의 삼각형과 웨이트에 연결되며 사용하지 않는 정점은 제거한다. 원본 전신을 사용하므로 이전 절차적 몸통과 머리를 이어 붙이는 방식이 아니다.

KOR의 흰색·네이비·코럴과 JPN의 인디고·코럴, 기존 프로필과 나머지 13명의 메시를 보존했다. 신체 표면에 맞춘 스포츠웨어 패널·칼라·소매·손목·양말 경계는 정지 좌표와 마스크를 사용하는 셰이더로 표현한다. 별도의 떠 있는 의상 장식 면이 없다. 스코트·둥근 운동화·끈은 authored geometry이고, 스코트 단면과 운동화 마지막 단면의 위치는 외형 데이터에 둔다. 발가락 표면은 운동화 아래에서 제거한다. 이는 로우폴리 fitted 의상이며 천 시뮬레이션이나 사실적인 스캔 모델은 아니다.

원본 출처·무료 배포본 해시는 기존 `Assets/ThirdParty/Quaternius/provenance.json`, 배포 허가는 `0/License_Standard.txt`에 있다. 추가 `body-bindings.json`은 이번에 실제 읽은 glTF/bin/텍스처/헤어/라이선스/설정과 생성 JSON의 SHA-256을 기록한다. 보관된 무료 원본을 재사용했으며 구매·추가 EULA 수락은 없다.

## 실제 Unity 비교

Unity **6000.3.26f1 / Metal**의 실제 런타임 스킨을 복제하여 양팔을 같은 6도 벌린 중립 자세로 렌더했다. 원본 리비전은 `69bdf9ca797fb8396a7b25bc040f0e75bcf8e5e0`이다. 변경 전 최초 촬영 뒤 같은 리비전의 원본 메시를 Git에서 별도 임시 Editor 폴더로 읽어 재현했다. 런타임 리소스는 덮어쓰지 않는다. 임시 에셋은 AssetDatabase로 정리한다.

전후 모두 900×1100, 같은 정면·측면·얼굴 카메라, 같은 조명과 배경을 쓴다. `.work/proportion-review/{before,after}-{KOR,JPN}-{front,side,face}.png` 12장은 원본 카메라 픽셀이다. `comparison-{KOR,JPN}-{front,side,face}.png` 6장은 이 픽셀을 자르거나 리사이즈하지 않고 위쪽에 설명만 추가한 비교표다. `before-assets.txt`/`after-assets.txt`는 촬영한 메시 파일 해시·이름·정점·UV 개수, `review.json`은 PNG 해시를 기록한다. 전신·얼굴·측면을 실제 이미지로 열어 보고 목 왜곡·머리 크기·의상 경계·스코트 옆면·발목 틈을 수정했다.

**모두 staged art preview다.** 경기 입력·점수·접촉을 만들지 않으며 이 자료를 이동·타격·온라인 동작이나 새 Mac 빌드의 증거로 사용하지 않는다. 실행 중인 사용자의 일반 게임 앱에는 접근하거나 덮어쓰지 않았다. 현재 AC는 외형 검토에 한정한다. Windows 실물·BLE·외장 IMU·두 PC 검증 결과도 아니다. 12일·3인 계획과 별도 부착 외장 IMU 요구는 변경하지 않는다.

## 재현·검사

Unity Editor에서 이 작업 트리를 닫은 상태로 실행한다. Python은 `tools/requirements-verification.txt`를 설치한 프로젝트 로컬 venv를 사용한다.

```sh
./.work/verification-venv/bin/python3 tools/adapt-athlete-bodies.py
./.work/verification-venv/bin/python3 tools/review-athlete-proportions.py --prepare-baseline 69bdf9ca797fb8396a7b25bc040f0e75bcf8e5e0
/Applications/Unity/Hub/Editor/6000.3.26f1/Unity.app/Contents/MacOS/Unity -batchmode -quit -projectPath "$PWD" -executeMethod DigitalTennis.Editor.AthleteProportionEvidence.Before -logFile .work/before-proportions.log
/Applications/Unity/Hub/Editor/6000.3.26f1/Unity.app/Contents/MacOS/Unity -batchmode -quit -projectPath "$PWD" -executeMethod DigitalTennis.Editor.AthleteProportionEvidence.After -logFile .work/after-proportions.log
./.work/verification-venv/bin/python3 tools/review-athlete-proportions.py
./.work/verification-venv/bin/python3 -B -m unittest discover -s tools -p test_athlete_proportions.py -v
bash tools/unity.sh check
bash tools/unity.sh test-edit
```

외형용 Python 검사는 실제 원본·파생 해시, 15본 웨이트, 다른 13명과 페르소나 보존, 두 선수의 빈 화면 방지·팀 색상·촬영 메시 해시·전후 차이·같은 카메라 설정을 검사한다. Unity EditMode 검사는 전신 삼각형의 런타임 연결, UV 보존과 셰이더 프로필 전환, 정규화 웨이트·표정·법선, 팔꿈치 변형과 기존 게임 회귀를 검사한다. 이미지 개수나 JSON 주장만으로 외형 품질을 판정하지 않는다. 로컬 PNG·로그·테스트 XML과 `.work`는 Git에 포함하지 않는다.

이번 작업 트리의 실제 실행 결과: Unity `check` 통과, EditMode **169/169 통과·실패 0**, 전신/출처/비교 Python 검사 **4/4 통과**. 결과는 `.work/test-edit.xml`과 `.work/logs` 및 위 비교 파일에서 확인한다.

### 정수리 변형과 저장소 정리

전신 높이 보간 범위보다 위에 있는 헤어 정점이 마지막 높이에 고정되던 문제를 수정했다. 마지막 머리 구간의 기울기를 연장해 무료 원본의 정수리 볼륨을 보존한다. 한국 108개·일본 88개 정점에 영향이 있었으며, 원본 glTF와 생성 데이터의 실제 정수리 높이를 대조하는 재발 검사를 추가했다. 수정 후 Unity AFTER 화면을 새로 생성했고, 기존 버전의 BEFORE PNG는 유지했다.

기존 CRLF CSV 두 파일이 저장소의 `*.csv text eol=lf` 규칙과 충돌했다. `characters.csv`와 `audio-audition-plan.csv`만 LF로 정규화했으며 모든 행과 값은 그대로다. 전달 검사는 예외 없이 전체 작업 트리가 clean인지 확인한다. Unity가 만든 로컬 솔루션 이름 설정은 최종 검증 후 복원한다.

### 런지 중 한국 의상 변형

최종 native 연속 프레임에서 다리가 고정 치맛단을 통과하던 부분을 보완했다. 스커트 아래를 막던 원판을 제거하고 열린 치맛단으로 만들었다. 허리는 골반에 고정하고 아래 패널은 좌우 허벅지 웨이트를 점진적으로 섞어 달리기와 런지를 따라간다. 물리 천 시뮬레이션은 아니다. 최종 v9 메시로 중립 비교, Unity check·EditMode 169개, 별도 Mac 빌드와 실제 native 동작을 다시 생성해 확인했다. 직선 허리선과 로우폴리 목·승모근 윤곽은 여전히 남는 스타일 한계다.
