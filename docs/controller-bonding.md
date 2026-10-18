# 실제 컨트롤러 본딩 검수

2026-10-19 검토 브랜치. 실제 제품 대상은 **NU40-DK+별도 외장 IMU**이고, 연결된 NU-54는 센서 없는 BLE 시험용이다.

## 구현과 순서

기기별 설정→무선 상세→서비스 UUID 검색→OS 보호된 GATT/페어링→현재 peer의 암호화·bond 확인→LINK_PROBE/REPLY 왕복→외장 IMU 보정/서브·스윙·스킬→저장 순서다. 검색 결과나 단순 GATT 연결로 연결 완료를 표시하지 않는다. SDK의4바이트 보안 읽기는 암호화≥L2/bond를 요구한다. Mac 공개 API에 없는 OS 키 DB를 직접 열람한 것으로 표현하지 않는다.

현재 시험 정책은 사용자 승인된 **LE Secure Connections + Just Works**다. 통신 암호화·본딩은 유지되며 PIN/숫자 대조 MITM 인증은 제공하지 않는다. 보호된 CCC의 암호화 대기(ATT5/15)에는 제한 재시도하며 UI 전체 연결 대기는60초다. 승인 취소·기한 만료를 성공으로 바꾸지 않는다.

## 현재 실물 결과

NU-54 테니스 펌웨어 코드 설치·512바이트 단위 대조를 통과했다. 기존 앱 코드만 비공개 백업하고 기존 지갑 저장영역을 읽거나 지우지 않았다. Tennis 설정은 별도 ZMS, OS 캐시·bond는 별도 영속 identity1로 분리했다. NU40 GPIO는 nu40dk-kit 원본에 맞춘 앱 overlay를 사용하며 NU54 저장 맵을 적용하지 않는다.

Unity 일반 창에서 검색과 연결 요청을 수행했다. 첫 보안 설정의 인증 등급 불일치를 수정했고, 후속 네이티브 시도는 ATT15/암호화 미완료 및 SMP timeout을 기록했다. 단순 연결을 본딩으로 수용하지 않았다. 현재 Mac 수동 잠금 해제 후 일반 창의 OS 승인·최종 송수신·재접속 확인을 이어가야 한다. **실물 OS 본딩과 테니스 메시지 왕복 성공은 아직 미완료**다. IMU 준비·보정·게임 입력 성공도 주장하지 않는다.

## 소프트웨어 근거

protocol20바이트2,045사례/보안66,046사례, SDK44개, firmware host2모음 및 NU40/NU54·합성 외장센서 컴파일, Unity Edit189/189·Play43통과/1 NullGPU제외·별도 GPU마우스1/1·패킷 장면1/1을 구분한다. MOCK 검증은 실물 BLE 성공이 아니다. 최신 native helper·프로토콜·AI는 Packages/manifest.json의 Git SHA로 고정한다.

`-dt-input-observe <local-directory>`는 실제 키보드/native backend를 유지하는 수동 검수 기록이다. 이벤트 종류/길이·파싱 오류만 추가 기록하고 원시 데이터·기기 주소·키·OS stderr는 기록하지 않는다. 테스트 입력을 주입하거나 보정/저장 게이트를 우회하지 않는다. 기존 게임 캡처3개는 README에서 유지한다.

[전체 제품 책임/판정 위치](https://github.com/digital-tennis/digital-tennis-docs/blob/codex/controller-readiness/docs/project/architecture.md), [정본 컨트롤러 ICD](https://github.com/digital-tennis/digital-tennis-protocol/blob/12dae689fd9b25187426829c606529508c100e65/spec/controller-v2.md).
