# 빌드 없이 밸런스 조절하기

게임의 조절값은 CSV에, 계산과 적용 시점은 코드에 둡니다. 공통 기본값은 `Assets/StreamingAssets/Balance/gameplay.csv`, 15명 캐릭터의 능력치·고유 스킬은 `Assets/StreamingAssets/Balance/characters.csv`, 시각 효과는 `Assets/StreamingAssets/Effects/effects.csv`에서 관리합니다. AI 패키지는 게임이 전달한 설정으로 판단하며 자체 난이도 기본값을 갖지 않습니다.

## 실제로 수정하는 파일

첫 실행 시 기본 CSV를 아래 사용자 폴더로 복사합니다. 이미 있는 파일은 덮어쓰지 않습니다. 설치된 게임에서도 이 외부 파일을 수정하면 됩니다.

- macOS: `~/Library/Application Support/Digital Tennis/Digital Tennis/Balance/`
- Windows: `%USERPROFILE%/AppData/LocalLow/Digital Tennis/Digital Tennis/Balance/`
- 정확한 경로는 게임의 **설정 → 밸런스 CSV**에 표시됩니다. Unity의 회사/제품 이름에 따라 경로가 달라질 수 있으므로 화면의 경로를 우선합니다.
- `-dt-gameplay-file <절대경로>`, `-dt-balance-file <절대경로>`로 파일을 따로 지정할 수 있습니다. 자동 검사는 이 옵션으로 사용자의 설정을 보호합니다.

`StreamingAssets`는 새 설치용 기본값입니다. 이미 실행한 게임의 외부 CSV까지 자동으로 덮어쓰지 않습니다. 기본값으로 복구하려면 게임을 종료한 뒤 외부 파일을 백업하고 삭제합니다. 다음 실행에 다시 복사됩니다.

## 수정 예시: 이동 속도를 높이기

1. 설정 화면에서 `gameplay.csv`의 위치를 확인하고 파일을 백업합니다.
2. 텍스트 편집기나 스프레드시트에서 `move.speed` 행의 `value`만 `5`에서 `6`으로 바꿉니다.
3. UTF-8 CSV로 저장합니다. 소수점은 `.`를 사용합니다. `key,value,unit,description` 헤더와 모든 행을 유지합니다.
4. 공이 멈추고 서브를 기다리며 두 선수의 스킬 효과가 끝난 상태에서 **F5**를 누릅니다. 오프라인 설정 화면의 **CSV 다시 읽기**도 같은 동작입니다.
5. `밸런스 적용 완료 · 버전 …`을 확인하고 서브합니다. 이동 기본 속도가 6m/s가 됩니다. 실제 속도에는 캐릭터 스피드·체력·스킬 배율이 함께 반영됩니다.

랠리·공 회수·스킬 효과 진행 중에는 적용을 거부합니다. 나중에 안전한 시점에 다시 F5를 누르세요. 자동 예약 적용은 하지 않습니다. 온라인은 연결이 정상인 **호스트만** 밸런스를 적용합니다. 손님 PC의 다른 CSV는 경기 결과를 바꾸지 못합니다. 호스트 설정과 캐릭터 상태를 검증한 스냅샷으로 표시하고, 재접속에도 새 스냅샷을 사용합니다. 온라인에서 나가면 자신의 로컬 설정으로 복귀합니다.

## 파일별 책임

| 파일 | 바꿀 수 있는 항목 | 적용 |
|---|---|---|
| gameplay.csv | 공 물리·6종 샷·타격 범위·이동·서브·경기 대기·체력 계산·게이지 보너스·볼보이·AI 3단계 | 캐릭터 파일과 함께 서브 대기에서 일괄 적용 |
| characters.csv | 서브·스매시·발리·방어·체력·스피드, 고유 스킬 배율·회복·지속 시간·게이지 획득 | 공통 설정과 함께 일괄 적용 |
| effects.csv | 파티클 개수·크기·속도·수명·색 등 | 각 PC에서 F5로 즉시 적용; 경기 판정과 무관 |

한 밸런스 파일이 잘못되면 **gameplay.csv와 characters.csv 모두 이전 정상 버전을 유지**합니다. 일부 행만 적용하지 않습니다. 처음부터 외부 파일이 잘못된 경우 두 파일 모두 배포 기본 CSV를 사용합니다. 잘못된 행·누락·중복·알 수 없는 키·NaN/Infinity·범위 초과·서로 모순되는 높이/각도/체력 비용을 거부합니다. `ai.*.skill_stock`은 정수만 허용합니다. F5 결과와 실패 이유를 설정 화면에서 확인할 수 있습니다.

## 주요 계산

- 능력치 배율 = `1 + (캐릭터 능력치 - rating.center) × rating.scale`
- 일반 샷 속도 = `shot.base_speed + shot.power_speed × 입력 세기`
- 실제 이동 속도 = `move.speed × 스피드 배율 × 체력 배율 × 활성 스킬 배율`
- 실제 샷은 스트로크별 CSV 값에 능력치·체력·스킬 배율을 적용합니다.
- AI는 같은 속도·각도·현재 물리 계수·실제 캐릭터 샷 배율로 착지를 계산합니다. 난이도는 반응 시간·위치 오차·스윙 확률·최대 세기·스킬 사용 시점으로 나뉩니다.

