# 월드 캐릭터·코트·스킬 설계

15명 모두 프로젝트 고유의 가상 성인 선수입니다. 개인의 성별·문화적 배경·훈련 방식은 페르소나이며 국가나 인종 전체의 능력을 뜻하지 않습니다. 모든 기본 능력치 합계는 420입니다. 캐릭터의 완성형 일러스트/모션캡처 에셋 대신 독립적인 색·의상 실루엣·절차적 3D 리그/포즈/리본 효과를 구현합니다.

## 명칭과 규칙

포핸드/백핸드는 라켓을 쓰는 방향, 발리는 공이 바운스하기 전의 타격, 스매시는 머리 위 공을 아래로 치는 오버헤드입니다. 슬라이스와 로브도 별도 입력으로 제공합니다. [ITF 용어집](https://www.itftennis.com/en/about-us/organisation/tennis-glossary/)을 참고했습니다. 수비·체력·스피드는 샷 명칭이 아닌 이번 게임의 능력치 축입니다. 실제 회전 물리는 아직 없으므로 슬라이스는 낮은 궤적의 게임용 근사입니다.

서브의 첫 바운스 전 발리 금지, 타격 거리·높이, 자기 코트 조건, 네트·아웃·렛·두 번 바운스·득점 규칙을 스킬이 우회하지 못합니다. 필살기 연출은 양쪽 공·경기 시계를 1.2초 정지한 후 같은 샷에서 재개하며 공 위치나 점수를 새로 만들지 않습니다.

## 초기 밸런스

| 국가 | 캐릭터 | 서브 | 스매시 | 발리 | 수비 | 체력 | 스피드 | 합계 |
| --- | --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| 한국 | 한서윤 | 70 | 70 | 70 | 70 | 70 | 70 | 420 |
| 일본 | 아오야마 렌 | 65 | 60 | 82 | 65 | 60 | 88 | 420 |
| 중국 | 린웨 | 68 | 62 | 90 | 78 | 65 | 57 | 420 |
| 미국 | 마야 브룩스 | 92 | 80 | 60 | 62 | 60 | 66 | 420 |
| 러시아 | 니콜라이 소콜로프 | 72 | 68 | 55 | 93 | 82 | 50 | 420 |
| 인도 | 아라브 라오 | 60 | 65 | 65 | 78 | 95 | 57 | 420 |
| 독일 | 레나 베버 | 83 | 67 | 80 | 68 | 62 | 60 | 420 |
| 영국 | 테오 베넷 | 65 | 60 | 95 | 70 | 65 | 65 | 420 |
| 스페인 | 루시아 세라노 | 72 | 94 | 55 | 58 | 68 | 73 | 420 |
| 프랑스 | 카미유 로랑 | 70 | 65 | 86 | 72 | 57 | 70 | 420 |
| 호주 | 카이 모건 | 80 | 66 | 66 | 58 | 62 | 88 | 420 |
| 네덜란드 | 노르 더프리스 | 60 | 58 | 83 | 67 | 62 | 90 | 420 |
| 브라질 | 하파엘 코스타 | 58 | 78 | 66 | 80 | 73 | 65 | 420 |
| 모로코 | 살마 암라니 | 75 | 55 | 65 | 92 | 76 | 57 | 420 |
| 남아공 | 타보 은들로부 | 62 | 76 | 58 | 70 | 88 | 66 | 420 |

서브는 정상 서비스 박스를 유지하면서 비행 시간을 조절합니다. 스매시/발리는 해당 샷의 속도, 수비는 타격 허용 거리와 성공 리턴의 수비 게이지, 체력은 이동·타격 소모와 휴식 회복, 스피드는 자동 이동 속도에 적용됩니다. 지친 상태의 이동 저하는 최대 10%, 일반 타격 저하는 최대 4%로 제한합니다. 능력치와 스킬 배율을 높여도 샷이 코트 안에 자동 착지하지는 않습니다.

## 15개 페르소나와 고유 연출

### 한국 / 한서윤 (Han Seo-yun)

- 배경: Seoul, 26세, woman, Korean.
- 성격/생활: measured urban designer; practices footwork to percussion.
- 의상: hanbok seam lines and dancheong color accents. 기본/포인트/보조 색: #F3F1E9 / #C93445 / #263E80.
- 코트: **KR-SEO-01 / Han River Dawn**. hanok roof rhythm; riverside dawn. hard 표현은 시각 테마이며 물리·경계는 공통입니다.
- 고유 스킬: **Han River Crescendo**. 5초, 일반타격 ×1.1, 서브 ×1.06, 스매시 ×1.08, 발리 ×1.08, 수비거리 ×1.08, 이동 ×1.08, 체력 즉시 +8.
- 연출 키 0: 서로 다른 준비 자세·몸통 회전·도약 높이·라켓 스윕·리본 회전 수와 고유 배색을 사용합니다.

### 일본 / 아오야마 렌 (Ren Aoyama)

- 배경: Kyoto, 24세, man, Japanese.
- 성격/생활: quiet paper sculptor; quick split-step specialist.
- 의상: indigo workwear and folded-paper piping. 기본/포인트/보조 색: #182A57 / #EFE8D8 / #C7474B.
- 코트: **JP-KYO-01 / Indigo Courtyard**. folded paper screens; blue-hour courtyard. hard 표현은 시각 테마이며 물리·경계는 공통입니다.
- 고유 스킬: **Indigo Step**. 5초, 일반타격 ×1.02, 서브 ×1.02, 스매시 ×1.05, 발리 ×1.12, 수비거리 ×1.08, 이동 ×1.28, 체력 즉시 +0.
- 연출 키 1: 서로 다른 준비 자세·몸통 회전·도약 높이·라켓 스윕·리본 회전 수와 고유 배색을 사용합니다.

### 중국 / 린웨 (Lin Yue)

- 배경: Shanghai, 28세, woman, Han Chinese.
- 성격/생활: calligrapher and doubles tactician; circular racket preparation.
- 의상: jade sports jacket and ink-brush bands. 기본/포인트/보조 색: #187A68 / #E9D9B6 / #A7353D.
- 코트: **CN-SHA-01 / Jade Arcade**. shikumen arch geometry; ink-green evening. hard 표현은 시각 테마이며 물리·경계는 공통입니다.
- 고유 스킬: **Jade Circuit**. 5초, 일반타격 ×1.06, 서브 ×1.02, 스매시 ×1.02, 발리 ×1.27, 수비거리 ×1.12, 이동 ×1.04, 체력 즉시 +0.
- 연출 키 2: 서로 다른 준비 자세·몸통 회전·도약 높이·라켓 스윕·리본 회전 수와 고유 배색을 사용합니다.

### 미국 / 마야 브룩스 (Maya Brooks)

- 배경: New York, 29세, woman, Black American.
- 성격/생활: rooftop robotics coach; explosive serve preparation.
- 의상: varsity paneling and skyline stripes. 기본/포인트/보조 색: #163A78 / #F5F1E7 / #D44844.
- 코트: **US-NYC-01 / Skyline Rooftop**. stepped skyline; electric dusk. hard 표현은 시각 테마이며 물리·경계는 공통입니다.
- 고유 스킬: **Skyline Drive**. 4초, 일반타격 ×1.02, 서브 ×1.23, 스매시 ×1.16, 발리 ×1.02, 수비거리 ×1.02, 이동 ×1.02, 체력 즉시 +0.
- 연출 키 3: 서로 다른 준비 자세·몸통 회전·도약 높이·라켓 스윕·리본 회전 수와 고유 배색을 사용합니다.

### 러시아 / 니콜라이 소콜로프 (Nikolai Sokolov)

- 배경: Saint Petersburg, 34세, man, Russian and Tatar heritage.
- 성격/생활: patient lighting artist; long-rally counterplayer.
- 의상: winter training layers and aurora gradient trim. 기본/포인트/보조 색: #D7E9F0 / #314B80 / #B74551.
- 코트: **RU-SPE-01 / White Night Arcade**. metro-inspired arches; luminous white night. hard 표현은 시각 테마이며 물리·경계는 공통입니다.
- 고유 스킬: **Aurora Shield**. 6초, 일반타격 ×1.02, 서브 ×1.02, 스매시 ×1.02, 발리 ×1.02, 수비거리 ×1.28, 이동 ×1.05, 체력 즉시 +10.
- 연출 키 4: 서로 다른 준비 자세·몸통 회전·도약 높이·라켓 스윕·리본 회전 수와 고유 배색을 사용합니다.

### 인도 / 아라브 라오 (Aarav Rao)

- 배경: Jaipur, 27세, man, South Asian Indian; Telugu family.
- 성격/생활: textile engineer and endurance player; deliberate rhythmic breathing.
- 의상: jali geometry and bandhani-inspired dots. 기본/포인트/보조 색: #E78936 / #0E7C67 / #EFE2C0.
- 코트: **IN-JAI-01 / Monsoon Pavilion**. jali screens; warm sandstone after rain. hard 표현은 시각 테마이며 물리·경계는 공통입니다.
- 고유 스킬: **Monsoon Rhythm**. 6초, 일반타격 ×1.04, 서브 ×1.02, 스매시 ×1.02, 발리 ×1.04, 수비거리 ×1.13, 이동 ×1.08, 체력 즉시 +28.
- 연출 키 5: 서로 다른 준비 자세·몸통 회전·도약 높이·라켓 스윕·리본 회전 수와 고유 배색을 사용합니다.

### 독일 / 레나 베버 (Lena Weber)

- 배경: Berlin, 31세, woman, German.
- 성격/생활: bike mechanic and serve technician; economical motions.
- 의상: Bauhaus blocks and precise seam grid. 기본/포인트/보조 색: #242C34 / #D8B341 / #B53A3D.
- 코트: **DE-BER-01 / Vector Works**. industrial trusses; clean amber work lights. hard 표현은 시각 테마이며 물리·경계는 공통입니다.
- 고유 스킬: **Pulse Vector**. 5초, 일반타격 ×1.04, 서브 ×1.16, 스매시 ×1.04, 발리 ×1.17, 수비거리 ×1.05, 이동 ×1.04, 체력 즉시 +0.
- 연출 키 6: 서로 다른 준비 자세·몸통 회전·도약 높이·라켓 스윕·리본 회전 수와 고유 배색을 사용합니다.

### 영국 / 테오 베넷 (Theo Bennett)

- 배경: London, 33세, man, Black British; Jamaican family.
- 성격/생활: garden volunteer and net-rush player; dry wit.
- 의상: tailored training vest and garden lattice. 기본/포인트/보조 색: #245449 / #EADFC3 / #80619D.
- 코트: **GB-LON-01 / Clock Garden**. garden pergola and clock rings; overcast green. grass 표현은 시각 테마이며 물리·경계는 공통입니다.
- 고유 스킬: **Clockwork Rally**. 5초, 일반타격 ×1.04, 서브 ×1.02, 스매시 ×1.02, 발리 ×1.28, 수비거리 ×1.1, 이동 ×1.05, 체력 즉시 +0.
- 연출 키 7: 서로 다른 준비 자세·몸통 회전·도약 높이·라켓 스윕·리본 회전 수와 고유 배색을 사용합니다.

### 스페인 / 루시아 세라노 (Lucia Serrano)

- 배경: Seville, 25세, woman, Spanish; Andalusian family.
- 성격/생활: ceramics apprentice and overhead attacker; joyful competitive streak.
- 의상: ceramic red panels and fan-fold shoulder trim. 기본/포인트/보조 색: #B93632 / #E5AD43 / #243D62.
- 코트: **ES-SEV-01 / Ember Terrace**. terracotta arcade; golden late afternoon. clay 표현은 시각 테마이며 물리·경계는 공통입니다.
- 고유 스킬: **Ember Arc**. 4초, 일반타격 ×1.1, 서브 ×1.02, 스매시 ×1.27, 발리 ×1.02, 수비거리 ×1.02, 이동 ×1.04, 체력 즉시 +0.
- 연출 키 8: 서로 다른 준비 자세·몸통 회전·도약 높이·라켓 스윕·리본 회전 수와 고유 배색을 사용합니다.

### 프랑스 / 카미유 로랑 (Camille Laurent)

- 배경: Paris, 28세, nonbinary, French; North African family.
- 성격/생활: stage costume maker; alternating tempo and precise volleys.
- 의상: fencing-jacket seams and prism-color lining. 기본/포인트/보조 색: #E7DED0 / #335F9B / #BC4458.
- 코트: **FR-PAR-01 / Prism Promenade**. glass awning and atelier lines; rose-blue twilight. clay 표현은 시각 테마이며 물리·경계는 공통입니다.
- 고유 스킬: **Prism Tempo**. 4초, 일반타격 ×1.14, 서브 ×1.02, 스매시 ×1.02, 발리 ×1.21, 수비거리 ×1.04, 이동 ×1.04, 체력 즉시 +0.
- 연출 키 9: 서로 다른 준비 자세·몸통 회전·도약 높이·라켓 스윕·리본 회전 수와 고유 배색을 사용합니다.

### 호주 / 카이 모건 (Kai Morgan)

- 배경: Melbourne, 23세, man, Australian; Maori and European family.
- 성격/생활: coastal rescue volunteer; confident serve-and-run player.
- 의상: coastal training kit and wave hems. 기본/포인트/보조 색: #1E816E / #E7BF4D / #D9EEE5.
- 코트: **AU-MEL-01 / Coastal Deck**. shade sails and wave rails; bright sea breeze. hard 표현은 시각 테마이며 물리·경계는 공통입니다.
- 고유 스킬: **Coastal Surge**. 5초, 일반타격 ×1.02, 서브 ×1.12, 스매시 ×1.02, 발리 ×1.06, 수비거리 ×1.02, 이동 ×1.25, 체력 즉시 +0.
- 연출 키 10: 서로 다른 준비 자세·몸통 회전·도약 높이·라켓 스윕·리본 회전 수와 고유 배색을 사용합니다.

### 네덜란드 / 노르 더프리스 (Noor de Vries)

- 배경: Amsterdam, 30세, woman, Dutch; Indonesian family.
- 성격/생활: canal engineer; wide-angle net player.
- 의상: orange technical jacket and canal-line bands. 기본/포인트/보조 색: #E77E24 / #163B58 / #E4E9DD.
- 코트: **NL-AMS-01 / Orange Canal**. stepped gables and canal reflections; crisp morning. hard 표현은 시각 테마이며 물리·경계는 공통입니다.
- 고유 스킬: **Orange Orbit**. 5초, 일반타격 ×1.04, 서브 ×1.02, 스매시 ×1.02, 발리 ×1.17, 수비거리 ×1.04, 이동 ×1.23, 체력 즉시 +0.
- 연출 키 11: 서로 다른 준비 자세·몸통 회전·도약 높이·라켓 스윕·리본 회전 수와 고유 배색을 사용합니다.

### 브라질 / 하파엘 코스타 (Rafael Costa)

- 배경: Rio de Janeiro, 26세, man, Afro-Brazilian and Portuguese heritage.
- 성격/생활: community music tutor; fluid all-court counterattacker.
- 의상: wave-pavement print and lightweight green kit. 기본/포인트/보조 색: #1C8C66 / #ECC444 / #315D9C.
- 코트: **BR-RIO-01 / Verde Boardwalk**. wave pavement and hillside silhouettes; tropical sunset. clay 표현은 시각 테마이며 물리·경계는 공통입니다.
- 고유 스킬: **Verde Flow**. 5초, 일반타격 ×1.19, 서브 ×1.02, 스매시 ×1.08, 발리 ×1.06, 수비거리 ×1.14, 이동 ×1.03, 체력 즉시 +0.
- 연출 키 12: 서로 다른 준비 자세·몸통 회전·도약 높이·라켓 스윕·리본 회전 수와 고유 배색을 사용합니다.

### 모로코 / 살마 암라니 (Salma Amrani)

- 배경: Marrakesh, 29세, woman, Amazigh and Arab Moroccan.
- 성격/생활: architect and patient defensive strategist; strong spatial memory.
- 의상: zellige geometry and lightweight long-sleeve sportswear. 기본/포인트/보조 색: #C48346 / #258B84 / #EEE0BD.
- 코트: **MA-RAK-01 / Atlas Mosaic**. geometric arcade and tile bands; lantern dusk. clay 표현은 시각 테마이며 물리·경계는 공통입니다.
- 고유 스킬: **Atlas Echo**. 6초, 일반타격 ×1.02, 서브 ×1.13, 스매시 ×1.02, 발리 ×1.02, 수비거리 ×1.24, 이동 ×1.05, 체력 즉시 +8.
- 연출 키 13: 서로 다른 준비 자세·몸통 회전·도약 높이·라켓 스윕·리본 회전 수와 고유 배색을 사용합니다.

### 남아공 / 타보 은들로부 (Thabo Ndlovu)

- 배경: Cape Town, 32세, man, Black South African; isiZulu-speaking family.
- 성격/생활: trail guide and recovery specialist; calm under pressure.
- 의상: woven-line trim and mountain-contour jacket. 기본/포인트/보조 색: #247D62 / #D7B149 / #263A48.
- 코트: **ZA-CPT-01 / Cape Horizon**. flat mountain silhouette and woven rails; clear evening. hard 표현은 시각 테마이며 물리·경계는 공통입니다.
- 고유 스킬: **Cape Horizon**. 5초, 일반타격 ×1.02, 서브 ×1.02, 스매시 ×1.13, 발리 ×1.02, 수비거리 ×1.05, 이동 ×1.21, 체력 즉시 +22.
- 연출 키 14: 서로 다른 준비 자세·몸통 회전·도약 높이·라켓 스윕·리본 회전 수와 고유 배색을 사용합니다.

## 개별 스킬 동작의 차이

공통 길이는 1.2초이며 준비→집중→회전/스윕→복귀로 이어집니다. 아래는 현재 코드의 실제 포즈 파라미터입니다. 음수/양수 회전은 서로 반대 방향이며, 리본은 인물의 포인트 색과 기본 색을 사용합니다. 배경 장식은 각 문화의 모티프를 단순한 선으로 재해석한 원본 도형입니다.

| 선수 | 준비 기울기 / 회전 | 도약 / 리본 회전 계수 | 페르소나를 반영한 연출·코트 모티프 |
| --- | --- | --- | --- |
| 한서윤 | −24° / +95° | 0.14m / 2 | 타악 발놀림의 짧은 준비와 넓은 스윕, 한옥 지붕 곡선 |
| 아오야마 렌 | −12° / −125° | 0.08m / 3 | 종이 접듯 좁게 모으고 반대 방향 전개, 접힌 종이 윤곽 |
| 린웨 | −32° / +75° | 0.10m / 4 | 붓을 긋듯 낮게 모았다가 호를 그림, 골목 관문 |
| 마야 브룩스 | −48° / +110° | 0.24m / 2 | 옥상 운동의 큰 상체 준비와 점프, 계단형 스카이라인 |
| 니콜라이 소콜로프 | +16° / −70° | 0.05m / 5 | 낮은 중심의 방어 자세와 차가운 궤적, 백야 아케이드 |
| 아라브 라오 | −18° / +145° | 0.09m / 6 | 직조 리듬처럼 여러 궤적이 겹침, 격자 직물 |
| 레나 베버 | −8° / +60° | 0.12m / 4 | 짧고 정밀한 직선 준비, 산업 기하 블록 |
| 테오 베넷 | −26° / −100° | 0.07m / 3 | 일정한 박자의 작은 반전, 정원 시계 윤곽 |
| 루시아 세라노 | −55° / +160° | 0.25m / 2 | 가장 큰 준비·도약과 부채꼴 전개, 부채형 테라스 |
| 카미유 로랑 | −20° / −155° | 0.13m / 5 | 의상 주름처럼 긴 회전과 겹치는 색, 프리즘 |
| 카이 모건 | −36° / +85° | 0.20m / 3 | 파도에서 솟아오르듯 빠른 도약, 해안 물결 |
| 노르 더프리스 | −10° / +180° | 0.12m / 6 | 운하 순환을 닮은 반 바퀴 스윕, 계단 박공 |
| 하파엘 코스타 | −30° / −135° | 0.16m / 4 | 음악 지도자의 유연한 몸통 리듬, 보도 물결 |
| 살마 암라니 | +12° / +120° | 0.06m / 7 | 차분한 준비와 밀도 높은 기하 리본, 모자이크 별 |
| 타보 은들로부 | −42° / −85° | 0.18m / 5 | 호흡을 모은 뒤 강한 복귀 자세, 평평한 산 능선 |

이는 고유 동작을 검토하기 위한 절차적 리그입니다. 얼굴 표정, 세밀한 손가락·천 시뮬레이션, 전용 카메라 컷신과 완성형 캐릭터 원화는 후속 아트 제작 범위입니다.

## 게이지와 사용권

성공한 리턴에 공격 게이지 +attack_gain, 수비 게이지 +defense_gain×수비 보정이 쌓입니다. 빠른 공을 받아냈으면 4를 추가하고 정상 서브는 attack_gain의 절반을 줍니다. 실패·중복·지연·거부된 입력은 충전하지 않습니다. 단순 대기 시간으로 충전되지 않습니다.

100마다 사용권 1개, 최대 3개입니다. 3개가 되면 표시 게이지를 100으로 고정하고 추가 충전을 버립니다. 3→2개 사용 시 게이지를 0으로 비워 숨은 네 번째 사용권이 생기지 않게 합니다. 3개 미만에서는 사용 후 기존 부분 게이지를 유지합니다. 포인트 사이에는 사용권을 유지하고, 경기 재시작/캐릭터 교체에는 초기화합니다. 활성 스킬 중 중복 발동은 막습니다.

K 또는 Use skill 버튼으로 본인 스킬을 발동합니다. 연습 모드 G는 상대 스킬입니다. 입력은 PlayerInputSample.Skill을 통해 들어오므로 향후 캐릭터별 IMU 제스처 인식 결과를 같은 액션으로 변환하면 됩니다. 실제 외장 IMU·BLE 분류기는 이번 변경에 포함하지 않습니다. AI는 별도 저장소 AiSkillPolicy가 유효 리턴 직후 Easy 3개/Medium 2개/Hard 1개부터 사용을 판단합니다.

## 연출 참고와 독창적 적용

[Capcom 개발진의 문화·캐릭터 디자인 설명](https://sonyinteractive.com/en/news/blog/street-fighter-6-how-kimberly-and-manons-designs-were-inspired-by-their-cultures/)에서 개인의 생활과 문화를 의상·동작에 함께 연결하는 방향을 참고했습니다. [Sparking! ZERO 공식 제작자 인터뷰](https://en.bandainamcoent.eu/dragon-ball/news/dragon-ball-sparking-zero-producer-interview)와 [공식 가이드](https://en.bandainamcoent.eu/dragon-ball/news/dragon-ball-sparking-zero-the-beginners-guide)를 보고, 준비→집중/카메라 줌→회전·리본 폭발→복귀의 박자를 설계했습니다. 드래곤볼 캐릭터/공격/음성/애니메이션/에너지를 복제하지 않고 테니스 리그와 원형·기하 리본으로 구성했습니다. 점멸 대신 부드러운 밝기와 줌을 사용합니다.

## CSV로 빌드 없이 변경

기본 파일: `Assets/StreamingAssets/Balance/characters.csv`. 최초 실행 시 `Application.persistentDataPath/Balance/characters.csv`로 복사합니다. C 메뉴에 실제 파일 경로를 표시합니다. 필요하면 실행 인수 `-dt-balance-file /절대/경로/characters.csv`로 작업 파일을 선택합니다. CSV를 저장한 뒤 서브 대기 중 **F5** 또는 Reload balance CSV를 누릅니다. 진행 중인 스킬이 없을 때만 전체 파일을 한 번에 반영합니다. 오류가 있으면 이전 정상 값과 revision을 유지하고 사유를 표시합니다.

UTF-8(BOM 가능), 쉼표 구분, 소수점은 점, 15행과 정해진 헤더가 필요합니다. 능력치 1~100, 배율 1~1.35, 체력 회복 0~40, 지속 1~10초, 공격 충전 1~40, 수비 충전 1~30을 검사합니다. 기본 합계만 420이며 운영 튜닝 시 합계를 강제하지 않습니다. 국가 ID를 바꾸거나 행을 빠뜨리거나 중복시키면 거부됩니다.

온라인은 호스트의 적용된 능력치·revision·선수 선택·스킬 stock/gauge/체력/연출 ID/나이와 코트 ID를 스냅샷으로 전달합니다. 게스트 CSV가 달라도 게스트는 호스트가 보낸 유효 값만 표시하며 자체 물리·게이지·효과 타이머를 돌리지 않습니다. 게스트가 보낼 수 있는 것은 본인의 입력/캐릭터 선택 의도이며 코트·점수·스톡을 지정할 수 없습니다.

## 조작

C: 캐릭터/코트 메뉴, K: 본인 스킬, G: 연습 상대 스킬, F5: CSV 다시 읽기, S: 스매시(높이 1.75m 이상), V: 발리(바운스 0), X: 슬라이스, B: 로브. 기존 Q/W 포핸드/백핸드, Space 서브를 유지합니다. 캐릭터/코트는 첫 서브 전에 선택합니다. 연습·1인 모드에서는 상대 캐릭터도 선택하고 온라인에서는 각자 본인, 호스트가 코트를 선택합니다.

## 검증 기록

2026-10-15, Unity 6000.3.26f1 / Apple Silicon Mac.

- Unity EditMode **91/91**, PlayMode **22/22**, 프로젝트 check 통과. 실제 씬에서 네 가지 추가 타격, 정상 리턴 충전, 스킬 1개 소비, 1.2초 공/물리/점수 정지와 동일 샷 재개를 확인했습니다. 상·중·하 AI 각각 한 게임을 끝까지 진행했습니다.
- AI 저장소 독립 검사 **24건**, 서버 검사 **18건** 통과. AI 스킬 정책은 별도 패키지를 유지합니다.
- Mac 및 Windows Mono 빌드 성공. 실제 실행 검증은 Mac에서 했으며 물리 Windows PC·WAN·외장 IMU/BLE 검증은 수행하지 않았습니다.
- 서로 다른 PID의 실제 Unity 실행 파일 2개를 WebSocket 릴레이로 연결. 한국/스페인 선택, 스페인 코트, 네트 폴트→렛→더블 폴트→볼보이 회수 3회, 양쪽 **11회씩** 정상 리턴, 최대 **6타** 랠리, **4–1** 같은 승자와 다음 서브 5회를 확인했습니다.
- 양쪽 모두 사용권 최대 **3개**를 실제 충전한 뒤 스킬을 각각 1회 사용했습니다. 모든 관찰 프레임의 stock/gauge는 유효했고, 상태 불일치 **0건**입니다. 추가 충전 차단·숨은 네 번째 사용권 방지는 순수 경계값 검사에서도 통과했습니다.
- 실행 중 검증용 CSV의 KOR serve를 70→77로 저장하고 서브 대기에서 일반 ReloadBalance 입력을 전달. **재빌드 없이** 양쪽 serve=77, balanceRevision=3으로 일치했습니다.
- 경기 중 게스트 연결 중단 후 epoch **2**로 재접속. 최종 통신 오류/실행 오류 **0/0**, 호스트 물리 **3,081 tick**, 게스트 **0 tick**. 시점·좌우 반전·포핸드/백핸드·심판/볼보이 표시 검사도 통과했습니다.
- 첫 온라인 실행은 검증 종료 시 게스트를 먼저 닫아 호스트의 마지막 스냅샷이 거절되는 종료 경합 1건을 발견했습니다. 송신자인 호스트를 먼저 종료하도록 검증 수명 주기를 고치고, 오류 집계를 유지한 최종 재실행에서 0건을 확인했습니다.
- 15명 전체의 기본 코트와 1.2초 스킬 자세를 실제 GPU 게임 카메라로 저장했습니다. 전체 미리보기는 사용권을 명시적으로 부여하는 **연출 미리보기**이며 충전 검증의 증거로 사용하지 않습니다. 전체 미리보기 실행 오류 0, 생성 점수 0입니다.

Mac 잠금은 유지했습니다. 카메라 PNG에는 3D 경기만 포함되고 메뉴·점수·게이지(IMGUI)는 들어가지 않습니다. 창 모드 UI의 실사용 검증과 구분합니다. 경기 수치와 stock/효과/페르소나 메타데이터는 PNG 옆 JSON 및 combined.json에 기록합니다.

검토 순서는 게임 기존 PR #7→#8, 서버 PR #2 및 AI PR #2, AI 스쿼시 병합 SHA로 게임 의존성 갱신, 이번 게임 PR입니다. 이 작업에서는 기존 미병합 PR을 임의로 병합하지 않습니다.
