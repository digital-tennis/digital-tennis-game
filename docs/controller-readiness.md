# 컨트롤러 통신 확인과 입력 고도화 검수

2026-10-18 검토 브랜치 `codex/controller-readiness`. 기존 main은 PR24의 bf266aa이며 이 문서는 후속 변경의 결과다. NU40-DK에 **별도 외장 IMU**를 부착하는 요구와 최초 12일·3인 계획을 유지한다.

## 게임에서 확인하는 순서

설정 → 입력 컨트롤러 → 무선 컨트롤러 → 기기 찾기 → 본인 기기 연결 → **연결 통신 확인**으로 PC 송신/기기 응답을 먼저 확인한다. 이 버튼은 v2 LINK_PROBE를 보내고 같은 nonce의 LINK_REPLY를 2초 안에 기다린다. 응답은 센서가 없어도 가능하다. 시간은 PC 왕복 시간이며 IMU 지연이나 정확도가 아니다.

통신 성공은 보정이나 경기 사용 준비 완료가 아니다. 센서를 연결하고 정지 보정 및 서브·스윙·스킬 테스트를 끝내야 저장 버튼이 활성화된다. 취소는 원래 설정을 유지한다. 연결이 끊기거나 기기가 바뀌면 해당 연결의 진단/테스트를 다시 해야 한다. 키보드 상세에는 무선 연결 기능을 추가하지 않는다.

## 저장소 경계와 고정 의존성

| 제품 | 이번 변경 | 참조 |
| --- | --- | --- |
| protocol | v2 전용 LINK_PROBE=130 / LINK_REPLY=131, 기존 20바이트/CRC 보존 | d9a36826afbafa27a0e276cc53e44c904d05be53 |
| ble-unity | 연결별 nonce·만료, 보정과 독립된 결과, 네이티브 직렬 write 큐 | ecbf54c7d15fb7cb519ee0a8d93534a54a7abc90 |
| firmware | 유한 재전송 큐, 버튼 디바운스, 구간 기반 모션/선택 모드·녹화 규칙 | c2513e8ffc0f23449a372dfa0437dd309e808666 |
| game | 위 진단 UI·입력 연결과 검증기 화면 대기 보완 | 이 PR의 커밋 |

Unity manifest/lock은 위 protocol/SDK 전체 커밋으로 고정했다. 향후 스쿼시 병합 시 protocol → firmware/SDK → game → docs 순서로 진행하며 소비 제품을 **새 병합 커밋**으로 다시 고정해야 한다. 현재 PR을 올리는 작업에는 병합이 포함되지 않는다.

## 검증 결과와 재현

- protocol: C/C# 공통·오류·fuzz 2,045사례 및 sanitizer 통과.
- SDK: 39검사 통과, Mac universal 및 Windows 네이티브 헬퍼 교차 빌드 통과.
- firmware: host 2개 모음 통과, sensorless·합성 외장 MPU6050·합성 버튼 alias의 NU40-DK 3구성 컴파일 통과. 실제 보드에는 업로드하지 않았다.
- Unity 6000.3.26f1: 프로젝트 검사, EditMode 189/189, PlayMode 43통과·0실패·1 Null GPU 제외, 별도 GPU 마우스 검사 1/1, 패킷 장면 1/1 통과.
- Mac 일반 창 GPU 패킷 검증: 15화면, 오류0, Near 타격1/Far 타격2, 득점1, 다음 서브 및 단절 일시정지 통과. 설정→저장→경기의 실제 SDK/코덱/입력 어댑터를 사용하지만 **라디오는 명시적 MOCK**이다. 물리 마우스 입력이나 실물 페어링의 증거로 사용하지 않는다.
- 검증기의 화면 전환 직후 버튼 호출 실패를 발견해 렌더 완료/화면 전환 대기를 추가했고 일반 창 재검증을 통과했다. 사용자 입력 처리나 경기 판정은 변경하지 않았다.

```sh
bash tools/unity.sh check
bash tools/unity.sh test-controller
bash tools/unity.sh test-edit
bash tools/unity.sh test-play
bash tools/unity.sh test-mouse
bash tools/unity.sh build-verification-mac
# 검증 빌드에만 명시적으로 -dt-controller-packet-verify <별도 출력 폴더> 사용
```

README의 3개 이미지는 [기존 캡처의 출처](screenshots/README.md)를 가진 게임 장면이다. 새 실물 센서 검증 사진으로 취급하지 않는다.

검수 파일: [Unity 결과](evidence/controller-readiness-2026-10-18/unity-tests.json), [일반 창 MOCK 결과](evidence/controller-readiness-2026-10-18/gpu-mock-result.json), [통신 확인 화면](evidence/controller-readiness-2026-10-18/MOCK-sensorless-link.png).

## NU-54 참고 코드와 실기 현황

참고 코드 `nu-54v-dk-toy/products`의 ee12b7519336ed276060b83c69f4f863f557443a에서 연결별 수명/구독 검사, Notify 역압 처리, bonding 설정 보존, PC의 Notify 구독 뒤 직렬 WriteWithResponse 패턴을 검토했다. NU-54 애플리케이션의 결제·지갑·프로비저닝 기능이나 UUID/메시지를 테니스 프로토콜로 가져오지 않았다. NU-54 참고 서비스와 테니스 서비스는 서로 다른 계약이다.

연결된 NU54DK_v2_Pre-release USB와 켜진 Mac Bluetooth는 확인했다. SW3 페어링 모드 요청 뒤 원본 스캐너가 주변 10기기를 찾았지만 NU54/NUS/참고 사용자 서비스 광고를 찾지 못했다. USB의 조회 명령에도 응답이 없었다. 따라서 실물 연결·페어링·RX/TX 왕복은 **미검증**이다. 기존 펌웨어 이름과 LED 상태 확인이 필요하며 펌웨어 덮어쓰기/초기화는 하지 않았다. 센서 미장착 상태에서 보정 성공을 꾸며내지 않는다.

## 남은 실기와 배포 경계

실제 IMU 모델·배선·장착 축·왼손/오른손 임계값, 버튼 GPIO/극성, 실스윙 정확도/전파 지연·재연결을 장비 단계에서 검증한다. 합성 8프로필은 알고리즘 경계 테스트이며 사람의 8제스처 인식률이 아니다. Windows 실행/Windows BLE·서로 다른 두 PC/WAN은 미검증이다.

배포는 clean 커밋에서 `release-mac`, `release-windows` 및 `tools/package-release.py`로 만든다. 배포 manifest의 sourceCommit·development=false·allowDebugging=false와 SHA-256을 함께 확인한다. 이번 최종 배포 파일과 실행 결과는 docs 저장소의 컨트롤러 후속 검수 기록에 별도로 고정한다. Mac은 로컬 ad-hoc 미리보기이며 공증 배포는 수행하지 않는다.
