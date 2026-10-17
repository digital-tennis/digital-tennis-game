# KOR/JPN 전신 업그레이드의 무료 에셋 추적 (AC3)

현재 KOR/JPN은 아래의 과거 얼굴 전용 구현을 넘어, 보관된 **Standard FREE** 남녀 전신 glTF의 신체·얼굴·눈·눈썹 및 Hair_Long/Hair_SimpleParted 형상을 사용한다. `body-retarget.json`의 매핑으로 기존 접촉 리그에 맞추고 프로젝트에서 작성한 스포츠웨어·신발을 조합한다. `appearance.json` → `BodyKOR/BodyJPN.json` → `AthleteSkinSetup.AcquiredBody` → `Athletes/KOR,JPN.asset` → `AthleteSkin`이 실제 메시 경로다. `runtime-bindings.json`의 두 선수 항목은 이제 사용하지 않는 분리 head/hair 대신 실제 `body`와 메시 SHA-256을 기록한다. 다른 13명은 기존 참조를 유지한다.

모션은 보관된 `UAL1_Standard.glb`의 **Idle_Loop, Walk_Loop, Jog_Fwd_Loop, Sprint_Loop**에서 10개 본의 회전, 골반 높이, 두 발 위치를 추출한다. `motion-retarget.json` → `FullBodyMotion.json` → `AcquiredAthleteMotion.Apply` → `AthleteMotion`이 실제 재생 경로다. 취득한 테니스 전용 클립은 없다. 서브·포핸드·백핸드·회복의 접촉 제약 및 IK는 기존 프로젝트 코드와 새 보정의 범위이며 Quaternius 애니메이션으로 표시하지 않는다. Root motion은 내보내지 않는다. 이 문서의 출처 검증은 동작 품질·플레이 검증을 대체하지 않는다.