게이지의 0–100 표시, 최대 사용권 **3개**, 점수 15/30/40·듀스·어드밴티지, 폴트·렛·네트·라인 판정, 정식 코트/네트 치수는 규칙 및 데이터 계약입니다. 수치 적분 간격·오차 허용치·입력 유효시간·네트워크 제한은 안정성 계약입니다. 이 값과 순수 카메라/애니메이션 모양은 밸런스 CSV에 섞지 않습니다. 이번 범위는 1게임 MVP이며 세트·타이브레이크를 추가하지 않습니다.

## 새 밸런스 항목을 추가하는 개발자

`GameplayBalance`의 스키마에 키와 안전 범위를 추가하고 기본 CSV에 행을 추가하세요. 소비 함수에는 현재 `GameplayBalance`를 전달합니다. 코드에 `없으면 5` 같은 숨은 기본값을 만들지 않습니다. AI가 쓰는 항목이면 `AiTuning` 주입도 갱신합니다. 온라인은 고정된 정렬 순서의 값과 SHA-256 설정 해시를 함께 보내며, 키 수/범위/해시가 다른 스냅샷을 거부하므로 양쪽 게임 버전을 함께 배포해야 합니다. 해시는 동일 설정 확인용이며 인증 수단이 아닙니다.

검사는 잘못된 파일의 일괄 거부, 실제 샷·바운스·이동·체력 변화, 이벤트 ID 보존, 안전한 시점의 F5, 호스트 설정 우선 적용을 확인해야 합니다. 단순히 CSV를 읽었다는 검사만으로 완료하지 않습니다.

## 전체 공통 키와 허용 범위

범위는 저장 파일의 오타와 위험한 조합을 막기 위한 제한입니다. 모든 범위 안의 조합이 공정하거나 플레이하기 좋음을 보장하지는 않습니다. 한 번에 소수 항목을 바꾸고 1인·온라인 경기를 비교하세요.

