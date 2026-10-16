# 선수 음성·코트 효과음 업그레이드

## 오프라인 콘텐츠와 출처

15명의 기존 성인 선수에게 준비·스킬·승리·패배 4가지의 서로 다른 영어 대사, 총 60개를 제공합니다. 이름·서사·밸런스는 변경하지 않습니다. 한서윤의 여백 있는 설계, 렌의 접힌 자리에서의 재시작 등 기존 인물의 사건과 성장에 연결했습니다. 한국어 자막은 번역이며 음성은 **영어 Flite CMU 합성**입니다. 성우 연기나 각국 언어·억양 녹음이 아닙니다. 이전 `_alt` ID는 새 인물 대사에 연결되는 호환 별칭으로 유지합니다. 사용자 별도 대체 녹음은 보존됩니다.

[Kenney Impact Sounds](https://kenney.nl/assets/impact-sounds), [RPG Audio](https://kenney.nl/assets/rpg-audio)의 실제 무료 ZIP을 내려받고 내부 `License.txt`와 OGG 파일을 검사했습니다. 둘 다 CC0이며 라이선스 원문을 Audio 폴더에 유지합니다. 발걸음은 concrete 3종, 라켓 접촉은 light wood 3종, 스키드는 cloth 마찰 3종을 짧게 편집했습니다. 마지막 것은 실제 테니스 신발 현장 녹음이 아닌 천 마찰을 활용한 근사입니다. 트림·필터·음량 정규화·PCM 변환 내용, 원본 멤버명, ZIP 및 소스·결과 SHA-256, 직접 다운로드 URL은 `Assets/StreamingAssets/Audio/factory-provenance.json`에 기록합니다.

음성 생성 도구는 [공식 Flite 6c9f20d](https://github.com/festvox/flite/tree/6c9f20dc915b17f5619340069889db0aa007fcdc)입니다. BSD 계열의 원문 `FLITE-COPYING.txt`를 유지하고 엔진 바이너리는 배포하지 않습니다. 새 WAV는 오프라인이며 네트워크 합성 호출이 없습니다.

## 재생 경계

타격음은 기존 `TennisEffectGate`의 확정된 host contact만 소비합니다. guest도 이미 버퍼링된 effect/아바타 위치를 사용하고 물리·점수·입력을 쓰지 않습니다. 이동음은 표시된 위치의 이동 거리와 감속/방향 전환으로만 계산합니다. 순간이동·리셋은 발소리로 재생하지 않으며 일시정지·시네마틱에서는 걸음 측정을 중단합니다. 최소 보폭과 시간 간격, 효과음 cooldown으로 난사를 제한합니다.

음성은 6개까지 대기하며 4초 뒤 만료합니다. 같은 cue의 2.5초 cooldown과 심판 0.35초 cooldown을 적용합니다. 심판은 선수 대사를 중단하고 낮은 우선순위 대기를 비우며 자막도 현재 재생 음성으로 바뀝니다. 새 경기/epoch에는 대기를 초기화합니다. 준비·결과 자막도 FrontEnd 화면에서 볼 수 있습니다.

경기·서브·랠리·스킬·일시정지 BGM 무음 정책은 유지합니다. 음악은 소개·메뉴·결과에서 허용됩니다. 응원은 실제 totalPoints 증가에 한 번만 재생하고 PointEnd를 떠나면 즉시 멈춥니다. 음성과 효과음은 음악 금지와 별개입니다.

## 기존 설치와 사용자 편집

초기 설치는 새 WAV/CSV를 복사합니다. 기존 설치는 없는 파일만 추가하고 기존 CSV/WAV/자막을 덮어쓰지 않습니다. `factory-legacy.json`의 이전 파일명과 SHA-256이 모두 일치할 때만 메모리에서 새 공장 WAV로 연결합니다. 음량·fade 등 CSV 값은 유지합니다. 원래 이름에 직접 녹음한 파일도 해시가 다르면 그대로 사용합니다. 기존 공장 한국어 자막과 정확히 같은 문장은 새 번역으로 연결하고 사용자가 수정한 자막은 유지합니다. 누락된 새 cue만 기본 catalog에서 보충합니다. 잘못된 reload는 이전 bank를 유지합니다.

`audio.csv`의 file/volume 및 `subtitles.csv`는 계속 편집할 수 있습니다. 기존 `hit.wav`를 사용자 파일로 편집했거나 `hit` cue를 다른 파일로 연결하면 기본 타격 변주 대신 해당 커스텀 hit를 재생합니다. 새 step/skid/hit 변주의 cue별 파일도 바꿀 수 있습니다.

## 재현 가능한 검증

```sh
python3 tools/verify-court-audio.py
bash tools/unity.sh test-edit
bash tools/unity.sh check
bash tools/unity.sh build-mac
python3 tools/verify-native-court-audio.py --out .work/audio-upgrade/native
bash tools/unity.sh test-front
```

네이티브 검증은 별도 fresh/existing Audio 폴더와 balance/effects/rules CSV를 사용합니다. 실제 사용자 설치를 수정하지 않습니다. 기존 설치 모형은 한서윤 volume 0.19, 렌의 수정 WAV와 사용자 자막을 포함하고 종료 후 원본 바이트 해시를 비교합니다. 새 대사 청음은 실제 경기가 WaitServe 상태일 때의 **명시적 오디오 청음**이며 실제 스킬 획득이나 경기 승리의 증거로 쓰지 않습니다. 이어서 일반 입력 adapter와 AI가 실제 서브·랠리·득점을 진행합니다. 접촉 이벤트를 조작하지 않습니다.

게임 Camera PNG, cue별 AudioListener WAV, 실제 랠리 game-mix.wav와 관측 JSON은 `.work/audio-upgrade/native/{fresh,existing}`에만 저장합니다. 데스크톱/마이크를 캡처하지 않으며 Mac 잠금을 우회하지 않습니다. 녹음 시에도 실제 게임 출력은 유지합니다. 출력 파형은 해당 소스와 상관도를 비교합니다. 검증 이미지/빌드/로그는 커밋 대상이 아닙니다.

물리 Windows PC, 실제 BLE/별도 부착 외장 IMU 검증은 이번 오디오 검증의 범위가 아닙니다. 기존 12일·3인 개발 계획과 권한 분리는 유지됩니다.

## 실제 검증 결과 (2026-10-17, native Mac)

Unity 6000.3.26f1의 OSXPlayer / Metal에서 실행했습니다. EditMode 165개, FrontEnd PlayMode 6개, project check와 Mac build가 통과했습니다. 실제 관측 결과는 다음과 같습니다.

| 설치 | 직접 청음한 대사 | 발걸음 | 마찰 | 확정 접촉 | 실제 포인트 | 응원 | 경기 BGM | 런타임 오류 |
|---|---:|---:|---:|---:|---:|---:|---:|---:|
| fresh | 60 | 62 | 15 | 7 | 1 | 1 | 0.0 | 0 |
| existing | 8 | 62 | 15 | 7 | 1 | 1 | 0.0 | 0 |

새 설치는 15명 × 4상황의 60개, 기존 설치는 갱신·사용자 수정 경계가 다른 한국·일본의 8개 대사를 직접 재생했습니다. 68개 AudioListener 녹음 모두 소스와 비교를 통과했으며 최소 정규화 상관도는 1.00000입니다. 각 파일의 capture SHA-256과 상관도는 `.work/audio-upgrade/native/waveform-audit.json`에 기록했습니다. 두 설치에서 실제 입력으로 Near 3회 / AI Far 4회 접촉, 1포인트 후 다음 서브 준비를 확인했습니다. 각 bank는 160 cue를 읽었고 잘못된 CSV reload를 거절한 뒤 정상 revision 2로 복구했습니다.

기존 설치에서 한서윤 음량 0.19가 유지되면서 `KOR_ready_v2.wav`가 재생됐고, 렌은 수정한 `JPN_ready.wav`와 사용자 자막을 유지했습니다. CSV·자막·기존 WAV의 실행 전후 SHA-256도 같았습니다. 네이티브 PNG에서 한국어 자막을 직접 확인했습니다.

중간 종료된 두 기존 설치 전체 청음 실행은 통과 근거로 사용하지 않습니다. 파형 검사에서 발견한 이전 대사 혼입은 녹음 초기화와 오디오 콜백의 경쟁을 동기화해 수정하고, 새 설치 전체를 재녹음한 뒤 비교를 통과했습니다. 현재 근거는 위의 완료된 fresh/기존 설치 2개 보고서와 68개 파형 검사입니다. 재현 시 `--mode fresh` 또는 `--mode existing`으로 분리할 수 있습니다. 긴 기본 `--mode all` 실행은 두 검증을 순차로 수행합니다.