공식 원본: [Universal Base Characters](https://quaternius.itch.io/universal-base-characters), [Universal Animation Library](https://quaternius.itch.io/universal-animation-library). 2026-10-17에 두 공식 페이지의 무료 Standard 배포와 CC0 표시를 확인했다. ZIP 동봉 `0/License_Standard.txt`, `1/License.txt`는 **CC0 1.0 Universal / Public Domain Dedication**을 명시한다. 라이선스 원문은 바이트 그대로 보존한다. 유료 Source/Pro 파일이나 별도 EULA는 사용하지 않는다. 스포츠웨어 및 리타게팅 코드는 프로젝트 자체 작성이며 별도 무료 의상 팩을 취득했다는 뜻은 아니다.

| 실제 검사한 원본 ZIP | SHA-256 | ZIP 항목 수 |
|---|---|---|
| Universal Base Characters[Standard].zip | `fdbf1804c90dfc1ea03e992bff7da2dfd1a79318e13270a660180f9308455f40` | 128 |
| Universal Animation Library[Standard].zip | `cc73fc4e495b82958207316596317a3f40b9fa38065bde1027937452da537724` | 12 |

원본 아카이브만 기존 작업 공간에서 `.work/athlete-assets`로 재사용했으며 과거 런타임 검증 결과를 복사하지 않았다. `archive-inspection.json`은 이번 검사에서 모든 비디렉터리 멤버를 실제 읽어 해시한 목록, 보관 glTF/GLB의 프리미티브·스킨 관절, 전체 클립 목록 및 사용한 4개 클립의 채널·키 수를 기록한다. `provenance.json`은 원본 URL·ZIP 해시·원본 멤버 경로·원문 라이선스와 파생 데이터 해시를 연결한다. `body-bindings.json`과 `motion-bindings.json`은 실제 취득 파일·설정·출력의 해시 및 변환 도구를 연결한다. 원본 ZIP은 Git/Unity Assets에 넣지 않는다.

재검증 (보관 원본 ZIP 두 개를 `.work/athlete-assets`에 준비):

```sh
python3 tools/athlete_provenance.py --archives .work/athlete-assets
python3 tools/verify-athlete-assets.py --archives .work/athlete-assets
./.work/verification-venv/bin/python3 -B -m unittest discover -s tools -p test_athlete_provenance.py -v
```

검사는 ZIP 손상, 출처 누락, 파생 데이터·실제 메시 해시 변경, 설정·라이선스 누락, 제한 라이선스, 머리 트랙만 남은 경우를 거부한다. 재생성 테스트는 별도 임시 디렉터리에서 전신 2종과 전신 트랙을 재생성해 배포 데이터와 바이트 단위로 비교한다. `--record-inspection`/`--record-runtime`은 의도적인 재생성 시에만 사용한다. 통상 검증은 기록을 수정하지 않는다.

---

## 과거 15선수 얼굴 전용 변경 기록 (현재 KOR/JPN 구현 설명이 아님)

# 15선수 CC0 시각 에셋 적용

기존 성인 선수 이름·정체성과 밸런스는 유지한다. Quaternius **Universal Base Characters Standard FREE**의 실제 남녀 모델에서 얼굴·귀·눈·눈썹 메시를 가져왔으며, 무료 헤어 5종을 조합한다. 단순 색상 교체가 아니다. 기존 절차적 신체·스포츠웨어에 이 형상을 베이크하여 기존 15본 접촉 리그를 그대로 사용한다. 유료 Source 에디션이나 캐릭터 데모 컨트롤러는 포함하지 않는다.

공식 출처: [Base Characters](https://quaternius.itch.io/universal-base-characters), [Animation Library](https://quaternius.itch.io/universal-animation-library). 두 무료 ZIP에 동봉된 라이선스는 CC0 1.0이다. 실제 배포본의 이름과 파일 수·SHA-256, 보관한 개별 원본의 ZIP 내부 경로·SHA-256, 파생 데이터 해시는 `Assets/ThirdParty/Quaternius/provenance.json`에 있다. 원문 라이선스도 각 원본 폴더에 보관했다. 공식 페이지의 전체 제품 목록을 무료 파일 목록으로 간주하지 않는다. 이번 무료 캐릭터 ZIP에는 Superhero 성인 남녀 두 모델이 있다.

`tools/adapt-athlete-assets.py`는 원본 glTF의 월드 변환과 삼각형을 읽고 얼굴 부분을 추출한다. 눈의 원본 UV 텍스처를 정점 색으로 샘플링하고, 기존 선수별 얼굴 폭·턱·코 설정에 맞춘다. 헤어에도 원본 노드 변환을 적용한다. 파생 JSON은 `Assets/Resources/Characters/Acquired`에 보관한다. `AthleteSkinSetup`이 실제 스포츠웨어 메시와 합쳐 `Assets/Resources/Athletes/{ID}.asset`으로 만든다. 원본 피부의 인종·능력을 추론하지 않으며 피부색과 의상색은 기존 프로필을 따른다.

`appearance.json`의 `acquiredHead`와 `acquiredHair`가 선수별 원본을 지정한다. 모든 선수에 입체 소매 끝단·옆솔기·칼라/라펠을 적용하고 KOR/FRA의 겹침 패널은 곡면에 맞춰 분할한다. 팔꿈치·무릎은 분리된 끝마개 대신 연속 표면을 사용한다. 쇼츠의 상단 웨이트는 골반에 연결하고 허리밴드를 공유하며 밑창을 둥글게 구성한다. 곡면 무늬는 크기에 따른 적응형 분할과 별도 간격으로 겹침을 방지한다. KOR은 하나의 사선 패널과 붉은 테두리로 정리했다. 중복 전후면 TextMesh가 겹쳐 보이던 유니폼 글자를 숨겼다. 이름·국가는 기존 UI에서 표시한다.

| 선수 | 실제 헤어 원본 | 개인별 형태·의상 변형 |
|---|---|---|
| KOR 한서윤 | Hair_Long | 좁힌 뒤 머리와 리본·묶음, 가는 턱, 사선 겹침 패널·스코트 |
| JPN 아오야마 렌 | Hair_SimpleParted | 접힌 가르마, 남성 얼굴·넓은 어깨, 삼각 라펠·접힌 파이핑 |
| CHN 린웨 | Hair_Buns | 뒤 땋은 매듭, 좁은 턱, 붓결 사선 테이프 |
| USA 마야 브룩스 | Hair_Long | 높인 곱슬 묶음, 넓은 어깨, varsity V 패널 |
| RUS 니콜라이 소콜로프 | Hair_Buzzed | 짧은 머리와 넓은 턱, 장소매·겹친 재킷·레깅스 |
| IND 아라브 라오 | Hair_SimpleParted | 올린 앞머리, 좁은 어깨, 작은 격자 장식 |
| DEU 레나 베버 | Hair_BuzzedFemale | 짧은 비대칭 인상, 블록 패널·V 칼라 |
| GBR 테오 베넷 | Hair_SimpleParted | 폭을 줄인 단정한 머리, 조끼형 V 칼라 |
| ESP 루시아 세라노 | Hair_Long | 뒤 땋은 묶음, 부채 솔기·스코트 |
| FRA 카미유 로랑 | Hair_Long | 한쪽을 짧게 올린 보브, 겹침 스포츠 패널 |
| AUS 카이 모건 | Hair_SimpleParted | 넓힌 앞머리, 물결 밑단·V 칼라 |
| NLD 노르 더프리스 | Hair_Buns | 양쪽 땋은 장식, 세로 패널·스코트 |
| BRA 하파엘 코스타 | Hair_Buzzed | 추가 곱슬 장식, 반복 물결 솔기 |
| MAR 살마 암라니 | Hair_Buns | 뒤 코일 매듭, 마름모 소매·레깅스 |
| ZAF 타보 은들로부 | Hair_Buzzed | 추가 짧은 트위스트, 넓은 어깨·직조 윤곽 테이프 |

개인화는 원래 외형 카탈로그의 체형·의상 구성과 추가 장식을 함께 사용한다. 15개의 새 독립 원본을 구매/다운로드했다는 뜻은 아니다. 캐주얼 스타일의 로우폴리 얼굴이며 AAA 수준으로 주장하지 않는다.

## 애니메이션과 게임 경계

실제 무료 `UAL1_Standard.glb`의 `Idle_Loop`에서 head 로컬 회전 키를 추출했다. `AcquiredAthleteIdle`은 첫 키에 대한 상대 회전을 보간하여 기존 표시 시간에 따라 머리에만 작게 적용한다. 외부 Animator/데모 컨트롤러를 생성하지 않으며 root motion을 사용하지 않는다. 달리기·샷·서비스·스킬·IK의 기존 로직을 교체하지 않는다. 확인된 접촉 보정은 대기 포즈 이후 기존 경로에서 실행한다. 손·발·라켓 소켓, 입력·물리·점수·CSV 수치는 변경하지 않는다.

## 실제 Mac Unity 증거

Unity 6000.3.26f1 native macOS, Apple M2 Max Metal에서 `AthleteVisualEvidence.Before/After`를 실행했다. 실제 런타임 스킨을 복제해 각 선수의 동일한 중립 포즈·800×1000 카메라·배경·조명에서 전신과 얼굴을 `Camera.Render`로 촬영한다. Before는 수정 전 Git 메시를 별도 Editor 임시 폴더로 가져오고 촬영 후 Unity AssetDatabase로 제거한다. After는 현재 리소스 메시를 사용한다. 포인트·스윙·결과를 만들어 내지 않는다.

이미지와 해시 리포트는 추적하지 않는 `.work/athlete-visuals`에 있다. `comparison-face.png`와 `comparison-body.png`는 15명 각각의 전후 모습을 나란히 놓은 시트다. `review.json`은 실제 60장 해시·보이는 픽셀 수·30쌍 변경 픽셀 수를 기록한다. `tools/review-athlete-visuals.py`가 빈 이미지·크기 오류·변화 없는 쌍을 거부한다. KOR/JPN 얼굴·전신과 전체 시트를 직접 이미지로 열어 확인했다. 이는 에셋 검토 화면으로, 경기 플레이/오디오/Windows 하드웨어 검증 증거로 사용하지 않는다.

## 재현과 검증

수정 전 기준 커밋은 `730f6c335eb496de426a61caf875c8e0d498a598`이며 원본 메시 해시와 함께 `.work/athlete-visuals/baseline.json`에 기록했다. Before 캡처 재현은 `python3 tools/review-athlete-visuals.py --prepare-originals <기준-Git-ref>`로 원본 15개를 준비한 뒤 `AthleteVisualEvidence.Before`를 실행한다. 런타임 메시를 덮어쓰지 않는다.

무료 두 ZIP을 `.work/athlete-assets`에 보관한 뒤 `python3 tools/adapt-athlete-assets.py`로 파생 데이터를 재생성한다. Unity를 닫은 상태에서 다음을 실행한다.

```sh
/Applications/Unity/Hub/Editor/6000.3.26f1/Unity.app/Contents/MacOS/Unity -batchmode -quit -projectPath "$PWD" -executeMethod DigitalTennis.Editor.AthleteVisualEvidence.After -logFile .work/logs/athlete-after.log
python3 tools/verify-athlete-assets.py --record-runtime --archives .work/athlete-assets
python3 tools/review-athlete-visuals.py
bash tools/unity.sh check
bash tools/unity.sh test-edit
bash tools/unity.sh test-motion
```

`runtime-bindings.json`은 생성된 15개 메시의 SHA-256과 원본 연결을 기록한다. Unity EditMode 테스트는 15명 모두의 실제 런타임 리소스 참조, 가져온 얼굴 전체의 삼각형 인덱스·머리 본 할당, 정규화 웨이트·법선·표정, 팔꿈치 변형과 머리의 독립성, 외부 Idle의 실제 변화·회전 범위를 확인한다. 원본/파생 데이터의 변조, 미사용·누락 에셋, 유료/비CC0 정보는 Python 부정 사례 테스트에서도 거부한다.

검증 범위는 이번 에셋 적용에 한정한다. 기존 12일·3인 계획과 별도 외부 IMU, 호스트 권위·게스트 표시 구조를 유지한다. Windows 실물 PC 검증은 수행하지 않았다.

실행 결과: `bash tools/unity.sh test-edit`의 실제 XML은 166개 통과·0개 실패였다. 여기에는 가져온 얼굴 전체의 런타임 연결과 Idle 검증 2개, 기존 스킨 변형 검증 2개가 포함된다. `bash tools/unity.sh test-motion`은 4개 통과·0개 실패로 실제 랠리의 손-라켓 부착, 스트링 접촉·발 고정, 정상 스킬 게이트와 카메라 복원을 확인했다. 두 결과 파일은 `.work/test-edit.xml`, `.work/test-motion.xml`이다. 에셋 Python 검증의 4개 테스트도 통과했으며, 압축 원본의 SHA-256과 개별 ZIP 멤버도 대조했다.
