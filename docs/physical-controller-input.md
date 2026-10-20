# 물리 컨트롤러 입력과 테니스 해석

2026-10-20 / v3 검토본. NU40-DK + 별도 외장 IMU가 제품이며, NU54는 센서 없는 임시 버튼 타깃입니다.

## 책임과 데이터 흐름

`firmware 물리 이벤트 → protocol → SDK 중립 DTO → NativeControllerBackend → SensorPlayerInputSource → PlayerInputRouter/Adapter → 온라인 호스트의 PhysicalTennisInputResolver → SubmitInput`.

보드와 SDK는 공의 상태·서브권·점수를 모릅니다. 물리 패턴은 횡회전 +/−, 위→아래, 짧은 회전, 대각 하강/상승의 6종입니다. 버튼의 게임 기본 바인딩은 1=ControllerPrimary, 2/3=중립 횡회전, 4=스킬 요청입니다. Up은 backend에서 관찰하며 추가 게임 명령을 만들지 않습니다. 길게 누를 때도 한 번만 실행합니다. 바인딩을 바꾸려면 `ControllerDevices.cs`의 `SensorPlayerInputSource.Poll`을 수정합니다. 향후 홀드·연속 회전·캐릭터별 스킬 동작은 이 게임 입력 계층에서 이벤트 조합으로 해석합니다. 후속 개선에서는 아래의 게임 측 물리 패턴 인식기를 센서 입력에 연결합니다. 클릭/더블클릭과 장치에서 보내지 않는 정지/밀기 동작은 추론하지 않습니다.

| 물리 입력 | 호스트 경기 문맥 | 결과 |
| --- | --- | --- |
| OverheadDown | 자신의 서브 대기 | Serve |
| 같은 OverheadDown | 랠리/진행 중인 공 | Smash 후보. 실제 공 높이·거리·타이밍 검사 적용 |
| 같은 OverheadDown | 다른 선수의 서브/득점 후/경기 종료 | 거부 |
| ShortRotation | 바운드 전 | Volley 후보 |
| ShortRotation | 바운드 후 | 조준 부호에 따라 Forehand/Backhand 후보 |
| HorizontalPositive/Negative | 랠리 | Forehand/Backhand 후보 |
| ObliqueDown/Up | 랠리 | Slice/Lob 후보 |

테니스 의미를 변경할 곳은 `PhysicalTennisInputResolver.TryResolve`입니다. 세기·방향과 실제 물리는 기존 CSV와 타격 검사를 사용합니다. 중립 입력은 호스트 권한 확인 후 해석합니다. 게스트는 중립 문자열을 그대로 전송하며, 서버는 허용 종류·역할·epoch만 검사합니다.

## 설정과 호환

새 프로필은 PhysicalMotion=2 / PhysicalButtons=3, 기존 프로필은 Motion=0 / Buttons=1입니다. SDK는 호환을 위해 v2 LINK_PROBE를 보내고 새 펌웨어가 v3로 응답합니다. 모션 프로필은 현재 연결의 보정 성공 ACK와 센서 flags가 필요합니다. 설정 UI는 물리 패턴과 버튼 번호를 안내하며, 테스트 완료 후 저장해야 게임 입력 소스가 바뀝니다. 취소하면 저장된 설정을 보존하고, 단절·소스 변경 시 대기 입력을 폐기합니다.

구형 v1/v2 샷은 기존 의미로 해석합니다. 새 SDK는 양쪽을 지원하고 구형 SDK는 v3를 거부합니다. 온라인의 새 입력 종류를 사용하기 전에 매치 서버를 먼저 갱신해야 합니다. 실제 페어링·암호화·본딩 검사는 유지합니다.

## 재현과 증거의 한계

`bash tools/unity.sh check`, `test-edit`, `test-play`, `build-mac`을 실행합니다. 같은 프로젝트의 Editor를 먼저 닫습니다. `PhysicalInputTests`와 `PhysicalInputSceneTests`는 동일 물리 이벤트의 경기 상태·바운드·높이·중복 규칙을 검사합니다. `PacketControllerSceneTests`는 C 실행기에서 만든 20바이트를 모의 라디오 → 실제 SDK → 설정 저장 → 서브 입력으로 전달합니다. 테스트의 공 배치·모의 BLE·합성 센서는 실물 성공의 증거가 아닙니다.

두 실행 파일의 온라인 검증:

```sh
node tools/verify-online.mjs --server-repo /absolute/path/digital-tennis-match-server \
  --headless --transport relay --physical-inputs --out .work/physical-online
```