| key | 기본값 | 허용 범위 | 단위 | 의미 |
|---|---:|---|---|---|
| `physics.gravity` | 9.81 | 1–30 | m/s2 | 중력 |
| `physics.radius` | 0.12 | 0.03–0.3 | m | 공 반지름 |
| `physics.bounce` | 0.7 | 0.1–1 | ratio | 바운스 반발력 |
| `physics.ground_retention` | 0.96 | 0.1–1 | ratio | 바운스 후 수평 속도 |
| `physics.net_bounce` | 0.35 | 0–1 | ratio | 네트 반발력 |
| `physics.rest_speed` | 0.35 | 0.05–2 | m/s | 정지 판정 속도 |
| `shot.base_speed` | 10 | 2–30 | m/s | 최소 샷 속도 |
| `shot.power_speed` | 12 | 1–30 | m/s | 입력 세기에 따른 추가 속도 |
| `shot.yaw` | 25 | 1–45 | deg | 좌우 조준 최대 각도 |
| `shot.pitch` | 20 | 5–45 | deg | 기본 발사 각도 |
| `shot.pitch_aim` | 10 | 0–20 | deg | 상하 조준 각도 |
| `shot.default_power` | 0.35 | 0–1 | ratio | 키보드 기본 세기 |
| `smash.flight` | 0.5 | 0.2–2 | s | 스매시 기본 비행 시간 |
| `smash.aim_width` | 3 | 0–4 | m | 스매시 좌우 조준 폭 |
| `smash.target_width` | 3.8 | 0.5–4.115 | m | 스매시 착지 좌우 한계 |
| `smash.target_depth` | 7 | 2–10 | m | 스매시 착지 깊이 |
| `smash.aim_depth` | 2 | 0–3 | m | 스매시 깊이 조준 폭 |
| `lob.horizontal` | 0.6 | 0.1–1.5 | ratio | 로브 좌우 속도 배율 |
| `lob.lift` | 9 | 3–15 | m/s | 로브 상승 속도 |
| `lob.aim_lift` | 2 | 0–4 | m/s | 로브 상하 조준 |
| `lob.forward` | 0.65 | 0.1–1.5 | ratio | 로브 전진 속도 배율 |
| `slice.lift` | 0.85 | 0.1–1.5 | ratio | 슬라이스 상승 배율 |
| `slice.forward` | 0.92 | 0.1–1.5 | ratio | 슬라이스 전진 배율 |
| `volley.lift` | 0.85 | 0.1–1.5 | ratio | 발리 상승 배율 |
| `volley.forward` | 0.95 | 0.1–1.5 | ratio | 발리 전진 배율 |
| `hit.reach` | 1.5 | 0.3–3 | m | 기본 수평 타격 범위 |
| `hit.min_height` | 0.2 | 0.05–1 | m | 최소 타격 높이 |
| `hit.max_height` | 2.5 | 1–4 | m | 최대 타격 높이 |
| `hit.smash_height` | 1.75 | 0.5–4 | m | 스매시 최소 높이 |
| `hit.net_gap` | 0.2 | 0.05–1 | m | 네트 근처 타격 제외 거리 |
| `move.speed` | 5 | 1–10 | m/s | 기본 이동 속도 |
| `move.home_depth` | 8 | 3–11.885 | m | 복귀 위치 깊이 |
| `move.min_depth` | 0.8 | 0.2–3 | m | 네트 앞 이동 한계 |
| `move.contact_height` | 1 | 0.2–2.5 | m | 자동 이동 목표 타격 높이 |
| `serve.offset` | 1.5 | 0.3–3.8 | m | 서브 좌우 위치 |
| `serve.height` | 2.2 | 1–3.5 | m | 서브 시작 높이 |
| `serve.depth` | 12.6 | 11.9–15 | m | 서브 베이스라인 뒤 위치 |
| `serve.target_depth` | 4 | 1–6.4 | m | 서비스 박스 착지 목표 |
| `serve.flight` | 1.2 | 0.7–2 | s | 기본 서브 비행 시간 |
| `serve.receiver_depth` | 9 | 5–11.885 | m | 리시버 대기 위치 |
| `match.next_point_delay` | 1.25 | 0.2–5 | s | 득점 후 다음 서브 준비 시간 |
| `match.auto_serve_delay` | 0.75 | 0.1–5 | s | 연습 자동 서브 대기 |
| `rating.center` | 70 | 1–100 | rating | 평균 능력치 기준 |
| `rating.scale` | 0.004 | 0–0.01 | ratio/rating | 능력치 1점당 배율 변화 |
| `energy.move_floor` | 0.9 | 0.5–1 | ratio | 체력 0일 때 이동 배율 |
| `energy.shot_floor` | 0.96 | 0.5–1 | ratio | 체력 0일 때 샷 배율 |
| `energy.hit_cost` | 4 | 0–20 | energy | 타격 체력 비용 |
| `energy.hit_factor` | 1.2 | 1–2 | ratio | 타격 소모 기본 계수 |
| `energy.hit_stamina` | 0.006 | 0–0.01 | ratio/rating | 체력 능력치의 타격 비용 감소 |
| `energy.move_cost` | 0.8 | 0.1–2 | energy/m | 이동 체력 비용 |
| `energy.move_stamina` | 0.005 | 0–0.01 | energy/m/rating | 체력 능력치의 이동 비용 감소 |
| `energy.recovery` | 5 | 0–20 | energy/s | 대기 중 기본 회복 |
| `energy.recovery_stamina` | 0.05 | 0–0.2 | energy/s/rating | 체력 능력치별 회복 보너스 |
| `meter.fast_ball` | 18 | 1–50 | m/s | 빠른 공 수비 보너스 기준 |
| `meter.fast_bonus` | 4 | 0–30 | gauge | 빠른 공 수비 보너스 |
| `meter.serve_gain` | 0.5 | 0–2 | ratio | 유효 서브 게이지 배율 |
| `recovery.speed` | 5 | 1–10 | m/s | 볼보이 속도 |
| `recovery.pickup` | 0.65 | 0.1–3 | s | 공을 줍는 시간 |
| `ai.target_width` | 2.5 | 0–4.115 | m | AI 코너 조준 위치 |
| `ai.target_depth` | 8 | 2–11.885 | m | AI 착지 목표 깊이 |
| `ai.easy.reaction` | 0.35 | 0–1 | s | easy 반응 시간 |
| `ai.easy.error` | 1.5 | 0–3 | m | easy 목표 위치 오차 |
| `ai.easy.probability` | 0.65 | 0–1 | ratio | easy 스윙 시도 확률 |
| `ai.easy.max_power` | 0.5 | 0–1 | ratio | easy 최대 입력 세기 |
| `ai.easy.skill_stock` | 3 | 1–3 | stock | easy 스킬을 사용할 최소 사용권 |
| `ai.medium.reaction` | 0.2 | 0–1 | s | medium 반응 시간 |
| `ai.medium.error` | 0.8 | 0–3 | m | medium 목표 위치 오차 |
| `ai.medium.probability` | 0.85 | 0–1 | ratio | medium 스윙 시도 확률 |
| `ai.medium.max_power` | 0.65 | 0–1 | ratio | medium 최대 입력 세기 |
| `ai.medium.skill_stock` | 2 | 1–3 | stock | medium 스킬을 사용할 최소 사용권 |
| `ai.hard.reaction` | 0.1 | 0–1 | s | hard 반응 시간 |
| `ai.hard.error` | 0.25 | 0–3 | m | hard 목표 위치 오차 |
| `ai.hard.probability` | 0.95 | 0–1 | ratio | hard 스윙 시도 확률 |
| `ai.hard.max_power` | 0.8 | 0–1 | ratio | hard 최대 입력 세기 |
| `ai.hard.skill_stock` | 1 | 1–3 | stock | hard 스킬을 사용할 최소 사용권 |