검증용 소스는 이 옵션에서만 기본 서브·타격을 중립 입력으로 보냅니다. 완전한 점수 회귀를 위한 의도적 FaultServe 명령은 명시적 게임 명령으로 남습니다. 두 프로세스의 `localPhysicalInputs`와 호스트의 `remotePhysicalInputs`가 0보다 커야 하며, 게스트의 `physicsTicks`는 0이어야 합니다. 이 검증은 실제 장치·다른 PC·WAN 검증과 구분합니다. 보드 설치·추가 버튼 테스트·실물 IMU는 이번 작업에서 수행하지 않습니다.

## 이번 변경의 검증 결과

[검증 기록](evidence/physical-controller-input/validation.json)에 소스·패키지·실행 파일·C 패킷·XML 해시를 남겼습니다. Unity EditMode 202개와 PlayMode 46개가 통과했고, 렌더링이 필요한 포인터 1개는 별도 GPU 검사로 통과했습니다. 마지막 HUD 문구 보완 후 실제 패킷 설정 검사와 GPU 마우스 검사를 다시 통과하고 깨끗한 소스로 Mac을 빌드했습니다.

온라인은 같은 Mac의 두 실행 파일에서 릴레이로 86포인트·최대 6회 랠리·2–0 세트(7–6, 6–0), 타이브레이크 8–6이 일치했습니다. 호스트 로컬 중립 입력55개·게스트 중립 입력42개가 전달되었고, 게스트 물리 tick·프로토콜/런타임 오류는0이었습니다. 온라인의 소스 커밋은 `49a6353`, 이후 HUD/보정 안내만 보완한 최종 Mac 빌드 소스는 `44e40dd`입니다. 보완 후 경기·네트워크 코드는 바뀌지 않았습니다.

### 스쿼시 병합 참조 갱신

2026-10-20: protocol `2bb3f0e80a9280458ccfe087201504a4613994e7`, SDK `a0e705ef037ed554ce2c0dba33676784ea06c588`로 manifest와 Unity lock을 갱신했습니다. 병합된 패키지를 실제로 해석하는 Unity check와 패킷 설정 검사3개가 통과했습니다. 위의 센서·온라인 실행 증거는 당시 원래 커밋의 기록으로 보존합니다.

## 게임 측 캐릭터별 물리 스킬 패턴

선택된 센서 source를 `GestureSkillInputSource`로 감쌉니다. v3의 실제 MOTION6종만 같은 이름의 토큰으로 변환합니다. `Balance/gestures.csv`의15개 패턴은 서로 다르며, 경기의 로컬 선수ID(게스트는Far)로 선택합니다. 앞의 동작은 기존 중립 타격 후보로 전달하고, 완성하는 마지막 동작만Skill로 바꿉니다. 버튼4와 키보드K의Skill은 유지합니다. 버튼2/3이 같은 중립 타격 명령으로 연결되어도 로컬 입력의 `IsPhysicalMotion`은false이므로 모션 패턴에 섞지 않습니다. 이 표시는 게임 내부용이며 BLE/온라인 wire를 확장하지 않습니다. 장치/SDK는 캐릭터나 스킬 패턴을 알지 않습니다.

예: 한국은 횡회전− → 대각상승 → 횡회전+, 일본은 위→아래 → 대각상승 → 짧은회전입니다. 실제 손동작 정확도/편의성은 검증하지 않았습니다. 보드의 물리분류 임계값과 게임의 패턴간격은 서로 다른CSV입니다.

패턴은 스킬재고가 있고 기존효과/연출이 끝난 정상 게임에서만 인식합니다. 메뉴·설정·타이핑·정지·득점후·단절·선수/소스변경은 부분패턴을 폐기합니다. 완성은 기존Skill 요청을 낼 뿐이며 실제 재고·활성화·연출은 호스트의 일반 경로가 검증합니다. 점수/공/재고를 입력원에서 직접 바꾸지 않습니다.

정상 앱은 첫 실행에 `Application.persistentDataPath/Balance/gestures.csv`를 만들고 사용자파일을 덮어쓰지 않습니다. 수정 파일을 원자적으로 교체한 뒤 서브대기/효과종료 상태의F5로 다시 읽거나 게임을 다시 실행합니다. `-dt-gestures-file /absolute/isolated.csv`로 검증용파일을 분리할 수 있습니다. 잘못된 재로드는 이전 설정을 유지합니다.15명전체의ID·중복패턴·토큰·시간범위를 검사합니다.

재현: `test-edit`, `test-controller`, `test-motion`은15패턴·시간/중복·상태격리·패킷설정경로·기존실제랠리의충전/소비를 검사합니다. `test-ai-evaluation`은 실제SinglePlayerCourt의3난이도×3시드·두서비스게임을 측정합니다. 인간승률/실물센서인식률과 구분합니다.
